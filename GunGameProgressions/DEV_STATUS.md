# GunGame Progressions Development Status

`DEV_STATUS.md` is the cross-session handoff file. Status, plan, and testing
stay together here.

## Status

Last verified: `2026-07-15`
State: `released; active follow-up`

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
| Selector lifecycle risk | `PrepareModdedProfilesForSelector` polls without a timeout, cancellation, selector/scene-liveness check, or `finally` cleanup. Its cloned loading row and reflected component list are destroyed only after polling ends. | Confirmed by source inspection; high-confidence retained-object and periodic-work path. |
| Lifecycle mismatch | Background Modded refresh stops after `120s`; selector preparation has no equivalent limit. | Confirmed by source inspection; P0 fix required. |
| Runtime blocker | Windows H3VR build/runtime environment unavailable at task close | Documentation only; no code, package, deployment, or VR claim. |

### Next

P0: reproduce low-mod stutter/RAM growth with a fresh log, then bound and clean
up selector preparation before the existing count-aware persistence work.

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[ ]` | Bound selector preparation lifetime and cleanup. | At most one live selector routine; selector replacement, scene unload, and bounded timeout stop it; loading row is always destroyed; repeated polling errors are rate-limited; focused lifecycle test passes. |
| `[ ]` | Enforce count-aware Modded replacement. | Missing pair accepts first complete candidate; smaller/equal candidate keeps saved pair; strictly larger candidate replaces it; confirmed-empty still removes stale pair; focused test passes. |
| `[x]` | Consolidate handoff state. | `DEV_STATUS.md` holds Status, Plan, and Testing; legacy split files removed. |
| `[ ]` | Re-validate selector loading row in VR. | Concise status appears without blocking Vanilla choices. |
| `[ ]` | Improve general incomplete-metadata reconciliation. | Better valid mod coverage without weapon-specific hard-codes. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | Global mod completion signal | Current loader signal is loader-local; replace only with verified API. |

### Current blocker

Windows H3VR build/runtime environment was unavailable on 2026-07-15. Keep
this task at documentation and test-design stage until Windows returns; do not
claim a source fix, package, deployment, or VR result.

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
| Selector lifecycle | Instrument active selector routines and temporary loading rows; count returns to zero after selector replacement/unload and never grows across reloads. |
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
