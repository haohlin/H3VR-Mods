---
name: h3vr-mod-development
description: Use when implementing, debugging, building, packaging, deploying, VR-testing, or publishing an H3VR Unity/MeatKit, BepInEx/Harmony, data, map, asset-replacement, or Thunderstore mod from the Windows H3VR-Mods repository.
---

# H3VR Mod Development

## Scope and Authority

Use this skill for all work in the canonical private Windows repository. Keep
the environment-specific values outside Git:

| Variable | Supplies |
| --- | --- |
| `H3VR_WINDOWS_HOST` | SSH host or private alias |
| `H3VR_WINDOWS_REPOSITORY` | Windows checkout root |
| `H3VR_MANAGED_DLLS` | Installed H3VR managed-assemblies directory |
| `H3VR_DNSPY_SOURCE_ROOT` | Read-only decompiled-source cache |
| `H3VR_R2MODMAN_PROFILE_ROOT` | Active r2modman H3VR profile |

The tracked `build/environment.json` contains only `%VARIABLE%` placeholders.
For explicit paths, copy `build/environment.local.example.json` to the ignored
`build/environment.local.json` and fill it in privately. `tools/h3vr.ps1`
prefers that file and otherwise expands the environment variables.

On macOS, invoke Windows pipeline actions from an H3VR-Mods checkout through
`h3vr-remote run <Action> [Mod]`; it delegates to
`tools/h3vr-remote.sh <Action> [arguments]`, never an assumed interactive
shell variable. It loads the user-only, mode-`600`
`${XDG_CONFIG_HOME:-~/.config}/h3vr-mods/remote.env` (or
`H3VR_PRIVATE_CONFIG`) containing `H3VR_WINDOWS_HOST` and
`H3VR_WINDOWS_REPOSITORY`. Bootstrap from
`tools/h3vr-remote.env.example`; only variable names are tracked. If either
private configuration layer is absent, report its missing key and stop. Never
scan, print, commit, package, or publish local paths, hostnames, account IDs,
or credentials.

Use `h3vr-remote status` for remote Git inspection and
`h3vr-remote sync <branch>` for guarded fetch/fast-forward synchronization.
Use `h3vr-remote git <arguments>` only for scoped remote Git work.

Windows is the source of truth. Do not create an authoritative checkout or store Steam, r2modman, or Thunderstore secrets on macOS. A temporary macOS scratch directory is acceptable only for review or SHA-verified remote transfer.

## macOS/Windows Execution Boundary

Use the macOS checkout only to inspect, edit, and commit source (plus normal Git
review). Never run `dotnet build`, `dotnet test`, Unity, `tools/h3vr.ps1`, or
any H3VR `Verify`, `Build`, `Test`, `Package`, `Deploy`, or runtime command
locally. Run every validation and pipeline action on Windows through
`h3vr-remote run <Action> [Mod]`. A local command result is not H3VR evidence.

The managed DLLs are always the current game API. Decompiled source is a disposable, read-only cache: refresh it only when `SourceStatus` reports that it no longer matches the live DLLs, normally after an H3VR update. Never edit, commit, or treat the cache as authoritative.

## Cross-platform parity before local deployment

After every completed source, documentation, or skill change, synchronize the
macOS checkout, GitHub branch, and Windows checkout before a local package or
deployment:

1. Commit reviewed tracked changes on macOS and push the intended branch.
2. With the Windows worktree clean and Unity closed, run
   `h3vr-remote sync <branch>`. It must fast-forward; never force a dirty or
   divergent Windows checkout into parity.
3. Compare the exact macOS `HEAD`, pushed `origin/<branch>`, and
   `h3vr-remote git rev-parse HEAD`. Equal branch names are not proof.
4. User-global H3VR skills are untracked. When a shared skill changes, update
   the reviewed equivalent entrypoint on every configured platform and compare
   SHA-256 hashes. A missing or mismatched global skill blocks parity; never
   replace it from an unreviewed platform-local copy.

Do not `Package` or `Deploy` a new local candidate until parity passes. A
skill-only update does not require a rebuild, but must reach Git and each
configured global skill before the next deployment.

## Parallel agents

- One branch + macOS worktree per agent. Commit/push only that branch.
- Integration agent merges/rebases reviewed branches into target.
- Windows: one worktree per branch, or one locked executor. Never switch an owned checkout.
- Before/after each Windows `sync` or pipeline action: verify clean status, branch, and HEAD. Mismatch: stop.

## Serialized native PIP scope guardrail

For every serialized `PIPScopeController`, set both directions before package:
`FVRFireArmAttachment.AttachmentInterface = controller` and
`PIPScopeController.Attachment = firearmAttachment`. Keep `SubMounts`
non-null; empty is valid. One-way wiring can fail during pick-up or mounting;
add editor assertions for both links and VR-test spawn, pick-up, detach, mount.

## Cross-session mod state

Chat memory is only a convenience; tracked mod records are source of truth for
working state. Every active mod must keep `DESIGN.md` and `DEV_STATUS.md` in
its source root. Start at [MOD_STATE_INDEX.md](../../../MOD_STATE_INDEX.md),
then read both records for the affected mod before editing.

`DEV_STATUS.md` is the handoff file. Its `Status` section contains verified
facts/evidence/blockers, never guesses. Its `Plan` section contains one active
next item and acceptance conditions. Its `Testing` section contains repeatable
checks plus required VR cases. Update it whenever work changes design,
evidence, blockers, priority, or next step; commit it with source and tests.
Create files from `docs/mod-development` templates before starting a new active
mod. For Unity work, the real project assets and matching `.meta` files must be
Git-versioned; untracked editor-only work is not progress that another session
can safely continue.

## Mandatory handoff records

Before source inspection, debugging, planning, editing, or testing a mod, read
its `DESIGN.md` and `DEV_STATUS.md` after `MOD_STATE_INDEX.md`.

Close every mod task by updating affected `DEV_STATUS.md` sections to current
verified state: release/version boundary, evidence, blocker, next action, and
test limits.
Commit and push those handoff records to GitHub with the task. Handoff records
are maintainer documentation only: never add them to Thunderstore payloads, and
never rebuild, version-bump, deploy, or publish solely for a handoff-doc change.

## Unity Reference — Only When Needed

### Restricted scope-support reference

If a locally configured, access-controlled scope-support repository exists, use
its `h3vr-supported-scope-development` skill for private PIP scopes, reflex
sights, reticles, lasers, or thermal UI. That skill and its source material are
restricted: never copy their archives, assets, screenshots, paths, guide text,
or implementation details into public repositories, public documentation, or
Thunderstore payloads. Keep any private experiment separate from release source.
For private Unity-base scope delivery, a built ZIP or loaded plugin is only a
candidate. Follow that private skill's build-root, exact-install, log, and
human Item Spawner proof gates before calling an item registered or working.

Consult `references/h3vr-modding-wiki-map.md`, its pinned wiki snapshot, and
`references/unity-content-source-roots.md` **only** when a task creates or
changes Unity content: a scene, `GameObject`, prefab, component, `MonoBehaviour`,
material, mesh, texture, audio, shader, ID, or AssetBundle. They are reference
guidance for Unity/MeatKit, Atlas, and asset-replacement work; the current
Windows repository, live assemblies, and wrapper remain authoritative.

For private vanilla-game or mod-package asset extraction and reconstruction,
also read `references/private-asset-rip-reconstruction.md` before running an
extractor or importing recovered content. It defines the local-only archive,
the boundary between recovered visuals and new behavior, and the required
Unity/MeatKit/VR proof. AssetRipper output is an inspection input, never a
claim that the full game can be rebuilt or distributed.

For established code-only or data-only work—such as `GunGameProgressions`
generation—do not read the Unity references or run a Unity workflow. Follow the
relevant code/data section below and consult the wiki only if Unity content
becomes part of the requested change.

## Choose the development route

```text
Requested change
├─ Runtime code, Harmony, configuration, or generated data
│  └─ Code/data route
└─ Prefab, scene, item, weapon, material, mesh, audio, or AssetBundle
   └─ Unity/MeatKit route — human in the loop
```

## Design guardrail — minimal, reusable changes

For every H3VR mod, prefer the smallest change that fixes the root cause and
fits the existing architecture. Extend one shared policy, resolver, lifecycle,
or framework before adding a second path. Do not add per-item exceptions,
duplicate generators, speculative features, or large rewrites when metadata or
an existing general mechanism can express the rule. Every new rule needs a
focused positive and negative regression test plus its owning design/policy
record. Keep runtime work bounded; no new hot polling, prefab materialization,
or repeated allocation on the play path unless the live API requires it.

### Code/data route

Use this route for BepInEx/Harmony plugins, data generators, configuration, and
other changes that do not author Unity content. Codex owns the source, tests,
package, deploy, and log review. The human supplies product intent and gives
explicit authorization before any public release; Unity GUI work is not part of
this route.

#### GunGame Progressions

Before changing `GunGameProgressions`, read its `DESIGN.md`, `DEV_STATUS.md`,
`GENERATION_POLICY.md`, `BRANDING.md`, package README, and focused tests.
`DESIGN.md` owns lifecycle, integration, persistence, and backlog;
`GENERATION_POLICY.md` owns shared loadout compatibility and regression cases;
`BRANDING.md` owns approved listing copy. Keep the implementation aligned with
all three; do not duplicate a compatibility rule in a separate Vanilla or
Modded generator path.

### Unity/MeatKit route — human in the loop

Use this route for authored Unity content. The goal is to minimize manual work,
not to pretend a headless editor can make visual or ergonomic decisions.

```text
Human: outcome + visual intent
  -> Codex: reference/API research, branch, code, metadata, tests
  -> Windows Unity: compile, runtime tests, MeatKit build/package
  -> Human only if needed: visual placement or subjective VR check
  -> Codex: inspect logs/package, commit, push
  -> Human: explicit publish approval
```

Human manual work is limited to visual authoring, subjective VR feel, and release approval.

| Codex owns | Human owns only when needed |
| --- | --- |
| Reference-prefab/API research; scripts; IDs; spawner/build metadata; Git; automated prefab tests; headless Unity; MeatKit build/package; package/log validation | Mesh/art choices; material appearance; visual placement of mounts/colliders/grab points; subjective handling/reload/firing feel; release approval |

Use Unity GUI for visual assets, hierarchy inspection, drag-and-drop references,
and final feel. Use batch Unity for repeatable work: compile, custom editor test
methods, AssetDatabase checks, and MeatKit builds. Batch Unity is not a visual
authoring substitute.

For a MeatKit package build, call the built-in editor entry point
`MeatKit.MeatKit.DoBuild` only after validating the intended project and build
profile. Do not rely on whichever profile was last selected in an interactive
window. If a manual action repeats, add a focused editor command and a
headless-runtime test so Codex can own it on later mods.

The MeatKit-Lite workspace is single-writer. Close Unity before pull,
branch-switch, or merge work; reopen it and wait for a complete import before
using the GUI again. Save assets in their owning project root and commit matching
`.meta` files; never hand-copy Unity assets between projects.

### Repeatable Unity edit-build-test gate

1. Before opening Unity, read the mod status, descriptor, MeatKit profile, and
   focused validation. Close Unity before every Git sync or branch operation.
2. Edit the prefab asset in Prefab Mode. Apply an intentional scene-instance
   override to the correct prefab, save, close Unity, then inspect the
   authoritative source diff. Inspector visibility alone does not prove that
   the prefab asset was saved.
3. Commit reviewed assets and matching `.meta` files. Sync the exact commit to
   Windows only with its Unity editor closed; never build a dirty or stale
   Unity checkout.
   For a historical or local-only variant that must coexist in one MeatKit
   project, move its owned assets into a uniquely named
   `Assets/Projects/<Variant>` folder while preserving their GUIDs. Do not
   clone the full project, replace shared configuration, or run a current-mod
   migration on archived assets. Build it through ignored explicit
   `-EnvironmentConfigPath` and `-ModsConfigPath` sidecars that retain the
   same project root but use a unique package and deployment folder.
4. Run `h3vr-remote run Test`, then `h3vr-remote run Build <ModName>`. Accept
   the Unity build only when it exits successfully, writes the descriptor's
   current success marker, and produces a fresh expected source ZIP. Unity 5.6
   can compile then exit before its editor method; a pre-existing ZIP is never
   proof.
5. Asset-level tests must read serialized prefab data. A cache assigned in
   `Awake` or `Start` is not serializable test evidence; inspect the serialized
   component list or run a lifecycle-aware runtime test.
6. `Build` validates the Unity source package; `Package` creates the
   `build/artifacts` archive and receipt. Only after an unchanged successful
   build may `Package` and `Deploy` use `-ReuseExistingUnityPackage`.
7. VR remains the proof for spawning, pick-up, mounting, inputs, optics, and
   feel. After the tester finishes, inspect `TailLogs` and update the status
   record before release.

## Start Every Change

1. Connect through `ssh "$H3VR_WINDOWS_HOST"` and preserve any user changes.
   Inspect `git status --short --branch` before editing; do not reset, clean,
   or overwrite unrelated files.
2. Confirm Windows `main`, local `main`, and `origin/main` share history before
   syncing or switching branches:

```powershell
git fetch origin --prune
git merge-base HEAD origin/main
git rev-list --left-right --count HEAD...origin/main
```

   No merge base means unrelated histories, not an ordinary branch conflict.
   Preserve the Windows head on a recovery branch and stop for a user decision;
   never reset, force-push, or auto-merge across that boundary.
3. Before review or edits, preserve untracked user files and check whether the
   **tracked** local checkout is clean:

```powershell
git diff --quiet
git diff --cached --quiet
```

   If both commands succeed, run `git pull --ff-only` after the topology check
   so the local branch receives remote updates. If tracked changes exist, do
   not pull, merge, reset, or overwrite them; report the divergence first.
   Untracked files alone never authorize cleanup.
4. Work on a feature branch, not `main`. Keep each intentional change focused and commit only the relevant files.
5. Run the pipeline preflight from the repository root:

```powershell
powershell.exe -ExecutionPolicy Bypass -File "$env:H3VR_WINDOWS_REPOSITORY\tools\h3vr.ps1" -Action Preflight
```

6. Check whether the decompiled cache still matches the live DLLs:

```powershell
.\tools\h3vr.ps1 -Action SourceStatus
```

Run `RefreshSource` only when `SourceStatus` is stale or the H3VR managed DLLs changed after a game update:

```powershell
.\tools\h3vr.ps1 -Action RefreshSource
.\tools\h3vr.ps1 -Action SourceStatus
```

`RefreshSource` replaces the private cache configured by
`H3VR_DNSPY_SOURCE_ROOT` with source generated from the live
`Assembly-CSharp.dll` and `Assembly-CSharp-firstpass.dll`. The repository pins
`ilspycmd` 8.2 because the Windows build environment has the .NET 7 SDK; do not
casually upgrade it to an ILSpy release that requires a newer SDK.

## Investigate the Current Game API

Use the generated source only for read-only target discovery and compatibility checks:

```powershell
.\tools\h3vr.ps1 -Action FindType -Query FVRPooledAudioSource
.\tools\h3vr.ps1 -Action FindMethod -Query FVRMovementManager.FindValidPointCurved
.\tools\h3vr.ps1 -Action GrepSource -Query UpdateWhiteOut
```

Before writing a Harmony patch, confirm `SourceStatus` is current, then inspect the target type and method signature. Register every runtime target in `build/mods.json`, then use `Verify` to catch target drift before VR testing.

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
3. Confirm source status is current, inspect the target API, and declare every Harmony `type`/`method` in `patchTargets`.
4. Add a complete descriptor to `build/mods.json`: `kind`, source project, assembly or generator, package name, deployment folder, layout, payload, and patch targets.
5. Add Thunderstore metadata and a release icon before packaging.

### Existing data mods

`GunGameProgressions` is a Python generator. Keep generation deterministic by using an explicit seed and run it in pipeline staging, never by mutating tracked release data as a test side effect:

```powershell
python .\GunGameProgressions\jsonGen.py --output <staging-output> --seed 0
```

The generator must continue to produce the expected eight pools with internally consistent IDs. A new data-only mod must also be registered in `build/mods.json` with its generator, payload, package metadata, deployment folder, and its selected package layout.

### Unity content only

Follow the Unity/MeatKit human-in-the-loop route above. Use the wiki map to
select the item, map, or asset-replacement route, then use the Unity 5.6.7f1
MeatKit-Lite workspace and matching project root from
`references/unity-content-source-roots.md`. Keep source assets and matching
`.meta` files; make moves in Unity; exclude caches and generated output.

Validate the authored object graph (components, transforms, physics, materials,
audio, IDs, mounts, and scene registration), the MeatKit build profile/build
item/dependencies/package, and the relevant in-game interaction. Use bespoke
`MonoBehaviour` scripts in the MeatKit project; use a BepInEx library only for
shared behavior. The wrapper supports tested `unity` descriptors. Normal
`Build`, `Package`, and `Deploy` invoke Unity batch mode with the descriptor's
exact build method. `Build` validates the generated Unity source package;
`Package` creates the release archive/receipt, and `Deploy` installs that
validated artifact. Use `-ReuseExistingUnityPackage` only for the exact
unchanged source/profile/descriptor state after a successful build; never use
it as normal post-edit build behavior.

Unity 5.6 may compile changed scripts then exit before `-executeMethod` runs.
For every `unity` descriptor, clear or fingerprint the expected source ZIP
before build, require its configured success-log marker, and retry batch mode
once when the first invocation only imports scripts. A pre-existing or
unchanged ZIP is never build proof. When an asset-level test fails, first check
whether it asserted a runtime cache rather than serialized prefab data.

For a custom magazine, also read `references/custom-magazines.md`. It covers
the magazine-specific reference prefab, visible rounds, feed/capacity settings,
Object ID/Item Spawner ID, bundle inclusion, and VR reload validation.

## Validate, Build, and Package

Use the helper as the single build and release interface:

```powershell
.\tools\h3vr.ps1 -Action Test
.\tools\h3vr.ps1 -Action Verify -Mod ThePing
.\tools\h3vr.ps1 -Action Build -Mod ThePing
.\tools\h3vr.ps1 -Action Package -Mod ThePing
.\tools\h3vr.ps1 -Action Deploy -Mod BubbleLevel
```

Run `Test` for every change. It executes `tests\H3vrPipeline.Tests\H3vrPipeline.Tests.csproj`; these tests cover registry parsing, package layout/metadata validation, GunGame generation, and receipt behavior. Run `Verify -Mod <name>` whenever a descriptor or Harmony patch changes. A release candidate requires successful `Test`, `Verify`, `Build`, and `Package` for that mod.

Packages are written below `build\artifacts`, staging is below `build\staging`, and generated receipts are below `build\receipts`; these are ignored by Git. Preserve the configured layout on each mod. The current `ThePing` and `GunGameProgressions` packages use verified `legacy-flat` layouts. Do not rewrite legacy releases to a `BepInEx/plugins` layout without explicitly validating the target mod's ecosystem requirements; choose the layout per descriptor.

Each package must contain valid `manifest.json`, `README.md`, `icon.png`, and every declared payload file. Check the generated package receipt for the package SHA-256, version, commit, and artifact path.

For Unity content only, also run the project-local Unity/MeatKit validation and
asset build selected by the map. When an explicit tested `unity` wrapper kind
exists, run its registered H3VR-Mods package flow. Inspect the source-package
marker/freshness after `Build`, then the artifact receipt after `Package`; do
not expect `build/artifacts` to contain a new archive after `Build` alone.
Neither layer substitutes for an in-game interaction test.

## Deploy and VR-Test

Deploy only a successfully built and packaged artifact:

```powershell
.\tools\h3vr.ps1 -Action Deploy -Mod ThePing
.\tools\h3vr.ps1 -Action Logs
.\tools\h3vr.ps1 -Action TailLogs
```

`Deploy` installs under the Default profile's `BepInEx\plugins` directory and backs up an existing mod deployment before replacing it. It also creates a `build\receipts\<mod>-<version>-<timestamp>-vrtest.md` checklist. Do not copy DLLs directly into the profile or remove a deployment outside this command.

For a descriptor with `registerWithR2modman: true`, `Deploy` also creates the
active profile's r2modman local-package cache and list entry. Manifest
`author` plus `name` must equal `deploymentFolder`, so r2modman GUI controls
the exact deployed package. Do not edit `mods.yml` by hand.

For a clean test session, archive then clear logs with:

```powershell
.\tools\h3vr.ps1 -Action ClearLogs
```

The VR tester launches the r2modman Default profile, exercises the change in H3VR, and records the outcome and notes in the generated receipt. A VR `PASS` is recommended release evidence, but it is not a publish gate. After the tester reports completion, inspect `LogOutput.log` through `TailLogs` or `Logs` for plugin-load failures, BepInEx errors, Harmony exceptions, missing dependencies, and runtime exceptions.

### Authorized remote Modded-profile launch

Normally the human starts H3VR. With explicit user authorization, Codex may
validate the installed r2modman profile by clearing logs, deriving the current
Doorstop launch arguments from the profile's private `.doorstop_version`, then
starting the Steam game URI through a short-lived Task Scheduler entry in the
active interactive console session. This reliably delivers the profile
arguments to an already-running Steam client; a direct `Steam.exe -applaunch`
task can return success without creating `h3vr.exe`. SSH service-session
launches are invalid because they do not own the desktop.

Verify the interactive H3VR process and BepInEx profile preloader in the log,
then inspect the target plugin/version and generated runtime data. Stop H3VR
and remove the temporary task immediately after testing. Derive every profile
path, active user, session ID, and task name locally; never commit or report
those private values. This automates runtime/log validation only, not visual or
subjective VR acceptance.

## Publish to Thunderstore

Publishing is deliberate and always requires explicit user authorization. It is not a CI step.

### Brand copy contract

Treat a mod's maintained short description as product/brand copy, not as a
release note. Before changing a Thunderstore manifest description, locate the
mod's canonical description source and preserve it verbatim. An operational
notice may be prefixed or suffixed, but it must never replace or reword the
canonical slogan without explicit product approval. Add a focused release test
that proves the manifest still contains the canonical text.

```powershell
.\tools\h3vr.ps1 -Action Publish -Mod ThePing -VrApproved
```

The command requires both `-Publish` and `-VrApproved`, a package built from the committed version, and the `H3VRMods:Thunderstore` Credential Manager secret. It rejects a reused or non-incremented version and surfaces TCLI/API failures instead of treating them as published. Never print, commit, transfer, or otherwise expose the publish token.

## Remote Work and CI

When a macOS agent needs to transfer a small change, stage it outside the repository and use SHA-verified transfer with an atomic replacement on Windows. The local `h3vr-remote` helper supports `run`, `fetch`, and `push`; it is a local convenience tool, not repository source.

GitHub Actions runs `.github/workflows/verify.yml` only on hosted Linux and
only for its scoped portable source/data paths. It must never call
`h3vr-remote`, a remote shell, Windows PowerShell, Unity, MeatKit, H3VR,
credentials, package, deploy, or publish tools. It cannot restore game-linked
packages, perform live source validation, or establish release evidence.
Windows `Test`, `Build`, `Package`, preflight, and `Verify` remain explicit
manual/runtime gates before a real release.

## Completion Checklist

Before requesting review or merging a development branch:

- [ ] `git status` contains only the intended changes.
- [ ] macOS `HEAD`, GitHub branch, and Windows `HEAD` are the same commit.
- [ ] Every changed user-global H3VR skill has a matching reviewed SHA-256 on
      each configured platform.
- [ ] `SourceStatus` is current; source was refreshed after the last managed-DLL change when applicable.
- [ ] Every new or changed Harmony target passes `Verify`.
- [ ] For Unity content, the wiki map route was selected; source assets and
      matching `.meta` files are committed, and Unity caches/generated bundles
      are excluded.
- [ ] For Unity content, its MeatKit build profile/build items/dependencies and
      generated package are validated. A wrapper release payload is registered
      only through an explicit tested `unity` build kind.
- [ ] For Unity content, automated Unity checks cover all repeatable behavior;
      any remaining visual or subjective VR check is stated explicitly rather
      than implied by a successful batch build.
- [ ] Unity VR testing covers the authored object or scene's real interaction,
      lifecycle, and (for weapons) loading, controls, firing, and impact flow.
- [ ] `Test`, `Build`, and `Package` passed for each affected mod.
- [ ] Package receipt has the expected SHA-256, version, and payload layout.
- [ ] Deployment used `Deploy`; retain the VR receipt when a VR test is performed.
- [ ] BepInEx logs show no unresolved plugin or Harmony errors after VR testing, when performed.
- [ ] A Thunderstore publish, if requested, used explicit authorization and `-VrApproved`.
