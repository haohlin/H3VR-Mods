# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-22`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Selectable-profile implementation committed as `c0bc640`; custom panel and direct Harmony prefixes removed. | Windows parity proven. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.WeaponChangedEvent`, profile loader, and GunGame Ammo/Extra quickbelt slots inspected. | Source API verified; current runtime behavior pending. |
| Automated checks | Windows `Verify GunGameCursedRandom` passed; Windows `Test` passed `106/106`, including direct WeaponBuffer interception, managed-spare ownership, and deploy process guard. | Passed. |
| Build / package | Windows release build passed `0` warnings and `0` errors. Package SHA-256 `4C1AE70B9F489CE957AAA93F1BBC3E1D9157BF3008E4BE7B3F90AB1556F66A5E`. | Passed. |
| Deploy / profile repair | `d7f5c74` deployed. Existing malformed Cursed local-package entry was replaced atomically; post-write strict validation passed and a same-directory backup was created. | r2modman reopen proof pending. |
| Runtime start failure | Fresh log: each Cursed tier requests nonexistent `MagazineG17`; native GunGame promotes after null-feed recovery until profile ends. `WeaponChangedEvent` never reaches random replacement. | Root cause confirmed; fix pending. |
| Feed repair candidate | `1d9afbe` changes all 64 tiers to live `MagazineG17Standard`; Windows `Test` `104/104`, `Verify`, and release build `0` warnings/errors passed. | Await H3VR close for deploy. |
| Direct-spawn deployment | `72aac16` intercepts `WeaponBuffer.SpawnAsync` before native placeholder spawn, uses empty coroutine after vanilla random API starts, and deletes only Cursed gun plus spares still in Cursed-managed slots. | Windows `Test` `105/105`, `Verify`, build `0` warnings/errors passed; deployed at `67c366`, VR proof pending. |
| Profile-label deployment | `c5b5130` changes profile JSON and runtime selection constant to `HLin-Random Cursed`; focused test requires exact match. | `105b3f5` passed Windows `Test` `106/106`, `Verify`, build `0` warnings/errors, and deployed to Default after two live H3VR-process checks; logs cleared. |
| Deploy safety | `105b3f5` adds `Assert-H3vrStopped` before packaging and target replacement. | Future deploys fail instead of overwriting profile files while H3VR runs. |
| Runtime fallback regression | Fresh BepInEx trace proves `WeaponBuffer.SpawnAsync` hook registered but never logged entry; `WeaponChangedEvent` then skipped fallback because registration was treated as execution. Native G17 remained. | Root cause confirmed. |
| Fallback deployment | `1b3529a` treats only a pending random spawn as direct-hook success; otherwise `WeaponChangedEvent` starts post-spawn replacement. | `5c483ac` deployed to Default after live guard passed; logs cleared; VR trace pending. |
| Spare-feed deployment | Live trace proved vanilla random result created one Bergmann magazine, which was loaded into gun, leaving `spares=0`. `b273e36` clones the loaded feed through its vanilla FVRObject wrapper only when selected Ammo/Extra has an empty slot, then spawn-locks and tracks it. | `8cca5b6` deployed to Default after live guard passed; logs cleared; VR trace pending. |
| Spare cleanup barrier | Live trace proved Cursed cleanup schedules `Destroy`, leaving the prior managed spare in its quickbelt slot for the same frame. Slot placement alternated on promotions. `8f86f7e` waits one frame after Cursed cleanup before checking slots. | Windows `Test` `106/106`, `Verify`, build `0` warnings/errors passed; deploy pending H3VR exit. |
| Transition/feed hardening | `e8f8e75` resolves direct `WeaponBuffer`, progression, and hand-option types through active GunGame assembly; acknowledges direct event once; queues only newer transitions; waits after cleanup; fills magazine/clip/speedloader safely; retains attached loaded feeds; creates or preserves exactly one matching spare in empty configured Ammo/Extra slot; deletes discarded generated feeds. | Windows `Test` `107/107`, `Verify`, release build `0` warnings/errors passed; deployed to Default profile; logs cleared; VR proof pending. |
| Multi-SpawnAsync regression | Fresh VR trace: first direct hook starts random API, then two more GunGame `SpawnAsync` calls were allowed and created native G17 equipment. G17 occupied selected Ammo slot, so matching spare used Extra; native G17 stayed in hand with random gun. | Root cause confirmed; source candidate suppresses every pending direct call and runs native cleanup before random hand equip; Windows checks/deploy pending. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| Runtime profile event | Prove Cursed Random profile loads, event subscription fires, and random replacement starts. | Windows build, deploy, VR test. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |
| r2modman UI | Confirm Default profile opens and shows Cursed local package after repaired YAML. | User reopen test. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Inspect root-cause runtime log. | Direct Harmony prefixes registered but never entered; custom `GameSettings` lookup never found live settings. |
| `[x]` | Deploy current profile/event implementation. | Windows `Verify`, `Test`, `Build`, `Package`, and `Deploy` passed for `25fbd9b`; r2modman local registration succeeded. |
| `[x]` | Repair malformed Default profile Cursed entry. | Windows `Test` passed `103/103`; `d7f5c74` deploy atomically replaced entry and validated written YAML. |
| `[x]` | Repair Cursed placeholder feed. | All 64 entries use `MagazineG17Standard`; Windows `Test` `104/104`, `Verify`, and build passed for `1d9afbe`. |
| `[x]` | Implement direct Cursed spawn and narrow quickbelt cleanup. | `72aac16` passes Windows `Test` `105/105`, `Verify`, and build. |
| `[x]` | Deploy direct-spawn profile after H3VR closes. | Default profile received `72aac16`; fresh trace and VR proof remain pending. |
| `[x]` | Validate profile-label rename. | Windows `Test` `106/106`, `Verify`, and `Build` pass for `105b3f5`. |
| `[x]` | Deploy profile-label rename after live H3VR validation. | Default profile received `HLin-Random Cursed`; logs cleared. |
| `[x]` | Deploy direct-hook fallback repair after live H3VR validation. | `5c483ac` deployed to Default; fresh trace still needs random API and completed-loadout proof. |
| `[x]` | Deploy spawn-locked spare-feed repair after live H3VR validation. | `8cca5b6` deployed to Default; fresh trace still needs compatible-spare placement proof. |
| `[x]` | Build, verify, deploy transition/feed hardening. | `e8f8e75`: Windows `Test` `107/107`, `Verify`, release build `0` warnings/errors; guarded Default-profile deployment passed. |
| `[ ]` | Suppress duplicate native GunGame spawn calls and deploy. | No native G17 gun/magazine; matching spare lands in configured Ammo slot; one random gun remains in selected hand. |
| `[ ]` | Validate transition/feed hardening in VR. | Direct hook logs entry; no native G17; every empty configured Ammo/Extra slot gets one matching spare; user-owned/moved items survive; magazine, clip, speedloader, and battery paths log without exceptions. |
| `[ ]` | Prove current profile/event implementation loads. | Fresh BepInEx startup log says `subscribed to ... WeaponChangedEvent` and `Select HLin-Random Cursed`; no legacy `SpawnAndEquip hook installed` trace. |
| `[ ]` | Human VR smoke test. | Selected HLin-Random Cursed start/promotion/demotion replace placeholder gear with random loaded gun; occupied Ammo/Extra quickbelt slots remain unchanged. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P1 | Support more than 64 HLin-Random Cursed weapon-count slots. | Add only if user needs more than native profile's 64 tiers. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Game source | `h3vr-remote run SourceStatus` | Passed before implementation. |
| Profile payload | `h3vr-remote run Verify GunGameCursedRandom` | Passed. |
| Pipeline | `h3vr-remote run Test` | Passed: `106/106` for `105b3f5`. |
| Profile-label test | `h3vr-remote run Test`; `Verify GunGameCursedRandom`; `Build GunGameCursedRandom`; `Deploy GunGameCursedRandom` | Passed: `106/106`; Verify passed; build `0` warnings/errors; Default deploy passed after live H3VR-process validation for `105b3f5`. |
| Fallback repair | `h3vr-remote run Test`; `Verify GunGameCursedRandom`; `Build GunGameCursedRandom`; `Deploy GunGameCursedRandom` | Passed: `106/106`; Verify passed; build `0` warnings/errors; Default deploy passed after live guard for `5c483ac`. |
| Spare-feed repair | `h3vr-remote run Test`; `Verify GunGameCursedRandom`; `Build GunGameCursedRandom`; `Deploy GunGameCursedRandom` | Passed: `106/106`; Verify passed; build `0` warnings/errors; Default deploy passed after live guard for `8cca5b6`. |
| Spare cleanup barrier | `h3vr-remote run Test`; `Verify GunGameCursedRandom`; `Build GunGameCursedRandom` | Passed: `106/106`; Verify passed; build `0` warnings/errors for `8f86f7e`. |
| Transition/feed hardening | `h3vr-remote run Test`; `Verify GunGameCursedRandom`; `Build GunGameCursedRandom`; `Deploy GunGameCursedRandom` | Passed: `107/107`; Verify passed; release build `0` warnings/errors; guarded Default deploy passed for `e8f8e75`; logs cleared. |
| Build / package | `h3vr-remote run Build GunGameCursedRandom`; `Package` | Build passed for `72aac16`; package/deploy waits for H3VR close. |
| Deploy | `h3vr-remote run Deploy GunGameCursedRandom` | Passed for `d7f5c74`; malformed Cursed entry replaced and written YAML strictly validated. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Profile selection | `HLin-Random Cursed` appears in native progression choices and stays selected after health/kills/weapon-count changes. | Pending. |
| r2modman profile | Default profile opens without YAML parse error; local Cursed package is visible and enabled. | Pending. |
| Game start | One Item Spawner random gun appears directly in selected hand, with random attachments logged. | Pending. |
| Direct start | No placeholder G17 or G17 magazine appears before Cursed random gun. | Pending. |
| Promotion / demotion | Previous Cursed gun disappears; new random gun arrives; only Cursed spare still in its original Ammo/Extra slot is removed. All moved and unrelated quickbelt objects remain. | Pending. |
| Failure fallback | Missing Item Spawner leaves existing profile gun intact and logs one warning. | Pending. |

### Release gate

- [x] Current Windows source status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Current transition/feed DLL and profile deployed; logs cleared.
- [ ] BepInEx log checked after H3VR launch.
- [ ] Required VR interaction completed.
