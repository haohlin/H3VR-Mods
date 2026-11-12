# H3VR-Mods

BepInEx and Thunderstore mods for H3VR (Hot Dogs, Horseshoes & Hand Grenades), published on Thunderstore as HLin_Mods.

## Mods

| Mod | Folder | Thunderstore |
| --- | --- | --- |
| GunGame Progressions | `GunGameProgressions/` | [GunGame_Progressions](https://thunderstore.io/c/h3vr/p/HLin_Mods/GunGame_Progressions/) |
| BubbleLevel | `BubbleLevel/` | [BubbleLevelSet](https://thunderstore.io/c/h3vr/p/HLin_Mods/BubbleLevelSet/) |
| NightForcePlus | `NightForcePlus/` | [NightForcePlus](https://thunderstore.io/c/h3vr/p/HLin_Mods/NightForcePlus/) |
| The Ping | `ThePing/` | [The_Ping](https://thunderstore.io/c/h3vr/p/HLin_Mods/The_Ping/) |
| Teleport | `Teleport/` | [Teleport](https://thunderstore.io/c/h3vr/p/HLin_Mods/Teleport/) |
| No WhiteOut | `RemoveWhiteOut/` | [No_WhiteOut](https://thunderstore.io/c/h3vr/p/HLin_Mods/No_WhiteOut/) |

The version and work state of each actively developed mod are in [MOD_STATE_INDEX.md](MOD_STATE_INDEX.md).

`BetterGasTank/`, `BetterHearing/`, `BetterScopeControl/`, `BetterSosigSpawner/`, `FasterSpectator/`, `LongRangeDrone/`, and `SR_Character/` hold older or experimental work that the build pipeline does not cover.

## Repository Layout

| Path | Holds |
| --- | --- |
| `<ModName>/` | One mod: its source, its `Thunderstore/` package page files (README, changelog, manifest, icon), and for active mods `DESIGN.md` and `DEV_STATUS.md` |
| `build/` | `mods.json` mod descriptors and environment templates; build output written here is ignored |
| `tools/` | `h3vr.ps1` build, package, and deploy pipeline; `h3vr-remote.sh` wrapper for macOS; package validator |
| `tests/` | Pipeline and contract tests |
| `docs/mod-development/` | Templates for `DESIGN.md` and `DEV_STATUS.md` |
| `skills/`, `.claude/`, `.codex/` | Workflow instructions for coding agents |

Unity content for BubbleLevel and NightForcePlus lives in a separate Unity project repository. This repository holds their pipeline descriptors and handoff records only.

## Working in This Repository

Start with [AGENTS.md](AGENTS.md), then [skills/h3vr-remote-development/SKILL.md](skills/h3vr-remote-development/SKILL.md). Builds, packaging, deployment, and runtime checks run on a Windows machine with H3VR installed. Machine-specific settings stay in ignored local files and never enter Git.

The portable test suite runs anywhere the .NET SDK is installed. CI runs it on Windows for pushes to `main` and `feat/**` and for pull requests:

```bash
dotnet test tests/H3vrPipeline.Tests/H3vrPipeline.Tests.csproj
```

## License

[MIT](LICENSE)
