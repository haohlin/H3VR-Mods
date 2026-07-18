# GunGame Progressions — Development Status

Read [DESIGN.md](DESIGN.md) for lifecycle. Read
[GENERATION_POLICY.md](GENERATION_POLICY.md) for loadout rules. This file is
cross-session handoff source of truth.

## Status

Last verified: `2026-07-18`

State: `1.4.0 released on Thunderstore; Modded optic-policy candidate awaiting Windows validation`

| Area | Verified fact | Evidence |
| --- | --- | --- |
| Public release | `1.4.0` is published. | Exact Thunderstore download URL returned HTTP `200`. |
| Golden Vanilla | Tracked offline Rot and Mixed files contain `661` unique non-Slingshot firearms. | Focused baseline test; both profiles. |
| Installed runtime before candidate | Vanilla profiles contain `657`; Modded profiles contain `47`. | Windows installed profile inspection. |
| Vanilla count root cause | Five golden weapons were rejected by catalog identity gate; one newer valid firearm was present, yielding net `657`. | Installed-vs-golden diff. |
| Modded count root cause | Valid G28 variants declare direct magazines and Picatinny but omit optional identity tags, so same gate rejects them. | Installed runtime catalog inspection. |
| Memory boundary | Capture reads `FVRObject` catalog only. No `GetGameObject*` call is allowed. | Source test; previous Windows A/B eliminated large prefab residency path. |
| Candidate generator | Catalog proof now accepts identity tags, direct compatible feed, exact nonzero magazine/clip interface, or `GravitonBeamer`; Modded cartridge fallback needs direct compatibility. | Windows test suite: `83/83` passed. |
| Candidate persistence | First Modded pair writes; later pair replaces only when strictly larger; confirmed loader-complete empty snapshot removes stale files. | Windows test suite: `83/83` passed. |
| Candidate package | `1.4.0` built and deployed. | Windows `Verify`, `Build`, `Package`, and `Deploy` passed. |
| One-minute implementation | Startup schedules one-, five-, and ten-minute one-shot Modded snapshots and logs each completed scan's total duration plus captured entry count. | Focused regression test; Windows pipeline `83/83` passed. |
| Real Modded-profile run | H3VR loaded the intended profile and this plugin with its dependencies; no exporter exception occurred. | BepInEx live log. |
| Live profile result | Runtime Vanilla contains `662` firearms: every golden `661` plus one newer valid firearm. Runtime Modded contains `54`; G28 variants use direct magazines and catalog scopes. | Generated-pool and catalog inspection. |
| Live memory result | No runaway growth during the catalog-only candidate run; loader memory settled after loading. | Windows process observation. |
| Launch transport | A direct `Steam.exe -applaunch` Task Scheduler action can return success without creating `h3vr.exe`. Steam game URI with the same r2modman Doorstop profile arguments launches the interactive game process reliably. | Windows interactive-session launch comparison. |
| One-minute runtime result | Initial Modded scan: `867 ms`, `2` entries while loaders registered. Scheduled one-minute scan: `1,053 ms`, `1,435` entries and saved pools. | Real Modded-profile BepInEx log. |
| External preloader messages | Deli/MonoMod messages appeared in both successful and incomplete attempts; they are not evidence that GunGame Progressions failed. | Successful historical and current log comparison. |
| Release boundary | Release source, skill guidance, and handoff record are on `main`; package was rebuilt from `main` before publish. | Windows Git, Test, Verify, Build, Package, and Thunderstore publish evidence. |
| Compatibility candidate | Runtime 05 tests former exclusions; `Slingshot` remains sole blacklist. Modded normal/probe pools use safe universal optic policy: exact route first, Modded reflex/RMR allowed, curated vanilla RMR/red-dot/low-power/LPVO fallback only. | Windows `SourceStatus`, `Verify`, `Test` `88/88`, `Build`, `Package`, and `Deploy` passed on feature commit `dc3d374`. |
| Offline fallback | Both tracked Vanilla pools remain byte-equivalent after regeneration from shared policy version `15`; the known-good Vanilla content did not change. | Windows `OfflineProfileGenerator`, clean Git diff after staging refresh, then full Windows test suite `88/88`. |
| Runtime log limitation | Configured Default profile has no BepInEx log file yet. | `Preflight` fails only its log-path check; package deployment succeeded. Launch profile once before log monitoring. |
| Live Compatibility Probe | Runtime 05 generated `46` test firearms from a `1,148`-entry Modded capture. | Live BepInEx receipt and trace: `compatibility probe updated: 46 test firearms.` |
| Live GunGame failure | `GrappleGun` with direct `MagazineGrappleGun` fails GunGame chamber loading, then throws from `WeaponBuffer.SpawnImmediate` before optic mounting. | Live BepInEx: `Error while trying to load gun chambers manually for a gun: GrappleGun(Clone)` plus `NullReferenceException` / `KeyNotFoundException` stack traces. |
| Safety boundary | Spawn safety rejects unavailable IDs and wrong categories, but cannot prove GunGame-specific chamber/load semantics from catalog metadata alone. | `GrappleGun` passes catalog feed/category validation yet fails upstream GunGame spawn. |
| Unrelated loader noise | Failed pool-file loads belong to a separate installed map package, not GunGame Progressions. | Live log paths identify that package's `GunGameWeaponPool_*.json` files. |
| Runtime 05 overlap | `44/46` Runtime 05 firearms already exist in released `1.4.0` Vanilla fallback. Only `COOLCLOSEDBOLT` and `JunkyardFlameThrower` are absent. | Current Runtime 05 JSON compared with release commit `c713b72` Vanilla pool. |
| MP5 metadata gap | MP5/SP5 entries expose a compatible adapter but no firearm mount tag or adapter output tag. Candidate resolver reads explicit adapter `PhysicalMountTypes` and selects exact vanilla `Scope_G3SG1` for `MP5RailMount`; it never reads the adapter prefab. | Live `ObjectData.json`: `MP5PicatinnyAdapter` is `Adapter` on `MP5RailMount`, with empty `ProvidedMountTypes`; `Scope_G3SG1` is the verified scope-class entry for that mount. |
| MP5 duplication | Released Vanilla has `29` MP5/SP5 variants; Runtime 05 has `19` more duplicates. | Release/current pool audit. |
| Modded optic gap | Existing fallback selected any catalog `Scope`, including Modded/high-power scopes, and ignored reflex/RMR role fallback. Some Modded VAL entries still omit all mount metadata. | Source plus live catalog audit. Metadata-empty guns cannot be identified as Russian without unsafe name/prefab inference. |
| Safe Modded optic candidate | Modded magnified scopes are rejected. Exact Russian routes select vanilla PSO-1 `MagnifierPSO1`; this one legacy object ID is normalized to `Scope`. Unresolved handgun, CQC, and rifle routes select curated vanilla RMR, red-dot/low-power, and LPVO/low-power candidates. Explicit MP5 adapter metadata resolves `MP5RailMount` to `Scope_G3SG1`. | H3VR scope reference confirms PSO-1 is a 4x side-rail scope; classifier keeps every other magnifier excluded. Candidate awaits Windows build/test/deploy. |
| No runtime prefab materialization | Spawn safety now mounts only `Extra` objects spawned by GunGame. It no longer creates adapters or replacement optics. | Source regression rejects `GetGameObject*` and `UnityEngine.Object.Instantiate` in `GunGameSpawnSafety.cs`. |

## Plan

| State | Next item | Acceptance |
| --- | --- | --- |
| Complete | Sync candidate to Windows; run `SourceStatus`, `Test`, `Verify`, `Build`, `Package`, and `Deploy`. | All checks passed; Windows test suite `83/83`. |
| Complete | Validate one-minute rescan and timing log in the real Modded profile. | Initial and one-minute scan logs recorded; capture remains frame-budgeted and profiles are valid. |
| Pending | VR smoke test G28/direct-magazine + Picatinny scope, non-box shotgun shells, and invalid-mod skip. | No wrong loose cartridge, wrong magazine, mount mismatch, exception, or progression crash. |
| Complete | Merge release source to `main`; publish Thunderstore `1.4.0`. | Main updated; package upload finalized; exact download resolves. |
| Pending | Optional VR handling smoke test. | Human checks G28/direct magazine, scope, shotguns, and progression feel. |
| In progress | VR-test Runtime 05 and global Picatinny fallback. | Launch configured profile once, inspect BepInEx log, cycle every Runtime 05 weapon; record spawn/feed/physical-optic failures. No exception, wrong feed, or runaway memory. |
| Pending decision | Classify confirmed Runtime 05 failures before allowing them in normal pools. | Keep each failed firearm testable in Runtime 05; exclude it from normal pools only with recorded live evidence and regression test. |
| Pending design | Cap near-identical firearm variants, beginning with MP5/SP5. | Approve generic catalog-signature grouping or an explicit family rule; preserve two representative variants if requested. |
| In progress | Build/deploy metadata-only Modded optic route. | Windows Test, Verify, Build, Package, Deploy pass; no `GetGameObject*`/runtime instantiate path remains. Then VR: Modded handgun gets RMR; Picatinny rifle gets vanilla low-power/LPVO; Russian rail gets PSO-1 `MagnifierPSO1`; MP5 adapter route gets `Scope_G3SG1`; no duplicate optic or spawn exception. |

## Testing

| Check | Command / action | Expected |
| --- | --- | --- |
| Game API | `h3vr.ps1 -Action SourceStatus` | Current. |
| Focused/full pipeline | `h3vr.ps1 -Action Test` | Golden count, catalog proof, cartridge-negative, persistence, and existing regressions pass. |
| Harmony targets | `h3vr.ps1 -Action Verify -Mod GunGameProgressions` | Kodeman targets resolve. |
| Release artifact | `h3vr.ps1 -Action Build -Mod GunGameProgressions`; `Package` | `1.4.0` package with no player Modded pool. |
| One-minute runtime generation | Deploy, launch the Modded profile through an interactive Steam URI task, inspect BepInEx log. | Passed: `startup 1-minute rescan requested`; initial `867 ms`/`2` entries; one-minute `1,053 ms`/`1,435` entries. |
| Published artifact | Request exact version download URL. | Passed: redirected download resolves HTTP `200`. |
| Manual VR | G28; mod rifle with no direct feed; Russian/proprietary mount; pump/break shotgun; GunGame reload. | Direct/exact gear only; unsafe object skipped; saved Modded pair persists. |
| Compatibility candidate | Deployed safe-optic candidate; launch configured profile, select Modded pool and Runtime 05, then inspect log. | Each candidate either works with correct feed/optic behavior or is recorded with exact failure. `Slingshot` never appears. |
| Offline fallback refresh | `OfflineProfileGenerator`, then `--verify`. | Both tracked Vanilla pools match policy version `15` and remain unchanged before any release. |

Do not run H3VR tests/builds on macOS. Do not package `DESIGN.md`,
`DEV_STATUS.md`, or `GENERATION_POLICY.md`.
