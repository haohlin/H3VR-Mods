# H3VR Mod State Index

Git records source. These files record working state between chats, machines,
and agents. Chat history is never source of truth.

## Session rule

```text
start: read index + mod DESIGN/DEV_STATUS; preserve untracked files; if tracked local tree is clean, fetch, prove ancestry, then `git pull --ff-only`; inspect Windows state
work:  update DEV_STATUS Plan section when scope changes; add test before behavior change
finish: update DEV_STATUS Status evidence + Plan next step + commit source and docs together
```

Create two files from [templates](docs/mod-development/README.md) before work
starts on any new active mod. Do not create handoff records for dormant folders
without verified state.

| Active mod | State | Required records |
| --- | --- | --- |
| [BubbleLevel](BubbleLevel/DEV_STATUS.md) | Released; optional material/regression follow-up | [design](BubbleLevel/DESIGN.md), [development status](BubbleLevel/DEV_STATUS.md) |
| [GunGameProgressions](GunGameProgressions/DEV_STATUS.md) | Released; compatibility follow-up | [design](GunGameProgressions/DESIGN.md), [development status](GunGameProgressions/DEV_STATUS.md) |
