# GunGame Progressions — Development Status

Read [DESIGN.md](DESIGN.md) for lifecycle. Read
[GENERATION_POLICY.md](GENERATION_POLICY.md) for loadout rules. This file is
cross-session handoff source of truth.

## Status

Last verified: `2026-07-16`

State: `1.4.0 candidate validated in the real Modded profile; one-minute rescan and timing evidence pending Windows rebuild/deploy before publication`

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
| Real Modded-profile run | H3VR loaded the intended profile and this plugin with its dependencies; no exporter exception occurred. | BepInEx live log. |
| Live profile result | Runtime Vanilla contains `662` firearms: every golden `661` plus one newer valid firearm. Runtime Modded contains `54`; G28 variants use direct magazines and catalog scopes. | Generated-pool and catalog inspection. |
| Live memory result | No runaway growth during the catalog-only candidate run; loader memory settled after loading. | Windows process observation. |

## Plan

| State | Next item | Acceptance |
| --- | --- | --- |
| Complete | Sync candidate to Windows; run `SourceStatus`, `Test`, `Verify`, `Build`, `Package`, and `Deploy`. | All checks passed; Windows test suite `83/83`. |
| In progress | Add and validate one-minute Modded rescan plus concise timing log. | Real Modded H3VR log records the one-minute scan; capture remains frame-budgeted and profiles remain valid. |
| Pending | VR smoke test G28/direct-magazine + Picatinny scope, non-box shotgun shells, and invalid-mod skip. | No wrong loose cartridge, wrong magazine, mount mismatch, exception, or progression crash. |
| Pending | Merge release source to `main`; publish Thunderstore `1.4.0`. | Package/manifest/changelog/version agree; Thunderstore exact version URL resolves. |

## Testing

| Check | Command / action | Expected |
| --- | --- | --- |
| Game API | `h3vr.ps1 -Action SourceStatus` | Current. |
| Focused/full pipeline | `h3vr.ps1 -Action Test` | Golden count, catalog proof, cartridge-negative, persistence, and existing regressions pass. |
| Harmony targets | `h3vr.ps1 -Action Verify -Mod GunGameProgressions` | Kodeman targets resolve. |
| Release artifact | `h3vr.ps1 -Action Build -Mod GunGameProgressions`; `Package` | `1.4.0` package with no player Modded pool. |
| Runtime generation | Deploy, launch once, inspect generated pool IDs/counts and BepInEx log. | No prefab materialization, no periodic polling, no generation exception; one-minute timing line appears. |
| Manual VR | G28; mod rifle with no direct feed; Russian/proprietary mount; pump/break shotgun; GunGame reload. | Direct/exact gear only; unsafe object skipped; saved Modded pair persists. |

Do not run H3VR tests/builds on macOS. Do not package `DESIGN.md`,
`DEV_STATUS.md`, or `GENERATION_POLICY.md`.
