# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-20`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Current unvalidated source replaces custom startup-panel and direct Harmony prefixes with selectable `Cursed Random` profile plus `Progression.WeaponChangedEvent` subscription. | Windows build/test pending after H3VR closes. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.WeaponChangedEvent`, profile loader, and GunGame Ammo/Extra quickbelt slots inspected. | Verified source API; current profile/event behavior pending. |
| Automated checks | Windows `Test` passed `101/101` for the previous diagnostic package. The selectable-profile checks are unrun. | Historical only. |
| Build / package | Windows release build completed with `0` warnings and `0` errors. Diagnostic package SHA-256 `CB839BCD738754334E2CB18F4DF1D75CA5662CEEA73C36FB875C8EDDA31DB3E0`. | Passed. |
| Deploy / VR | Diagnostic package remains deployed to active r2modman Default profile. | New profile/event implementation not yet built or deployed. |

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
| `[>]` | Build and deploy profile/event implementation. | Windows `Verify`, `Test`, `Build`, `Package`, and `Deploy` pass after H3VR closes. |
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
| Profile payload | `h3vr-remote run Verify -Mod GunGameCursedRandom` | Pending after profile payload change. |
| Pipeline | `h3vr-remote run Test` | Passed: `101/101`. |
| Build / package | `h3vr-remote run Build -Mod GunGameCursedRandom`; `Package` | Pending profile/event candidate. |
| Deploy | `h3vr-remote run Deploy -Mod GunGameCursedRandom` | Pending profile/event candidate. |

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
