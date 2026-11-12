# GunGame Progressions

GunGame, supercharged.

GunGame Progressions expands GunGame with compatible gear, distinct difficulty styles, and weapon and Sosig pools that reflect the content enabled in your H3VR profile.

> **Modded profiles generate in the background; time depends on mod count. Reload the GunGame map to show them.**

## Requirement

- [GunGame](https://thunderstore.io/c/h3vr/p/Kodeman/GunGame/)

## Choose a Pool

The built-in vanilla selection covers **660 firearms** with validated compatible feeds, ready for a dependable Rot-focused game. Its versioned vanilla metadata snapshot and both fallback profiles are included in every package.

| Profile | Weapons | Enemies | Play style |
| --- | --- | --- | --- |
| HLin - Vanilla Rot | Vanilla firearms | Rotwieners only | A consistent, lower-pressure progression. |
| HLin - Modded Rot | Currently enabled mod firearms only | Rotwieners only | Practice your active mod collection without mixing in vanilla guns. |
| HLin - Vanilla Mixed Enemy | Vanilla firearms | Active vanilla Sosigs | A varied vanilla combat climb. |
| HLin - Modded Mixed Enemy | Currently enabled mod firearms only | Active vanilla and custom Sosigs | A full mixed encounter using the modded weapons you have enabled. |

Rot pools are the most predictable option. Mixed Enemy pools are for a more varied session, with a wide range of weapons and opponents. Earlier versions named these profiles "Runtime 01 - Vanilla Rot" through "Runtime 04 - Modded Mixed Enemy"; saved Modded profiles are renamed automatically.

## Modded Profile Refresh

Your last complete Modded profiles remain available after restarting H3VR. The mod refreshes them in the background and replaces them only after a complete new pair is ready; a confirmed empty active mod set removes the old pair so disabled items cannot be selected.

## First Start

The packaged Vanilla profiles are available immediately. Runtime generation then refreshes them from the live vanilla registry and builds Modded profiles after content finishes registering.

| When | What happens |
| --- | --- |
| H3VR starts | Vanilla metadata and profiles refresh as soon as the object registry is available. |
| One, five, ten, and thirty minutes after H3VR starts | Background Modded rescans check for content that registered late. The thirty-minute rescan is the last one. |
| First GunGame map | Vanilla choices remain available, and any saved Modded pair is restored. Entering a map does not start a scan. |
| Later GunGame maps | They open normally. Reload the map to show a newly saved Modded pair; reloading does not start generation. |
| After the thirty-minute rescan | Nothing starts another background refresh until H3VR restarts. |
| A refresh finds more compatible mod guns | It replaces the Modded profiles for the next GunGame load. |
| A refresh finds the same or fewer guns | It keeps the larger existing profiles. Exception: after a GunGame Progressions update changes its loadout rules, a rescan at ten minutes or later can replace them once. |

Each scheduled refresh takes a snapshot of the Modded content registered so far, then builds the profiles in the background while the game keeps running. If the Modded profiles are not ready yet, play a Vanilla profile, then reload the GunGame map after a later refresh finishes.

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
- An optic that matches a gun's own mount is chosen first. A modded gun without one falls back to a vanilla red dot or low-power scope, attached only when the gun has a sight mount that fits it. Magnified scopes from mods are never chosen.
- Reflex sights and scopes can appear; magnifiers and unrelated attachments are left out.
- Every run can offer a different compatible choice, keeping familiar weapons from feeling identical.

## Your Enabled Content

The pools follow the content enabled in your current H3VR profile. Vanilla pools stay vanilla. Modded pools are built only from enabled mod firearms. A gun from a weapon mod you later disable or uninstall is skipped when it would spawn, and it leaves the pool when a refresh replaces the saved Modded profiles. The mixed modded pool can also draw from enabled custom Sosigs.

## License

MIT. See the repository [LICENSE](https://github.com/haohlin/H3VR-Mods/blob/main/LICENSE).

## Changelog

See the **Changelog** tab on this package page.
