# Changelog

All notable changes to GunGame Progressions are documented here.

## 1.4.0

- Add one-minute Modded rescan for earlier late-loader coverage; five- and ten-minute rescans remain.
- Log each completed Modded scan's duration and catalog entry count for lightweight runtime measurement.
- Restore catalog-proven firearms that omit optional identity tags, while still excluding malformed firearm entries.
- Use direct compatible feeds or exact magazine/clip interfaces for Modded pools; never assign a mod firearm a loose cartridge from shared caliber alone.
- Keep scope selection catalog-only: direct attachments and exact physical mount types only; no runtime prefab loading.
- Generate every available Modded snapshot in the background and retain saved profiles until a strictly larger compatible pair is ready.

## 1.3.9

- Restore the original package-card summary after the Modded-profile refresh note.
- Replace the fixed time estimate with "depends on mod count."

## 1.3.8

- Use an ASCII-only package-card notice so the Modded-profile refresh message displays correctly on mobile.
- Add a Markdown callout to the package Details page.

## 1.3.7

- Keep completed Modded profiles across H3VR restarts while generating refreshed profiles in the background.
- Reconcile mod firearm and feed data against resolved runtime prefab components; exclude props registered as firearms.
- Preserve the verified magazine, clip, speedloader, shell, and optic compatibility rules across Vanilla and Modded profiles.
- Add a prominent listing note for Modded-profile generation time and the GunGame reload step.

## 1.3.6

- Runtime pools now refresh once after enabled content finishes loading; the included vanilla profiles remain ready during startup.
- Prevented magnifier attachments from being selected as automatic scope loadouts.
- Improved runtime loadout validation for compatible firearms, feeds, and optics.

## 1.3.5

- Added separate Vanilla and Modded Rot and Mixed Enemy pools, so modded pools use only the weapon mods currently enabled in your profile.
- Added mixed enemy pacing: basic Sosigs are common, operators are uncommon, and heavy or high-tier operators are rare.
- Improved compatible loadouts: firearms receive the right magazines, clips, speedloaders, or cartridges, with suitable optics only on matching mounts.
- Updated the Thunderstore listing with a clearer player guide to pools, difficulty, and compatible gear.

## 1.3.4

- Refreshed the Thunderstore listing and icon with the verified vanilla catalog counts and runtime support for active modded guns and custom Sosigs.

## 1.3.3

- Mixed Enemy profiles now use GunGame Count mode: the default is 3 kills to advance and every kill contributes one advance.
- Reworked mixed-enemy values as spawn weights, preserving frequent easy core enemies and rare hard or unusual enemies without inflating the progression counter.
- Added a Count-mode vanilla mixed fallback so a newly installed package starts at three kills immediately.
- Added release documentation and packaged changelog metadata.

## 1.3.2

- Classify runtime Sosigs by built-in `SosigEnemyID` enum membership rather than UGC module ownership.
- Restore the vanilla mixed profile and restrict custom Sosigs to the active-content mixed profile.

## 1.3.1

- Favored Rotwieners, SWAT, MercWieners, and Operators through repeated equal-value entries while retaining every active Sosig type.

## 1.3.0

- Added startup metadata export with a later stable refresh after mod loading.
- Added origin-split vanilla and all-active Rot and Mixed Enemy runtime profiles.
- Added physical-mount-verified PIP scope and reflex sight selection.
- Excluded firearms without a compatible feed instead of assigning generic ammunition.

## 1.2.9

- Reduced packaged offline content to a single Rot baseline while making runtime-generated profiles the primary workflow.

## 1.2.8

- Added exact mount matching for optics, excluded magnifiers, and removed stale runtime pools before regeneration.

## 1.2.7

- Fixed runtime rules loading on H3VR Mono and enforced magazine, clip, then cartridge feed priority.

## 1.2.6

- Added Gauntlet and Bounty runtime modes.

## 1.2.4

- Converted profiles to GunGame Advanced format and added the independent runtime metadata exporter.

## Earlier releases

- Added and curated the original vanilla all-in-one firearm pools, blacklists, and additional weapon coverage through versions 1.0.0 to 1.2.0.
