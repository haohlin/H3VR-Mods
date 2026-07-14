# BubbleLevel Status

Last verified: `2026-07-14`
State: `active`

## Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Tracked repo source | Only 2024 legacy/simple controller files | Stale; not release authority. |
| Windows MeatKit source | Current gravity/damping controller, prefabs, `.meta`, test tools, build profile | Exists; not yet Git-versioned. |
| Unity checks | Nested chains, damping, limits, mount geometry, candidate previews passed on 2026-07-11 | Passed. |
| Sensitivity | 1° = `0.095774`; 4° saturates | Needs tune. |
| Package | MeatKit `BubbleLevelSet` `2.0.3` built on 2026-07-11 | Built. |
| Deployment / VR | No current receipt or live H3VR log evidence | Not verified. |
| Materials | CC0 preview renders exist; production prefabs unchanged | Awaiting approval. |

## Blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Unity source not versioned | Import current MeatKit project assets into `H3VR-unity-projects` | Codex |
| Sensitivity misses requirement | Tune, automate, rerun checks | Codex |
| Visual choice | Approve/reject material candidate | Human |
| Runtime proof | Deploy and VR-test real mounts/rails | Human + Codex |

## Next

[`PLAN.md`](PLAN.md) item 1: version authoritative Unity source before behavior
or visual changes.
