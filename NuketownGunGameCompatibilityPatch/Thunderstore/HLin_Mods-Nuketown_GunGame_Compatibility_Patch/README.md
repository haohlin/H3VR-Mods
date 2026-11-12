# Nuketown GunGame Compatibility Patch

Makes the original **BO1 Nuketown GunGame** map show up again next to current GunGame maps.

The patch registers the map that the original Nuketown package already installed. It contains no map files and does not replace or edit anything in that package.

## Requirement

- [BO1 Nuketown GunGame](https://thunderstore.io/c/h3vr/p/localpcnerd/NuketownGunGame/) `2.1.6`
- [GunGame](https://thunderstore.io/c/h3vr/p/Kodeman/GunGame/)
- [Atlas](https://thunderstore.io/c/h3vr/p/nrgill28/Atlas/)

## Use

1. Install through Thunderstore or r2modman so the three requirements come with it.
2. Start H3VR.
3. Select **BO1 Nuketown** from the Atlas map list.

## What It Does Not Change

- GunGame rules, progression, and balance.
- `NuketownGunGame.dll`, the map, and its models, sounds, textures, and weapon pools.
- Your r2modman profile files.
- Frame rate: the patch runs once at startup and does nothing afterwards.

## If the Map Does Not Appear

The patch supports Nuketown GunGame `2.1.6` only. With any other version, or a missing file, it skips registration and writes one line that contains `Nuketown compatibility registration skipped:` to the BepInEx log. The line names the reason. Install version `2.1.6`, then restart H3VR.

The map also stays hidden when Atlas or GunGame is missing or out of date. The patch does not load without them, and it skips registration when Atlas cannot register the map. Install or update both, then restart H3VR.

## License

MIT. See the repository [LICENSE](https://github.com/haohlin/H3VR-Mods/blob/main/LICENSE).
