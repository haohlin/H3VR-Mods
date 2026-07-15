# GunGame Progressions Status

Last verified: `2026-07-15`
State: `released; active follow-up`

## Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Release | Thunderstore package `1.3.9` | Published. |
| Runtime design | Four runtime pool contract and background Modded refresh | Documented in `DESIGN.md`. |
| Compatibility rules | Shared resolver and regression matrix | Documented in `GENERATION_POLICY.md`. |
| Persistence | Last complete Modded pair restores across restart | Implemented; see design. |
| P0 root cause | Modded receipt records saved weapon count, but promotion gate ignores it | Confirmed by source inspection; no fix yet. |
| Critical gap | Smaller or equal complete candidate can still replace larger saved pair | Not fixed; P0. |
| Runtime blocker | Windows H3VR build/runtime environment unavailable at task close | Documentation only; no code, package, deployment, or VR claim. |

## Next

[`PLAN.md`](PLAN.md) P0: add failing count-aware promotion regression, then
implement and verify on Windows when runtime environment returns.
