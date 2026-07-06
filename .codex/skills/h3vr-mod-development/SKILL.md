---
name: h3vr-mod-development
description: Implement, debug, build, package, deploy, VR-test, and publish H3VR BepInEx/Harmony mods from the Windows H3VR-Mods repository. Use for any current or new H3VR mod, including ThePing, GunGame Progressions, Teleport, RemoveWhiteOut, source decompilation, r2modman logs, and Thunderstore releases.
---

# H3VR Mod Development

## Scope and Authority

Use this skill for all work in the canonical Windows repository:

- Repository: `E:\Dev\H3VR-Mods`
- SSH target: `win-pc` (`haohan@DESKTOP-1L9D72F.local` is the fallback)
- Live H3VR managed assemblies: `E:\Steam\steamapps\common\H3VR\h3vr_Data\Managed`
- Generated, read-only source cache: `E:\Dev\H3VR-DnSpy-Source`
- r2modman Default profile: `C:\Users\y\AppData\Roaming\r2modmanPlus-local\H3VR\profiles\Default`

Windows is the source of truth. Do not create an authoritative checkout or store Steam, r2modman, or Thunderstore secrets on macOS. A temporary macOS scratch directory is acceptable only for review or SHA-verified remote transfer.

The managed DLLs are always the current game API. Decompiled source is a disposable, read-only cache: regenerate and replace it from those DLLs before developing or changing a Harmony patch. Never edit, commit, or treat the cache as authoritative.

## Start Every Change

1. Connect through `ssh win-pc` and preserve any user changes. Inspect `git status --short --branch` before editing; do not reset, clean, or overwrite unrelated files.
2. Work on a feature branch, not `main`. Keep each intentional change focused and commit only the relevant files.
3. Run the pipeline preflight from the repository root:

```powershell
powershell.exe -ExecutionPolicy Bypass -File E:\Dev\H3VR-Mods\tools\h3vr.ps1 -Action Preflight
```

4. Refresh the decompiled cache before implementing or validating Harmony code:

```powershell
.\tools\h3vr.ps1 -Action RefreshSource
.\tools\h3vr.ps1 -Action SourceStatus
```

`RefreshSource` replaces `E:\Dev\H3VR-DnSpy-Source` with source generated from the live `Assembly-CSharp.dll` and `Assembly-CSharp-firstpass.dll`. The repository pins `ilspycmd` 8.2 because the Windows build environment has the .NET 7 SDK; do not casually upgrade it to an ILSpy release that requires a newer SDK.

## Investigate the Current Game API

Use the generated source only for read-only target discovery and compatibility checks:

```powershell
.\tools\h3vr.ps1 -Action FindType -Query FVRPooledAudioSource
.\tools\h3vr.ps1 -Action FindMethod -Query FVRMovementManager.FindValidPointCurved
.\tools\h3vr.ps1 -Action GrepSource -Query UpdateWhiteOut
```

Before writing a Harmony patch, inspect the target type and method signature in the fresh cache. Register every runtime target in `build/mods.json`, then use `Verify` to catch target drift before VR testing.

Current registered targets are:

| Mod | Target validation |
| --- | --- |
| `ThePing` | `FVRPooledAudioSource.Play` |
| `Teleport` | `FVRMovementManager.FindValidPointCurved` |
| `RemoveWhiteOut` | `WW_TeleportMaster.UpdateWhiteOut`, `WW_StandaloneWeatherSystem.UpdateWhiteOut` |
| `GunGameProgressions` | Data generator only; no Harmony target |

## Implement Mod Changes

### Existing code mods

For .NET/BepInEx/Harmony mods such as `ThePing`, retain the local project patterns: target `net35`, use the existing BepInEx/Harmony references, expose the version through the project plugin metadata, and keep Harmony targets in `build/mods.json`. Build source belongs under the mod project; generated DLLs and release artifacts do not.

Use the same rules for a new plugin:

1. Create the project using the existing code-mod structure as the template.
2. Add the BepInEx plugin metadata and use an explicit `net35` target.
3. Refresh source, inspect the target API, and declare every Harmony `type`/`method` in `patchTargets`.
4. Add a complete descriptor to `build/mods.json`: `kind`, source project, assembly or generator, package name, deployment folder, layout, payload, and patch targets.
5. Add Thunderstore metadata and a release icon before packaging.

### Existing data mods

`GunGameProgressions` is a Python generator. Keep generation deterministic by using an explicit seed and run it in pipeline staging, never by mutating tracked release data as a test side effect:

```powershell
python .\GunGameProgressions\jsonGen.py --output <staging-output> --seed 0
```

The generator must continue to produce the expected eight pools with internally consistent IDs. A new data-only mod must also be registered in `build/mods.json` with its generator, payload, package metadata, deployment folder, and its selected package layout.

## Validate, Build, and Package

Use the helper as the single build and release interface:

```powershell
.\tools\h3vr.ps1 -Action Test
.\tools\h3vr.ps1 -Action Verify -Mod ThePing
.\tools\h3vr.ps1 -Action Build -Mod ThePing
.\tools\h3vr.ps1 -Action Package -Mod ThePing
```

Run `Test` for every change. It executes `tests\H3vrPipeline.Tests\H3vrPipeline.Tests.csproj`; these tests cover registry parsing, package layout/metadata validation, GunGame generation, and receipt behavior. Run `Verify -Mod <name>` whenever a descriptor or Harmony patch changes. A release candidate requires successful `Test`, `Verify`, `Build`, and `Package` for that mod.

Packages are written below `build\artifacts`, staging is below `build\staging`, and generated receipts are below `build\receipts`; these are ignored by Git. Preserve the configured layout on each mod. The current `ThePing` and `GunGameProgressions` packages use verified `legacy-flat` layouts. Do not rewrite legacy releases to a `BepInEx/plugins` layout without explicitly validating the target mod's ecosystem requirements; choose the layout per descriptor.

Each package must contain valid `manifest.json`, `README.md`, `icon.png`, and every declared payload file. Check the generated package receipt for the package SHA-256, version, commit, and artifact path.

## Deploy and VR-Test

Deploy only a successfully built and packaged artifact:

```powershell
.\tools\h3vr.ps1 -Action Deploy -Mod ThePing
.\tools\h3vr.ps1 -Action Logs
.\tools\h3vr.ps1 -Action TailLogs
```

`Deploy` installs under the Default profile's `BepInEx\plugins` directory and backs up an existing mod deployment before replacing it. It also creates a `build\receipts\<mod>-<version>-<timestamp>-vrtest.md` checklist. Do not copy DLLs directly into the profile or remove a deployment outside this command.

For a clean test session, archive then clear logs with:

```powershell
.\tools\h3vr.ps1 -Action ClearLogs
```

The VR tester launches the r2modman Default profile, exercises the change in H3VR, and records the outcome and notes in the generated receipt. Treat the result as accepted only when the receipt records `PASS`. After the tester reports completion, inspect `LogOutput.log` through `TailLogs` or `Logs` for plugin-load failures, BepInEx errors, Harmony exceptions, missing dependencies, and runtime exceptions.

## Publish to Thunderstore

Publishing is deliberate and always requires explicit user authorization after a passing VR receipt. It is not a CI step.

```powershell
.\tools\h3vr.ps1 -Action Publish -Mod ThePing -VrApproved
```

The command requires a matching passing VR receipt, a package built from the committed version, and the `H3VRMods:Thunderstore` Credential Manager secret. It rejects a reused or non-incremented version and surfaces TCLI/API failures instead of treating them as published. Never print, commit, transfer, or otherwise expose the publish token.

## Remote Work and CI

When a macOS agent needs to transfer a small change, stage it outside the repository and use SHA-verified transfer with an atomic replacement on Windows. The local `h3vr-remote` helper supports `run`, `fetch`, and `push`; it is a local convenience tool, not repository source.

GitHub Actions runs `.github/workflows/verify.yml` for the pipeline tests and package checks. It cannot perform live source validation because the proprietary game DLLs are intentionally absent, and it must never deploy, read credentials, or publish. Windows preflight and `Verify` remain required before a real release.

## Completion Checklist

Before requesting review or merging a development branch:

- [ ] `git status` contains only the intended changes.
- [ ] Fresh decompilation completed before any Harmony change.
- [ ] Every new or changed Harmony target passes `Verify`.
- [ ] `Test`, `Build`, and `Package` passed for each affected mod.
- [ ] Package receipt has the expected SHA-256, version, and payload layout.
- [ ] Deployment used `Deploy`; the VR receipt is present and, for release, marked `PASS`.
- [ ] BepInEx logs show no unresolved plugin or Harmony errors after VR testing.
- [ ] A Thunderstore publish, if requested, used explicit authorization and `-VrApproved`.
