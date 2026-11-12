# Nuketown GunGame Compatibility Patch Development Status

## Status

Last verified: `2026-07-18`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Compatibility plugin, package metadata, and changelog committed through `30456b0` on `feat/nuketown-gungame-compat`. | Verified |
| Runtime cause | Default-profile log skips Nuketown's `GunGame 1.0.3` because core `GunGame 1.0.4` owns same plugin ID; package contains `nuketown` while skipped DLL targets `gungame`. | Verified |
| Package | Owner package files, `nuketown` bundle, descriptor, and pools are present. | Verified |
| Test / verify | Windows `h3vr.ps1 -Action Test`: 87 passed, 0 failed. `Verify` passed. | Verified |
| Build / package | Windows Release build: 0 warnings, 0 errors. Package `1.0.0` SHA-256: `86FCFF9632E8722087E71EAE4928498571011C4A02B0A288D434BD0956135009`. | Verified |
| Deploy | Wrapper deployed package and created a rollback-capable receipt. | Verified |
| Launch / VR | r2modman native Modded launch created `h3vr.exe`. BepInEx loaded patch `1.0.0`; Atlas registered BO1 Nuketown; compatibility registration took 91 ms and logs no per-frame work. | Verified |
| Runtime warning | Profile BepInEx is `5.4.17`; deployed plugin was compiled against `5.4.20`, producing a non-fatal compatibility warning despite successful load. | Verified |
| BepInEx reference pin | Project file now references `BepInEx.Core` `5.4.17` instead of `5.*`; a source test requires the pin. Source edited on macOS, not built. Build, package, deploy, and launch rows above describe the package built before the pin. | Pinned in source; portable test run, Windows rebuild, and startup proof pending |
| Atlas dependency pin | Manifest now pins `nrgill28-Atlas-1.0.1` instead of `1.0.0`, matching the plugin's `BepInDependency` minimum of Atlas `1.0.1`; evidence is in `DESIGN.md` Decisions. Manifest edited on macOS. The Windows package recorded above still carries the `1.0.0` pin. | Repackage pending; portable test run pending |
| Skip log and player guide | Skip log line now ends with the consequence for the player, and a trailing period in the reason is trimmed. Player guide is reorganized into requirements, steps, unchanged items, and troubleshooting; it quotes the skip log prefix, which a source test keeps identical. Troubleshooting also names a missing or outdated Atlas or GunGame as a cause; that the patch does not load without them follows from its dependency attributes and was not observed in a live run. Source edited on macOS, not built. | Portable test run and Windows rebuild pending |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| BepInEx reference warning | Reference is pinned in source. Rebuild on Windows, redeploy, and repeat startup proof; the BepInEx log must show no version warning for this plugin. | Codex |
| GunGame dependency pin not checked | Manifest pins `Kodeman-GunGame-1.0.2` and the plugin requires GunGame plugin version `1.0.2` or newer. Read the plugin version inside that release's DLL, as was done for Atlas, before publishing. | Owner |
| Full map gameplay acceptance | Runtime load and GunGame progression test in H3VR. | Codex + player VR acceptance |

## Plan

Keep one item active.

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Implement, package, and deploy compatibility plugin. | Package contains only the compatibility DLL and metadata; Windows build, package, and deploy passed. |
| `[x]` | Launch and inspect BepInEx registration. | r2modman native launch created `h3vr.exe`; log shows one compatibility registration and Atlas scene registration. |
| `[>]` | Perform manual VR gameplay acceptance. | BO1 Nuketown appears, loads, starts, promotes, demotes, respawns, and exits normally. |
| `[ ]` | Rebuild with pinned BepInEx reference and repeat startup proof. | Windows `Test`, `Verify`, and Release build pass; redeployed plugin loads without a BepInEx version warning. |
| `[ ]` | Publish with explicit user approval. | Versioned package passes release checks. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| Medium | Additional upstream Nuketown versions | Only 2.1.6 package layout is verified. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Source / unit | `h3vr.ps1 -Action Test` | 87 passed, 0 failed on Windows. |
| Source API | `h3vr.ps1 -Action Verify -Mod NuketownGunGameCompatibilityPatch` | Passed on Windows. |
| Build / package | `Build`, `Package`, `Deploy` through wrapper | Build 0 warnings / 0 errors; package and deployment succeeded. |
| Source edits after last Windows run | Portable `dotnet test tests/H3vrPipeline.Tests/H3vrPipeline.Tests.csproj`; then Windows `Test`, `Verify`, and `Build`. | Pending; not run since the BepInEx reference pin and the skip log change. Evidence in rows above predates both. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Startup | Compatibility plugin loads once and Atlas registers BO1 Nuketown. | Verified: `Nuketown GunGame Compatibility Patch 1.0.0` loaded; Atlas registered BO1 Nuketown; registration took 91 ms. |
| Map selector | Atlas list shows the entry as **BO1 Nuketown**, the label the player guide names, and the map loads. | Pending |
| Gameplay | Start, kill promotion, death demotion, respawn, and exit work. | Pending |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Deployment receipt created.
- [x] BepInEx log checked.
- [ ] Rebuild, redeploy, and log recheck after BepInEx reference pin and skip log change.
- [ ] Required VR interaction completed.
