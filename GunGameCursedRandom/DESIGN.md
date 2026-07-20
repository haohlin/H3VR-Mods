# GunGame Cursed Random Design

## Purpose

Give each GunGame start, promotion, and demotion a fully random vanilla Item
Spawner firearm while retaining GunGame's Sosig, kill, and run lifecycle.

## Scope

| In scope | Out of scope |
| --- | --- |
| Clone one GunGame startup-toggle row for `RANDOM CURSED GUNS`. | Edit GunGame maps, prefabs, or profiles. |
| Use `ItemSpawnerV2.BTN_TryToSpawnRandomGun()` when enabled. | Reimplement H3VR random-gun, ammo, or attachment selection. |
| Replace only `Progression.SpawnAndEquip`; retain GunGame promotion/demotion and Sosig behavior. | Change GunGame progression counts, enemies, or selected quickbelt slots. |
| Give spawned firearm to configured GunGame hand; load first compatible feed; put up to two spares in GunGame Ammo and Extra quickbelt slots. | Guarantee compatibility for every modded Item Spawner object. |

## Architecture

```text
GunGame settings start
  -> clone existing toggle row
  -> persistent enabled state

GunGame SpawnAndEquip when enabled
  -> ItemSpawnerV2 BTN_TryToSpawnRandomGun
  -> wait for vanilla random result
  -> remove previous GunGame/random equipment
  -> load, hand-equip, quickbelt up to two spare feeds, log loadout
```

## Invariants

- Disabled mode does not change GunGame behavior.
- Enabled mode bypasses only profile weapon spawning, never the GunGame
  promotion, demotion, enemy, or run-count lifecycle.
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
| Random progression forces on at each H3VR startup. | Prevents a persisted false setting from bypassing the only progression hook while Atlas menu UI remains unverified; an available menu row can still change the current session. | 2026-07-20 |
| Reuse the Auto Loading row as startup UI template. | Adds one native-looking option without Unity scene edits. | 2026-07-19 |
| Keep selected profile for enemy mode and run length only. | GunGame still needs its normal lifecycle; firearm profile contents are ignored. | 2026-07-19 |
| Preserve filled Ammo and Extra quickbelt slots. | Player-prepared magazines and scopes stay selected; random spares use empty slots only. | 2026-07-20 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| P0 | Runtime smoke test on GunGame map with Item Spawner V2. | Start/promotion/demotion each yield one hand-equipped random gun. |
| P0 | Validate generated magazine, cartridge, clip, and speedloader paths. | Loaded gun and spare quickbelt feed work without exceptions. |
| P0 | Capture forced-override diagnostic trace. | Log proves patch owners, each transition entry, vanilla random result, cleanup, feed, quickbelt, and hand transfer. |
| P1 | Verify UI placement on every supported GunGame map. | Toggle remains readable and click-target works. |
