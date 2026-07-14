# GunGame Progressions Testing

## Automated

| Check | Command | Pass evidence |
| --- | --- | --- |
| Public CI | GitHub Actions `Verify H3VR Packages` | Portable data/package tests pass; no H3VR-only assemblies required. |
| Full Windows pipeline | `tools/h3vr.ps1 -Action Test` | All pipeline and runtime profile tests pass. Wrapper enables exporter tests only here. |
| API targets | `tools/h3vr.ps1 -Action Verify -Mod GunGameProgressions` | External GunGame spawn targets resolve. |
| Package | `tools/h3vr.ps1 -Action Build -Mod GunGameProgressions`, then `Package` | Receipt has expected version, payload, hash. |

## H3VR acceptance

| Case | Expected result |
| --- | --- |
| Startup | Vanilla pools available; game stays responsive. |
| Many mods loading | Modded work remains background; no main-thread freeze. |
| Selector reload | Saved/generated Modded pair appears with Vanilla pair. |
| Invalid generated object | Bad loadout skips; progression continues without crash. |
| Disable mod content | Confirmed empty refresh removes stale IDs. |

Record deployed version, BepInEx status lines, selected pool counts, and VR
result in `STATUS.md` after each runtime task.
