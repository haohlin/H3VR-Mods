---
name: h3vr-remote-development
description: Use for any H3VR-Mods change reviewed on macOS and built, tested, deployed, or released through the private Windows H3VR environment.
---

# Remote H3VR Mod Development

## Purpose

The macOS checkout is a portable review and Git workspace. The private Windows
checkout is authoritative: it builds against installed H3VR assemblies,
packages releases, deploys to r2modman, and is the only H3VR runtime.

Never commit game assemblies, decompiled source, generated artifacts,
credentials, local paths, host names, account names, machine IDs, or connection
details.

## Private Local Configuration

Keep these values outside Git, either in the local shell environment or the
ignored `build/environment.local.json` file:

| Variable | Supplies |
| --- | --- |
| `H3VR_WINDOWS_HOST` | SSH host or private alias |
| `H3VR_WINDOWS_REPOSITORY` | Absolute Windows checkout root |
| `H3VR_MANAGED_DLLS` | Installed H3VR managed assemblies directory |
| `H3VR_DNSPY_SOURCE_ROOT` | Private read-only decompiled-source cache |
| `H3VR_R2MODMAN_PROFILE_ROOT` | Active r2modman H3VR profile |

`build/environment.json` is public and uses only expandable environment-variable
placeholders. To use explicit local paths, copy
`build/environment.local.example.json` to the ignored
`build/environment.local.json`, then fill it in privately. The pipeline prefers
that local file and otherwise expands the public placeholders.

Set the private shell values before running remote commands:

```bash
export H3VR_WINDOWS_HOST='<private-ssh-host-or-alias>'
export H3VR_WINDOWS_REPOSITORY='<private-windows-checkout-path>'
```

Do not treat a macOS build as an H3VR compatibility check.

## Start Every Task

1. Confirm SSH and inspect the Windows working tree. Never reset or overwrite
   existing dirty changes.

   ```bash
   ssh "$H3VR_WINDOWS_HOST" "git -C \"$H3VR_WINDOWS_REPOSITORY\" status --short --branch"
   ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Preflight"
   ```

2. Prove the Git topology before synchronizing. The Windows `main`, local
   `main`, and `origin/main` must share an ancestor before treating a branch
   name as equivalent across machines:

   ```bash
   git fetch origin --prune
   git merge-base HEAD origin/main
   git rev-list --left-right --count HEAD...origin/main
   ```

   If `merge-base` has no result, stop. Preserve the Windows head on a named
   recovery branch and report the unrelated histories; never force-push,
   reset, or merge them automatically. The user must choose the canonical
   history or explicitly approve a deliberate integration.

3. Synchronize through Git deliberately. Before review or edits, update the
   local checkout with `git pull --ff-only`. Before a Windows build, ensure the
   intended commit is present in the configured Windows checkout. Keep commits
   scoped; do not erase another developer's work with broad copies.

4. Read the affected mod, package source, `build/mods.json`, relevant tests, and
   `tools/h3vr.ps1` before implementation. Reuse existing local patterns.

## Game Source and Harmony Targets

Installed managed DLLs are the current game API and remain read-only. The
decompiled source cache is for searching and Harmony-target validation only; it
is never repository source.

```bash
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action SourceStatus"
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Verify -Mod ThePing"
```

Refresh decompiled source only after a game update or explicit request:

```bash
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action RefreshSource"
```

Use `FindType`, `FindMethod`, and `GrepSource` for read-only discovery.
Register Harmony targets in `build/mods.json` so `Verify` catches API drift.

## Implement and Test

1. Keep changes narrow; add focused tests for changed shared behavior.
2. Run the repository suite on Windows:

   ```bash
   ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Test"
   ```

3. For an affected mod, run `Build` and inspect its output. For Harmony mods,
   run `Verify` before deployment.

   ```bash
   ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Build -Mod <ModName>"
   ```

Compilation proves managed references only. BepInEx logs and a VR test prove
runtime behavior.

## Unity and MeatKit

For Unity content, close the editor before pull, branch-switch, or merge work.
Reopen it after import completes. Save assets in their owning project root with
matching `.meta` files; do not hand-copy Unity assets between projects.

Use the editor for visual placement and subjective feel. Use batch Unity and
MeatKit for repeatable compile, object-graph, AssetDatabase, and package checks.
Validate the prefab/scene wiring, MeatKit build profile and dependencies,
generated package, BepInEx log, and real in-game interaction before calling a
Unity-content change complete.

## Add a New Mod

Start from an existing mod with the same delivery style. Add project/package
source, icon, README, changelog, focused tests, and a `build/mods.json`
descriptor. Extend `tools/h3vr.ps1` only when a new supported build kind is
needed. A new mod is ready when `Build`, `Test`, `Package`, and `Deploy` work
through the wrapper.

## Package and Deploy

The wrapper is canonical. It creates ignored staging, artifact, and receipt
files below `build/`. Stop H3VR before deployment.

```bash
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Package -Mod <ModName>"
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Deploy -Mod <ModName>"
```

After the user tests in VR, inspect logs without launching the game yourself:

```bash
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action TailLogs"
```

## Release to Thunderstore

Publish only with explicit user authorization. Bump the version only at release
time, keeping the project, Thunderstore manifest, changelog, and tests in sync.
Never store a publish token in source, Git configuration, logs, shell history,
or cross-machine transfers.

```bash
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Test"
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Package -Mod <ModName>"
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Deploy -Mod <ModName>"
ssh "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$H3VR_WINDOWS_REPOSITORY\\tools\\h3vr.ps1\" -Action Publish -Mod <ModName> -Publish -VrApproved"
```

Verify a publish from its exact package URL, not a cached versions page:

```text
https://thunderstore.io/package/download/<Namespace>/<PackageName>/<Version>/
```

## Recovery

- SSH unavailable: verify `H3VR_WINDOWS_HOST` in your private configuration;
  do not guess an address, account, password path, or machine name.
- Deploy blocked: stop H3VR; do not overwrite files held by the game.
- Runtime error: collect `TailLogs`, compare the deployed DLL hash, and inspect
  the exact Harmony target or profile entry before editing.
- Publish page stale: inspect the version-specific package URL and manifest
  before retrying; never publish a duplicate version.
