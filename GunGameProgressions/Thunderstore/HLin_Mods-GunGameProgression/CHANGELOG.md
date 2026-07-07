# Changelog

All notable changes to GunGame Progressions are documented here.

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
