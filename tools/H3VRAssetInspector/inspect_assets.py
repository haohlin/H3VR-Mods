#!/usr/bin/env python3
"""Read-only Unity bundle inspector for H3VR mod research.

The tool emits a configuration manifest, not extracted assets. Bundle copies live
only in caller-owned scratch storage and are removed by the PowerShell wrapper.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
import tempfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


UNITY_MAGICS = (b"UnityFS", b"UnityRaw", b"UnityWeb")
INTEREST_TERMS = (
    "pipscope",
    "pip scope",
    "scope",
    "reticle",
    "lens",
    "magnification",
    "windage",
    "elevation",
    "zero",
    "parallax",
    "vignette",
    "chromatic",
    "rendertexture",
    "shader",
    "material",
    "camera",
)
INTEREST_OBJECT_TYPES = {
    "GameObject",
    "MonoBehaviour",
    "Material",
    "Shader",
    "MeshRenderer",
    "SkinnedMeshRenderer",
    "Camera",
    "Transform",
    "Texture2D",
}
MAX_SIGNALS_PER_OBJECT = 120
MAX_SIGNAL_STRING_LENGTH = 384
PIP_SCOPE_COMPONENT_FIELDS = (
    "OptionType",
    "Geo",
    "Axis",
    "Interp",
    "Values",
    "UsesIncrementalValues",
    "IncrementalValue",
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_directory(path: Path) -> str:
    digest = hashlib.sha256()
    for child in sorted((item for item in path.rglob("*") if item.is_file()), key=lambda item: item.as_posix()):
        relative = child.relative_to(path).as_posix().encode("utf-8")
        digest.update(len(relative).to_bytes(8, "big"))
        digest.update(relative)
        digest.update(bytes.fromhex(sha256_file(child)))
    return digest.hexdigest()


def has_unity_magic(data: bytes) -> bool:
    return any(data.startswith(magic) for magic in UNITY_MAGICS)


def file_has_unity_magic(path: Path) -> bool:
    with path.open("rb") as stream:
        return has_unity_magic(stream.read(16))


def source_kind(path: Path) -> str:
    if path.is_dir():
        return "directory"
    if zipfile.is_zipfile(path):
        return "zip"
    if file_has_unity_magic(path):
        return "bundle"
    raise ValueError("Input must be a Unity bundle, ZIP archive, or directory containing Unity bundles.")


def safe_archive_relative_path(name: str) -> Path:
    candidate = Path(name)
    if candidate.is_absolute() or ".." in candidate.parts:
        raise ValueError("ZIP contains an unsafe entry path.")
    return candidate


def stage_bundles(input_path: Path, kind: str, destination: Path) -> list[dict[str, str]]:
    destination.mkdir(parents=True, exist_ok=True)
    staged: list[dict[str, str]] = []

    def stage_bytes(label: str, data: bytes) -> None:
        index = len(staged)
        target = destination / ("{0:03d}-{1}".format(index, Path(label).name))
        target.write_bytes(data)
        staged.append(
            {
                "label": label,
                "stagedName": target.name,
                "sha256": sha256_file(target),
            }
        )

    if kind == "bundle":
        stage_bytes(input_path.name, input_path.read_bytes())
    elif kind == "zip":
        with zipfile.ZipFile(input_path) as archive:
            for entry in sorted((item for item in archive.infolist() if not item.is_dir()), key=lambda item: item.filename):
                relative = safe_archive_relative_path(entry.filename)
                with archive.open(entry) as stream:
                    data = stream.read()
                if has_unity_magic(data[:16]):
                    stage_bytes(relative.as_posix(), data)
    else:
        for source in sorted((item for item in input_path.rglob("*") if item.is_file()), key=lambda item: item.as_posix()):
            if file_has_unity_magic(source):
                stage_bytes(source.relative_to(input_path).as_posix(), source.read_bytes())

    if not staged:
        raise ValueError("No Unity bundle entries found in input.")
    return staged


def is_interesting_text(value: str) -> bool:
    value = value.lower()
    return any(term in value for term in INTEREST_TERMS)


def is_interesting_key(value: str) -> bool:
    return is_interesting_text(value.replace("_", " "))


def normalize_value(value: Any) -> Any:
    if value is None or isinstance(value, (bool, int, float)):
        return value
    if isinstance(value, str):
        return value[:MAX_SIGNAL_STRING_LENGTH]
    if isinstance(value, dict):
        pointer_keys = {"m_FileID", "m_PathID"}
        if pointer_keys.intersection(value):
            return {key: value.get(key) for key in sorted(pointer_keys) if key in value}
        return {"objectKeys": sorted(str(key) for key in value)[:20]}
    if isinstance(value, (list, tuple)):
        if all(item is None or isinstance(item, (bool, int, float, str)) for item in value[:16]):
            return [normalize_value(item) for item in value[:16]]
        return {"arrayLength": len(value)}
    return str(value)[:MAX_SIGNAL_STRING_LENGTH]


def pointer_path_id(value: Any) -> int | None:
    if not isinstance(value, dict):
        return None
    path_id = value.get("m_PathID")
    if isinstance(path_id, int):
        return path_id
    return None


def script_identity(value: Any, fallback_name: str) -> dict[str, str]:
    if not isinstance(value, dict):
        return {"className": fallback_name}
    result = {
        "className": str(value.get("m_ClassName") or fallback_name),
    }
    for source_key, target_key in (("m_Namespace", "namespace"), ("m_AssemblyName", "assembly")):
        detail = value.get(source_key)
        if detail:
            result[target_key] = str(detail)
    return result


def normalize_pip_scope_components(value: Any) -> Any:
    """Keep controller visual-component configuration, without arbitrary object data."""
    if not isinstance(value, (list, tuple)):
        return normalize_value(value)
    components: list[dict[str, Any]] = []
    for component in value[:32]:
        if not isinstance(component, dict):
            continue
        components.append(
            {
                key: normalize_value(component.get(key))
                for key in PIP_SCOPE_COMPONENT_FIELDS
                if key in component
            }
        )
    return components


def reference_summary(record: dict[str, Any] | None) -> dict[str, Any] | None:
    if not record:
        return None
    summary: dict[str, Any] = {"pathId": record.get("pathId"), "type": record.get("type")}
    for key in ("name", "gameObjectName", "script"):
        value = record.get(key)
        if value:
            summary[key] = value
    return summary


def collect_signals(value: Any, path: str = "", depth: int = 0, result: list[dict[str, Any]] | None = None) -> list[dict[str, Any]]:
    signals = result if result is not None else []
    if depth > 8 or len(signals) >= MAX_SIGNALS_PER_OBJECT:
        return signals
    if isinstance(value, dict):
        for key, child in value.items():
            field_path = key if not path else path + "." + str(key)
            if is_interesting_key(str(key)):
                signals.append({"path": field_path, "value": normalize_value(child)})
                if len(signals) >= MAX_SIGNALS_PER_OBJECT:
                    return signals
            collect_signals(child, field_path, depth + 1, signals)
    elif isinstance(value, (list, tuple)):
        for index, child in enumerate(value[:64]):
            collect_signals(child, path + "[" + str(index) + "]", depth + 1, signals)
            if len(signals) >= MAX_SIGNALS_PER_OBJECT:
                return signals
    elif isinstance(value, str) and is_interesting_text(value):
        signals.append({"path": path, "value": normalize_value(value)})
    return signals


def load_unitypy() -> Any:
    try:
        import UnityPy  # type: ignore[import-not-found]
    except ModuleNotFoundError as error:
        raise RuntimeError("UnityPy is missing. Run the SetupInspector action before inspection.") from error
    return UnityPy


def configure_typetree(environment: Any, managed_directories: Iterable[Path]) -> str | None:
    directories = [directory for directory in managed_directories if directory.is_dir()]
    if not directories:
        return None
    try:
        from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator  # type: ignore[import-not-found]
    except (ImportError, ModuleNotFoundError) as error:
        return "TypeTreeGeneratorAPI is unavailable; custom MonoBehaviour fields may be incomplete: " + str(error)

    unity_version = next(
        (
            getattr(getattr(obj, "assets_file", None), "unity_version", None)
            for obj in environment.objects
            if getattr(getattr(obj, "assets_file", None), "unity_version", None)
        ),
        None,
    )
    if not unity_version:
        return "Unity version was unavailable; custom MonoBehaviour fields may be incomplete."
    try:
        generator = TypeTreeGenerator(unity_version)
        for directory in directories:
            generator.load_dll_folder(str(directory))
        environment.typetree_generator = generator
    except Exception as error:  # tool must preserve a partial manifest when optional decoding fails
        return "TypeTree generation failed; custom MonoBehaviour fields may be incomplete: " + str(error)
    return None


def inspect_bundle(path: Path, source: dict[str, str], managed_directories: Iterable[Path]) -> dict[str, Any]:
    unitypy = load_unitypy()
    environment = unitypy.load(str(path))
    warning = configure_typetree(environment, managed_directories)
    type_counts: dict[str, int] = {}
    records: list[dict[str, Any]] = []
    versions: set[str] = set()

    object_info: list[tuple[Any, str, str]] = []
    mono_scripts: dict[int, dict[str, str]] = {}
    game_object_names: dict[int, str] = {}

    for obj in environment.objects:
        object_type = getattr(getattr(obj, "type", None), "name", "Unknown")
        try:
            name = str(obj.peek_name() or "")
        except Exception:
            name = ""
        object_info.append((obj, object_type, name))
        if object_type == "GameObject":
            game_object_names[getattr(obj, "path_id", 0)] = name[:MAX_SIGNAL_STRING_LENGTH]
        if object_type != "MonoScript":
            continue
        try:
            mono_scripts[getattr(obj, "path_id", 0)] = script_identity(obj.parse_as_dict(), name)
        except Exception:
            mono_scripts[getattr(obj, "path_id", 0)] = {"className": name}

    for obj, object_type, name in object_info:
        type_counts[object_type] = type_counts.get(object_type, 0) + 1
        version = getattr(getattr(obj, "assets_file", None), "unity_version", None)
        if version:
            versions.add(str(version))

        should_parse = object_type in INTEREST_OBJECT_TYPES or is_interesting_text(name)
        if not should_parse:
            continue

        signals: list[dict[str, Any]] = []
        parse_error: str | None = None
        parsed: Any = None
        try:
            parsed = obj.parse_as_dict()
            signals = collect_signals(parsed)
        except Exception as error:  # malformed or stripped data should not discard other objects
            parse_error = str(error)[:MAX_SIGNAL_STRING_LENGTH]

        script: dict[str, str] | None = None
        game_object_name: str | None = None
        if object_type == "MonoBehaviour" and isinstance(parsed, dict):
            script = mono_scripts.get(pointer_path_id(parsed.get("m_Script")))
            game_object_name = game_object_names.get(pointer_path_id(parsed.get("m_GameObject")))

        script_text = " ".join(script.values()) if script else ""
        if not (signals or is_interesting_text(name) or is_interesting_text(script_text) or object_type in {"Material", "MonoBehaviour", "GameObject"}):
            continue
        if script and script.get("className") == "PIPScopeController" and isinstance(parsed, dict):
            components = parsed.get("Components")
            if components is not None:
                signals.append({"path": "Components", "value": normalize_pip_scope_components(components)})
        record: dict[str, Any] = {
            "type": object_type,
            "name": name[:MAX_SIGNAL_STRING_LENGTH],
            "pathId": getattr(obj, "path_id", None),
            "signals": signals,
        }
        if script:
            record["script"] = script
        if game_object_name:
            record["gameObjectName"] = game_object_name
        if parse_error:
            record["parseError"] = parse_error
        records.append(record)

    records_by_path_id = {
        record["pathId"]: record
        for record in records
        if isinstance(record.get("pathId"), int)
    }
    for record in records:
        references: list[dict[str, Any]] = []
        for signal in record["signals"]:
            path_id = pointer_path_id(signal["value"])
            if path_id is None or path_id == 0:
                continue
            target = reference_summary(records_by_path_id.get(path_id))
            if target is not None:
                references.append({"field": signal["path"], "target": target})
        if references:
            record["references"] = references

    result: dict[str, Any] = {
        "source": source,
        "unityVersions": sorted(versions),
        "objectTypeCounts": dict(sorted(type_counts.items())),
        "interestingObjects": records,
    }
    if warning:
        result["warning"] = warning
    return result


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Emit a read-only structural manifest for Unity bundles.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--scratch-root", type=Path)
    parser.add_argument("--stage-bundles-directory", type=Path)
    parser.add_argument("--managed-directory", action="append", type=Path, default=[])
    parser.add_argument("--expected-sha256")
    parser.add_argument("--list-candidates", action="store_true")
    parser.add_argument("--version", action="version", version="H3VRAssetInspector 1")
    return parser.parse_args()


def main() -> int:
    args = parse_arguments()
    input_path = args.input.resolve()
    if not input_path.exists():
        raise ValueError("Input path does not exist.")
    kind = source_kind(input_path)
    source_hash = sha256_directory(input_path) if input_path.is_dir() else sha256_file(input_path)
    if args.expected_sha256 and source_hash.lower() != args.expected_sha256.lower():
        raise ValueError("Input SHA-256 does not match --expected-sha256.")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    scratch_parent = args.scratch_root.resolve() if args.scratch_root else args.output.parent / "scratch"
    scratch_parent.mkdir(parents=True, exist_ok=True)
    scratch = Path(tempfile.mkdtemp(prefix="h3vr-asset-inspection-", dir=str(scratch_parent)))
    staged_directory = args.stage_bundles_directory.resolve() if args.stage_bundles_directory else scratch / "bundles"

    try:
        staged = stage_bundles(input_path, kind, staged_directory)
        manifest: dict[str, Any] = {
            "format": "h3vr-unity-inspection-manifest-v1",
            "generatedAt": datetime.now(timezone.utc).isoformat(),
            "source": {
                "name": input_path.name,
                "kind": kind,
                "sha256": source_hash,
            },
            "candidates": staged,
            "bundles": [],
        }
        if not args.list_candidates:
            for staged_candidate in staged:
                candidate_path = staged_directory / staged_candidate["stagedName"]
                manifest["bundles"].append(inspect_bundle(candidate_path, staged_candidate, args.managed_directory))
        args.output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    finally:
        if not args.stage_bundles_directory:
            shutil.rmtree(scratch, ignore_errors=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print("H3VRAssetInspector: " + str(error), file=sys.stderr)
        raise SystemExit(1)
