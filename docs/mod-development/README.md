# Cross-session mod records

Each active mod owns four tracked files in its source root:

| File | Holds | Update when |
| --- | --- | --- |
| `DESIGN.md` | Intent, architecture, constraints, accepted decisions | Design changes |
| `STATUS.md` | Verified current facts, commit/build/deploy/VR evidence, blockers | Every completed investigation or implementation task |
| `PLAN.md` | Ordered next work; active item; acceptance condition | Scope or priority changes |
| `TESTING.md` | Repeatable checks and manual acceptance cases | Test flow changes |

## Start and finish

```text
start
  read four mod records
  inspect Git + Windows source/build/runtime state
  reconcile stale records before changing code

finish
  record evidence, not assumptions
  mark plan item done or blocked
  commit source, tests, records together
```

Use templates in this directory for new active mods. `MOD_STATE_INDEX.md` is
the repository entry point. For Unity mods, tracked source must include all
owned assets and matching `.meta` files; an untracked Unity workspace is never
enough to describe a releasable state.
