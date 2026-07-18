# Nuketown GunGame Compatibility Patch Development Status

## Status

Last verified: `2026-07-18`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Compatibility plugin, package metadata, and changelog committed through `3af3a34` on `feat/nuketown-gungame-compat`. | Verified |
| Runtime cause | Default-profile log skips Nuketown's `GunGame 1.0.3` because core `GunGame 1.0.4` owns same plugin ID; package contains `nuketown` while skipped DLL targets `gungame`. | Verified |
| Package | Owner package files, `nuketown` bundle, descriptor, and pools are present. | Verified |
| Test / verify | Windows `h3vr.ps1 -Action Test`: 87 passed, 0 failed. `Verify` passed. | Verified |
| Build / package | Windows Release build: 0 warnings, 0 errors. Package `1.0.0` SHA-256: `86FCFF9632E8722087E71EAE4928498571011C4A02B0A288D434BD0956135009`. | Verified |
| Deploy | Wrapper deployed package and created a rollback-capable receipt. | Verified |
| Launch / VR | Active Windows desktop, Steam, loader, and map prerequisites verified. Three interactive-task Steam-URI dispatch variants produced no `h3vr.exe` or BepInEx output; direct Explorer task returned exit code `1`. | Blocked externally |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| BepInEx startup proof | Start H3VR from the active Windows Steam desktop, then inspect the profile log. The remote task dispatcher cannot currently trigger the Steam URI despite a valid user session. | User / Codex |
| Full map gameplay acceptance | Runtime load and GunGame progression test in H3VR. | Codex + player VR acceptance |

## Plan

Keep one item active.

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Implement, package, and deploy compatibility plugin. | Package contains only the compatibility DLL and metadata; Windows build, package, and deploy passed. |
| `[>]` | Launch and inspect BepInEx registration. | A Steam-desktop launch creates `h3vr.exe`; log shows one compatibility registration and Atlas scene registration. |
| `[ ]` | Perform manual VR gameplay acceptance. | Start, progression, death, respawn, and exit work normally. |
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

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Startup | Compatibility plugin loads once and Atlas registers BO1 Nuketown. | Pending Steam-desktop launch. |
| Map selector | BO1 Nuketown appears and loads. | Pending |
| Gameplay | Start, kill promotion, death demotion, respawn, and exit work. | Pending |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Deployment receipt created.
- [ ] BepInEx log checked.
- [ ] Required VR interaction completed.
