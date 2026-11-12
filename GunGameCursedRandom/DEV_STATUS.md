# GunGame Cursed Random Development Status

## Status

Last verified: `2026-07-22`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Completion-aware retry implementation `6b9f63c` is the deployed build. Later commits reword three log messages, the profile description, and the package README; behavior is unchanged. | `6b9f63c` built, tested, and deployed on Windows; later copy changes not built on Windows. |
| Live API | Windows `SourceStatus` current; `ItemSpawnerV2.BTN_TryToSpawnRandomGun`, GunGame `Progression.WeaponChangedEvent`, profile loader, and GunGame Ammo/Extra quickbelt slots inspected. | Source API verified; current runtime behavior pending. |
| Automated checks | Windows `Verify GunGameCursedRandom` passed; Windows `Test` passed `107/107` for `6b9f63c`. | Passed. |
| Portable checks | At `973f9df`: hosted run `43206511541` on `ubuntu-latest` concluded success, with the `[WindowsH3vrFact]` tests skipped. Isolated macOS clone, .NET SDK 8.0.428: `109` passed in full mode with `H3VR_METADATA_EXPORTER_TESTS=1`, `48` passed and `61` skipped in CI mode. `GunGameCursedRandom.csproj` compiles against NuGet reference libraries with `0` warnings and `0` errors. | Passed; not on the Windows host. |
| Build / package | Windows release build passed `0` warnings and `0` errors for `6b9f63c`. Package SHA-256 `393D5AEA4E42340328DFE51C28274B556F78E19781113D8D19E0431440324F4F`. | Passed. |
| Deploy | Guarded deploy of `6b9f63c` to the Default profile after the H3VR-stop check; logs cleared. The malformed Cursed local-package entry in the profile was replaced atomically by `d7f5c74`, its written YAML validated, and a same-directory backup created. | VR proof and r2modman reopen proof pending. |
| Deploy safety | `105b3f5` adds `Assert-H3vrStopped` before packaging and target replacement. | Future deploys fail instead of overwriting profile files while H3VR runs. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Runtime Item Spawner location | GunGame scene must expose active `ItemSpawnerV2`. | Windows runtime test. |
| Runtime profile event | Prove Cursed Random profile loads, event subscription fires, and random replacement starts. | Windows build, deploy, VR test. |
| VR behavior | Confirm gun hand retrieval, feed loading, quickbelt spare, attachment logging, and previous-item cleanup. | Human VR test. |
| r2modman UI | Confirm Default profile opens and shows Cursed local package after repaired YAML. | User reopen test. |
| Package icon | `icon.png` is byte-identical to The Ping's icon (MD5 `f34fee1f83df6ffa0f6722bb22c11395`). A distinct 256x256 icon is required before any publish. | Owner artwork and approval. |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[>]` | Rebuild and redeploy the current source. | Windows `Verify`, `Test`, `Build`, `Package`, and guarded `Deploy` pass for the commit that contains the reworded log messages, profile description, and README. |
| `[ ]` | Prove current profile/event implementation loads. | Fresh BepInEx startup log says `subscribed to ... WeaponChangedEvent` and `Select HLin-Random Cursed`; no legacy `SpawnAndEquip hook installed` trace. |
| `[ ]` | Validate duplicate-spawn repair in VR. | No native G17 gun/magazine; matching spare lands in configured Ammo slot; one random gun remains in selected hand. |
| `[ ]` | Validate transition/feed hardening in VR. | Direct hook logs entry; no native G17; every empty configured Ammo/Extra slot gets one matching spare; user-owned/moved items survive; magazine, clip, speedloader, and battery paths log without exceptions. |
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
| Profile payload | `h3vr-remote run Verify GunGameCursedRandom` | Passed for `6b9f63c`. |
| Pipeline | `h3vr-remote run Test` | Passed `107/107` for `6b9f63c`. Source regression requires native-routine completion tracking, three-attempt cap, native firearm/feed validation, no `Promote()` skip, timeout inflight gate, and native-equipment preservation on direct timeout/final failure. |
| Build / package / deploy | `h3vr-remote run Build GunGameCursedRandom`; `Package GunGameCursedRandom`; `Deploy GunGameCursedRandom` | Passed for `6b9f63c`: release build `0` warnings/errors, package SHA-256 above, guarded Default deploy, log clear. |

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
- [x] Automated checks pass for `6b9f63c`.
- [x] Package payload/version verified for `6b9f63c`.
- [x] Latest completion-aware retry DLL deployed after H3VR-stop guard.
- [ ] Current source rebuilt and redeployed on Windows.
- [ ] Distinct package icon in place.
- [ ] BepInEx log checked after H3VR launch.
- [ ] Required VR interaction completed.
