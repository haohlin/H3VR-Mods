# BubbleLevel Development Status

`DEV_STATUS.md` is the cross-session handoff file. Status, plan, and testing
stay together here.

## Status

Last verified: `2026-07-17`
State: `active; 2.0.3 remains last runtime-verified release`

### Handoff convention

Since `2026-07-15`, this file is the sole BubbleLevel handoff record. It
replaces split `STATUS.md`, `PLAN.md`, and `TESTING.md` files.

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Unity source | In-place `Assets/Projects` Git root in `H3VR-unity-projects`; matching origin `main` commit | Versioned. |
| Controller | Current gravity/damping controller, prefabs, `.meta`, test tools, build profile | Versioned in Unity repository. |
| Shared controller migration | Unity source commit `e9fc5b9` prepares BubbleLevelSet `2.0.4`: it owns `GravityBubbleLevelController` for rail and NightForce scope behavior. NightForce `1.0.5` declares `BubbleLevelSet-2.0.4` as an external dependency rather than embedding its types. Wrapper script GUIDs and serialized field names remain unchanged. | Source changed; Windows Unity/runtime validation pending. |
| Pipeline wrapper | Windows `h3vr-remote run Test` passed `85/85`; preflight reports generated source current. | Passed. |
| Unity runtime checks | Licensed Unity batch `RunAll` passed on 2026-07-14 | Passed. |
| Sensitivity | Center `-0.008400`; 1° `0.095774` = `0.104174` travel; 4° reaches stop | Passed. |
| 180° rail | Local positions `0.095774` / `-0.107402`; world movement dot `1.000000` | Passed. |
| Package | MeatKit `BubbleLevelSet` `2.0.3` rebuilt on 2026-07-14 from exact profile | Passed. |
| Wrapper package/deploy | Wrapper validated package, backed up old install, and deployed `2.0.3` with receipt | Passed. |
| Deployment / VR | User confirmed `2.0.3` works in H3VR, including corrected reverse/180° rail behavior | Passed. |
| Thunderstore | `HLin_Mods-BubbleLevelSet` `2.0.3` published from fresh validated package | Published. |
| Documentation | Source `README.md` and standalone `CHANGELOG.md` expanded; package title, short description, build profile, and `2.0.3` package remain unchanged | Published to Unity source `main`. |
| Materials | CC0 preview renders exist; production prefabs unchanged | Awaiting approval. |

### Next

Monitor release feedback. Material variants remain optional until visual approval.

### Resume boundary

1. `2.0.3` is current public runtime/package release. Later documentation is
   source-only; do not rebuild, deploy, or publish it by itself.
2. New runtime/content work starts on Windows: Unity `RunAll`, wrapper
   `Test`/`Build`/`Package`, deployment, then recorded VR result.
3. First candidate work: material approval or expanded VR coverage for offset
   rail, 30 mm mounts, nested chain, inversion, and settle behavior.

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Version current MeatKit-Lite BubbleLevel project in `H3VR-unity-projects`. | In-place `Assets/Projects` Git root tracks scripts, assets, build profile, tests, and `.meta`; caches/bundles excluded. |
| `[x]` | Run licensed Unity checks, including 180° rail mount. | 1° movement `0.104174`; 4° saturates; limits/damping/nested/reverse-mount tests pass. |
| `[x]` | Add repeatable Unity/MeatKit build and deploy integration. | Exact profile package validated; deploy receipt created without manual file copy. |
| `[x]` | Validate release-critical H3VR behavior. | User confirmed deployed `2.0.3` works in-game, including corrected reverse/180° rail behavior. |
| `[x]` | Document BubbleLevelSet usage and release history. | Unity source `README.md` provides contents, behavior, use, and compatibility; `CHANGELOG.md` provides full version history without changing packaged `2.0.3`. |
| `[x]` | Keep `2.0.3` handoff ready. | `DESIGN.md` and `DEV_STATUS.md` state current release, source-only documentation boundary, verified behavior, and first resume action. |
| `[x]` | Consolidate handoff state. | `DEV_STATUS.md` holds Status, Plan, and Testing; legacy split files removed. |
| `[>]` | Validate shared BubbleLevel/NightForce gravity-controller migration. | Windows Unity BubbleLevel `2.0.4` and NightForce `1.0.5` runtime tests, MeatKit package-boundary checks, and H3VR regression cases pass; `2.0.3` remains last runtime-verified release until then. |
| `[ ]` | Expand post-release H3VR regression coverage. | Offset rail, 30 mm mounts, nested chain, inversion, and settle behavior recorded when practical. |
| `[ ]` | Decide material candidate. | Explicit approval/rejection; approved path has applied-proof render. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | New geometry/material variants | Existing physical behavior and source control first. |

## Testing

### Automated Unity / MeatKit

| Check | Entry point | Pass evidence |
| --- | --- | --- |
| Controller and prefab checks | `HLin Mods > BubbleLevel > Run all runtime tests` | Log contains `[BubbleLevelRuntime] PASS`. |
| Sensitivity calibration | `HLin Mods > BubbleLevel > Calibrate rail prefab for sniper sensitivity` | 1° and 4° assertions pass. |
| Candidate art preview | `HLin Mods > BubbleLevel > Visual review > Render CC0 candidate previews` | Two preview PNGs; production material signatures unchanged. |
| Package | `HLin Mods > BubbleLevel > Build BubbleLevel package` | Exact profile builds expected ZIP and MeatKit reports completion. |
| Fresh batch build | `h3vr.ps1 -Action Build -Mod BubbleLevel` | Wrapper deletes prior ZIP, sees MeatKit completion marker, retries once only after script import. |

### H3VR acceptance

| Case | Expected result |
| --- | --- |
| Level rail, left/right cant | Bubble follows gravity, centers level, stops at travel limits. |
| Nested rail/attachment chain | Same gravity direction after each mounting layer. |
| Bidirectional rail, same world cant | Normal and 180° mount settle on same world uphill side; local-X signs are opposite. |
| Upside-down / rapid handling | Bubble stays in tube; no jitter, escape, or exception. |
| 30 mm black and tan mounts | Bubble and cosine indicator behave without controller conflict. |
| Spawn/attach/reload scene | Every listed item spawns, mounts, detaches, and survives scene lifecycle. |

### Evidence and release boundary

Record package version/hash, deployment receipt, BepInEx log result, and VR
outcome in this file's Status section. Unity editor checks do not prove H3VR
runtime behavior. Use `-ReuseExistingUnityPackage` only to deploy a previously
validated package; normal `Build`, `Package`, and `Deploy` invoke Unity batch
mode.

`2.0.3` was freshly packaged through MeatKit, published to Thunderstore, and
confirmed in H3VR by the user. Reverse/180° rail gravity response is verified
release-critical regression case.

Documentation follow-up changed Unity-source Markdown only: `README.md` and
`CHANGELOG.md`. Build profile version, package title, approved short
description, item assets, and published `2.0.3` ZIP were unchanged; no Unity or
MeatKit rebuild is required for that source-documentation update.

Shared controller source was added after `2.0.3`: local structural checks and
diff whitespace checks pass, but no local .NET/Unity compiler or private Windows
configuration is available. The new NightForce runtime test and existing
BubbleLevel runtime test must both run before treating it as package or VR
evidence.

`2.0.3` is last runtime-verified package. A future session must not treat the
source-only documentation commit as build or VR evidence.
