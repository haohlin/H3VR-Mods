# GunGame Progressions — Development Status

Read [DESIGN.md](DESIGN.md) for lifecycle. Read
[GENERATION_POLICY.md](GENERATION_POLICY.md) for loadout rules. This file is
cross-session handoff source of truth.

## Status

Last verified: `2026-07-19`

State: `1.4.0 released on Thunderstore; private candidate deployed, runtime Modded-file proof pending`

| Area | Verified fact | Evidence |
| --- | --- | --- |
| Public release | `1.4.0` is published. | Exact Thunderstore download URL returned HTTP `200`. |
| Golden Vanilla | Policy 19 tracked offline Rot and Mixed files contain `660` unique firearms. BrownBess is removed by the new global blacklist. | Windows generator, `--verify`, and focused baseline test. |
| Installed runtime before candidate | Vanilla profiles contain `657`; Modded profiles contain `47`. | Windows installed profile inspection. |
| Vanilla count root cause | Five golden weapons were rejected by catalog identity gate; one newer valid firearm was present, yielding net `657`. | Installed-vs-golden diff. |
| Modded count root cause | Valid G28 variants declare direct magazines and Picatinny but omit optional identity tags, so same gate rejects them. | Installed runtime catalog inspection. |
| Memory boundary | Capture reads `FVRObject` catalog only. No `GetGameObject*` call is allowed. | Source test; previous Windows A/B eliminated large prefab residency path. |
| Candidate generator | Catalog proof now accepts identity tags, direct compatible feed, exact nonzero magazine/clip interface, or `GravitonBeamer`; Modded cartridge fallback needs direct compatibility. | Windows test suite: `83/83` passed. |
| Candidate persistence | First Modded pair writes; later pair replaces only when strictly larger; confirmed loader-complete empty snapshot removes stale files. A newer generation-policy version may replace a smaller complete pair only after the ten-minute startup gate. | Focused regression; Windows `Test` `95/95`, `Verify`, `Build`, `Package`, and `Deploy` passed. |
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
| Runtime 05 worklist | Remaining test candidates: Airgun, Flaregun, MF_Medical180, Pocket1906, Quackenbush1886. Probe-list fingerprinting refreshes this profile only; Vanilla/Modded cache keys stay unchanged. | Windows `Test` `95/95`; post-deploy source and installed-package audit. |
| Live Modded generation | Runtime 02 and 04 are absent in current session. Startup captured `2` Modded entries; one-, five-, and ten-minute rescans each captured `1,124` entries but wrote no Modded pair. | Live Windows file audit: only Runtime 01, 03, and 05 exist; no Modded receipt exists. |
| Failure boundary | Each `1,124`-entry Modded capture enters background generation then logs `using packaged fallback pools.` Runtime 05 still writes its five-gun probe. This message is reached only when Modded `RuntimeGenerationJob.Error` is non-null. | Live BepInEx log plus `GenerateModdedPoolCandidate` source path. Exact exception is Debug-level only and was not emitted by current BepInEx logging configuration. |
| Root cause | Post-1.4.0 `BuildModdedWithDiagnostics` skipped Vanilla work and set `rot` to `null`; Runtime 02 then dereferenced `enemies[0].EnemyNameString`, throwing inside background job. | Source trace: Modded-only builder sets `rot = null`; `CreateScenarioPool` dereferences `enemies[0]`; exception is caught by `RuntimeGenerationJob`. |
| Candidate repair | Global blacklist includes PlungerLauncher. Modded capture pre-merges plain catalog metadata and uses 1.4.0 shared `BuildWithDiagnostics`; specialized Modded-only builder removed. | Windows `SourceStatus`, `Test` `95/95`, `Verify`, `Build`, `Package`, and private `Deploy` passed. |
| Policy 20 fallback | Both tracked Vanilla fallback pools contain `659` firearms and omit PlungerLauncher. | Windows `OfflineProfileGenerator`, `--verify`, and focused fallback test. |
| Candidate deployment | Private `1.4.0` package deployed with policy 20 fallback. No Thunderstore upload occurred. | Windows `Deploy`; package SHA-256 `DA97AE536346F5FE9B7B7F88CF33AEDDA71A05A89769BCB51832BB2D1A339950`. |
| Current deployment | `1.4.0` package built and deployed with five Runtime 05 candidates. | Windows `Test` `95/95`, `Verify`, `Build`, `Package`, `Deploy`; post-deploy installed `profile-rules.json` and generated-pool audit. |
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
| Policy replacement | A newer policy version keeps strict count growth through immediate/one-/five-minute scans. The ten-minute startup scan may replace once with a smaller complete pair; partial/empty safeguards remain. | Focused persistence and scheduler regressions; Windows `Test` `95/95`, `Verify`, `Build`, `Package`, and `Deploy` passed. |
| Current build candidate | Feature-branch package remains version `1.4.0`; deployed for private VR validation, not a public release. | Windows `Verify`, `Test` `95/95`, `Build`, `Package`, and `Deploy` passed; SHA-256 `EDAF4ACE56C297422A27B2980DF2CD08CDFB0D6B5AA90FD49749B921536BBD56`. |
| Global startup warmup | Immediate and 1/5/10-minute Modded refreshes have one idempotent owner in plugin `Awake()`; `Start()` is only a no-op fallback. It does not depend on a GunGame scene. Only the ten-minute callback opens policy-version replacement eligibility. | Source-level lifecycle regression; Windows `Test` `95/95`, `Verify`, `Build`, `Package`, and `Deploy` passed. Runtime ten-minute observation remains pending. |
| Runtime 05 boundary | Runtime 05 is local Debug-only. Release build/package neither generates nor restores it; Release package contains no Runtime 05 pool. Debug has isolated `-debug` artifacts and cannot publish. | Windows `Test` `97/97`; `Verify`; Release build/package/deploy; Release ZIP audit `0` Runtime 05 entries; installed exporter reports `CompatibilityProbeEnabled=False`; Debug build/package passed; Debug publish guard passed. |

## Plan

| State | Next item | Acceptance |
| --- | --- | --- |
| Complete | Split Runtime 05 by Debug/Release build configuration. | Windows `Test` `97/97`; Release DLL reports probe disabled; Release ZIP has no Runtime 05 files; Debug DLL reports probe enabled; Debug package cannot publish. |
| Complete | Validate ten-minute policy-version promotion gate. | Windows `Test` `95/95`, `Verify`, `Build`, `Package`, and `Deploy` passed; focused tests prove early retention and complete ten-minute eligibility. |
| Complete | Sync candidate to Windows; run `SourceStatus`, `Test`, `Verify`, `Build`, `Package`, and `Deploy`. | All checks passed; Windows test suite `83/83`. |
| Complete | Validate one-minute rescan and timing log in the real Modded profile. | Initial and one-minute scan logs recorded; capture remains frame-budgeted and profiles are valid. |
| Pending | VR smoke test G28/direct-magazine + Picatinny scope, non-box shotgun shells, and invalid-mod skip. | No wrong loose cartridge, wrong magazine, mount mismatch, exception, or progression crash. |
| Complete | Merge release source to `main`; publish Thunderstore `1.4.0`. | Main updated; package upload finalized; exact download resolves. |
| Pending | Optional VR handling smoke test. | Human checks G28/direct magazine, scope, shotguns, and progression feel. |
| Pending | Human VR-test narrowed Runtime 05. | Airgun, Flaregun, MF_Medical180, Pocket1906, and Quackenbush1886 each spawn, load, fire, and advance without error. |
| In progress | Capture H3VR runtime proof for stable Modded builder restoration. | H3VR writes Runtime 02/04 plus Modded receipt after a Modded capture, logs `pools ready`, and no longer logs packaged fallback for that job. |
| Pending decision | Classify remaining Runtime 05 failures from spawn-safety log. | Add only confirmed broken IDs to shared global blacklist with focused regression. |
| Pending design | Cap near-identical firearm variants, beginning with MP5/SP5. | Approve generic catalog-signature grouping or an explicit family rule; preserve two representative variants if requested. |
| Complete | Build/deploy metadata-only Modded optic route. | Windows SourceStatus, Test `90/90`, Verify, Build, Package, Deploy passed. |
| Complete | Keep runtime generation metadata-only and remove avoidable hot-path work. | Windows Test `93/93`, Verify, Build, Package passed. No pool policy or release version changed. |
| Complete | Audit packaged local metadata scope coverage, including Runtime 05. | Two Vanilla pools: 661/661 scopes; current Runtime 05 audit: 22/22 scopes. |
| Complete | Apply release-policy Runtime 02/04/05 exclusions and Runtime 05 forced tests. | Windows `95/95`, Verify, Build, Package, Deploy; historical policy 18 evidence only. |
| Pending | Human VR-test policy 20 Runtime 05 and Modded refresh. | Airgun appears; PlungerLauncher is absent from every profile; Runtime 02/04 write after Modded capture; a bad loadout advances instead of stalling/crashing. |
| Pending | VR-test metadata-only Modded optic route. | Modded handgun gets RMR; Picatinny rifle gets vanilla low-power/LPVO; Russian rail gets PSO-1 `MagnifierPSO1`; MP5 adapter route gets `Scope_G3SG1`; no duplicate optic, loose replacement, or spawn exception. |
| Pending | Human/runtime-observe game-wide startup warmup. | Start H3VR, remain outside GunGame through ten minutes, then open/reload GunGame. BepInEx shows initial and scheduled scans; saved Modded pair is selectable; policy-version replacement is absent before ten minutes and eligible at ten minutes. |

## Testing

| Check | Command / action | Expected |
| --- | --- | --- |
| Game API | `h3vr.ps1 -Action SourceStatus` | Current. |
| Focused/full pipeline | `h3vr.ps1 -Action Test` | Passed `95/95`: global/runtime blacklist, no Runtime 05 bypass, Airgun audit, iterator skip, and existing regressions. |
| Harmony targets | `h3vr.ps1 -Action Verify -Mod GunGameProgressions` | Kodeman targets resolve. |
| Release artifact | `h3vr.ps1 -Action Build -Mod GunGameProgressions`; `Package` | Passed: `1.4.0` package with no player Modded pool. |
| One-minute runtime generation | Deploy, launch the Modded profile through an interactive Steam URI task, inspect BepInEx log. | Passed: `startup 1-minute rescan requested`; initial `867 ms`/`2` entries; one-minute `1,053 ms`/`1,435` entries. |
| Game-wide warmup regression | `h3vr.ps1 -Action Test`, then start H3VR in any non-GunGame mode and wait at least one minute before opening GunGame. | Startup scheduler runs from `Awake()` once; initial and one-minute scans run without selector interaction; selector restores generated pair after reload. |
| Policy-version replacement gate | `h3vr.ps1 -Action Test`; then retain a saved Modded receipt from an older policy and observe early plus ten-minute scans. | Passed static/worker guard: early snapshots keep equal/smaller pair; runtime observation remains required for ten-minute timing. |
| Runtime 05 release boundary | `h3vr.ps1 -Action Test`, then `Build`/`Package`/`Deploy` default Release; `Build`/`Package -GunGameBuildConfiguration Debug`; Debug publish guard. | Passed: `97/97`; Release ZIP has `0` Runtime 05 files and installed exporter reports disabled; Debug exporter reports enabled; Debug package is isolated and publish is rejected. |
| Published artifact | Request exact version download URL. | Passed: redirected download resolves HTTP `200`. |
| Manual VR | G28; mod rifle with no direct feed; Russian/proprietary mount; pump/break shotgun; GunGame reload. | Direct/exact gear only; unsafe object skipped; saved Modded pair persists. |
| Compatibility candidate | Select Runtime 05, then inspect log. | Exactly Airgun, Flaregun, MF_Medical180, Pocket1906, and Quackenbush1886 appear; each has nonempty `Extra`; rejected loadout logs IDs and promotes without crash/stall. |
| Live Modded profile | Start H3VR and wait through a background rescan. | Runtime 02 and 04 files plus `runtime-generation-modded-receipt.json` appear; log reports pools ready, not `using packaged fallback pools.` |
| Offline fallback refresh | `OfflineProfileGenerator`, then `--verify`. | Passed: both tracked Vanilla pools match policy version `20`; count is `659`. |
| Local Runtime 05 audit | `dotnet run --project GunGameProgressions\OfflineProfileGenerator\OfflineProfileGenerator.csproj -c Release -- --input GunGameProgressions\ObjectData.json --probe-output build\staging\runtime05-local-metadata-audit.json` | Passed via Windows test: Airgun present, global/runtime-blacklisted IDs absent, no empty `Extra`; never package this file. |

Do not run H3VR tests/builds on macOS. Do not package `DESIGN.md`,
`DEV_STATUS.md`, or `GENERATION_POLICY.md`.
