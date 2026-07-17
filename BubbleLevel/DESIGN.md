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
| Rail/attachment level | `BubbleLevel` | Wrapper over shared gravity motion; keeps BubbleLevel calibration. |
| Integrated 30 mm mounts | `BubbleLevelMount` | Separate pivot/rotation controller; test separately. |
| NightForce integrated scope level | `BubbleLevelScope` | Uses the same shared gravity-motion source; scope calibration stays independent. |

`GravityBubbleLevelController.cs` owns `GravityBubbleLevelMotion`: one shared
gravity projection, spring/drag, settle, and hard-stop source. MeatKit exports
prefab scripts only from their owning package, so each package compiles this
source while retaining its own component type. Wrapper classes retain their
existing Unity component identities and serialized field names.

BubbleLevelSet is self-contained (apart from OtherLoader). NightForcePlus
depends on BubbleLevelSet for the Black mount by object ID; it ships neither
BubbleLevelSet component types nor mount content.

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
