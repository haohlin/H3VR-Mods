# GunGame Progressions

GunGame Progressions provides Advanced GunGame profiles and a lightweight runtime exporter for H3VR. It builds compatible loadouts from the content currently enabled in the player's H3VR profile, without modifying the GunGame plugin.

## Current Catalog

The packaged vanilla fallback profiles cover **615 supported vanilla firearms** with **553 compatible mags** selected from verified compatible loadouts. At runtime, the exporter refreshes the catalog for the player's installed content, adding all supported active modded guns and custom Sosigs without referencing disabled or missing mods.

## Requirement

- [GunGame](https://thunderstore.io/c/h3vr/p/Kodeman/GunGame/)

## Included Profiles

The package always includes these safe vanilla fallbacks:

| Profile | Firearms | Enemies |
| --- | --- | --- |
| Runtime 01 - Vanilla Rot | Vanilla only | Rotwieners only |
| Runtime 03 - Vanilla Mixed Enemy | Vanilla only | Rot, Scout, Riflewiener, SpecOps, and Heavy |

After H3VR finishes loading active content, the exporter writes runtime profiles from the live object and Sosig registries:

| Profile | Firearms | Enemies |
| --- | --- | --- |
| Runtime 01 - Vanilla Rot | Active vanilla | Rotwieners only |
| Runtime 02 - Modded Rot | Active vanilla and enabled modded firearms | Rotwieners only |
| Runtime 03 - Vanilla Mixed Enemy | Active vanilla | All active vanilla spawnable Sosigs |
| Runtime 04 - Modded Mixed Enemy | Active vanilla and enabled modded firearms | All active vanilla and enabled modded spawnable Sosigs |

Profiles 02 and 04 are generated only when active modded firearms exist. "Modded" profiles include the vanilla set as well as enabled modded content; they do not reference disabled or absent mods.

GunGame reads profiles during startup and does not support a live profile reload. The packaged fallbacks are therefore immediately selectable. A runtime profile set refreshed after other mods finish loading is reliably available on the next H3VR launch.

## Mixed Enemy Progression

Mixed Enemy profiles use **Count mode**. The default is **3 kills to advance**, and each player kill advances the current weapon by exactly one kill. The GunGame menu can change this count from 1 upward.

Enemy values control spawn likelihood only. Core H3VR factions (`RW_*`, `M_Swat_*`, `M_MercWiener_*`, and `Comperator_*`) receive extra weighted entries so they appear more often. Easier core enemies have higher weights: Rot 8, Scout 5, Riflewiener 3, SpecOps 2, and Heavy 1. Other active spawnable Sosigs remain eligible at lower weight.

## Loadouts and Optics

- Every firearm uses compatible ammunition only: magazine first, then clip, then cartridge or speedloader when required.
- All compatible feed options are recorded in `MagNames`.
- Optics are assigned only when the firearm and optic have an exact verified physical mount match.
- Reflex sights and PIP scopes are eligible; magnifiers and unclassified attachments are excluded.
- Firearms without compatible feeds, and items listed in `profile-rules.json`, are excluded.

## Runtime Files

The exporter writes these files under the installed `HLin_Mods-GunGame_Progressions` folder:

- `ObjectData.json`: active object metadata snapshot.
- `GunGameWeaponPool_Runtime_*.json`: generated GunGame profiles.
- `RuntimePools/`: runtime generation receipt and catalog data.

The package contains no game assemblies or static list of modded objects. Each player obtains a profile for their own active mod set at runtime.

## License

MIT. See the repository [LICENSE](https://github.com/h3vr-modding/H3VR-Mods/blob/main/LICENSE).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
