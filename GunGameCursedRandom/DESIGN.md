# GunGame Cursed Random Design

## Purpose

Give each selected `Cursed Random` GunGame profile start, promotion, and
demotion a fully random vanilla Item Spawner firearm while retaining GunGame's
Sosig, kill, and run lifecycle.

## Scope

| In scope | Out of scope |
| --- | --- |
| Ship `GunGameWeaponPool_Cursed_Random.json` for normal GunGame profile selection. | Edit GunGame maps or prefabs. |
| Use `ItemSpawnerV2.BTN_TryToSpawnRandomGun()` after Cursed Random profile transitions. | Reimplement H3VR random-gun, ammo, or attachment selection. |
| Subscribe to `Progression.WeaponChangedEvent`; retain native promotion/demotion and Sosig behavior. | Change GunGame progression counts, enemies, or selected quickbelt slots. |
| Give spawned firearm to configured GunGame hand; load first compatible feed; put up to two spares in GunGame Ammo and Extra quickbelt slots. | Guarantee compatibility for every modded Item Spawner object. |

## Architecture

```text
GunGame profile loader
  -> Cursed Random appears in normal progression choices
  -> player selects Cursed Random

Native GunGame weapon transition
  -> Progression.WeaponChangedEvent
  -> ItemSpawnerV2 BTN_TryToSpawnRandomGun
  -> wait for vanilla random result
  -> remove previous GunGame/random equipment
  -> load, hand-equip, quickbelt up to two spare feeds, log loadout
```

## Invariants

- Any profile other than `Cursed Random` does not change GunGame behavior.
- Cursed Random replaces only completed native profile equipment, never
  GunGame promotion, demotion, enemy, or run-count lifecycle.
- Cursed Random contains 64 valid placeholder tiers, preserving native
  weapon-count selection through 64 weapons.
- Use the live `ItemSpawnerV2` random-gun API; do not maintain a second random
  weapon or attachment selector.
- No polling: one coroutine runs per GunGame equipment transition and stops
  after a bounded result wait.
- If Item Spawner or its result is unavailable, retain normal GunGame profile
  spawning instead of crashing or deleting current equipment.
- Log one completed random loadout with firearm, feeds, and attachments.

## Decisions

| Decision | Reason | Date |
| --- | --- | --- |
| Use native Cursed Random profile selection. | Live log proves custom startup lookup never found `GameSettings`; profile loader is already stable, visible map UI. | 2026-07-20 |
| Subscribe to `WeaponChangedEvent`, not `SpawnAndEquip`. | Live log never entered registered direct Harmony prefixes; GunGame source invokes this event after equipment transition. Runtime proof remains required. | 2026-07-20 |
| Preserve filled Ammo and Extra quickbelt slots. | Player-prepared magazines and scopes stay selected; random spares use empty slots only. | 2026-07-20 |
| Remove custom Atlas-panel behavior. | New profile is visible through GunGame's existing profile loader; no scene lifecycle/UI clone is needed. | 2026-07-20 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| P0 | Runtime smoke test on GunGame map with Item Spawner V2. | Start/promotion/demotion each yield one hand-equipped random gun. |
| P0 | Validate generated magazine, cartridge, clip, and speedloader paths. | Loaded gun and spare quickbelt feed work without exceptions. |
| P0 | Verify Cursed Random profile load and native event subscription. | Log names profile load, event subscription, and selected transition. |
| P0 | Verify profile choice appears on supported GunGame maps. | `Cursed Random` is visible/selectable in normal profile list. |
