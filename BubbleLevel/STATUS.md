# BubbleLevel Status

Last verified: `2026-07-14`
State: `active`

## Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Unity source | In-place `Assets/Projects` Git root in `H3VR-unity-projects`; matching origin `main` commit | Versioned. |
| Controller | Current gravity/damping controller, prefabs, `.meta`, test tools, build profile | Versioned in Unity repository. |
| Earlier Unity checks | Nested chains, damping, limits, mount geometry, candidate previews passed on 2026-07-11 | Re-run now license available. |
| Sensitivity | Earlier check: 1° = `0.095774`; 4° saturates | Recalibration pending. |
| Package | MeatKit `BubbleLevelSet` `2.0.3` built on 2026-07-11 | Valid package exists; never deployed through wrapper. |
| Deployment / VR | Installed profile was old `2.0.1`; no `2.0.3` receipt or live H3VR log | Pending. |
| Materials | CC0 preview renders exist; production prefabs unchanged | Awaiting approval. |

## Blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Repeatable package path absent | Wrapper lacked Unity build/package/deploy support | Codex: in progress |
| Sensitivity and reverse-mount proof absent | Run Unity tests; tune only if checks fail | Codex |
| Visual choice | Approve/reject material candidate | Human |
| Runtime proof | Deploy and VR-test real mounts/rails | Human + Codex |

## Next

[`PLAN.md`](PLAN.md) item 1: run licensed Unity checks, package `2.0.3`,
deploy through wrapper, then capture H3VR log and VR evidence.
