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

### Required private-config resolver

On macOS, do not depend on an interactive shell profile or rediscover a
machine. From an H3VR-Mods checkout, use short global command
`h3vr-remote run <Action> [Mod]`; it delegates to
`tools/h3vr-remote.sh <Action> [arguments]`. It reads only the user-owned,
mode-`600` private file
`${XDG_CONFIG_HOME:-~/.config}/h3vr-mods/remote.env` (or
`H3VR_PRIVATE_CONFIG`) for `H3VR_WINDOWS_HOST` and
`H3VR_WINDOWS_REPOSITORY`. Bootstrap it from the tracked
`tools/h3vr-remote.env.example`; never copy real values into the repository.

On Windows, keep game, r2modman, Unity, and source-cache values in the
ignored `build/environment.local.json`. The wrapper must fail with a missing
variable/configuration name when either private layer is absent; do not scan
for an alternate host or checkout and do not print private values.

Use `h3vr-remote status` for remote Git inspection and
`h3vr-remote sync <branch>` for guarded fetch/fast-forward synchronization.
Do not use raw SSH in normal H3VR workflow.

Do not treat a macOS build as an H3VR compatibility check.

## Cross-session mod state

Tracked mod records, not chat history, preserve working state. Every active
mod must contain `DESIGN.md` and `DEV_STATUS.md`. Start at
`MOD_STATE_INDEX.md`, read both affected-mod records before editing, then
inspect real Windows Git/build/runtime evidence. `DEV_STATUS.md` has three
sections: `Status` holds verified facts/evidence/blockers; `Plan` holds the
active next item and acceptance condition; `Testing` holds repeatable checks
and manual acceptance. Update it with verified results, blockers, and next item
after every task; commit it with source and tests. Create records from
`docs/mod-development` templates before new active mod work. Unity
source/assets and matching `.meta` files must be versioned in the authoritative
Unity repository; untracked Unity workspace changes are not handoff-safe
progress.

## Mandatory handoff records

Before source inspection, debugging, planning, editing, or testing a mod, read
its `DESIGN.md` and `DEV_STATUS.md` after `MOD_STATE_INDEX.md`.

Close every mod task by updating affected `DEV_STATUS.md` sections to current
verified state: release/version boundary, evidence, blocker, next action, and
test limits.
Commit and push those handoff records to GitHub with the task. Handoff records
are maintainer documentation only: never add them to Thunderstore payloads, and
never rebuild, version-bump, deploy, or publish solely for a handoff-doc change.

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

3. Synchronize through Git deliberately. Before review or edits, preserve
   untracked user files and check whether the **tracked** local worktree is
   clean:

   ```bash
   git diff --quiet && git diff --cached --quiet
   ```

   If it is clean, run `git pull --ff-only` after the topology check so the
   local branch receives any remote update. If tracked changes exist, do not
   pull, merge, reset, or overwrite them; report the divergence first.
   Untracked files alone never authorize cleanup. Before a Windows build,
   ensure the intended commit is present in the configured Windows checkout.
   Keep commits scoped; do not erase another developer's work with broad
   copies.

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

1. Keep changes narrow. Prefer extending one shared policy/resolver/framework
   over new per-item branches, duplicate generators, or large rewrites. Add a
   focused positive and negative test for changed shared behavior. Keep new
   runtime work bounded: no hot polling, prefab materialization, or repeated
   allocation on the play path unless the live API requires it.
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

### GunGame Progressions reference set

Before modifying `GunGameProgressions`, read its `DESIGN.md`, `DEV_STATUS.md`,
`GENERATION_POLICY.md`, `BRANDING.md`, player README, and focused tests.
Lifecycle/integration decisions belong in the design document; shared
weapon/feed/optic rules and every reported negative case belong in the policy.
Keep Vanilla, Modded, and offline Vanilla generation on the one shared resolver.

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

### Authorized remote Modded-profile launch

Normally the user starts H3VR. When the user explicitly asks Codex to launch
the installed Modded profile, use this repeatable path instead of a normal game
or Steam launch:

1. Stop any previous H3VR process and archive logs through the wrapper.
2. Read the active r2modman profile root from private configuration. Confirm its
   BepInEx preloader and inspect `.doorstop_version`.
3. Derive the official r2modman Doorstop arguments from that version: v3/default
   uses the legacy enable/target names; v4 uses its enabled/target-assembly
   names. Never copy loader files or manually overlay plugins.
4. Launch the Steam game URI with those arguments through a temporary Task
   Scheduler task in the active interactive console session
   (`TASK_LOGON_INTERACTIVE_TOKEN`). Resolve the current interactive
   user/session dynamically. The URI keeps the profile Doorstop arguments and
   reliably forwards them to an already-running Steam client. A direct
   `Steam.exe -applaunch` task may return success without creating `h3vr.exe`;
   do not treat its task result as launch proof. SSH service session launches
   do not own the desktop and must not be used for this.
5. Verify that H3VR runs in the interactive session and that `LogOutput.log`
   names the profile preloader plus the target plugin/version. Then inspect
   pool files and concise plugin timing/status lines.
6. When testing ends, stop H3VR and delete the temporary scheduled task. Keep
   profile paths, session IDs, user names, task names, and launch arguments
   private; none belong in source, docs, or public logs.

This is a runtime-validation procedure, not an unattended VR substitute. Use
it only with explicit authorization; visual/feel acceptance remains human VR
work.

## Release to Thunderstore

Publish only with explicit user authorization. Bump the version only at release
time, keeping the project, Thunderstore manifest, changelog, and tests in sync.
Never store a publish token in source, Git configuration, logs, shell history,
or cross-machine transfers.

### Brand copy contract

Treat a mod's maintained short description as product/brand copy, not as a
release note. Before changing a Thunderstore manifest description, locate the
mod's canonical description source and preserve it verbatim. An operational
notice may be prefixed or suffixed, but it must never replace or reword the
canonical slogan without explicit product approval. Add a focused release test
that proves the manifest still contains the canonical text.

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
