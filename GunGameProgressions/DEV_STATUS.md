# GunGame Progressions Development Status

`DEV_STATUS.md` is the cross-session handoff file. Status, plan, and testing
stay together here.

## Status

Last verified: `2026-07-16`
State: `catalog-only memory fix deployed; runtime A/B test pending`

### Handoff convention

Since `2026-07-15`, this file is the sole GunGame Progressions handoff record.
It replaces split `STATUS.md`, `PLAN.md`, and `TESTING.md` files.

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Release | Thunderstore package `1.3.9` | Published. |
| Runtime design | Four runtime pool contract and background Modded refresh | Documented in `DESIGN.md`. |
| Compatibility rules | Shared resolver and regression matrix | Documented in `GENERATION_POLICY.md`. |
| Persistence | Last complete Modded pair restores across restart | Implemented; see design. |
| P0 root cause | Modded receipt records saved weapon count, but promotion gate ignores it | Confirmed by source inspection; no fix yet. |
| Critical gap | Smaller or equal complete candidate can still replace larger saved pair | Not fixed; P0. |
| Stability report | With only dependencies or one simple gun mod, player reports roughly once-per-second stutter, rising RAM, then crash. | Field report from pre-fix selector path; Windows runtime/log reproduction still required. |
| Root cause | Selector-side `PrepareModdedProfilesForSelector` was an unbounded `do … while (true)` poll. Each selector created a cloned loading row and reflected component list; neither selector replacement nor scene unload cancelled it. | Confirmed by source inspection; high-confidence retained-object and periodic-work path. |
| Source change | Selector now restores persisted profiles once, then returns. It has no loading row, live-insertion, poll, capture, or build path. | Windows source and focused lifecycle tests pass. |
| Background bound | One coordinator caches OtherLoader reflection per attempt and probes immediately, then once at a five-second deadline. It captures ready/stable content or stops. | Source changed locally; Windows test/build/runtime verification pending. |
| Logging bound | OtherLoader reflection failures log once per attempt; object-registry exception logs once per plugin run. | Windows build/test pass; runtime log validation pending. |
| Windows pipeline | `SourceStatus`, `Preflight`, `Test`, `Verify -Mod GunGameProgressions`, `Build`, `Package`, and `Deploy` ran on `2026-07-16` for commit `6a12f40`. | Passed: source current; `83/83` tests; GunGame target verification; tracked fallback/profile build verification. |
| Late-content rescans | Plugin schedules `WaitForSecondsRealtime` requests at five and ten minutes after startup. They use the same background candidate/persistence path and never edit an active selector. | Windows red/green test complete; deployed test build passes `83/83`, Verify, Build, and Package. Runtime observation pending. |
| Deployed test package | GunGame Progressions `1.3.9` test package includes five/ten-minute rescans, one immediate readiness probe plus one final five-second probe, and catalog-only capture. | Windows package/deploy passed while H3VR was stopped on `2026-07-16`; package SHA-256 `CEC395D3BFA2E800AFA9687161BE033801ED9F6C556678CCB6EBF641964482CA`. No public release or version bump. |
| Candidate coordinator bound | Latest source uses one immediate probe plus one final probe at five seconds. It then captures only ready/stable content or stops. | Windows test/build/package/deploy passed. Runtime log and idle-stutter verification pending. |
| Runtime memory investigation | With a large active mod set, H3VR logged one Modded capture (`1435` entries) and one `pools ready`; no repeated GunGame capture/generation trace followed. Process private memory rose during startup, then held about `60.5 GB` across the following two-minute observation. | Current code is not showing an ongoing ten-second generation loop in this run. |
| P0 memory root cause | `CaptureRuntimeMetadata` called `GetGameObjectAsync()` for every firearm and attachment, plus each Modded magazine, clip, speedloader, and cartridge. A full catalog pass materialized a very large set of Anvil/OtherLoader prefabs and their bundles. | Enabled/disabled Windows A/B shows roughly `60.5 GB` private versus `19.8 GB` private after registry quiet. This is the high-confidence cause of the persistent RAM spike. |
| Candidate memory fix | Profile capture now reads only lightweight `FVRObject` metadata. It has no `GetGameObject*` call, skips incomplete catalog entries, and leaves actual object materialization to GunGame spawn. | Windows test/build/package/deploy passed. Enabled A/B runtime verification pending. |
| Disabled A/B baseline | With GunGame Progressions disabled, the modded H3VR run held `5.70 GB` working / `19.69 GB` private at first sample, `5.73/19.79 GB` after 38 seconds, and `5.70/19.76 GB` after 98 seconds. BepInEx contained zero Progressions lines. | If the profile differed only by this mod, this is strong confirmation that the enabled path creates the large persistent memory residency rather than a periodic leak. |
| Latest disabled sample | H3VR running without GunGame Progressions: `6.77 GB` working / `22.12 GB` private. | Confirms the current baseline remains far below the enabled capture run; game is still running for the user's baseline observation. |

### Next

P0: Run enabled/disabled Windows A/B with the deployed package. Compare
post-loader private memory after registry quiet, verify one capture without
prefab materialization, and observe low-mod idle/reload for stutter or growth.

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[ ]` | Runtime-verify bounded background Modded coordination. | Windows tests pass; low-mod idle/reload has no periodic stutter, monotonic memory growth, or crash. |
| `[~]` | Limit a readiness attempt to five seconds. | Windows test/build/package/deploy pass; runtime log proves no attempt waits or polls past one final five-second check. |
| `[~]` | Remove prefab materialization from capture. | Profile capture has no `GetGameObject*` call; Windows A/B evidence shows no large startup memory residency caused by GunGame Progressions. |
| `[ ]` | Enforce count-aware Modded replacement. | Missing pair accepts first complete candidate; smaller/equal candidate keeps saved pair; strictly larger candidate replaces it; confirmed-empty still removes stale pair; focused test passes. |
| `[x]` | Consolidate handoff state. | `DEV_STATUS.md` holds Status, Plan, and Testing; legacy split files removed. |
| `[ ]` | Improve catalog metadata coverage. | Better valid mod coverage without weapon-specific hard-codes or prefab inspection. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | Global mod completion signal | Current loader signal is loader-local; replace only with verified API. |

### Current blocker

Windows source verification passed. H3VR runtime evidence is still absent:
do not claim package, deployment, or VR validation until the tested DLL is
deployed and the low-mod idle/reload regression is observed.

## Testing

### Automated

| Check | Command | Pass evidence |
| --- | --- | --- |
| Public CI | GitHub Actions `Verify H3VR Portable Checks` | Portable data/tests pass; no H3VR-only assemblies required. |
| Full Windows pipeline | `tools/h3vr.ps1 -Action Test` | All pipeline and runtime profile tests pass. Wrapper enables exporter tests only here. |
| API targets | `tools/h3vr.ps1 -Action Verify -Mod GunGameProgressions` | External GunGame spawn targets resolve. |
| Package | `tools/h3vr.ps1 -Action Build -Mod GunGameProgressions`, then `Package` | Receipt has expected version, payload, hash. |

### H3VR acceptance

| Case | Expected result |
| --- | --- |
| Startup | Vanilla pools available; game stays responsive. |
| Many mods loading | Modded work remains background; no main-thread freeze. |
| Low-mod idle/reload stability | Dependencies-only and one-simple-gun-mod installs run idle for ten minutes, then enter/exit/reload GunGame repeatedly; no once-per-second hitch, monotonic memory growth, or crash. |
| Prefab-memory A/B | Same Windows profile starts once with GunGame Progressions disabled, then once enabled. Compare post-loader working/private memory after the registry is quiet; inspect logs to prove one bounded capture and no full-registry prefab materialization. |
| Late-content rescans | Five and ten real-time minutes after plugin start, log one rescan request each; candidate generation remains background-only and a subsequent GunGame reload exposes any new persisted pair. |
| Selector lifecycle | Selector restores saved pair once and returns; no temporary loading rows or selector-owned polling coroutines exist across reloads. |
| Selector reload | Saved/generated Modded pair appears with Vanilla pair. |
| Invalid generated object | Bad loadout skips; progression continues without crash. |
| Disable mod content | Confirmed empty refresh removes stale IDs. |

### P0 persistence regression — next Windows task

Write this failing test before production code:

| Saved pair | Candidate | Expected result |
| --- | --- | --- |
| none | complete, `24` weapons/pool | Promote first pair. |
| complete, `32` weapons/pool | complete, `24` weapons/pool | Keep saved pair. |
| complete, `32` weapons/pool | complete, `32` weapons/pool | Keep saved pair. |
| complete, `32` weapons/pool | complete, `33` weapons/pool | Replace saved pair. |
| complete, count unavailable | complete, any count | Keep saved pair conservatively. |
| complete, any count | confirmed empty | Remove stale pair. |

Use persisted receipt `eligibleWeaponsPerPool` first; if unavailable, inspect
both persisted pool files. If count still cannot be proven, preserve existing
pair. Run failing and passing test through Windows before implementation is
called complete.

Record deployed version, BepInEx status lines, selected pool counts, and VR
result in this file's Status section after each runtime task.
