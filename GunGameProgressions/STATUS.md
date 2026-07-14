# GunGame Progressions Status

Last verified: `2026-07-14`
State: `released; active follow-up`

## Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Release | Thunderstore package `1.3.9` | Published. |
| Runtime design | Four runtime pool contract and background Modded refresh | Documented in `DESIGN.md`. |
| Compatibility rules | Shared resolver and regression matrix | Documented in `GENERATION_POLICY.md`. |
| Persistence | Last complete Modded pair restores across restart | Implemented; see design. |
| Critical gap | Smaller complete candidate can still replace larger saved pair | Not fixed; P0. |

## Next

[`PLAN.md`](PLAN.md) P0: enforce count-aware Modded promotion with failing
regression test first.
