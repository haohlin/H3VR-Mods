# NightForcePlus Development Status

`DEV_STATUS.md` is cross-session handoff file. Status, plan, and testing stay together here.

## Status

Last verified: `2026-07-17`
State: `1.0.5 candidate deployed; release unrequested`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Unity source | `H3VR-unity-projects/NightForcePlus` versioned and clean before migration branch. | Verified. |
| Existing scope bubble | `BubbleLevelScope.cs` maps root Euler Z directly to local X. | Legacy behavior identified. |
| Included mount | Black mount content is no longer packaged. NightForce requests it from BubbleLevelSet by object ID. | Package boundary validated. |
| Shared motion source | Unity source commit `6112947` owns `GravityBubbleLevelMotion`; MeatKit compiles that one source into each package while preserving existing scope component identity and fields. | Windows Unity suite passed. |
| Package boundary | NightForce `1.0.5` manifest requires `HLin_Mods-BubbleLevelSet-2.0.4`; its DLL contains shared-motion metadata but no BubbleLevelSet component or mount types. | Package inspection passed. |
| Unity runtime test | `NightForcePlusRuntimeTests` checks references, one-degree response, reverse mounting, limits, source boundary, and exact profile build. | Passed in Windows batch mode. |
| Package candidate | MeatKit package and archive validation passed. SHA-256 `B5ABFF605116697FD64BCAED276A5FD98BDC36988A5771CDD0463E44C61CAF7C`. | Passed. |
| Candidate deployment | Validated `1.0.5` package deployed after BubbleLevelSet `2.0.4`; installed manifest confirms the BubbleLevelSet dependency. | Awaiting user H3VR test. |
| Pipeline wrapper | Windows `h3vr-remote run Test` passed `85/85`; NightForcePlus descriptor and wrapper command are covered. Preflight reports generated source current. | Passed. |
| Windows Unity source | Editor closed; feature branch checked out. Generated `BubbleLevel/CHANGELOG.md.meta` is untracked and preserved. | Tracked source unchanged by tests. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Release authorization | BubbleLevelSet `2.0.4` must be published before NightForcePlus `1.0.5`; merge tested commits into `main` afterward. | Maintainer release decision |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Migrate scope controller to shared motion source and add source/runtime tests. | One source governs both packages while MeatKit-safe component types remain package-local. |
| `[x]` | Validate Unity/MeatKit package on Windows. | Runtime tests, wrapper tests, packages, manifests, and DLL metadata pass. |
| `[>]` | Perform H3VR acceptance. | Candidate is installed; user will test scope bubble, Black mount, controls, and logs when available. |
| `[ ]` | Version and publish only with explicit authorization. | Package/version/docs/dependency alignment verified; BubbleLevel releases first; the published commits are merged to `main`. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | Reticle, art, or UI refresh | Shared behavior and regression safety take priority. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Source contract | `dotnet test --filter FullyQualifiedName~NightForcePipelineTests` | Shared source and wrapper/profile contract pass. |
| Unity controller | `HLin Mods > NightForcePlus > Run all runtime tests` | Scope gravity, limits, and reversed-mount assertions pass. |
| Pipeline | `h3vr.ps1 -Action Test`, then `Build` and `Package` | Windows output reports zero failures and expected package. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Scope level and cant | Scope bubble centers level, follows gravity, stops in tube, settles without jitter. | Pending. |
| Scope reversed/180-degree mount | Bubble remains on same world-uphill side. | Pending. |
| Black included mount | Mount bubble behavior remains correct. | Pending. |
| Optic controls | Zoom, reticle, zero, elevation, and windage remain functional. | Pending. |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Deployment receipt created.
- [>] BepInEx log and VR interaction await user test.
