# GunGame Progressions Testing

## Automated

| Check | Command | Pass evidence |
| --- | --- | --- |
| Public CI | GitHub Actions `Verify H3VR Portable Checks` | Portable data/tests pass; no H3VR-only assemblies required. |
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

## P0 persistence regression — next Windows task

Write this failing test before production code:

| Saved pair | Candidate | Expected result |
| --- | --- | --- |
| none | complete, `24` weapons/pool | Promote first pair. |
| complete, `32` weapons/pool | complete, `24` weapons/pool | Keep saved pair. |
| complete, `32` weapons/pool | complete, `32` weapons/pool | Keep saved pair. |
| complete, `32` weapons/pool | complete, `33` weapons/pool | Replace saved pair. |
| complete, count unavailable | complete, any count | Keep saved pair conservatively. |
| complete, any count | confirmed empty | Remove stale pair. |

Use persisted receipt `eligibleWeaponsPerPool` first; if unavailable, inspect
both persisted pool files. If count still cannot be proven, preserve existing
pair. Run failing and passing test through Windows before implementation is
called complete.

Record deployed version, BepInEx status lines, selected pool counts, and VR
result in `STATUS.md` after each runtime task.
