# GunGame Cursed Random Design

## Purpose

Give each selected `HLin-Random Cursed` GunGame profile start, promotion, and
demotion a fully random vanilla Item Spawner firearm while retaining GunGame's
Sosig, kill, and run lifecycle.

## Scope

| In scope | Out of scope |
| --- | --- |
| Ship `GunGameWeaponPool_Cursed_Random.json` for normal GunGame profile selection. | Edit GunGame maps or prefabs. |
| Use `ItemSpawnerV2.BTN_TryToSpawnRandomGun()` after HLin-Random Cursed profile transitions. | Reimplement H3VR random-gun, ammo, or attachment selection. |
| Subscribe to `Progression.WeaponChangedEvent`; retain native promotion/demotion and Sosig behavior. | Change GunGame progression counts, enemies, or selected quickbelt slots. |
| Give spawned firearm to configured GunGame hand; load first compatible feed; ensure one matching spawn-locked spare in an empty GunGame Ammo or Extra quickbelt slot. | Guarantee compatibility for every modded Item Spawner object. |

## Architecture

```text
GunGame profile loader
  -> HLin-Random Cursed appears in normal progression choices
  -> player selects HLin-Random Cursed

Native GunGame WeaponBuffer.SpawnAsync
  -> Cursed prefix starts ItemSpawnerV2 BTN_TryToSpawnRandomGun
  -> returns empty native coroutine; no placeholder G17 or G17 magazine
  -> wait for vanilla random result
  -> remove previous Cursed gun and only spare feeds still in Cursed-managed slots
  -> load, hand-equip, clone one loaded feed through its vanilla FVRObject wrapper if no spare exists
  -> quickbelt spawn-lock spare in empty Ammo/Extra slot, log loadout
```

## Invariants

- Any profile other than `HLin-Random Cursed` does not change GunGame behavior.
- HLin-Random Cursed intercepts only native `WeaponBuffer.SpawnAsync`; promotion,
  demotion, enemy, and run-count lifecycle remain native.
- HLin-Random Cursed contains 64 valid placeholder tiers, preserving native
  weapon-count selection through 64 weapons.
- Use the live `ItemSpawnerV2` random-gun API; do not maintain a second random
  weapon or attachment selector.
- No polling: one coroutine runs per GunGame equipment transition and stops
  after a bounded result wait.
- If direct interception or Item Spawner is unavailable, retain normal GunGame
  profile spawning instead of crashing or deleting current equipment.
- Resolve GunGame types from the active `WeaponBuffer` / `Progression` assembly;
  never select a duplicate disabled GunGame assembly through global lookup.
- A `WeaponChangedEvent` caused by a direct interception only acknowledges that
  same pending transition. Later promotions remain eligible for generation.
- Only Cursed spare feeds still occupying the exact Ammo or Extra slot selected
  by Cursed are removed on transition. Moved feeds and every other quickbelt
  object remain player-owned.
- A loaded magazine, clip, speedloader, or cartridge gets one matching spare
  only when an assigned Ammo or Extra slot is empty; no player-owned slot is
  replaced.
- Generated feeds already attached to the firearm count as loaded. Loose,
  incompatible generated feeds are removed after one compatible spare decision.
- Log one completed random loadout with firearm, feeds, and attachments.

## Decisions

| Decision | Reason | Date |
| --- | --- | --- |
| Use native HLin-Random Cursed profile selection. | Live log proves custom startup lookup never found `GameSettings`; profile loader is already stable, visible map UI. | 2026-07-20 |
| Subscribe to `WeaponChangedEvent`, not `SpawnAndEquip`. | Live log never entered registered direct Harmony prefixes; GunGame source invokes this event after equipment transition. Runtime proof remains required. | 2026-07-20 |
| Preserve filled Ammo and Extra quickbelt slots. | Player-prepared magazines and scopes stay selected; random spares use empty slots only. | 2026-07-20 |
| Remove custom Atlas-panel behavior. | New profile is visible through GunGame's existing profile loader; no scene lifecycle/UI clone is needed. | 2026-07-20 |
| Intercept `WeaponBuffer.SpawnAsync`. | It is GunGame's actual two-argument spawn coroutine. Returning an empty coroutine after random API start prevents visible placeholder G17 spawn while preserving native progression flow. | 2026-07-22 |
| Track only Cursed-owned gun and slot-bound spares. | Broad object tracking deleted feeds after players moved them. Identity-checking only Cursed's original spare slots preserves all other quickbelt content. | 2026-07-22 |
| Clone loaded feed from its FVRObject wrapper. | H3VR's random-gun button instantiates `FVRObject.GetGameObject()`; matching that native construction yields one compatible spare without a second weapon roll. | 2026-07-22 |
| Bind reflection to active GunGame assembly. | BepInEx can load an older GunGame DLL beside active version; global type lookup can patch inactive `WeaponBuffer`. | 2026-07-22 |
| Acknowledge direct transition event once. | Native `WeaponChangedEvent` can follow suppressed `SpawnAsync`; treating it as a second transition rolls and destroys an unnecessary random gun. | 2026-07-22 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| P0 | Runtime smoke test on GunGame map with Item Spawner V2. | Start/promotion/demotion each yield one hand-equipped random gun. |
| P0 | Validate generated magazine, cartridge, clip, and speedloader paths. | Loaded gun and spare quickbelt feed work without exceptions. |
| P0 | Verify HLin-Random Cursed profile load and native event subscription. | Log names profile load, event subscription, and selected transition. |
| P0 | Verify profile choice appears on supported GunGame maps. | `HLin-Random Cursed` is visible/selectable in normal profile list. |
