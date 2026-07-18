# Nuketown GunGame Compatibility Patch

Makes the original **BO1 Nuketown GunGame** map appear beside current GunGame
maps without replacing its DLL, bundle, pools, or any owner asset.

## What it does

- Validates exact Nuketown GunGame `2.1.6` package layout at startup.
- Registers only its existing `nuketown` Atlas bundle once.
- Runs no `Update`, coroutine, polling loop, Harmony patch, or scene scan.

## What it does not do

- Does not modify r2modman profile files.
- Does not replace `NuketownGunGame.dll`.
- Does not include Nuketown map files, models, sounds, textures, or pools.
- Does not change GunGame rules, progression, balance, or performance outside
  one startup registration call.

## Compatibility

Supports `localpcnerd-NuketownGunGame` `2.1.6` with `Kodeman-GunGame` and
Atlas installed through Thunderstore. Unsupported layouts fail closed with a
clear BepInEx log message.

After installation, start H3VR and select **BO1 Nuketown** from Atlas maps.
