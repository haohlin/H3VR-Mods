---
name: h3vr-remote-development
description: Use for any H3VR-Mods change reviewed on macOS and built, tested, deployed, or released through the private Windows H3VR environment.
---

# Remote H3VR Mod Development

## Purpose

The macOS checkout is only for source inspection, editing, and normal Git
review. Never build, test, package, deploy, run Unity, or run H3VR locally on
macOS. The private Windows checkout is authoritative: it builds against
installed H3VR assemblies, packages releases, deploys to r2modman, and is the
only H3VR runtime. Invoke every validation and pipeline action from macOS with
`h3vr-remote run <Action> [Mod]`; local command results are not H3VR evidence.

GitHub CI is a separate hosted-only portable-source check. It runs on hosted
Linux for its scoped source/data paths and must never call `h3vr-remote`, a
remote shell, Windows PowerShell, Unity, MeatKit, H3VR, credentials, package,
deploy, or publish tools. CI failure is not live-game evidence; Windows remains
the explicit manual/runtime validation surface.

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
Use `h3vr-remote git <arguments>` only for scoped remote Git work. Do not use
raw SSH in normal H3VR workflow.

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

1. Confirm the private resolver and inspect the Windows working tree. Never
   reset or overwrite existing dirty changes. Use the wrapper rather than a
   raw remote shell for normal pipeline work.

   ```bash
   h3vr-remote status
   h3vr-remote git status --short --branch
   h3vr-remote run Preflight
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
   local branch receives any remote update. For Unity content, first close the
   editor on the checkout being synchronized. If tracked changes exist, do not
   pull, merge, reset, or overwrite them; report the divergence first.
   Untracked files alone never authorize cleanup. Before a Windows build,
   ensure the intended commit is present in the configured Windows checkout.
   Keep commits scoped; do not erase another developer's work with broad
   copies.

### Cross-platform parity before local deployment

After every completed implementation, documentation, or workflow-skill change,
make the macOS checkout, GitHub branch, and Windows checkout converge before
any local package or deployment:

1. Commit the reviewed tracked changes on macOS and push the intended branch.
2. With the Windows worktree clean and Unity closed, run
   `h3vr-remote sync <branch>`. It must fast-forward; never force a divergent
   or dirty Windows checkout into parity.
3. Prove one exact commit on all three sides: compare macOS `HEAD`, the pushed
   `origin/<branch>`, and `h3vr-remote git rev-parse HEAD`. A matching branch
   name is not sufficient.
4. User-global H3VR skill entrypoints are not Git-tracked. When a shared skill
   changes, update the reviewed equivalent entrypoint on every configured
   platform and compare its SHA-256. A missing or different global skill is a
   parity failure, not an invitation to copy an unreviewed platform-local file.

Do not run `Package` or `Deploy` for a new local candidate until this parity
gate passes. A skill-only change needs no rebuild, but still needs Git and
global-skill parity before the next deployment.

4. Read the affected mod, package source, `build/mods.json`, relevant tests, and
   `tools/h3vr.ps1` before implementation. Reuse existing local patterns.

## Game Source and Harmony Targets

Installed managed DLLs are the current game API and remain read-only. The
decompiled source cache is for searching and Harmony-target validation only; it
is never repository source.

```bash
h3vr-remote run SourceStatus
h3vr-remote run Verify ThePing
```

Refresh decompiled source only after a game update or explicit request:

```bash
h3vr-remote run RefreshSource
```

Use `FindType`, `FindMethod`, and `GrepSource` for read-only discovery.
Register Harmony targets in `build/mods.json` so `Verify` catches API drift.

### Harmony runtime-proof gate

Keep four proofs separate:

- `Verify` proves the current DLL resolves the source type and method.
- A BepInEx startup log proves the plugin loaded.
- `Harmony.GetPatchInfo` during startup proves a patch registered.
- None proves the intended runtime method ran or produced the game result.

Before changing control flow, configuration, or a fallback after a Harmony
failure, collect bounded runtime evidence: a prefix/postfix entry at the target;
post-scene runtime type, assembly, `MethodInfo`, and patch owners/priorities;
entry logs for immediate callers or alternate lifecycle paths; and the expected
postcondition in that same test. If target entry is absent, do not blame
configuration or downstream API. First trace callers and target identity with
one-shot or transition logs, never `Update` polling. Run one explicit game
start/promotion/demotion test and inspect `TailLogs` before adding behavior.

## Implement and Test

1. Keep changes narrow. Prefer extending one shared policy/resolver/framework
   over new per-item branches, duplicate generators, or large rewrites. Add a
   focused positive and negative test for changed shared behavior. Keep new
   runtime work bounded: no hot polling, prefab materialization, or repeated
   allocation on the play path unless the live API requires it.
2. Run the repository suite on Windows:

   ```bash
   h3vr-remote run Test
   ```

3. For an affected mod, run `Build` and inspect its output. For Harmony mods,
   run `Verify` before deployment.

   ```bash
   h3vr-remote run Build <ModName>
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

### Unity/MeatKit edit-build-test gate

Use this sequence for every prefab, scene, or MeatKit change:

1. Read the mod's `DEV_STATUS.md`, descriptor, build profile, and focused
   validation before opening Unity. Close Unity before every pull, sync,
   branch switch, or merge. Keep one editor as the workspace writer.
2. In Unity, edit the prefab asset in Prefab Mode. If an intentional scene
   instance override is edited, apply it to the correct prefab explicitly.
   Save, close Unity, then inspect the authoritative Unity checkout with
   `git diff --check` and the changed prefab/YAML before synchronizing. A
   visible Inspector field is not proof that the prefab asset was saved.
3. Commit the reviewed source and matching `.meta` files, then use the guarded
   Windows source sync only while the Windows editor is closed. Do not build
   an unsynchronized or dirty Unity checkout.
   For a historical or local-only Unity variant that must coexist in one
   project, keep it in a uniquely named `Assets/Projects/<Variant>` folder
   with preserved asset GUIDs. Do not clone the entire MeatKit project or
   replace the shared pipeline configuration. Use an ignored, explicit
   `-EnvironmentConfigPath` and `-ModsConfigPath` sidecar pair that names the
   same project root and a unique package/deployment folder. Its batch build
   method must validate only that variant; it must not run a current-mod
   migration on archived assets.
4. Run `h3vr-remote run Test`, then `h3vr-remote run Build <ModName>`. A Unity
   build passes only when its process succeeds, the descriptor's success marker
   is present in the current log, and the expected source package was freshly
   written. A pre-existing ZIP is not proof; Unity 5.6 may compile and exit
   before the requested editor method runs.
5. Static prefab tests must inspect serialized data that exists before Unity
   lifecycle methods. Do not assert a component cache populated in `Awake` or
   `Start`; inspect the serialized component list or run a lifecycle-aware
   runtime test instead.
6. `Build` creates the validated Unity source package. `Package` creates the
   release artifact and receipt under `build/`; do not search that artifact
   directory after `Build` alone. After an unchanged successful build, use
   `h3vr-remote run Package <ModName> -ReuseExistingUnityPackage`, then
   `h3vr-remote run Deploy <ModName> -ReuseExistingUnityPackage` for an
   authorized local test deployment. Never reuse a package after Unity source,
   profile, or descriptor changes.
7. A deployed package still needs human VR proof of item spawning, pick-up,
   mounting, input, and authored optics/interaction. After the tester reports
   back, run `h3vr-remote run TailLogs` and record the result before release.

On failure, stop before package or deploy, read the exact current Unity/pipeline
log, classify it as import/compile, serialized-data test, build-marker/package,
or runtime failure, and fix that one cause. Never deploy a stale last-successful
artifact as evidence for a newer source change.

## Add a New Mod

Start from an existing mod with the same delivery style. Add project/package
source, icon, README, changelog, focused tests, and a `build/mods.json`
descriptor. Extend `tools/h3vr.ps1` only when a new supported build kind is
needed. A new mod is ready when `Build`, `Test`, `Package`, and `Deploy` work
through the wrapper.

## Package and Deploy

The wrapper is canonical. It creates ignored staging, artifact, and receipt
files below `build/`. Stop H3VR before deployment.

For a descriptor with `registerWithR2modman: true`, `Deploy` also registers the
package in the active profile's r2modman local-mod list and cache. Keep the
manifest `author` plus `name` equal to `deploymentFolder` as
`<author>-<name>` so GUI enable, disable, and uninstall target the deployed
folder. Do not hand-edit `mods.yml` or copy a DLL around the wrapper.

```bash
h3vr-remote run Package <ModName>
h3vr-remote run Deploy <ModName>
```

After the user tests in VR, inspect logs without launching the game yourself:

```bash
h3vr-remote run TailLogs
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
h3vr-remote run Test
h3vr-remote run Package <ModName>
h3vr-remote run Deploy <ModName>
h3vr-remote run Publish <ModName> -Publish -VrApproved
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
- Unity build error: do not package or deploy. Verify the current log's
  configured marker and fresh source package, then fix the one failed
  import/compile, serialized-data test, or build-method condition.
- Publish page stale: inspect the version-specific package URL and manifest
  before retrying; never publish a duplicate version.
