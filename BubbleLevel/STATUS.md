# BubbleLevel Status

Last verified: `2026-07-14`
State: `active`

## Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Unity source | In-place `Assets/Projects` Git root in `H3VR-unity-projects`; matching origin `main` commit | Versioned. |
| Controller | Current gravity/damping controller, prefabs, `.meta`, test tools, build profile | Versioned in Unity repository. |
| Unity runtime checks | Licensed Unity batch `RunAll` passed on 2026-07-14 | Passed. |
| Sensitivity | Center `-0.008400`; 1° `0.095774` = `0.104174` travel; 4° reaches stop | Passed. |
| 180° rail | Local positions `0.095774` / `-0.107402`; world movement dot `1.000000` | Passed. |
| Package | MeatKit `BubbleLevelSet` `2.0.3` rebuilt on 2026-07-14 from exact profile | Passed. |
| Wrapper package/deploy | Wrapper validated package, backed up old install, and deployed `2.0.3` with receipt | Passed. |
| Deployment / VR | New package installed; no post-deployment H3VR log or VR interaction result yet | Pending. |
| Materials | CC0 preview renders exist; production prefabs unchanged | Awaiting approval. |

## Blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Visual choice | Approve/reject material candidate | Human |
| Runtime proof | Deploy and VR-test real mounts/rails | Human + Codex |

## Next

[`PLAN.md`](PLAN.md) item 1: capture H3VR log and real VR mount evidence for
deployed `2.0.3`.
