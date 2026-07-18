# NightForcePlus Design

## Purpose

High-magnification NightForce optic with integrated bubble level and included
Black BubbleLevel mount. Both bubbles must follow world gravity consistently.

## Scope

| In scope | Out of scope |
| --- | --- |
| Shared gravity controller, NightForce regression coverage, package-source hygiene, native PIP migration, authorized Thunderstore release | Reticle/art/UI redesign or copied game/third-party assets |

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

Native PIP uses a project-owned runtime bridge. The current MeatKit editor API
does not expose H3VR's current PIP types at compile time, although the running
game does. The serialized prefab therefore retains `ScopeShaderZoom` only as
the source of NightForce-owned zoom, zero, reticle, and color data. Before the
legacy component runs in H3VR, `NativePipScopeBootstrap` resolves the installed
PIP API, creates the native scope/camera/controller/interactions, transfers the
NightForce data, and removes the legacy components. No game DLL, shader, or
third-party asset is copied into the project or package.

## Invariants

- Existing `baseObject`, `attachment`, and `level_bubble` prefab references stay valid.
- Bubble position derives from world gravity projected onto its own vial parent.
- Local-X travel stays within configured hard stops under inversion and rapid rotation.
- Normal and 180-degree mounting move toward one shared world-uphill side.
- BubbleLevel keeps released 3.25-degree calibration; NightForce starts at 2.64 degrees.
- Scope zoom, reticle, zero, elevation, windage, mesh, and UI wiring remain unchanged.
- Native PIP runtime creation uses only current installed H3VR types and
  NightForce-owned serialized references/data.

## Decisions

| Decision | Reason | Date |
| --- | --- | --- |
| Shared static motion source in BubbleLevel | One physics implementation source; MeatKit-safe package-local component types. | 2026-07-17 |
| Keep thin component wrappers | Changing script GUIDs or prefab component types risks missing-script migration. | 2026-07-17 |
| External BubbleLevel package boundary | NightForce declares BubbleLevelSet for the Black mount by object ID; it includes no BubbleLevelSet component types or mount content. | 2026-07-17 |
| Release-to-main rule | A released package must correspond to the final `main` commit; `main` tracks the current public releases, while feature branches remain unreleased. | 2026-07-17 |
| Release documentation boundary | User authorized a documentation-only archive overlay. It replaced README and added CHANGELOG from source `main`; DLLs, bundles, manifest, and every other archive entry content hash remained unchanged before publish. | 2026-07-18 |
| Manifest-first PIP research | Default to hash-backed structural manifests and Unity batch audits. An owner-authorized private full rip may validate recovery/reconstruction, but raw payloads never enter repository source, release packages, or Thunderstore. | 2026-07-18 |
| Native PIP reference basis | Fixed-scope and variable-scope reference manifests, plus current installed PIP API source, establish native camera, material, reticle, controller, and direct-hand interaction relationships before changing the NightForce prefab. | 2026-07-18 |
| Runtime PIP bridge | MeatKit's compile API lacks the current PIP types. Keep the prefab/package self-contained with a reflection bridge that resolves those types only from the installed game at runtime. | 2026-07-18 |

## Known limits / backlog

| Priority | Item | Done when |
| --- | --- | --- |
| P1 | Versioned package dependency refresh | NightForce release depends on BubbleLevelSet `2.0.4` for the Black mount. |
| P1 | Profile/README release alignment | Verified versioned release records matching profile, README, changelog, and dependency versions. |
| P1 | Native PIP scope migration | Recreate camera, game PIP material, project-owned reticle, controller, and hand controls in NightForce; all source assets remain original to this project. |

## Private reconstruction boundary

Owner-authorized full-rip work is isolated from this release project. It may
recover a reference package/game into a disposable private Unity project, prove
that a locally reconstructed prefab can compile and package, and support human
comparison in H3VR. It is not NightForce source, does not change the released
`1.0.5` package, and cannot be published. Any future public NightForce update
must recreate the behavior with NightForce-owned assets and pass the normal
Unity, MeatKit, and H3VR gates.

## Native PIP migration basis

Two compact Thunderstore reference packages were inspected by source hash through
the local structural-manifest tool. No raw extraction output is project source.

- `cityrobo-HSProdukt_VHS_2-1.1.2`, SHA-256
  `56E136A3AC32919538207D156F4ED1F6184A04D21B12D54050514A31B8F7A11A`,
  verifies fixed-power hierarchy, native `PIPScope`/`PIPScope_Camera`, and
  PIP material keywords/properties.
- `Niko666-L115A3_Mustang-1.0.1`, SHA-256
  `8BC2DA3509FDF849F6E834D8C640FFE2A331B2C430D86395411D84F09FF90F9A`,
  verifies variable-power hierarchy: `PIPScopeController`,
  `PIPScopeMagnificationInteraction`, elevation/windage
  `PIPScopeInteraction`, and PIP material.
- Current installed game source confirms the API is present:
  `PIPScope`, `PIPScope_Camera`, `PIPScopeController`,
  `PIPScopeInteraction`, `PIPScopeMagnificationInteraction`, and
  `PIPScopeComponent`.

NightForce will use the installed game PIP components and shader/material
contract. It will retain or create only NightForce-owned meshes, reticle art,
materials, transforms, and serialized values; it will not copy a game or
another author's shader, material, texture, mesh, prefab, or code.
