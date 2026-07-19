# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-19`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Feature branch `feat/gungame-cursed-random`; standalone net35 BepInEx plugin uses reflection only for live Item Spawner and quickbelt APIs. | Final Windows revalidation pending. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.SpawnAndEquip`, `GameSettings.Start`, and GunGame Ammo/Extra quickbelt slots inspected. | Verified. |
| Automated checks | Focused metadata/source tests added. | Final Windows `Test` pending. |
| Build / package | Version `1.0.0` package metadata and payload declared. | Final Windows package pending. |
| Deploy / VR | Prior deployment receipt exists; current source still needs deployment. | Deploy / VR pending. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[>]` | Revalidate build, package, and deploy on Windows. | `Verify`, `Test`, `Build`, `Package`, and `Deploy` pass with current managed assemblies. |
| `[>]` | Inspect first runtime log. | Plugin load plus one complete `Cursed random GunGame spawn` line; no Harmony exception. |
| `[ ]` | Human VR smoke test. | Toggle works; start/promotion/demotion replace old gear with a random loaded gun and spare feed. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P1 | Per-map UI fallback. | Add only if clone template is missing in a verified supported GunGame map. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Game source | `h3vr-remote run SourceStatus` | Passed before implementation. |
| Harmony targets | `h3vr-remote run Verify -Mod GunGameCursedRandom` | Final validation pending. |
| Pipeline | `h3vr-remote run Test` | Final validation pending. |
| Build / package | `h3vr-remote run Build -Mod GunGameCursedRandom`; `Package` | Final validation pending. |
| Deploy | `h3vr-remote run Deploy -Mod GunGameCursedRandom` | Final deployment pending. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Startup option | Left GunGame settings panel has `RANDOM CURSED GUNS`; off is vanilla, on is random. | Pending. |
| Game start | One Item Spawner random gun appears directly in selected hand, with random attachments logged. | Pending. |
| Promotion / demotion | Previous random gear disappears; new random gun arrives; spare generated feed enters ammo quickbelt slot. | Pending. |
| Failure fallback | Missing Item Spawner leaves existing profile gun intact and logs one warning. | Pending. |

### Release gate

- [x] Current Windows source status checked.
- [ ] Automated checks pass.
- [ ] Package payload/version verified.
- [ ] Deployment receipt created.
- [ ] BepInEx log checked after H3VR launch.
- [ ] Required VR interaction completed.
