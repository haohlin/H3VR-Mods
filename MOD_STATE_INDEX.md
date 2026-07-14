# H3VR Mod State Index

Git records source. These files record working state between chats, machines,
and agents. Chat history is never source of truth.

## Session rule

```text
start: read index + mod DESIGN/STATUS/PLAN/TESTING + inspect Windows state
work:  update PLAN when scope changes; add test before behavior change
finish: update STATUS evidence + PLAN next step + commit source and docs together
```

Create four files from [templates](docs/mod-development/README.md) before work
starts on any new active mod. Do not create status files for dormant folders
without verified state.

| Active mod | State | Required records |
| --- | --- | --- |
| [BubbleLevel](BubbleLevel/STATUS.md) | Active Unity/MeatKit migration | [design](BubbleLevel/DESIGN.md), [plan](BubbleLevel/PLAN.md), [testing](BubbleLevel/TESTING.md) |
| [GunGameProgressions](GunGameProgressions/STATUS.md) | Released; compatibility follow-up | [design](GunGameProgressions/DESIGN.md), [plan](GunGameProgressions/PLAN.md), [testing](GunGameProgressions/TESTING.md) |
