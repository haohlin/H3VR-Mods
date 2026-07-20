# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-20`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Working tree adds no-behavior tracing on `GameManager.StartGame`, `Progression.Promote`, and `Progression.Demote`, plus post-scene runtime method identity and current Harmony-prefix owner probes. | Windows revalidation pending. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.SpawnAndEquip`, `GameSettings.Start`, and GunGame Ammo/Extra quickbelt slots inspected. | Verified. |
| Automated checks | Prior Windows checks apply only to forced-prefix source; focused test now asserts root-cause trace hooks and GameManager target registration. | Revalidation pending. |
| Build / package | Prior package SHA-256 `EA9A194D44B22084D5E7BDF050F853B4A64CBF622DF96FB09F47B748F0F8A8D2` proved plugin load and hook registration but no hook entry. | Diagnostic package pending. |
| Deploy / VR | Runtime log proved plugin load and `SpawnAndEquip` hook registration, then no `SpawnAndEquip` entry. | Root-cause trace deployment pending. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| Missing hook entry | Existing trace cannot distinguish uncalled GameManager transition from runtime method/patch replacement. | Diagnostic scene probes and transition prefixes. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[>]` | Build and deploy root-cause diagnostic trace. | `Verify`, `Test`, `Build`, `Package`, and `Deploy` pass. |
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
| Harmony targets | `h3vr-remote run Verify -Mod GunGameCursedRandom` | Pending diagnostic source. |
| Pipeline | `h3vr-remote run Test` | Pending diagnostic source. |
| Build / package | `h3vr-remote run Build -Mod GunGameCursedRandom`; `Package` | Pending diagnostic source. |
| Deploy | `h3vr-remote run Deploy -Mod GunGameCursedRandom` | Pending diagnostic source. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Startup option | Default-enabled random progression starts without relying on `RANDOM CURSED GUNS`; menu-row visibility remains a separate map lifecycle follow-up. | Pending. |
| Game start | One Item Spawner random gun appears directly in selected hand, with random attachments logged. | Pending. |
| Promotion / demotion | Previous random gear disappears; new random gun arrives; random spare feeds use only empty Ammo/Extra slots. | Pending. |
| Failure fallback | Missing Item Spawner leaves existing profile gun intact and logs one warning. | Pending. |

### Release gate

- [x] Current Windows source status checked.
- [ ] Automated checks pass.
- [ ] Package payload/version verified.
- [ ] Deployment receipt created.
- [ ] BepInEx log checked after H3VR launch.
- [ ] Required VR interaction completed.
