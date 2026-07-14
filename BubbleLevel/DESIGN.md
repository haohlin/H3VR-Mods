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

Current production Unity project must be versioned in `H3VR-unity-projects`
with project assets and matching `.meta` files. Generated bundles, `Library`,
and `.vs` remain untracked. This repository records shared mod docs and code
only when it is identical to authoritative Unity source.

## Known gap

Current tested configuration reports `0.095774` local movement at 1°. It does
not meet `>= 0.1`; tune full-scale angle and rerun all checks before release.
