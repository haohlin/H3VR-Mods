# NightForcePlus Design

## Purpose

High-magnification NightForce optic with integrated bubble level and included
Black BubbleLevel mount. Both bubbles must follow world gravity consistently.

## Scope

| In scope | Out of scope |
| --- | --- |
| Shared gravity controller, native PIP scope, reticle calibration, package-source hygiene, authorized Thunderstore release | Reticle/art/UI redesign beyond validated calibration |

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

The optic uses H3VR's serialized native `PIPScope`, `PIPScopeController`, and
direct-hand PIP interactions. The retired `ScopeShaderZoom` component is not
present at runtime. The magnification ring moves continuously across 7--35x
with native geometric progression; its visual ring positions are derived from
that progression rather than the archived wheel-angle list.

## Invariants

- Existing `baseObject`, `attachment`, and `level_bubble` prefab references stay valid.
- Bubble position derives from world gravity projected onto its own vial parent.
- Local-X travel stays within configured hard stops under inversion and rapid rotation.
- Normal and 180-degree mounting move toward one shared world-uphill side.
- BubbleLevel keeps released 3.25-degree calibration; NightForce starts at 2.64 degrees.
- User-adjusted control and mount transforms, mesh, and attachment wiring remain unchanged.
- The optic uses native PIP components only; no `ScopeShaderZoom` runtime component or
  archived magnification-angle mapping remains.
- No static legacy scope Canvas or render camera is serialized. `PIPScopeController`
  creates its native popup UI at runtime.
- Rear lens, front lens, PIP scope root, and PIP camera share one positive forward optical
  axis. Native clip distances derive from front-lens spacing.
- Magnification, elevation, and windage proxy transforms remain authored placements while
  their native interactions retain controller links and click feedback.
- Reticles are first-focal-plane. Their native angular canvas size is fixed while
  magnification changes smoothly, so their apparent size follows the archived
  `M / 7` visual multiplier without copying the archived shader value.

## Decisions

| Decision | Reason | Date |
| --- | --- | --- |
| Shared static motion source in BubbleLevel | One physics implementation source; MeatKit-safe package-local component types. | 2026-07-17 |
| Keep thin component wrappers | Changing script GUIDs or prefab component types risks missing-script migration. | 2026-07-17 |
| External BubbleLevel package boundary | NightForce declares BubbleLevelSet for the Black mount by object ID; it includes no BubbleLevelSet component types or mount content. | 2026-07-17 |
| Release-to-main rule | A released package must correspond to the final `main` commit; `main` tracks the current public releases, while feature branches remain unreleased. | 2026-07-17 |
| Release documentation boundary | User authorized a documentation-only archive overlay. It replaced README and added CHANGELOG from source `main`; DLLs, bundles, manifest, and every other archive entry content hash remained unchanged before publish. | 2026-07-18 |
| Native PIP optic | Replace the obsolete custom scope runtime with H3VR's PIP scope/controller/interactions while preserving user-adjusted prefab transforms. | 2026-07-20 |
| Reticle scaling boundary | The archived shader started at `0.846` and multiplied by `M / 7`. `0.846` is mesh/UV-shader-specific, so the native PIP implementation preserves the transferable `M / 7` FFP behavior and uses angular reticle canvases. | 2026-07-20 |
| Native PIP presentation repair | Remove serialized legacy presentation, normalize the supported PIP optical axis, and preserve authored controls while restoring native interaction feedback. | 2026-07-20 |
| Private recovered-prefab boundary | Generic importer may resolve a full GUID-plus-local-ID script pointer into a private inspection candidate, but copies no recovered C# and may never supply release/runtime content. ST6T comparison is diagnostic; NightForce remains authored native PIP. | 2026-07-20 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| P1 | Versioned package dependency refresh | NightForce release depends on BubbleLevelSet `2.0.4` for the Black mount. |
| P1 | Profile/README release alignment | Verified versioned release records matching profile, README, changelog, and dependency versions. |
| P1 | Native PIP VR acceptance | Spawn, grab, rail mount, magnification, zero/elevation/windage, reticle switching, and reticle centering pass in H3VR. |
| P2 | Generic ATACR reticle source data | Its artwork has no verified manufacturer subtension; replace it or measure it before treating it as MOA/MRAD-accurate. |
