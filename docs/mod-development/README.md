# Cross-session mod records

Each active mod owns two tracked files in its source root:

| File | Holds | Update when |
| --- | --- | --- |
| `DESIGN.md` | Intent, architecture, constraints, accepted decisions | Design changes |
| `DEV_STATUS.md` | Handoff file: verified status, plan, testing, evidence, blockers | Every completed investigation or implementation task |

## Start and finish

```text
start
  read design + development status
  inspect Git + Windows source/build/runtime state
  reconcile stale records before changing code

finish
  record evidence, not assumptions
  update Status, Plan, and Testing sections
  commit source, tests, records together
```

Use templates in this directory for new active mods. `MOD_STATE_INDEX.md` is
the repository entry point. For Unity mods, tracked source must include all
owned assets and matching `.meta` files; an untracked Unity workspace is never
enough to describe a releasable state.
