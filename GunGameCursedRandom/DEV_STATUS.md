# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-19`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Feature branch `feat/gungame-cursed-random`; standalone net35 BepInEx plugin uses reflection only for live Item Spawner and quickbelt APIs, with bounded startup/scene UI injection. | Implemented. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.SpawnAndEquip`, `GameSettings.Start`, and GunGame Ammo/Extra quickbelt slots inspected. | Verified. |
| Automated checks | Windows `Verify` passed; Windows `Test` passed `100/100`. | Passed. |
| Build / package | Windows Release build passed without warnings; version `1.0.0` package SHA-256 `61F9C926D72DA94F415FB3740BB1D3A5840FBBB42E864A81DE030101029EF3A5`. | Passed. |
| Deploy / VR | Windows deployment completed and created a VR receipt; no H3VR launch/log evidence yet. | VR test pending. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Build, package, and deploy on Windows. | `Verify`, `Test`, `Build`, `Package`, and `Deploy` pass with current managed assemblies. |
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
| Harmony targets | `h3vr-remote run Verify -Mod GunGameCursedRandom` | Passed. |
| Pipeline | `h3vr-remote run Test` | Passed `100/100`. |
| Build / package | `h3vr-remote run Build -Mod GunGameCursedRandom`; `Package` | Passed, Release, zero warnings. |
| Deploy | `h3vr-remote run Deploy -Mod GunGameCursedRandom` | Passed; VR receipt created. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Startup option | Left GunGame settings panel has `RANDOM CURSED GUNS`; off is vanilla, on is random. | Pending. |
| Game start | One Item Spawner random gun appears directly in selected hand, with random attachments logged. | Pending. |
| Promotion / demotion | Previous random gear disappears; new random gun arrives; spare generated feed enters ammo quickbelt slot. | Pending. |
| Failure fallback | Missing Item Spawner leaves existing profile gun intact and logs one warning. | Pending. |

### Release gate

- [x] Current Windows source status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Deployment receipt created.
- [ ] BepInEx log checked after H3VR launch.
- [ ] Required VR interaction completed.
