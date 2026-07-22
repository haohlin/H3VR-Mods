# Changelog

## Unreleased

- Rename selectable progression profile to `HLin-Random Cursed`.
- Start Cursed random generation before GunGame's native placeholder spawn.
- Preserve player quickbelt items; only remove Cursed spare feeds still in their
  original Cursed-managed Ammo or Extra slot.
- Replace unverified custom startup-panel control with native `HLin-Random Cursed`
  progression profile.
- Subscribe to GunGame's post-spawn `WeaponChangedEvent`, then replace only
  HLin-Random Cursed profile equipment through H3VR's vanilla random-gun API.
- Bind direct Harmony and selected-hand reflection to active GunGame assembly,
  acknowledge direct transition events once, and queue only genuinely newer
  transitions.
- Fill supported magazines, clips, and speedloaders safely; preserve an
  already-loaded generated feed; leave one matching spawn-locked spare only in
  an empty configured Ammo or Extra slot.
- Suppress every native `WeaponBuffer.SpawnAsync` call during a pending Cursed
  transition, then clear native GunGame equipment before handing over random gun.
- Keep transition pending through handoff and optional feed setup. Feed/setup
  failures now preserve equipped random firearm and queue next promotion.
- After vanilla random API starts, remove parent-created native GunGame
  placeholder equipment only while its random-result field still holds prior
  result; prevent duplicate G17 without risking synchronously spawned random gun.
- Ignore wrapperless generated feed children and load loose wrapper-backed feeds
  before attached fallback; valid magazines no longer lose to `unknown` child
  objects.

## 1.0.0

- Initial optional random-gun GunGame progression mode.
