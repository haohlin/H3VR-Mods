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
