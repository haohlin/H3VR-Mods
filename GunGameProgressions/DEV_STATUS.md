# GunGame Progressions — Development Status

Read [DESIGN.md](DESIGN.md) for lifecycle. Read
[GENERATION_POLICY.md](GENERATION_POLICY.md) for loadout rules. This file is
cross-session handoff source of truth.

## Status

Last verified: `2026-07-19`

State: `1.4.0 released on Thunderstore; policy-19 candidate passed Windows validation/package; deployment waits for H3VR to close`

| Area | Verified fact | Evidence |
| --- | --- | --- |
| Public release | `1.4.0` is published. | Exact Thunderstore download URL returned HTTP `200`. |
| Golden Vanilla | Policy 19 tracked offline Rot and Mixed files contain `660` unique firearms. BrownBess is removed by the new global blacklist. | Windows generator, `--verify`, and focused baseline test. |
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
| Policy 19 | Global blacklist now excludes Slingshot, BrownBess, Degle, JunkyardFlameThrower, LaserPistol, MF_Flamethrower, and Stinger from every generated profile. Runtime-only curation still removes known unsafe/duplicate candidates. | Windows `Test` `95/95`; policy-19 fallback regeneration. |
| Spawn safety | Invalid pre-buffer data, immediate spawn errors, and iterator-time buffer errors now clear the loadout, log gun/feed/optic IDs, and promote next frame. | Windows `Test` `95/95`, `Verify`, `Build`, `Package`; H3VR VR runtime validation pending. |
| Policy 19 package | Version `1.4.0` package built from `8c88569`. | Windows `Verify`, `Build`, `Package`; SHA-256 `942A97C7C553B6EDDD53704EA3EE7BE9CB3F480B27E6923B43B131A42D24403B`. |
| Runtime 05 promotion | GravitonBeamer, Jackhammer, M320GrenadeLauncher, M72A7, MF_Signaler, MF_Syringegun, OTS38, P11, P6Twelve, PocketHammer1903, and Whizzbanger passed human testing and already occur in both standard Vanilla profiles through the shared resolver. They need no blacklist or allowlist change. | Generated Vanilla pool audit. |
| Runtime 05 worklist | Remaining test candidates: Airgun, Flaregun, MF_Medical180, Pocket1906, Quackenbush1886. Probe-list fingerprinting refreshes this profile only after deployment. | Source change; Windows validation pending. |
| Deployment blocker | H3VR process remains open on Windows. | Live process check before deployment. |
| Offline fallback | Both tracked Vanilla pools remain byte-equivalent after shared policy version `18`; known-good Vanilla content did not change. | Windows `Verify`, `Test` `95/95`, `Build`, and `Package`. |
| Runtime log limitation | Configured Default profile has no BepInEx log file yet. | `Preflight` fails only its log-path check; package deployment succeeded. Launch profile once before log monitoring. |
| Prior live Compatibility Probe | Before runtime exclusions, Runtime 05 generated `46` test firearms from a `1,148`-entry Modded capture. | Historical BepInEx receipt and trace. |
| Live GunGame failure | `GrappleGun` with direct `MagazineGrappleGun` fails GunGame chamber loading, then throws from `WeaponBuffer.SpawnImmediate` before optic mounting. | Live BepInEx: `Error while trying to load gun chambers manually for a gun: GrappleGun(Clone)` plus `NullReferenceException` / `KeyNotFoundException` stack traces. |
| Safety boundary | Spawn safety rejects unavailable IDs and wrong categories, but cannot prove GunGame-specific chamber/load semantics from catalog metadata alone. | `GrappleGun` passes catalog feed/category validation yet fails upstream GunGame spawn. |
| Unrelated loader noise | Failed pool-file loads belong to a separate installed map package, not GunGame Progressions. | Live log paths identify that package's `GunGameWeaponPool_*.json` files. |
| Prior Runtime 05 overlap | Before exclusions, `44/46` Runtime 05 firearms also existed in released `1.4.0` Vanilla fallback. | Historical Runtime 05 JSON audit. |
| MP5 metadata gap | MP5/SP5 entries expose a compatible adapter but no firearm mount tag or adapter output tag. Candidate resolver reads explicit adapter `PhysicalMountTypes` and selects exact vanilla `Scope_G3SG1` for `MP5RailMount`; it never reads the adapter prefab. | Live `ObjectData.json`: `MP5PicatinnyAdapter` is `Adapter` on `MP5RailMount`, with empty `ProvidedMountTypes`; `Scope_G3SG1` is the verified scope-class entry for that mount. |
| MP5 duplication | Offline Vanilla keeps its known-good variants; all 19 requested MP5/SP5 variants are excluded from Runtime 02/04/05. | Exact runtime blacklist regression; metadata-only Runtime 05 audit. |
| Modded optic gap | Existing fallback selected any catalog `Scope`, including Modded/high-power scopes, and ignored reflex/RMR role fallback. Some Modded VAL entries still omit all mount metadata. | Source plus live catalog audit. Metadata-empty guns cannot be identified as Russian without unsafe name/prefab inference. |
| Safe Modded optic candidate | Modded magnified scopes are rejected. Exact Russian routes select vanilla PSO-1 `MagnifierPSO1`; this one legacy object ID is normalized to `Scope`. Unresolved handgun, CQC, and rifle routes select curated vanilla RMR, red-dot/low-power, and LPVO/low-power candidates. Explicit MP5 adapter metadata resolves `MP5RailMount` to `Scope_G3SG1`. | H3VR scope reference confirms PSO-1 is a 4x side-rail scope; classifier keeps every other magnifier excluded. Windows suite `90/90`, Verify, Build, Package, and Deploy passed on `8b73ff1`. |
| No runtime prefab materialization | Spawn safety now mounts only `Extra` objects spawned by GunGame. It no longer creates adapters or replacement optics. | Source regression rejects `GetGameObject*` and `UnityEngine.Object.Instantiate` in `GunGameSpawnSafety.cs`; Windows test `90/90` passed. |
| Runtime-cost boundary | Modded capture remains two-millisecond metadata slices. One cached OtherLoader probe is read per request; delayed selector-event reflection retries every ten seconds; metadata merge/build/write run below-normal. Modded refresh indexes Vanilla metadata for compatibility but does not rebuild Vanilla weapon lists. | Source guard plus Windows `93/93` test suite. |
| Local scope audit | Both tracked 660-gun Vanilla pools have `0` empty `Extra` entries. Metadata-only Runtime 05 test confirms Airgun is emitted with a nonempty optic. | Windows `Test` `95/95`. |
| Runtime 05 release baseline | Release policy 18 configured `54` candidate IDs, including five diagnostic overrides. Policy 19 removes bypasses and applies global exclusions. | Historical local metadata report; policy-19 Windows audit pending. |
| Policy replacement | A complete candidate from a newer policy version replaces a saved Modded pair once even when safety exclusions make it smaller; partial/empty safeguards remain. | Focused persistence regression; Windows `Test` `95/95`. |
| Current build candidate | Feature-branch package remains version `1.4.0`; deployed for private VR validation, not a public release. | Windows `Verify`, `Test` `95/95`, `Build`, `Package`, and `Deploy` passed. |
| Global startup warmup | The existing immediate and 1/5/10-minute Modded refresh schedule now has one idempotent owner invoked from plugin `Awake()`; `Start()` is only a no-op fallback. It no longer depends on any GunGame scene being opened. | Source-level lifecycle regression updated; Windows validation pending because current remote SSH host-key verification fails. |

## Plan

| State | Next item | Acceptance |
| --- | --- | --- |
| Complete | Sync candidate to Windows; run `SourceStatus`, `Test`, `Verify`, `Build`, `Package`, and `Deploy`. | All checks passed; Windows test suite `83/83`. |
| Complete | Validate one-minute rescan and timing log in the real Modded profile. | Initial and one-minute scan logs recorded; capture remains frame-budgeted and profiles are valid. |
| Pending | VR smoke test G28/direct-magazine + Picatinny scope, non-box shotgun shells, and invalid-mod skip. | No wrong loose cartridge, wrong magazine, mount mismatch, exception, or progression crash. |
| Complete | Merge release source to `main`; publish Thunderstore `1.4.0`. | Main updated; package upload finalized; exact download resolves. |
| Pending | Optional VR handling smoke test. | Human checks G28/direct magazine, scope, shotguns, and progression feel. |
| In progress | Validate and deploy narrowed Runtime 05. | H3VR is closed; Runtime 05 contains only five untested candidates. Confirmed firearms stay in ordinary Vanilla output; no Vanilla/Modded rebuild occurs solely from probe-list change. |
| Pending decision | Classify remaining Runtime 05 failures from spawn-safety log. | Add only confirmed broken IDs to shared global blacklist with focused regression. |
| Pending design | Cap near-identical firearm variants, beginning with MP5/SP5. | Approve generic catalog-signature grouping or an explicit family rule; preserve two representative variants if requested. |
| Complete | Build/deploy metadata-only Modded optic route. | Windows SourceStatus, Test `90/90`, Verify, Build, Package, Deploy passed. |
| Complete | Keep runtime generation metadata-only and remove avoidable hot-path work. | Windows Test `93/93`, Verify, Build, Package passed. No pool policy or release version changed. |
| Complete | Audit packaged local metadata scope coverage, including Runtime 05. | Two Vanilla pools: 661/661 scopes; current Runtime 05 audit: 22/22 scopes. |
| Complete | Apply release-policy Runtime 02/04/05 exclusions and Runtime 05 forced tests. | Windows `95/95`, Verify, Build, Package, Deploy; historical policy 18 evidence only. |
| Pending | Human VR-test policy 19 Runtime 05. | Airgun appears; globally blacklisted IDs stay absent; a bad loadout advances instead of stalling/crashing. |
| Pending | VR-test metadata-only Modded optic route. | Modded handgun gets RMR; Picatinny rifle gets vanilla low-power/LPVO; Russian rail gets PSO-1 `MagnifierPSO1`; MP5 adapter route gets `Scope_G3SG1`; no duplicate optic, loose replacement, or spawn exception. |
| In progress | Validate game-wide startup warmup. | Start H3VR, remain outside GunGame past one minute, then open/reload GunGame. BepInEx shows initial and scheduled Modded scans; saved Modded pair is selectable. |

## Testing

| Check | Command / action | Expected |
| --- | --- | --- |
| Game API | `h3vr.ps1 -Action SourceStatus` | Current. |
| Focused/full pipeline | `h3vr.ps1 -Action Test` | Passed `95/95`: global/runtime blacklist, no Runtime 05 bypass, Airgun audit, iterator skip, and existing regressions. |
| Harmony targets | `h3vr.ps1 -Action Verify -Mod GunGameProgressions` | Kodeman targets resolve. |
| Release artifact | `h3vr.ps1 -Action Build -Mod GunGameProgressions`; `Package` | Passed: `1.4.0` package with no player Modded pool. |
| One-minute runtime generation | Deploy, launch the Modded profile through an interactive Steam URI task, inspect BepInEx log. | Passed: `startup 1-minute rescan requested`; initial `867 ms`/`2` entries; one-minute `1,053 ms`/`1,435` entries. |
| Game-wide warmup regression | `h3vr.ps1 -Action Test`, then start H3VR in any non-GunGame mode and wait at least one minute before opening GunGame. | Startup scheduler runs from `Awake()` once; initial and one-minute scans run without selector interaction; selector restores generated pair after reload. |
| Published artifact | Request exact version download URL. | Passed: redirected download resolves HTTP `200`. |
| Manual VR | G28; mod rifle with no direct feed; Russian/proprietary mount; pump/break shotgun; GunGame reload. | Direct/exact gear only; unsafe object skipped; saved Modded pair persists. |
| Compatibility candidate | Deploy policy 19, select Runtime 05, then inspect log. | Airgun appears; global/runtime blacklists absent; each emitted entry has nonempty `Extra`; rejected loadout logs IDs and promotes without crash/stall. |
| Offline fallback refresh | `OfflineProfileGenerator`, then `--verify`. | Passed: both tracked Vanilla pools match policy version `19`; count is `660`. |
| Local Runtime 05 audit | `dotnet run --project GunGameProgressions\OfflineProfileGenerator\OfflineProfileGenerator.csproj -c Release -- --input GunGameProgressions\ObjectData.json --probe-output build\staging\runtime05-local-metadata-audit.json` | Passed via Windows test: Airgun present, global/runtime-blacklisted IDs absent, no empty `Extra`; never package this file. |

Do not run H3VR tests/builds on macOS. Do not package `DESIGN.md`,
`DEV_STATUS.md`, or `GENERATION_POLICY.md`.
