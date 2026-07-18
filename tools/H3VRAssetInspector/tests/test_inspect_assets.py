import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


TOOL = Path(__file__).resolve().parents[1] / "inspect_assets.py"
TOOL_SPEC = importlib.util.spec_from_file_location("h3vr_asset_inspector", TOOL)
assert TOOL_SPEC and TOOL_SPEC.loader
INSPECTOR = importlib.util.module_from_spec(TOOL_SPEC)
TOOL_SPEC.loader.exec_module(INSPECTOR)


class InspectAssetsTests(unittest.TestCase):
    def test_mono_behaviour_links_resolve_without_unitypy(self):
        self.assertEqual(42, INSPECTOR.pointer_path_id({"m_FileID": 0, "m_PathID": 42}))
        self.assertIsNone(INSPECTOR.pointer_path_id({"m_FileID": 0}))
        self.assertIsNone(INSPECTOR.pointer_path_id(None))
        self.assertEqual(
            {
                "className": "PIPScope",
                "namespace": "FistVR",
                "assembly": "Assembly-CSharp.dll",
            },
            INSPECTOR.script_identity(
                {
                    "m_ClassName": "PIPScope",
                    "m_Namespace": "FistVR",
                    "m_AssemblyName": "Assembly-CSharp.dll",
                },
                "fallback",
            ),
        )
        self.assertEqual(
            {"x": 1.0, "y": 2.0},
            INSPECTOR.normalize_configuration_value({"x": 1.0, "y": 2.0}),
        )
        self.assertEqual(
            {"time": 0.0, "value": 3.0},
            INSPECTOR.normalize_configuration_value({"time": 0.0, "value": 3.0}),
        )
        self.assertEqual(
            [("Components[0].Geo", 123)],
            list(INSPECTOR.pointer_paths({"Components": [{"Geo": {"m_FileID": 0, "m_PathID": 123}}]})),
        )

    def test_pip_scope_components_and_reference_summaries_stay_structural(self):
        components = INSPECTOR.normalize_pip_scope_components(
            [
                {
                    "OptionType": 0,
                    "Geo": {"m_FileID": 0, "m_PathID": 123},
                    "Axis": 2,
                    "Interp": 1,
                    "Values": [0.0, 30.0],
                    "UsesIncrementalValues": False,
                    "IncrementalValue": 0.0,
                    "NotPartOfTheContract": "discarded",
                }
            ]
        )
        self.assertEqual(
            [
                {
                    "OptionType": 0,
                    "Geo": {"m_FileID": 0, "m_PathID": 123},
                    "Axis": 2,
                    "Interp": 1,
                    "Values": [0.0, 30.0],
                    "UsesIncrementalValues": False,
                    "IncrementalValue": 0.0,
                }
            ],
            components,
        )
        self.assertEqual(
            {
                "pathId": 123,
                "type": "MeshRenderer",
                "gameObjectName": "ScopeLens",
            },
            INSPECTOR.reference_summary(
                {"pathId": 123, "type": "MeshRenderer", "name": "", "gameObjectName": "ScopeLens"}
            ),
        )

    def test_list_candidates_records_hashes_without_unitypy(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / "scope.zip"
            bundle = b"UnityFS\x00scope-bundle"
            with zipfile.ZipFile(archive, "w") as zip_file:
                zip_file.writestr("bundle/scope", bundle)
                zip_file.writestr("README.md", "not an asset bundle")
            output = root / "manifest.json"

            result = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    "--input",
                    str(archive),
                    "--output",
                    str(output),
                    "--list-candidates",
                ],
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            manifest = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual("zip", manifest["source"]["kind"])
            self.assertEqual(hashlib.sha256(archive.read_bytes()).hexdigest(), manifest["source"]["sha256"])
            self.assertEqual(1, len(manifest["candidates"]))
            self.assertEqual("bundle/scope", manifest["candidates"][0]["label"])
            self.assertEqual(hashlib.sha256(bundle).hexdigest(), manifest["candidates"][0]["sha256"])
            self.assertEqual([], manifest["bundles"])

    def test_expected_hash_rejects_different_input(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            bundle = root / "scope.bundle"
            bundle.write_bytes(b"UnityFS\x00scope-bundle")
            output = root / "manifest.json"
            result = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    "--input",
                    str(bundle),
                    "--output",
                    str(output),
                    "--expected-sha256",
                    "0" * 64,
                    "--list-candidates",
                ],
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertNotEqual(0, result.returncode)
            self.assertIn("does not match", result.stderr)
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
