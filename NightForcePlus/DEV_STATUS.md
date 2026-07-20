# NightForcePlus Development Status

`DEV_STATUS.md` is cross-session handoff file. Status, plan, and testing stay together here.

## Status

Last verified: `2026-07-20`
State: `1.0.5 native-PIP repair built as a local candidate; package/deploy and H3VR acceptance pending`

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
| Pipeline wrapper | Windows `h3vr-remote run Test` passed `85/85`; NightForcePlus descriptor and wrapper command are covered. Preflight reports generated source current. | Passed. |
| Windows Unity source | Editor closed; Unity source checkout is on release `main`. | Source synced. |
| Native PIP migration | `PIPScope`, controller, front/rear PIP lenses, direct-hand magnification/elevation/windage interactions, and attachment references are serialized. `ScopeShaderZoom` is absent at runtime. | Windows runtime suite passed. |
| Native magnification | Smooth 7--35x geometric magnification and matching native geometric ring positions are serialized. No archived wheel-angle mapping is used. | Windows runtime suite passed. |
| Reticle visual scaling | Archived custom shader behavior was `0.846 * (M / 7)`. Native FFP preserves the transferable `M / 7` apparent-scale multiplier. Named MOA-XT, MIL-XT, and TREMOR3 canvases have measured angular FOVs. | Windows runtime suite passed. |
| Current candidate package | Pipeline tests passed `100/100`; Windows Unity batch build completed; runtime PIP/gravity checks and archive validation passed. Candidate SHA-256 `D52569AA66CB7E0A735781ADD7191C0D5956DF1746C651697F0CB7676712C25C`. | Passed. |
| Prior candidate deployment | Earlier native-PIP candidate deployed locally with a pipeline-created VR receipt. User runtime report superseded it. It is not a public release. | Superseded by repaired candidate. |
| Runtime failure report | User observed a permanently visible legacy menu, black scope glass, and inactive direct controls. Runtime log contained no NightForce exception. | Root cause traced to serialized legacy presentation plus invalid native PIP axis/clip data. |
| Native PIP repair | Repaired prefab has no serialized Canvas, one native PIP camera, positive lens axis, native clip spacing, and native click feedback on magnification/elevation/windage. Authored control transforms are unchanged. | Serialized contract passed in Windows Unity batch run. |
| PIP optical gizmo placement | `NightForcePlus_PIPScope`/camera and `NightForcePipLensRear` share local Z `-0.017600005`; `NightForcePipLensFront` is local Z `0.3324`, exactly `frontLensOffset` (`0.35`) ahead of the scope anchor. The second orange ring is PIPScope's computed front-view exit-pupil guide, about `0.101 m` beyond the objective at 35x. | Verified from current prefab and native `PIPScope.OnDrawGizmosSelected`; no transform change required. |
| Fresh Unity source package | Unity emitted the NightForce build success marker and wrote a new source ZIP after static contract validation. The older wrapper can report a launcher exit before Unity 5.6's detached batch worker exits; pipeline now waits for that worker. | Package/deploy must run through updated wrapper after Git parity. |
| Legacy review variant | Legacy assets live beside current source in `Assets/Projects/NightForcePlusLegacy`. Its profile, prefab, bundle, Item Spawner ID/path, package, and r2modman Default-profile record use `NightForcePlusLegacy`; current NightForcePlus prefab/profile were rechecked unchanged. | Windows batch build, package validation, and local deployment passed; VR acceptance pending. |

### Open blockers

Manual H3VR acceptance remains required for repaired native PIP candidate: Item Spawner availability, pickup, rail mount, direct controls, reticle selection/illumination, visual centering/subtension, and non-black scope view in VR.

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Migrate scope controller to shared motion source and add source/runtime tests. | One source governs both packages while MeatKit-safe component types remain package-local. |
| `[x]` | Validate Unity/MeatKit package on Windows. | Runtime tests, wrapper tests, packages, manifests, and DLL metadata pass. |
| `[x]` | Perform H3VR acceptance. | User verified both deployed candidates in H3VR. |
| `[x]` | Publish with explicit authorization. | BubbleLevelSet `2.0.4` published first. Documentation-only archive overlay preserved non-document payload hashes, package validation passed, and exact Thunderstore download URL returned HTTP `200` for `1.0.5`. |
| `[x]` | Replace custom scope runtime with native PIP scope. | Serialized PIP components, geometric magnification, reticle canvases, and Windows batch checks pass. |
| `[x]` | Deploy native-PIP candidate locally. | Validated candidate is installed with a VR receipt. |
| `[x]` | Repair native PIP presentation and interaction data. | Legacy presentation removed; positive optical axis, native camera, controls, and feedback pass serialized checks. |
| `[x]` | Build and deploy isolated legacy review variant. | Fresh `NightForcePlusLegacy` package validates, deploys to Default profile, and registers as a separate local r2modman mod. |
| `[ ]` | Package and deploy repaired native-PIP candidate. | Source/package validation succeeds through updated pipeline and local receipt matches current source. |
| `[ ]` | Perform legacy variant H3VR acceptance. | Item Spawner shows `NightForcePlusLegacy`; it spawns, picks up, mounts, and renders as historical baseline. |
| `[ ]` | Perform repaired native-PIP H3VR acceptance. | Item Spawner, pickup, rail mount, controls, reticles, non-black view, and visual alignment pass in H3VR. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | Generic ATACR reticle calibration | It has no verified reticle-unit source data; retain it as visual-only until source artwork/subtensions are available. |
| P2 | Reticle, art, or UI refresh | Tune named reticle visual baselines only after native-PIP manual acceptance. |

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
| Scope level and cant | Scope bubble centers level, follows gravity, stops in tube, settles without jitter. | User verified release candidate in H3VR. |
| Scope reversed/180-degree mount | Bubble remains on same world-uphill side. | User verified release candidate in H3VR. |
| Black mount dependency | BubbleLevelSet-supplied mount behavior remains correct. | User verified release candidate in H3VR. |
| Native optic spawn/grab/mount | Item Spawner entry appears; scope picks up and mounts normally. | Pending candidate VR test. |
| Legacy optic review | `NightForcePlusLegacy` appears as separate Item Spawner entry and can be compared with current native candidate. | Pending VR test. |
| Native optic controls | Smooth magnification plus elevation/windage/zero direct controls work. | Pending candidate VR test. |
| Reticle selection and illumination | All listed reticles cycle and illuminate correctly. | Pending candidate VR test. |
| Reticle centering and subtension | Reticle stays centered; named MOA/MIL/TREMOR markings have expected target angular scale. | Pending candidate VR test. |
| Native PIP presentation | No legacy menu remains visible; native popup appears only during control use; scope view is not black. | Pending repaired-candidate VR test. |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [x] Automated checks pass: pipeline `100/100`; Unity runtime PIP/gravity suite passes.
- [x] Fresh MeatKit source package created from the exact profile after repair.
- [ ] Pipeline package/deploy receipt for the repaired candidate.
- [ ] User H3VR acceptance for repaired native PIP interaction, visual view, and reticle visuals.
- [x] Historical release: BubbleLevelSet `2.0.4` published before NightForcePlus `1.0.5`.
- [x] Historical release: exact Thunderstore download URL returned HTTP `200` for `1.0.5`.
