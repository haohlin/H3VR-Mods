# GunGame Progressions Development Status

`DEV_STATUS.md` is the cross-session handoff file. Status, plan, and testing
stay together here.

## Status

Last verified: `2026-07-16`
State: `source change pending Windows verification`

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
| Stability report | With only dependencies or one simple gun mod, player reports roughly once-per-second stutter, rising RAM, then crash. | Field report; Windows runtime/log reproduction still required. |
| Root cause | Selector-side `PrepareModdedProfilesForSelector` was an unbounded `do … while (true)` poll. Each selector created a cloned loading row and reflected component list; neither selector replacement nor scene unload cancelled it. | Confirmed by source inspection; high-confidence retained-object and periodic-work path. |
| Source change | Selector now restores persisted profiles once, then returns. It has no loading row, live-insertion, poll, capture, or build path. | Implemented locally on `fix/gungame-background-refresh`; Windows pipeline pending. |
| Background bound | One coordinator caches OtherLoader reflection per attempt; it captures at loader completion, five seconds registry quiet when unavailable, or 30 seconds stable while `Loading`. Missing registry stops after 30 seconds. | Implemented locally; Windows automated/runtime verification pending. |
| Logging bound | OtherLoader reflection failures log once per attempt; object-registry exception logs once per plugin run. | Implemented locally; Windows verification pending. |
| Windows access | Private SSH alias reaches Windows. A unique pipeline checkout was found clean on `main`; its environment variable is not configured, so commands locate that checkout transiently without committing path data. | Windows Test/Verify/Build has not run yet. |

### Next

P0: run Windows `Test`, `Verify`, and `Build` for the bounded coordinator
change. Then reproduce low-mod idle/reload stability with BepInEx logs and
memory observation before the existing count-aware persistence work.

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[ ]` | Verify bounded background Modded coordination. | Selector owns only persisted restore; no selector UI clone/poll/capture/build path remains; cached readiness reflection and 30-second stable limit pass Windows tests; low-mod idle/reload has no periodic stutter or monotonic memory growth. |
| `[ ]` | Enforce count-aware Modded replacement. | Missing pair accepts first complete candidate; smaller/equal candidate keeps saved pair; strictly larger candidate replaces it; confirmed-empty still removes stale pair; focused test passes. |
| `[x]` | Consolidate handoff state. | `DEV_STATUS.md` holds Status, Plan, and Testing; legacy split files removed. |
| `[ ]` | Improve general incomplete-metadata reconciliation. | Better valid mod coverage without weapon-specific hard-codes. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | Global mod completion signal | Current loader signal is loader-local; replace only with verified API. |

### Current blocker

No Windows Test/Verify/Build or H3VR runtime evidence yet for this source
branch. Do not claim package, deployment, or VR validation until those commands
run against the unique remote checkout.

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
