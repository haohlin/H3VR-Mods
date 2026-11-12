# GunGame Cursed Random

GunGame where every tier is a random gun from H3VR's Item Spawner.

Adds one profile, **HLin-Random Cursed**, to GunGame's normal profile list. Every start, promotion, and demotion hands you whatever the Item Spawner's random-gun button rolls: firearm, ammunition, and attachments.

> **Needs a GunGame map that contains an Item Spawner.** Without one, this profile gives you a Glock 17 on every tier.

## Requirement

- [GunGame](https://thunderstore.io/c/h3vr/p/Kodeman/GunGame/)

## Use

1. Open a GunGame map.
2. Select **HLin-Random Cursed** from the profile list.
3. Set Sosig count, health, kills, weapon count, and quickbelt slots as usual.
4. Start the game.

## What Stays the Same

- GunGame still controls Sosigs, kills to advance, hand setting, run length, and quickbelt slots.
- Anything already in your quickbelt stays there. One spare magazine, clip, speedloader, or cartridge is added only when an Ammo or Extra slot is empty.
- The profile has 64 tiers, so GunGame's weapon-count selector works up to 64 weapons.
- Other profiles are not changed.

## If You Get a Glock 17 Every Tier

The map has no Item Spawner, so there is no random-gun button for the mod to use. GunGame then runs the profile's built-in placeholder, a Glock 17 on every tier. Choose another profile on this map, or use a GunGame map that has an Item Spawner.

A single Glock 17 tier can also appear when three random rolls in a row produce a gun that cannot be loaded.

The BepInEx log records each random loadout (firearm, feeds, attachments) and one warning when no Item Spawner is found.

## License

MIT. See the repository [LICENSE](https://github.com/haohlin/H3VR-Mods/blob/main/LICENSE).
