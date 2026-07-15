# BubbleLevel Status

Last verified: `2026-07-14`
State: `released`

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
| Deployment / VR | User confirmed `2.0.3` works in H3VR, including corrected reverse/180° rail behavior | Passed. |
| Thunderstore | `HLin_Mods-BubbleLevelSet` `2.0.3` published from fresh validated package | Published. |
| Documentation | Source `README.md` and standalone `CHANGELOG.md` expanded; package title, short description, build profile, and `2.0.3` package remain unchanged | Published to Unity source `main`. |
| Materials | CC0 preview renders exist; production prefabs unchanged | Awaiting approval. |

## Next

Monitor release feedback. Material variants remain optional until visual approval.

## Session handoff

1. Start from this folder and its four tracked records; chat history is not required.
2. `2.0.3` is current public runtime/package release. The later documentation
   update is source-only; do not rebuild, deploy, or publish it by itself.
3. For any new runtime/content change, use Windows as build/runtime authority,
   run Unity `RunAll`, wrapper `Test`/`Build`/`Package`, then deploy and record
   the VR result here.
4. First candidate work: material approval or expanded VR coverage for offset
   rail, 30 mm mounts, nested chain, inversion, and settle behavior.
