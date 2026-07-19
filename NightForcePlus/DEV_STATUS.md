# NightForcePlus Development Status

`DEV_STATUS.md` is cross-session handoff file. Status, plan, and testing stay together here.

## Status

Last verified: `2026-07-19`
State: `1.0.5 attachment-link repair built and packaged; deployment and VR retest pending`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Unity source | NightForce Unity root was initialized and committed before repair; stable baseline remains local `main` and repaired PIP work is on a local feature branch. | Verified. |
| Existing scope bubble | `BubbleLevelScope.cs` maps root Euler Z directly to local X. | Legacy behavior identified. |
| Included mount | Black mount content is no longer packaged. NightForce requests it from BubbleLevelSet by object ID. | Package boundary validated. |
| Shared motion source | Unity source commit `6112947` owns `GravityBubbleLevelMotion`; MeatKit compiles that one source into each package while preserving existing scope component identity and fields. | Windows Unity suite passed. |
| Package boundary | NightForce `1.0.5` manifest requires `HLin_Mods-BubbleLevelSet-2.0.4`; its DLL contains shared-motion metadata but no BubbleLevelSet component or mount types. | Package inspection passed. |
| Unity runtime test | `NightForcePlusRuntimeTests` checks references, one-degree response, reverse mounting, limits, source boundary, and exact profile build. | Passed in Windows batch mode. |
| Package candidate | MeatKit package and archive validation passed. SHA-256 `B5ABFF605116697FD64BCAED276A5FD98BDC36988A5771CDD0463E44C61CAF7C`. | Passed. |
| Deployment / H3VR | Validated `1.0.5` package deployed after BubbleLevelSet `2.0.4`; installed manifest confirms the BubbleLevelSet dependency. User verified the mod in H3VR before release authorization. | Passed. |
| Release documentation overlay | Source `main` documentation replaced only ZIP `README.md` and added `CHANGELOG.md`; every existing non-document archive entry kept its content hash. `H3vrPipeline validate` passed. Release SHA-256 `0C6E24310314E3132CA9FA9F660B9FDE5AFB1351AE02EBA0C50FD38C590F2021`. | Passed. |
| Pipeline wrapper | Windows `h3vr.ps1 -Action Test` passed `87/87`; NightForcePlus descriptor and wrapper command are covered. Preflight reports generated source current. | Passed. |
| Windows Unity source | Editor closed before source mutation; `main` remains the stable baseline and repaired PIP work is isolated on a feature branch. | Source versioned. |
| Headless package inspection | Hash-locked NightForce `1.0.5` archive produced two structural bundle manifests and two Unity `5.6.7f1` batch audits. Temporary bootstrap source and scratch bundles were removed after the audit. | Passed; research evidence only, no prefab/material change. |
| Legacy inspection cleanup | New manifest/audit evidence replaced stale raw rips, obsolete extraction tools, and superseded generated NightForce package candidates. Pinned inspector tooling and manifest evidence remain ignored/local. | Completed. |
| Native PIP references | Hash-backed fixed and variable PIP reference manifests plus current installed PIP source identify required controller, camera, lens, reticle, material, magnification, zeroing, and direct-hand interaction relationships. | Passed; baseline complete before prefab migration. |
| Native PIP first candidate | User H3VR test showed only generated turrets; they could not turn and native PIP UI did not appear. Source review found the bridge removed the legacy attachment interface without linking the native controller to the existing firearm attachment. | Failed; superseded. |
| Native PIP repaired bridge | Initial bridge candidate left `PIPScopeController.Attachment` null at live `OnAttach()`, causing pick-up/mount failure. | Failed; superseded. |
| Serialized attachment repair | Source now serializes `FVRFireArmAttachment.AttachmentInterface` and `PIPScopeController.Attachment` to each other, and initializes `SubMounts` when absent. | Unity regression passed. |
| Native PIP Unity contract | `NightForcePlusRuntimeTests.RunAll` passed after asserting both attachment directions, non-null `SubMounts`, direct-hand colliders, and gravity checks. | Passed. |
| Native PIP package | Fresh MeatKit build completed for `NightForcePlus 1.0.5`; SHA-256 `E4E5C8B514B349FDB2A1338D3C6EA2068B2908F17980EFD433D6E31DB27AB1EF`. | Built; not deployed. |
| Native PIP deployment | Earlier repaired candidate was deployed, but user pick-up test exposed the null attachment link. Current package remains undeployed. | Superseded; latest VR test pending. |
| Owner-private full-rip validation | Owner-authorized AssetRipper exports for game and PIP reference packages batch-imported with Unity `5.6.7f1`. A separate private Git project produced a reconstructed L115 visual/PIP prefab, removed leaked legacy NightForce runtime types, and passed its editor contracts. | Passed; isolated research only, never NightForce source or release payload. |
| Owner-private full-game archive | Full-game export is retained outside all Git worktrees with a compact private archive manifest for future Unity inspection and scope/API comparison. | Preserved; no repository or release payload contains it. |
| Owner-private MeatKit package | Isolated private profile built and archive-validated a `0.0.2` r2modman ZIP. Manifest contains the standard `OtherLoader` dependency only; it contains no BubbleLevelSet or NightForce dependency. | Passed; human local H3VR comparison pending. |
| Feature repository suite | Windows feature-worktree `dotnet test` completed with `88` passed and zero failures. | Passed. |

### Open blockers

H3VR remains running. Current package deployment is deliberately paused until
the game exits, then spawn, pick-up, detach, and mount must complete with no
`FVRFireArmAttachmentInterface.OnAttach()` null reference.

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
| `[x]` | Validate full-rip reconstruction workflow privately. | Matching Unity import, isolated private prefab contracts, MeatKit ZIP, and archive manifest pass without putting recovered assets in a repository or release. |
| `[x]` | Repair NightForce native PIP attachment/controller wiring. | Unity migration serializes attachment, contract test passes, fresh MeatKit ZIP validates, and wrapper deploys it. |
| `[ ]` | Deploy serialized attachment repair and repeat live interaction check. | Scope spawns, picks up, detaches, and mounts without `FVRFireArmAttachmentInterface.OnAttach()` exception; PIP and controls remain active. |
| `[-]` | User VR-test repaired NightForce native PIP scope. | Mounted and held scope shows native PIP image/UI; zoom ring and both turrets turn; zeroing, reticle, and bubble still work. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | Reticle, art, or UI refresh | Shared behavior and regression safety take priority. |
| P1 | Native PIP scope migration | User VR acceptance pending for repaired candidate. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Source contract | `dotnet test --filter FullyQualifiedName~NightForcePipelineTests` | Shared source and wrapper/profile contract pass. |
| Unity controller | `HLin Mods > NightForcePlus > Run all runtime tests` | Scope gravity, limits, and reversed-mount assertions pass. |
| Pipeline | `h3vr.ps1 -Action Test`, then `Build` and `Package` | Windows output reports zero failures and expected package. |
| Structural package audit | `h3vr.ps1 -Action InspectAssets -InputPath <archive> -ExpectedSha256 <sha256>` | Parser manifest and Unity batch audit complete; bootstrap and scratch cleanup confirmed. |
| Native PIP reference audit | `h3vr.ps1 -Action InspectAssets` using each recorded reference SHA-256 | Fixed and variable scope manifests resolve native PIP components and their object relationships. |
| Native PIP prefab bridge | `HLin Mods > NightForcePlus > Migrate to native PIP scope`, then `Run all runtime tests` | Both attachment fields point to each other, `SubMounts` is non-null, direct-hand colliders remain present, and test reports `PASS`. |
| Native PIP package | Fresh Unity `Build`, then wrapper `Package`; deployment may reuse only that validated ZIP. | Latest candidate ZIP validates; deployment receipt remains pending. |
| Private reconstruction package | Private Unity batch rebuild/verify, then explicit private MeatKit profile build | Recovered visual/PIP prefab contract and package profile contract pass; ZIP opens with manifest, README, icon, plugin DLL, and asset bundles. |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Scope level and cant | Scope bubble centers level, follows gravity, stops in tube, settles without jitter. | User verified release candidate in H3VR. |
| Scope reversed/180-degree mount | Bubble remains on same world-uphill side. | User verified release candidate in H3VR. |
| Black mount dependency | BubbleLevelSet-supplied mount behavior remains correct. | User verified release candidate in H3VR. |
| Optic controls | Zoom, reticle, zero, elevation, and windage remain functional. | User verified release candidate in H3VR. |
| Native PIP controls | Scope image and native UI appear. Zoom ring, elevation, windage, zeroing, reticle, and bubble work mounted and held. | Deploy latest serialized attachment repair, then user VR test pending. |
| Private reconstruction ZIP | r2modman imports local ZIP; scope spawns; PIP image, reticle, controls, and bubble work. | Pending user local test; not release evidence. |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [x] Automated checks pass.
- [x] Package payload/version verified.
- [x] Deployment receipt created.
- [x] User H3VR acceptance completed.
- [x] BubbleLevelSet `2.0.4` published before NightForcePlus `1.0.5`.
- [x] Exact Thunderstore download URL returned HTTP `200` for `1.0.5`.
