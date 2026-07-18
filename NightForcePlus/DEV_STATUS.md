# NightForcePlus Development Status

`DEV_STATUS.md` is cross-session handoff file. Status, plan, and testing stay together here.

## Status

Last verified: `2026-07-18`
State: `1.0.5 published; H3VR-verified`

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
| Deployment / H3VR | Validated `1.0.5` package deployed after BubbleLevelSet `2.0.4`; installed manifest confirms the BubbleLevelSet dependency. User verified the mod in H3VR before release authorization. | Passed. |
| Release documentation overlay | Source `main` documentation replaced only ZIP `README.md` and added `CHANGELOG.md`; every existing non-document archive entry kept its content hash. `H3vrPipeline validate` passed. Release SHA-256 `0C6E24310314E3132CA9FA9F660B9FDE5AFB1351AE02EBA0C50FD38C590F2021`. | Passed. |
| Pipeline wrapper | Windows `h3vr.ps1 -Action Test` passed `87/87`; NightForcePlus descriptor and wrapper command are covered. Preflight reports generated source current. | Passed. |
| Windows Unity source | Editor closed; Unity source checkout is on release `main`. | Source synced. |
| Headless package inspection | Hash-locked NightForce `1.0.5` archive produced two structural bundle manifests and two Unity `5.6.7f1` batch audits. Temporary bootstrap source and scratch bundles were removed after the audit. | Passed; research evidence only, no prefab/material change. |
| Legacy inspection cleanup | New manifest/audit evidence replaced stale raw rips, obsolete extraction tools, and superseded generated NightForce package candidates. Pinned inspector tooling and manifest evidence remain ignored/local. | Completed. |
| Native PIP references | Hash-backed fixed and variable PIP reference manifests plus current installed PIP source identify required controller, camera, lens, reticle, material, magnification, zeroing, and direct-hand interaction relationships. | Passed; baseline complete before prefab migration. |
| Native PIP prefab bridge | Batch Unity migration created project-owned bridge references, UI anchor, and elevation/windage controls while preserving NightForce model, camera, switch, reticles, and legacy configuration source. | Passed. |
| Native PIP Unity contract | `NightForcePlusRuntimeTests.RunAll` completed with the native bridge/reference checks and existing gravity checks. | Passed. |
| Native PIP package | MeatKit completed `NightForcePlus 1.0.5`; wrapper package validation passed. Candidate SHA-256 `3F67DBD2EE19F45683752DE3BB604402AAAE733B3ECE68F26002F16FF1B2D6E4`. | Passed; test candidate only, not a release. |
| Native PIP deployment | Canonical wrapper deployed the validated candidate and recorded a VR receipt with backup of the prior profile folder. | Passed. |
| Native PIP H3VR launch | Profile preloader and legacy Doorstop contract were verified. Interactive Steam-URI task launches returned without `h3vr.exe` or a new BepInEx log. Temporary tasks and scratch logs were removed. | Manual launch required. |

### Open blockers

Native PIP requires one successful user Modded-profile launch. Automated package
and deployment proof exists, but the interactive Steam URI did not create an
H3VR process or BepInEx log, so it is not runtime proof.

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Migrate scope controller to shared motion source and add source/runtime tests. | One source governs both packages while MeatKit-safe component types remain package-local. |
| `[x]` | Validate Unity/MeatKit package on Windows. | Runtime tests, wrapper tests, packages, manifests, and DLL metadata pass. |
| `[x]` | Perform H3VR acceptance. | User verified both deployed candidates in H3VR. |
| `[x]` | Publish with explicit authorization. | BubbleLevelSet `2.0.4` published first. Documentation-only archive overlay preserved non-document payload hashes, package validation passed, and exact Thunderstore download URL returned HTTP `200` for `1.0.5`. |
| `[x]` | Establish a reproducible read-only scope-inspection baseline. | Archive hash, structural manifest, and Unity batch audit agree without retaining a raw rip. |
| `[x]` | Capture native fixed and variable PIP reference structures. | Manifests identify PIP camera/material/lens/reticle hierarchy, controller settings, and direct hand interactions without retaining copied payloads. |
| `[x]` | Map NightForce's existing model, controls, and reticle to native PIP fields. | Project-owned bridge references resolve to NightForce model/camera/switch/reticle data; legacy controller is explicitly replaced at runtime. |
| `[-]` | Build and validate NightForce native PIP prefab migration. | Unity test, MeatKit package audit, and deployment pass; user H3VR runtime/interaction acceptance remains. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | Reticle, art, or UI refresh | Shared behavior and regression safety take priority. |
| P1 | Native PIP scope migration | Active. Current reference evidence is complete; NightForce object-graph mapping and prefab change remain. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Source contract | `dotnet test --filter FullyQualifiedName~NightForcePipelineTests` | Shared source and wrapper/profile contract pass. |
| Unity controller | `HLin Mods > NightForcePlus > Run all runtime tests` | Scope gravity, limits, and reversed-mount assertions pass. |
| Pipeline | `h3vr.ps1 -Action Test`, then `Build` and `Package` | Windows output reports zero failures and expected package. |
| Structural package audit | `h3vr.ps1 -Action InspectAssets -InputPath <archive> -ExpectedSha256 <sha256>` | Parser manifest and Unity batch audit complete; bootstrap and scratch cleanup confirmed. |
| Native PIP reference audit | `h3vr.ps1 -Action InspectAssets` using each recorded reference SHA-256 | Fixed and variable scope manifests resolve native PIP components and their object relationships. |
| Native PIP prefab bridge | `HLin Mods > NightForcePlus > Migrate to native PIP scope`, then `Run all runtime tests` | Serialized bridge keeps NightForce-owned references; test reports `PASS`. |
| Native PIP package | `h3vr.ps1 -Action Package -Mod NightForcePlus -ReuseExistingUnityPackage`, then `Deploy` | Candidate ZIP validates and deployment receipt is written. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Scope level and cant | Scope bubble centers level, follows gravity, stops in tube, settles without jitter. | User verified release candidate in H3VR. |
| Scope reversed/180-degree mount | Bubble remains on same world-uphill side. | User verified release candidate in H3VR. |
| Black mount dependency | BubbleLevelSet-supplied mount behavior remains correct. | User verified release candidate in H3VR. |
| Optic controls | Zoom, reticle, zero, elevation, and windage remain functional. | User verified release candidate in H3VR. |
| Native PIP controls | Scope image, reticle, zoom ring, elevation, windage, zeroing, and bubble work on mounted and held scope. | Pending user Modded-profile launch and interaction test. |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Deployment receipt created.
- [x] User H3VR acceptance completed.
- [x] BubbleLevelSet `2.0.4` published before NightForcePlus `1.0.5`.
- [x] Exact Thunderstore download URL returned HTTP `200` for `1.0.5`.
