# GunGame Progressions

GunGame, supercharged.

GunGame Progressions expands GunGame with compatible gear, distinct difficulty styles, and weapon and Sosig pools that reflect the content enabled in your H3VR profile.

## Requirement

- [GunGame](https://thunderstore.io/c/h3vr/p/Kodeman/GunGame/)

## Choose a Pool

The built-in vanilla selection covers **615 firearms** with **553 compatible magazines**, ready for a dependable Rot-focused game.

| Profile | Weapons | Enemies | Play style |
| --- | --- | --- | --- |
| Runtime 01 - Vanilla Rot | Vanilla firearms | Rotwieners only | A consistent, lower-pressure progression. |
| Runtime 02 - Modded Rot | Currently enabled mod firearms only | Rotwieners only | Practice your active mod collection without mixing in vanilla guns. |
| Runtime 03 - Vanilla Mixed Enemy | Vanilla firearms | Active vanilla Sosigs | A varied vanilla combat climb. |
| Runtime 04 - Modded Mixed Enemy | Currently enabled mod firearms only | Active vanilla and custom Sosigs | A full mixed encounter using the modded weapons you have enabled. |

Rot pools are the most predictable option. Mixed Enemy pools are for a more varied session, with a wide range of weapons and opponents.

## First Start

Vanilla profiles are generated at startup. The first GunGame session deliberately gives mod content time to register before Kodeman reads its pool files.

| When | What happens |
| --- | --- |
| H3VR starts | A 15-second warmup runs while H3VR and mod loaders continue normally. |
| First GunGame map | After the startup warmup, the selector waits 15 seconds, generates the current Modded profiles, then loads them. |
| Later GunGame maps | They open normally; a background refresh starts. |
| GunGame map closes | Another background refresh runs, so content that finished loading during the session can be included. |
| A refresh finds more compatible mod guns | It replaces the Modded profiles for the next GunGame load. |
| A refresh finds the same or fewer guns | It keeps the larger existing profiles. |

This is a yielding wait, not a hard main-thread sleep: mod loaders continue to run. If you enter GunGame during the initial 15-second startup warmup, the first selector waits for the remaining startup warmup and its own 15-second window, then finishes the profile write. Later content continues to be discovered in the background.

## Enemy Pacing

Mixed Enemy pools are built around a steady difficulty rise:

- Rotwieners and basic front-line Sosigs appear most often.
- Standard combat Sosigs are common enough to keep runs varied.
- Specialist and operator Sosigs are uncommon.
- Heavy and high-tier operators are rare, so they remain a challenge rather than the normal encounter.

Mixed Enemy pools use GunGame's **Count mode**, starting at **3 kills to advance**. Each kill advances the current weapon by one, and GunGame lets you choose any higher or lower count from its own menu.

## Compatible Loadouts

- Guns receive usable ammunition: magazines first, then clips or speedloaders, then cartridges when needed.
- An internally fed shotgun receives its compatible shells. A true box-mag shotgun receives its compatible magazine; the generator does not treat every shotgun as shell-fed.
- Optics appear only when they fit the firearm's own mounting point.
- Reflex sights and scopes can appear; magnifiers and unrelated attachments are left out.
- Every run can offer a different compatible choice, keeping familiar weapons from feeling identical.

## Your Enabled Content

The pools follow the content enabled in your current H3VR profile. Vanilla pools stay vanilla. Modded pools use only enabled mod firearms, so disabled or uninstalled weapon mods are never selected. The mixed modded pool can also draw from enabled custom Sosigs.

## License

MIT. See the repository [LICENSE](https://github.com/haohlin/H3VR-Mods/blob/main/LICENSE).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
