# NightForcePlus Design

## Purpose

High-magnification NightForce optic with integrated bubble level and included
Black BubbleLevel mount. Both bubbles must follow world gravity consistently.

## Scope

| In scope | Out of scope |
| --- | --- |
| Shared gravity controller, NightForce regression coverage, package-source hygiene, authorized Thunderstore release | Reticle/art/UI redesign |

## Architecture

```text
world gravity
  -> BubbleLevel/GravityBubbleLevelController.cs: GravityBubbleLevelMotion
  -> BubbleLevel or BubbleLevelScope wrapper
  -> bubble local-X travel with damping and hard stops
```

`GravityBubbleLevelMotion` is owned by the BubbleLevel Unity source. MeatKit
requires the script used by a prefab to be included in that prefab's package,
so both package assemblies compile this one source while `BubbleLevel` and
`BubbleLevelScope` retain their existing component classes, serialized field
names, and prefab script GUIDs.

## Invariants

- Existing `baseObject`, `attachment`, and `level_bubble` prefab references stay valid.
- Bubble position derives from world gravity projected onto its own vial parent.
- Local-X travel stays within configured hard stops under inversion and rapid rotation.
- Normal and 180-degree mounting move toward one shared world-uphill side.
- BubbleLevel keeps released 3.25-degree calibration; NightForce starts at 2.64 degrees.
- Scope zoom, reticle, zero, elevation, windage, mesh, and UI wiring remain unchanged.

## Decisions

| Decision | Reason | Date |
| --- | --- | --- |
| Shared static motion source in BubbleLevel | One physics implementation source; MeatKit-safe package-local component types. | 2026-07-17 |
| Keep thin component wrappers | Changing script GUIDs or prefab component types risks missing-script migration. | 2026-07-17 |
| External BubbleLevel package boundary | NightForce declares BubbleLevelSet for the Black mount by object ID; it includes no BubbleLevelSet component types or mount content. | 2026-07-17 |
| Release-to-main rule | A released package must correspond to the final `main` commit; `main` tracks the current public releases, while feature branches remain unreleased. | 2026-07-17 |
| Release documentation boundary | User authorized a documentation-only archive overlay. It replaced README and added CHANGELOG from source `main`; DLLs, bundles, manifest, and every other archive entry content hash remained unchanged before publish. | 2026-07-18 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| P1 | Versioned package dependency refresh | NightForce release depends on BubbleLevelSet `2.0.4` for the Black mount. |
| P1 | Profile/README release alignment | Verified versioned release records matching profile, README, changelog, and dependency versions. |
