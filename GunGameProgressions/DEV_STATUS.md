# GunGame Progressions — Development Status

Read [DESIGN.md](DESIGN.md) for lifecycle. Read
[GENERATION_POLICY.md](GENERATION_POLICY.md) for loadout rules. This file is
cross-session handoff source of truth.

## Status

Last verified: `2026-07-16`

State: `1.4.0 runtime validated in the real Modded profile; release ready`

| Area | Verified fact | Evidence |
| --- | --- | --- |
| Public release | `1.3.9` is published. | Thunderstore/package source. |
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

## Plan

| State | Next item | Acceptance |
| --- | --- | --- |
| Complete | Sync candidate to Windows; run `SourceStatus`, `Test`, `Verify`, `Build`, `Package`, and `Deploy`. | All checks passed; Windows test suite `83/83`. |
| Complete | Validate one-minute rescan and timing log in the real Modded profile. | Initial and one-minute scan logs recorded; capture remains frame-budgeted and profiles are valid. |
| Pending | VR smoke test G28/direct-magazine + Picatinny scope, non-box shotgun shells, and invalid-mod skip. | No wrong loose cartridge, wrong magazine, mount mismatch, exception, or progression crash. |
| In progress | Merge release source to `main`; publish Thunderstore `1.4.0`. | Package/manifest/changelog/version agree; Thunderstore exact version URL resolves. |

## Testing

| Check | Command / action | Expected |
| --- | --- | --- |
| Game API | `h3vr.ps1 -Action SourceStatus` | Current. |
| Focused/full pipeline | `h3vr.ps1 -Action Test` | Golden count, catalog proof, cartridge-negative, persistence, and existing regressions pass. |
| Harmony targets | `h3vr.ps1 -Action Verify -Mod GunGameProgressions` | Kodeman targets resolve. |
| Release artifact | `h3vr.ps1 -Action Build -Mod GunGameProgressions`; `Package` | `1.4.0` package with no player Modded pool. |
| One-minute runtime generation | Deploy, launch the Modded profile through an interactive Steam URI task, inspect BepInEx log. | Passed: `startup 1-minute rescan requested`; initial `867 ms`/`2` entries; one-minute `1,053 ms`/`1,435` entries. |
| Manual VR | G28; mod rifle with no direct feed; Russian/proprietary mount; pump/break shotgun; GunGame reload. | Direct/exact gear only; unsafe object skipped; saved Modded pair persists. |

Do not run H3VR tests/builds on macOS. Do not package `DESIGN.md`,
`DEV_STATUS.md`, or `GENERATION_POLICY.md`.
