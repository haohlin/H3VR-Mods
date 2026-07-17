# BubbleLevel Design

## Purpose

Sniper-oriented rail and mount bubble levels. Bubble responds to world gravity,
not a fixed local Euler angle.

## Controllers

```text
world gravity
  -> project onto bubble parent's local right axis
  -> equilibrium local X
  -> spring + liquid drag + static settle
  -> hard travel stop
```

| Part | Controller | Rule |
| --- | --- | --- |
| Rail/attachment level | `BubbleLevel` | Gravity-projected local-X travel, damping, hard stops. |
| Integrated 30 mm mounts | `BubbleLevelMount` | Separate pivot/rotation controller; test separately. |

## Behavior contract

- Attachment chains and pre-canted mounts use world gravity through final object transform.
- Bidirectional/180° rail mounting keeps bubble on same **world uphill** side for
  same physical cant. Local travel sign reverses with rail direction; world
  result must not. Never infer this from Euler angles.
- Bubble never leaves `[minimumLocalX, maximumLocalX]`, including inversion and rapid movement.
- 1° cant must move bubble at least `0.1` local units from center.
- 4° cant must reach travel stop.
- Motion must settle without visible jitter.
- No per-frame allocations or repeated vector normalization in steady state.

## Material decision

CC0 metal/plastic candidate assets and preview renders are review-only. Production
prefab material assignments need explicit visual approval and an applied-proof
render before build.

## Source authority

Current production Unity project is versioned in `H3VR-unity-projects`, whose
Git root is the in-place `Assets/Projects` folder. It tracks project assets and
matching `.meta` files. Generated bundles, `Library`, and `.vs` stay untracked.
This repository records pipeline and handoff docs only. Current verified state,
plan, and test evidence live in `DEV_STATUS.md`.
