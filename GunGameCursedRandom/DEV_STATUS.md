# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-20`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Feature branch `feat/gungame-cursed-random`; standalone net35 BepInEx plugin gives its `SpawnAndEquip` prefix `Priority.First`, uses reflection only for live Item Spawner and quickbelt APIs, preserves occupied Ammo/Extra slots, and defaults random mode on once. | Implemented. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.SpawnAndEquip`, `GameSettings.Start`, and GunGame Ammo/Extra quickbelt slots inspected. | Verified. |
| Automated checks | Windows `Verify -Mod GunGameCursedRandom` passed; Windows `Test` passed `100/100`; focused source test covers first-priority override and empty-slot-only spare placement. | Passed. |
| Build / package | Windows release build completed with `0` warnings and `0` errors. Package SHA-256 `273ABC46E6D9FDB3B619E914DAFDB9153A291BF91669C97C94E241B0908588D0`. | Passed. |
| Deploy / VR | Package deployed to active r2modman Default profile; Windows deployment receipt created. | Runtime log and VR test pending. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Revalidate and deploy forced first-priority override on Windows. | `Verify`, `Test`, `Build`, `Package`, and `Deploy` passed after H3VR exit. |
| `[>]` | Inspect first runtime log. | `Cursed random GunGame override requested`, then one complete `Cursed random GunGame spawn` line; no Harmony exception. |
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
| Pipeline | `h3vr-remote run Test` | Passed: `100/100`. |
| Build / package | `h3vr-remote run Build -Mod GunGameCursedRandom`; `Package` | Passed: `0` warnings, `0` errors; package SHA recorded above. |
| Deploy | `h3vr-remote run Deploy -Mod GunGameCursedRandom` | Passed; deployment receipt created. |

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
