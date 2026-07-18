# Nuketown GunGame Compatibility Patch Design

## Purpose

Make `localpcnerd-NuketownGunGame` 2.1.6 appear and run through Atlas alongside
the maintained `Kodeman-GunGame` package, without replacing or redistributing
Nuketown assets.

## Scope

| In scope | Out of scope |
| --- | --- |
| One BepInEx startup component that validates original package files, reuses its assembly hooks, and registers its `nuketown` bundle once. | Editing `NuketownGunGame.dll`, its bundle, scene, pools, or r2modman files. |
| Thunderstore dependencies on Nuketown, GunGame, and Atlas. | Redistributing owner assets or creating a replacement map project. |
| Startup and map-session log evidence. | Per-frame polling, gameplay balance changes, or unrelated GunGame patches. |

## Architecture

```text
Thunderstore dependencies
  -> compatibility plugin Awake once
  -> validate Nuketown package path, descriptor, and bundle
  -> Atlas RegisterScene(nuketown)
  -> scene loads its already-discovered original scripts
  -> original BO1 Nuketown map appears
```

## Invariants

- Package payload contains only compatibility DLL, manifest, README, and icon.
- Never modify, copy, or redistribute any Nuketown map file.
- No `Update`, coroutine polling, scene scan, or allocation after startup.
- Register only `localpcnerd-NuketownGunGame/nuketown`, once per process.
- Fail closed with clear log messages for unsupported or incomplete package state.
- Do not install Harmony patches, load a second Nuketown assembly, or alter
  original map code. Scene-owned GunGame hooks activate only when its map loads.

## Decisions

| Decision | Reason | Date |
| --- | --- | --- |
| Ship a compatibility plugin, not a patched replacement DLL. | Keeps owner package intact, updates safe, and payload distributable. | 2026-07-18 |
| Register only the original scene bundle. | Nuketown scene code owns its GunGame hooks at scene load; global duplicate hooks would widen scope and cost. | 2026-07-18 |
| Use reflection for Atlas registration. | Avoids bundling or compiling against Atlas binaries; reflection runs once only. | 2026-07-18 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| High | Runtime compatibility validation | Map registers, loads, starts, promotes, demotes, and returns without BepInEx errors. |
| Medium | Future Nuketown package support | Known package layout/version checks explicitly accept or reject each version. |
