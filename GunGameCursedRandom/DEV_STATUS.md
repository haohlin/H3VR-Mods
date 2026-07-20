# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-20`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Commit `c33d7f6` adds no-behavior tracing on `GameManager.StartGame`, `Progression.Promote`, and `Progression.Demote`, plus post-scene runtime method identity and current Harmony-prefix owner probes. | Windows Verify passed. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.SpawnAndEquip`, `GameSettings.Start`, and GunGame Ammo/Extra quickbelt slots inspected. | Verified. |
| Automated checks | Windows `Test` passed `101/101`; focused test asserts root-cause trace hooks and GameManager target registration. | Passed. |
| Build / package | Windows release build completed with `0` warnings and `0` errors. Diagnostic package SHA-256 `CB839BCD738754334E2CB18F4DF1D75CA5662CEEA73C36FB875C8EDDA31DB3E0`. | Passed. |
| Deploy / VR | Diagnostic package deployed to active r2modman Default profile; Windows deployment receipt created. | Root-cause runtime trace pending. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| Missing hook entry | Existing trace cannot distinguish uncalled GameManager transition from runtime method/patch replacement. | Diagnostic scene probes and transition prefixes. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Build and deploy root-cause diagnostic trace. | `Verify`, `Test`, `Build`, `Package`, and `Deploy` passed. |
| `[>]` | Inspect root-cause runtime log. | Runtime component assembly, patch owners, GameManager start, and progression transition entries identify missing call versus method replacement. |
| `[ ]` | Human VR smoke test. | Default-enabled start/promotion/demotion replace old gear with random loaded gun; occupied Ammo/Extra quickbelt slots remain unchanged. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P1 | Per-map UI fallback. | Add only if clone template is missing in a verified supported GunGame map. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Game source | `h3vr-remote run SourceStatus` | Passed before implementation. |
| Harmony targets | `h3vr-remote run Verify -Mod GunGameCursedRandom` | Passed. |
| Pipeline | `h3vr-remote run Test` | Passed: `101/101`. |
| Build / package | `h3vr-remote run Build -Mod GunGameCursedRandom`; `Package` | Passed: `0` warnings, `0` errors; package SHA recorded above. |
| Deploy | `h3vr-remote run Deploy -Mod GunGameCursedRandom` | Passed; diagnostic deployment receipt created. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Startup option | Default-enabled random progression starts without relying on `RANDOM CURSED GUNS`; menu-row visibility remains a separate map lifecycle follow-up. | Pending. |
| Game start | One Item Spawner random gun appears directly in selected hand, with random attachments logged. | Pending. |
| Promotion / demotion | Previous random gear disappears; new random gun arrives; random spare feeds use only empty Ammo/Extra slots. | Pending. |
| Failure fallback | Missing Item Spawner leaves existing profile gun intact and logs one warning. | Pending. |

### Release gate

- [x] Current Windows source status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Deployment receipt created.
- [ ] BepInEx log checked after H3VR launch.
- [ ] Required VR interaction completed.
