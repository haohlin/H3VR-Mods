# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-20`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Selectable-profile implementation committed as `c0bc640`; custom panel and direct Harmony prefixes removed. | Windows parity proven. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.WeaponChangedEvent`, profile loader, and GunGame Ammo/Extra quickbelt slots inspected. | Source API verified; current runtime behavior pending. |
| Automated checks | Windows `Verify GunGameCursedRandom` passed; Windows `Test` passed `102/102`. | Passed. |
| Build / package | Windows release build passed `0` warnings and `0` errors. Package SHA-256 `F5BD06AEA80ADBEE47D91E05B53E28526597F44E5D841D4C20D0C6D5043508C5`. | Passed. |
| Deploy / VR | Candidate deployed to active r2modman Default profile. | Human VR smoke test pending. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| Runtime profile event | Prove Cursed Random profile loads, event subscription fires, and random replacement starts. | Windows build, deploy, VR test. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Inspect root-cause runtime log. | Direct Harmony prefixes registered but never entered; custom `GameSettings` lookup never found live settings. |
| `[x]` | Build and deploy profile/event implementation. | Windows `Verify`, `Test`, `Build`, `Package`, and `Deploy` passed for `c0bc640`. |
| `[ ]` | Human VR smoke test. | Selected Cursed Random start/promotion/demotion replace placeholder gear with random loaded gun; occupied Ammo/Extra quickbelt slots remain unchanged. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P1 | Support more than 64 Cursed Random weapon-count slots. | Add only if user needs more than native profile's 64 tiers. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Game source | `h3vr-remote run SourceStatus` | Passed before implementation. |
| Profile payload | `h3vr-remote run Verify GunGameCursedRandom` | Passed. |
| Pipeline | `h3vr-remote run Test` | Passed: `102/102`. |
| Build / package | `h3vr-remote run Build GunGameCursedRandom`; `Package` | Passed. |
| Deploy | `h3vr-remote run Deploy GunGameCursedRandom` | Passed to Default profile. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Profile selection | `Cursed Random` appears in native progression choices and stays selected after health/kills/weapon-count changes. | Pending. |
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
