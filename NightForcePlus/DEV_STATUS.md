# NightForcePlus Development Status

`DEV_STATUS.md` is cross-session handoff file. Status, plan, and testing stay together here.

## Status

Last verified: `2026-07-22`
State: `VanillaRipScopes 0.0.1 Windows-built ZIP is ready for manual r2modman import; obsolete private scope packages were removed from Default profile. NightForce native-PIP runtime acceptance remains unresolved and needs a fresh post-cleanup H3VR log plus VR test.`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Unity source | `H3VR-unity-projects/NightForcePlus` branch `codex/vanilla-scope-importer` commit `85d85e4` names the three-candidate local package `VanillaRipScopes`; guarded Windows source payload hash verification passed. | Source synced. |
| Existing scope bubble | `BubbleLevelScope.cs` maps root Euler Z directly to local X. | Legacy behavior identified. |
| Included mount | Black mount content is no longer packaged. NightForce requests it from BubbleLevelSet by object ID. | Package boundary validated. |
| Shared motion source | Unity source commit `6112947` owns `GravityBubbleLevelMotion`; MeatKit compiles that one source into each package while preserving existing scope component identity and fields. | Windows Unity suite passed. |
| Package boundary | NightForce `1.0.5` manifest requires `HLin_Mods-BubbleLevelSet-2.0.4`; its DLL contains shared-motion metadata but no BubbleLevelSet component or mount types. | Package inspection passed. |
| Unity runtime test | `NightForcePlusRuntimeTests` checks references, one-degree response, reverse mounting, limits, source boundary, and exact profile build. | Passed in Windows batch mode. |
| Package candidate | MeatKit package and archive validation passed. SHA-256 `B5ABFF605116697FD64BCAED276A5FD98BDC36988A5771CDD0463E44C61CAF7C`. | Passed. |
| Deployment / H3VR | Validated `1.0.5` package deployed after BubbleLevelSet `2.0.4`; installed manifest confirms the BubbleLevelSet dependency. User verified the mod in H3VR before release authorization. | Passed. |
| Release documentation overlay | Source `main` documentation replaced only ZIP `README.md` and added `CHANGELOG.md`; every existing non-document archive entry kept its content hash. `H3vrPipeline validate` passed. Release SHA-256 `0C6E24310314E3132CA9FA9F660B9FDE5AFB1351AE02EBA0C50FD38C590F2021`. | Passed. |
| Pipeline wrapper | Dedicated macOS and Windows `codex/nightforce-runtime` worktrees are current. Windows suite passed `132/132`; guarded actions inventory/remove exact Default-profile packages and stop only `h3vr.exe` when explicitly authorized. | Passed. |
| Windows Unity source | Editor closed; guarded source payload from `codex/vanilla-scope-importer` commit `85d85e4` synchronized with verified hashes. | Source synced. |
| Native PIP migration | `PIPScope`, controller, front/rear PIP lenses, direct-hand magnification/elevation/windage interactions, and attachment references are serialized. `ScopeShaderZoom` is absent at runtime. | Windows runtime suite passed. |
| Native magnification | Smooth 7--35x geometric magnification and matching native geometric ring positions are serialized. No archived wheel-angle mapping is used. | Windows runtime suite passed. |
| Reticle visual scaling | Archived custom shader behavior was `0.846 * (M / 7)`. Native FFP preserves the transferable `M / 7` apparent-scale multiplier. Named MOA-XT, MIL-XT, and TREMOR3 canvases have measured angular FOVs. | Windows runtime suite passed. |
| Current candidate package | Windows Unity batch build and package validation completed after the ST6T comparison correction. The package is ready for an authorized local deployment; no public release. | Built and package-validated; not yet deployed. |
| Prior candidate deployment | Earlier native-PIP candidate deployed locally with a pipeline-created VR receipt. User runtime report superseded it. It is not a public release. | Superseded by repaired candidate. |
| Runtime failure report | User reported see-through NightForce glass, missing reticle/magnification, and two right-eye-only floating circles. BepInEx log loaded obsolete `NativePipScopeBootstrap` from profile scope packages, which current tested prefab does not serialize. | `HLin_Mods-L115PrivateScope`, `HLin_Mods-H3VRGameScopePrivate`, and superseded `HLin_Mods-LocalVanillaScopeCandidates` were removed after authorized `h3vr.exe` stop. Fresh post-cleanup log/VR proof pending. |
| Runtime ItemID conflict | Prior H3VR startup reported duplicate `NightForcePlus` registration. Source now uses `NightForcePlusPIP` / `NightForcePlus PIP`; both enabled packages need a fresh H3VR startup-log check before conflict is considered resolved. | Fresh-session verification pending. |
| Windows Unity source sync | Configured Windows MeatKit project is an unversioned import. Guarded, narrow source transfer copied only committed NightForcePlus source and matching metadata, then hash-verified every transferred file. | Passed. |
| Native PIP repair | Repaired prefab has no serialized Canvas, one native PIP camera, positive lens axis, native clip spacing, and native click feedback on magnification/elevation/windage. Authored control transforms are unchanged. | Serialized contract passed in Windows Unity batch run. |
| PIP optical placement | Original `Tango6.003_0` lens mesh had its visible plane `0.115426 m` ahead of its Transform pivot, so prior pivot-only alignment claim was false. New `NightForcePipLensCentered.asset` centers that plane. Rear PIP plane is the measured eyepiece end at local Z `-0.13655871`; front PIP plane is the visible objective glass at local Z `0.25538534`; native separation/clip plane is `0.39194405 m`. | Windows Unity runtime suite and fresh MeatKit build passed. |
| PIP physical diameter | Rear effective diameter is `42.8 mm`; front effective diameter matches the visible objective-glass mesh at `59.794 mm`. Serialized front/rear lens scales are `1.057317` / `1.4771477`; native front-lens gizmo remains a diagnostic guide, not visible game geometry. | Serialized geometry tests passed. |
| Physical PIP candidate package | Pipeline tests passed `102/102`; Unity emitted the exact NightForce build marker, exited batch mode successfully, and wrote fresh source ZIP SHA-256 `1113E3B8F18C1AAE83840C5D66E63F247F8CE7F1DA3CBCFB3FBBE60AF68E79FF`. | Built, not packaged/deployed to r2modman. |
| Fresh Unity source package | Unity emitted the NightForce build success marker and wrote a new source ZIP after static contract validation. The older wrapper can report a launcher exit before Unity 5.6's detached batch worker exits; pipeline now waits for that worker. | Package/deploy must run through updated wrapper after Git parity. |
| Repaired candidate deployment | Windows pipeline synced to `codex/nightforce-runtime`; automated suite passed `106/106`. Corrected completion-marker punctuation rejected neither fresh build nor package; validated current package deployed to active local profile while H3VR was closed. | Fresh H3VR startup and VR acceptance pending. |
| Legacy review variant | Legacy assets live beside current source in `Assets/Projects/NightForcePlusLegacy`. Its profile, prefab, bundle, Item Spawner ID/path, package, and r2modman Default-profile record use `NightForcePlusLegacy`; current NightForcePlus prefab/profile were rechecked unchanged. | Windows batch build, package validation, and local deployment passed; VR acceptance pending. |
| Vanilla scope reference archive | Authoritative source is the latest private AssetRipper export under `H3VR_PRIVATE_ASSET_LAB/exports/H3VRFull/AssetRipperProject/ExportedProject/Assets`. Archive inventory passed: 169,718 manifest entries, including 1,966 prefabs plus mesh/material/texture/shader classes. The old Unity full-rip import is deprecated and was not used. | Verified. |
| Reusable private prefab importer | Unity source branch `codex/vanilla-scope-importer` commit `1f317a0` accepts a leaf `.prefab` request through a temporary process variable, resolves exactly one raw-export prefab, copies only its visual closure, and rebinds the full `m_Script` GUID plus local file ID only when exact current native identities are known. The local-runtime path creates unique object/spawner/build metadata only after complete rebind. | Windows pipeline `126/126`; raw export and recovered C# remain private and excluded from Git and public distribution. |
| ST6T private candidate | `ST6T_1_6x24mm_Black.prefab` generic smoke created a private rebound candidate with `10/10` script references rebound, `0` unresolved, and `0` ambiguous. | Eligible for explicit local-only runtime packaging after rebuilt candidate metadata verifies. No public release or distribution. |
| ST6T vs NightForce comparison | Required native gap is `none`. ST6T-only type is `UnityEngine.SphereCollider`; NightForce-only types are `FistVR.FVRFireArmAttachmentMount` and authored `HLin_Mods.BubbleLevelScope.BubbleLevelScope`. | No recovered ST6T runtime component is needed in NightForce; authored mount and bubble additions remain intentional. |
| ST6T field and physical audit | Rebound ST6T candidate has `10/10` script references, `0` unresolved, and `0` ambiguous. Private audit captured serialized fields, active state, sibling/component sequence, local transforms, mesh bounds, and material slots. | `0` required native field schemas missing; `0` unreadable properties. Candidate/NightForce structural differences are recorded only in private inspection report. |
| High-power reference candidates | `Classic3-12x42mmScope.prefab` rebound `10/10`; `EVU_1_10x28mm.prefab` rebound `12/12`. Both have `0` unresolved and `0` ambiguous script pointers. | Full private visual import, script rebind, comparison, and field/layout audit passed for both. |
| Cross-reference PIP contract | ST6T 1–6x, Classic 3–12x, and EVU 1–10x all serialize active `PIPScope`, inactive mount-controlled `PIPScopeController`, and `Unlit/PIPScope` on both lenses. | NightForce now matches this native presentation/lifecycle contract. |
| Cross-reference gaps | Each candidate has `0` required native component/field-schema gap against NightForce. All carry `UnityEngine.SphereCollider`; NightForce instead carries its authored physical layout. Its extra `FVRFireArmAttachmentMount` is shared by EVU; its `BubbleLevelScope` is intentional. | Do not add a recovered component or alter physical layout without a runtime collision/interaction defect. |
| Native controller lifecycle | Current game `FVRFireArmAttachment` calls `OnAttach()` then activates `AttachmentInterface`, and deactivates it on detach. Rebound ST6T serializes its `PIPScopeController` object inactive. | NightForce `_Interface` now serializes inactive and the runtime test enforces mount-time activation semantics. |
| Native PIP presentation | Rebound ST6T uses `Unlit/PIPScope` on both PIP lenses. NightForce current source uses that shader and serializes the retired `ScopeRendererNightforce` MeshRenderer disabled. | Serialized parity is not visual proof; fresh VR test must establish reticle, magnification, and absence of floating legacy circles. |
| Mod-content Item Spawner metadata | User rule: every authored item must set `FVRObject.IsModContent: 1` and matching `SpawnerEntry.IsModded: 1`. NightForce source assets and generic local-candidate generator now enforce both; Windows Unity runtime tests and local preparation verifier enforce them too. | Fresh runtime registration proof pending. |
| VanillaRipScopes local package | Fresh private preparation completed with ST6T, LT3x9, and EVU 1–10 under new object/spawner IDs. Windows MeatKit build and package validation passed; `VanillaRipScopes-0.0.1.zip` SHA-256 `7372FBB40B6E2885935D0E539121C429F0E92345C4F10341043F9471BC33AF29` was fetched and hash-verified for manual import. | ZIP only; not deployed by pipeline, not published. |

### Open blockers

Manual H3VR acceptance remains required for repaired native PIP candidate: Item Spawner availability, pickup, rail mount, direct controls, reticle selection/illumination, visual centering/subtension, and non-black scope view in VR. Start with a fresh BepInEx log after obsolete-profile cleanup.

The vanilla-reference task uses only the authoritative raw export. Do not import recovered C# source into Unity. Full GUID-plus-local-ID rebinding must resolve every reference exactly before candidate packaging. User-authorized local-only packages may include fully rebound candidates with unique IDs, profile, and deployment folder. Keep raw exports, candidates, and local packages out of Git, GitHub, Thunderstore, and public distribution.

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
| `[x]` | Calibrate native PIP physical lens planes. | Center-pivot mesh, measured eyepiece/objective planes, effective diameters, camera clip spacing, and focused serialized checks pass in Windows Unity batch mode. |
| `[x]` | Build and deploy isolated legacy review variant. | Fresh `NightForcePlusLegacy` package validates, deploys to Default profile, and registers as a separate local r2modman mod. |
| `[ ]` | Isolate the repaired PIP Item Spawner identity. | Object definition and Item Spawner entry consistently use `NightForcePlusPIP` / `NightForcePlus PIP`, then both enabled packages register without a duplicate ItemID. |
| `[x]` | Synchronize the tested Unity source to Windows. | Narrow guarded source sync transferred only committed NightForcePlus source and matching metadata, with per-file hash verification. |
| `[x]` | Package and deploy repaired native-PIP candidate. | Pipeline tests passed; current package validated and deployed while H3VR was closed. |
| `[ ]` | Perform legacy variant H3VR acceptance. | Item Spawner shows `NightForcePlusLegacy`; it spawns, picks up, mounts, and renders as historical baseline. |
| `[ ]` | Perform repaired native-PIP H3VR acceptance. | Item Spawner, pickup, rail mount, controls, reticles, non-black view, and visual alignment pass in H3VR. |
| `[x]` | Verify latest raw scope export and import one scope. | Archive inventory contains the expected asset classes; ST6T visual closure imports from raw export only. |
| `[x]` | Generalize and validate private prefab importer. | Query-bound headless smoke/status/quarantine actions pass pipeline tests; exact script pointer rebinding creates only private candidates. |
| `[x]` | Compare ST6T with authored NightForce. | ST6T candidate is `10/10` rebound with no missing required native NightForce component; component differences are reported and intentional. |
| `[x]` | Audit ST6T fields, component sequence, and physical layout against NightForce. | Private report records all readable serialized fields plus node/component/visual layout; no required native field schema is absent. |
| `[x]` | Import and audit two high-power variable reference scopes. | Classic 3–12x and EVU 1–10x complete full private import/rebind/audit with zero unresolved/ambiguous scripts and zero required-native gap. |
| `[x]` | Correct native PIP lifecycle and presentation against ST6T. | `_Interface` starts inactive, attachment lifecycle activates it on mount, redundant scope renderer is disabled, and both PIP lenses use `Unlit/PIPScope`; Windows serialized checks pass. |
| `[x]` | Build three recovered scopes into one manual-test mod. | ST6T, LT3x9, and EVU 1–10 rebind exactly, receive `VanillaRip*` metadata, and build/package into hash-verified `VanillaRipScopes-0.0.1.zip` without deployment. |
| `[x]` | Deploy corrected native-PIP candidate locally. | H3VR was closed; exact validated package replaced Default-profile candidate. Fresh startup/VR test remains required. |
| `[ ]` | Verify post-cleanup Item Spawner registration. | After manual `VanillaRipScopes` import, BepInEx/OtherLoader log confirms only intended package bundles load and Item Spawner exposes its three new entries. |

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
| Private vanilla prefab smoke | `h3vr-remote run --worktree codex/nightforce-runtime UnityVanillaPrefabSmokeTest -Query <leaf.prefab>` then `UnityVanillaPrefabImportStatus` | Completion marker, no failure marker, and full rebind summary prove a private candidate only. |
| Private comparison | `h3vr-remote run --worktree codex/nightforce-runtime UnityVanillaPrefabCompareNightForce -Query <leaf.prefab>` then `UnityVanillaPrefabImportStatus` | Comparison marker reports required-native gap and symmetric component-type differences without modifying authored prefab content. |
| Private field/layout audit | `h3vr-remote run --worktree codex/nightforce-runtime UnityVanillaPrefabAuditNightForce -Query <leaf.prefab>` then `UnityVanillaPrefabImportStatus` | Audit marker has `0` missing required-native field schemas and `0` unreadable properties; private report retains fields, node order, transforms, bounds, and materials. |
| Local runtime candidate preparation | `h3vr-remote run --worktree codex/nightforce-runtime UnityVanillaRuntimeCandidatePrepare` then `UnityVanillaRuntimeCandidateStatus` | Preparation marker confirms fully rebound ST6T, LT3x9, and EVU 1–10 candidates plus unique local metadata. |
| VanillaRipScopes package | `Build` and `Package` for `VanillaRipScopes` | Fresh MeatKit marker, three required bundles, and hash-verified ZIP; manual r2modman import only. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Scope level and cant | Scope bubble centers level, follows gravity, stops in tube, settles without jitter. | User verified release candidate in H3VR. |
| Scope reversed/180-degree mount | Bubble remains on same world-uphill side. | User verified release candidate in H3VR. |
| Black mount dependency | BubbleLevelSet-supplied mount behavior remains correct. | User verified release candidate in H3VR. |
| Native optic spawn/grab/mount | `NightForcePlus PIP` entry appears independently from `NightForcePlusLegacy`; scope picks up and mounts normally. | Pending source synchronization and candidate VR test. |
| Legacy optic review | `NightForcePlusLegacy` appears as separate Item Spawner entry and can be compared with current native candidate. | Pending VR test. |
| Native optic controls | Smooth magnification plus elevation/windage/zero direct controls work; inactive-controller failure does not recur. | Pending candidate VR test. |
| Reticle selection and illumination | All listed reticles cycle and illuminate correctly. | Pending candidate VR test. |
| Reticle centering and subtension | Reticle stays centered; named MOA/MIL/TREMOR markings have expected target angular scale. | Pending candidate VR test. |
| Native PIP presentation | No legacy scope-renderer image or menu remains visible; native popup appears only during control use; scope view is not black. | Pending repaired-candidate VR test. |
| Native PIP physical alignment | Rear and front PIP lens planes sit on physical eyepiece and objective glass; scope image is visible and magnified. | Pending repaired-candidate VR test. |
| Vanilla Rip ST6T Black | Appears under `Attachments/Magnifier_Scope`; it spawns, grabs, mounts, renders PIP/reticle, and its controls work. | ZIP ready for manual import; user VR test pending. |
| Vanilla Rip LT3x9 | Appears under `Attachments/Magnifier_Scope`; it spawns, grabs, mounts, renders PIP/reticle, and its controls work. | ZIP ready for manual import; user VR test pending. |
| Vanilla Rip EVU 1–10x28 | Appears under `Attachments/Magnifier_Scope`; it spawns, grabs, mounts, renders PIP/reticle, and its controls work. | ZIP ready for manual import; user VR test pending. |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [x] Automated checks pass: pipeline `132/132`; latest `VanillaRipScopes` Windows build/package validation passes.
- [x] Fresh MeatKit source package created from the exact profile after physical PIP repair; SHA-256 `1113E3B8F18C1AAE83840C5D66E63F247F8CE7F1DA3CBCFB3FBBE60AF68E79FF`.
- [x] Pipeline package/deploy receipt for the repaired candidate.
- [x] Generic private importer and field/layout audit validate ST6T against authored NightForce; fresh repaired `1.0.5` candidate package validated and deployed locally.
- [x] `VanillaRipScopes` has exact rebind evidence, fresh build-marker evidence, three-bundle archive proof, and a hash-verified ZIP fetched for manual import; obsolete prior candidate deployment was removed.
- [ ] User H3VR acceptance for manually imported VanillaRip ST6T/LT3x9/EVU 1–10 spawning, PIP/reticle render, rail mount, and controls.
- [ ] User H3VR acceptance for repaired native PIP interaction, visual view, and reticle visuals.
- [x] Historical release: BubbleLevelSet `2.0.4` published before NightForcePlus `1.0.5`.
- [x] Historical release: exact Thunderstore download URL returned HTTP `200` for `1.0.5`.
