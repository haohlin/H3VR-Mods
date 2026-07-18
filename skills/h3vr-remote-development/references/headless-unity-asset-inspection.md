# Headless Unity Asset Inspection

Use this workflow to study scope packages and other Unity bundles without
making exported assets part of a repository. Structural manifests are default.
An explicitly authorized, owner-private full rip is a separate validation path;
it is never package source, Git content, or release payload.

## Purpose and boundary

The inspector accepts one immutable Unity bundle or release ZIP and writes a
small JSON manifest. It records hashes, object types, names, selected serialized
field paths, material/shader metadata, and a second Unity-editor audit. It does
not copy meshes, textures, shaders, audio, or raw serialized payload into the
repository.

Use a project’s own assets for a release. Treat inspected package data as
configuration evidence only. Do not commit, redistribute, or build from game or
third-party mod assets without permission.

## Owner-private full-rip validation

Use this only when the owner explicitly authorizes local reconstruction and
inspection. It answers a different question from the structural inspector:
whether a known package or the installed game can be recovered into a Unity
project for private comparison.

1. Create a private lab outside every repository. Check free space first.
2. Record source package/game hashes and retain source archives only in that
   private lab.
3. Give AssetRipper the package/game input plus relevant managed assemblies so
   serialized `MonoBehaviour` fields can be decoded.
4. Export to a new staging directory, verify `ProjectVersion.txt`, then move
   staging atomically to the final private export.
5. Batch-import with the matching Unity version. Record import completion and
   every decompiler/compile error separately from asset recovery results.
6. Treat a successful Unity import as asset-recovery evidence only. A playable
   private clone still needs a separate MeatKit reconstruction, package build,
   and human H3VR comparison.

Use `tools/H3VRAssetInspector/export_private_asset_rip.ps1` with explicit
private executable, input, output, and log paths. It starts AssetRipper in
headless mode, loads a file or folder, exports a Unity project, preserves
unfinished staging for review, and refuses to overwrite prior output.

Never copy private raw output into source projects, manifests, release ZIPs, or
Thunderstore. Delete it only after its manifest, import log, and any active
reconstruction have been reviewed.

## Setup

Set the private Python and managed-assembly settings through the normal local
environment configuration, then create the ignored isolated Python environment:

```powershell
.\tools\h3vr.ps1 -Action SetupAssetInspector
```

The environment pins UnityPy and its type-tree helper. It lives under ignored
build tooling and is disposable.

## Inspect one package

Use a recorded SHA-256 when source provenance matters:

```powershell
.\tools\h3vr.ps1 -Action InspectAssets `
  -InputPath <bundle-or-release-zip> `
  -ExpectedSha256 <sha256>
```

The command:

1. validates the immutable input hash;
2. stages only Unity bundle entries beneath ignored temporary storage;
3. emits a structural manifest below ignored inspection output;
4. opens every staged bundle in Unity batch mode for an independent asset and
   material/component audit; and
5. removes staging and the temporary batch script, unless
   `-KeepInspectionScratch` was explicitly requested for debugging.

`-SkipUnityVerification` is a parser-only diagnostic. It is not enough evidence
for a Unity-content implementation decision. Directory scans remain available
through `inspect_assets.py` for local analysis; wrapper runs intentionally
require one ZIP or bundle so the source hash stays unambiguous.

## AI-in-the-loop review

Give an agent the manifest, source hash, implementation question, and known
game-version boundary. Ask it to identify only evidence-backed relationships:

- component and script names associated with PIP, lens, reticle, camera, or
  adjustment controls;
- material and shader names, keywords, and serialized property paths;
- references that need a targeted second inspection; and
- evidence missing before a prefab, shader, or MeatKit change.

An agent can select a narrow follow-up inspection, run an owner-authorized
private rip, or draft a source change. It must not infer a visual result from
names alone, substitute extracted art for original work, or make a
package/release decision without normal Unity and VR validation.

## Evidence and cleanup

Keep the manifest and its input hash with the implementation discussion. They
are enough to reproduce the research decision without retaining a raw rip.
Delete old rip directories only after all of these are true:

- parser manifest completed with the expected input hash;
- Unity batch audit completed for every candidate bundle;
- manifest was reviewed for the needed scope relationship; and
- no active project or test references the old directory.

Remove exact obsolete directories, never broad parent directories or unknown
tools. Record what was removed and that it cannot be restored from the workflow.

## Sources

- [UnityPy](https://github.com/K0lb3/UnityPy) for read-only Unity serialized
  asset parsing.
- [Unity command-line arguments](https://docs.unity3d.com/Manual/command-line-arguments.html)
  for batch-mode execution.
- [AssetRipper documentation](https://assetripper.github.io/AssetRipper/) for
  interactive investigation when structural manifests are insufficient.
