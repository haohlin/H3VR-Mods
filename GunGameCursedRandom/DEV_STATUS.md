# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-20`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Working tree forces `EnableRandomCursedGuns=true` at every H3VR startup, retains one `Priority.First` `SpawnAndEquip(bool)` prefix, and adds bounded trace logging from patch install through random result, cleanup, quickbelt, and hand transfer. | Windows revalidation pending. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.SpawnAndEquip`, `GameSettings.Start`, and GunGame Ammo/Extra quickbelt slots inspected. | Verified. |
| Automated checks | Prior Windows checks apply only to deployed source `2b86c4f`; focused test now asserts forced startup setting and diagnostic trace strings. | Revalidation pending. |
| Build / package | Prior package SHA-256 `273ABC46E6D9FDB3B619E914DAFDB9153A291BF91669C97C94E241B0908588D0` has persisted-setting gate behavior. | Replacement package pending. |
| Deploy / VR | Live log proved plugin load but no `SpawnAndEquip` prefix entry; profile weapon therefore remained. | Replacement deployment pending H3VR exit. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| Prior persisted setting | Current deployed gate can be false despite default migration. | Replaced by per-session force enable; Windows runtime test. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[>]` | Build and deploy per-session forced override with full transition trace. | `Verify`, `Test`, `Build`, `Package`, and `Deploy` pass after H3VR exits. |
| `[>]` | Inspect first runtime log. | Patch-owner line, `SpawnAndEquip entered`, vanilla random result, cleanup, quickbelt, hand, and final loadout lines; no Harmony exception. |
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
| Harmony targets | `h3vr-remote run Verify -Mod GunGameCursedRandom` | Pending forced-override source. |
| Pipeline | `h3vr-remote run Test` | Pending forced-override source. |
| Build / package | `h3vr-remote run Build -Mod GunGameCursedRandom`; `Package` | Pending forced-override source. |
| Deploy | `h3vr-remote run Deploy -Mod GunGameCursedRandom` | Pending H3VR exit. |

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
