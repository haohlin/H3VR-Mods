# NightForcePlus Design

## Purpose

High-magnification NightForce optic with integrated bubble level and included
Black BubbleLevel mount. Both bubbles must follow world gravity consistently.

## Scope

| In scope | Out of scope |
| --- | --- |
| Shared gravity controller, NightForce regression coverage, package-source hygiene | Reticle/art/UI redesign, public release, Thunderstore publish |

## Architecture

```text
world gravity
  -> BubbleLevel/GravityBubbleLevelController.cs
  -> BubbleLevel or BubbleLevelScope wrapper
  -> bubble local-X travel with damping and hard stops
```

`GravityBubbleLevelController` is owned by BubbleLevel Unity source in
`HLin_Mods.BubbleLevelSet`. `BubbleLevel` and `BubbleLevelScope` keep their
existing component classes, serialized field names, and prefab script GUIDs.

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
| Shared abstract base in BubbleLevel source | One physics implementation; two Unity component identities remain stable. | 2026-07-17 |
| Keep thin component wrappers | Changing script GUIDs or prefab component types risks missing-script migration. | 2026-07-17 |
| No package version bump or publish now | Windows build/runtime configuration is unavailable; release versioning waits for verified package work. | 2026-07-17 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| P0 | Windows Unity and MeatKit validation | Current Unity runtime test, wrapper build, package, and H3VR VR cases pass. |
| P1 | Versioned package dependency refresh | NightForce release depends on first BubbleLevelSet release containing shared base class. |
| P1 | Profile/README release alignment | Verified versioned release records matching profile, README, changelog, and dependency versions. |
