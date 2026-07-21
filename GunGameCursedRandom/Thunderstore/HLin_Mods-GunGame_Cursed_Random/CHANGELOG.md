# Changelog

## Unreleased

- Start Cursed random generation before GunGame's native placeholder spawn.
- Preserve player quickbelt items; only remove Cursed spare feeds still in their
  original Cursed-managed Ammo or Extra slot.
- Replace unverified custom startup-panel control with native `Cursed Random`
  progression profile.
- Subscribe to GunGame's post-spawn `WeaponChangedEvent`, then replace only
  Cursed Random profile equipment through H3VR's vanilla random-gun API.

## 1.0.0

- Initial optional random-gun GunGame progression mode.
