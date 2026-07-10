---
name: h3vr-remote-development
description: Use for any H3VR-Mods change that is reviewed or edited from macOS and built, tested, deployed, or released on the remote Windows H3VR machine.
---

# Remote H3VR Mod Development

## Purpose

Use this workflow for all existing and new mods in this repository. The macOS
checkout is the portable review and Git workspace. The Windows checkout is the
authoritative game-development workspace: it builds against the installed H3VR
assemblies, packages releases, deploys into r2modman, and is the only place
where H3VR is run.

Do not treat a macOS build as an H3VR compatibility check. Do not put secrets,
game assemblies, decompiled game source, generated artifacts, or temporary
design notes in Git.

## Fixed Environment

| Purpose | Location |
| --- | --- |
| macOS mirror | `~/dev/H3VR-Mods` |
| Windows source of truth | `E:\Dev\H3VR-Mods` |
| SSH target | `win-pc` or `haohan@DESKTOP-1L9D72F.local` |
| H3VR managed assemblies | configured in `build/environment.json`; always read-only |
| r2modman Default profile | configured in `build/environment.json` |
| BepInEx log | configured in `build/environment.json` |
| release wrapper | `tools/h3vr.ps1` |

Always resolve current paths from `build/environment.json`; do not hardcode a
personal profile path in source code or package metadata.

## Start Every Task

1. Confirm SSH and inspect the Windows working tree. Never reset or overwrite
   existing dirty changes.

   ```bash
   ssh win-pc 'git -C E:\Dev\H3VR-Mods status --short --branch'
   ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Preflight'
   ```

2. Synchronize through Git deliberately. Before reviewing or editing on macOS,
   update `~/dev/H3VR-Mods` with `git pull --ff-only`. Before a Windows build,
   ensure the intended commit is present in `E:\Dev\H3VR-Mods`. Keep Windows
   changes and Mac changes in scoped commits; do not use broad copies to erase
   someone else's work.

3. Read the existing mod, its package source, `build/mods.json`, relevant tests,
   and `tools/h3vr.ps1` before choosing an implementation. Reuse local patterns.

## Game Source and Harmony Targets

The installed managed DLLs are the current game API and must remain read-only.
The decompiled source root is a convenience for searching and Harmony target
validation; it is not source code for this repository and must never be edited
or committed.

Check status before Harmony work:

```bash
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action SourceStatus'
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Verify -Mod ThePing'
```

Refresh the decompiled source only after an H3VR game update, or when it is
missing and the user explicitly requests refresh. Do not refresh it routinely:

```bash
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action RefreshSource'
```

Use `FindType`, `FindMethod`, and `GrepSource` for read-only discovery. Register
Harmony patch targets in `build/mods.json` so `Verify` catches API drift before
VR testing.

## Implement and Test

1. Keep code changes narrow and add focused tests for changed shared behavior.
   Prefer metadata/component analysis over name-specific rules. Name hard-coding
   is acceptable only for an explicit blacklist or a documented engine quirk.
2. Run the repository suite on Windows after changes:

   ```bash
   ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Test'
   ```

3. For a single mod, also run `Build` and inspect the output. For Harmony mods,
   run `Verify` against the current decompiled reference before deployment.

   ```bash
   ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Build -Mod <ModName>'
   ```

Do not claim a runtime or VR behavior is fixed from a successful compilation
alone. The build validates managed references; the BepInEx log and a VR test
validate runtime behavior.

## Add a New Mod

Start from an existing mod with the same delivery style rather than creating a
new build convention. Add the project, package source, icon, README, changelog,
and focused tests. Then register the mod in `build/mods.json`, including its
project/package paths, deployment folder, payload, package layout, and Harmony
targets when applicable. Extend the `-Mod` `ValidateSet` in `tools/h3vr.ps1` so
the wrapper accepts the new mod.

Do not add game DLLs, decompiled source, built DLLs, release ZIPs, runtime
metadata, or generated profile receipts to the repository. A new mod is ready
only when `Build`, `Test`, `Package`, and `Deploy` work through the wrapper.

## Package and Deploy

The wrapper is the canonical package/deploy mechanism. It creates staging,
artifact, and receipt files under `build/`; those generated files are ignored
and must not be added to Git.

Stop H3VR before deployment. Then package and deploy:

```bash
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Package -Mod <ModName>'
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Deploy -Mod <ModName>'
```

`Deploy` repackages the intended source, installs it into the Default r2modman
profile, backs up the prior plugin folder, and creates a VR-test receipt. Verify
the installed DLL hash matches the release build for code mods.

After the user tests in VR, inspect the log without launching the game yourself:

```bash
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action TailLogs'
```

Use `Logs` for the full filtered log and `ClearLogs` only when an archived copy
is appropriate for a focused retest.

## GunGameProgressions Rules

GunGameProgressions has two packaged offline vanilla fallbacks:

- `Runtime 01 - Vanilla Rot`
- `Runtime 03 - Vanilla Mixed Enemy`

The metadata exporter generates the runtime set only after the active object
and Sosig registries have finished late loading. Validate generated runtime
profiles from live `ObjectData.json`, never from a static list of installed mods.
They must reference only content enabled for that player and must not replace
the safe packaged fallbacks before their final refresh completes.

For loadouts, require a compatible feed in this priority order: magazine, then
clip/speedloader, then cartridges only when the firearm has no compatible
magazine or clip. Attach optics only after a verified physical mount match;
exclude magnifiers and non-optic attachments. Validate generated profiles with
the repository tests and the runtime generation receipt before asking for VR
testing.

## Release to Thunderstore

Publish only after the user explicitly requests a release. Bump the version
only at release time, and keep all version-bearing files and release tests in
sync: project file, Thunderstore `manifest.json`, changelog, and expectations.

Release sequence:

```bash
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Test'
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Package -Mod <ModName>'
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Deploy -Mod <ModName>'
ssh win-pc 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\Dev\H3VR-Mods\tools\h3vr.ps1" -Action Publish -Mod <ModName> -Publish -VrApproved'
```

The publishing token is a persistent per-user Windows environment variable
named `TCLI_AUTH_TOKEN`; configure it once through `SetPublishToken`. Never
write it to source, Git configuration, logs, shell history, or macOS files.
The wrapper uses `dotnet tool run tcli`, performs a lightweight target-version
duplicate check, and refuses duplicate releases.

Verify a publish by downloading the exact version URL and reading the ZIP
manifest. The Thunderstore versions HTML page can be cached and is not primary
proof of publication:

```text
https://thunderstore.io/package/download/<Namespace>/<PackageName>/<Version>/
```

## Commit and Sync

After verification, inspect the intended diff, exclude `build/` output and game
data, then commit only the release source, package metadata, tests, and skill
changes. Push the Windows `main` branch and update the macOS checkout with a
fast-forward pull. Do not merge unrelated dirty work as part of a release.

## Recovery

- SSH unavailable: do not invent a new IP or password path. Retry `win-pc`, then
  `haohan@DESKTOP-1L9D72F.local`, and diagnose connectivity before changing code.
- Deploy blocked: ensure `h3vr.exe` is stopped; do not overwrite files held by
  the running game.
- Runtime error: collect `TailLogs`, compare the deployed DLL hash, and inspect
  the exact Harmony target or profile entry before altering code.
- Publish appears successful but the UI is stale: test the version-specific
  download URL and inspect its `manifest.json` before retrying. Never republish
  the same version.
