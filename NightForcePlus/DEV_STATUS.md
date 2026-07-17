# NightForcePlus Development Status

`DEV_STATUS.md` is cross-session handoff file. Status, plan, and testing stay together here.

## Status

Last verified: `2026-07-17`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Unity source | `H3VR-unity-projects/NightForcePlus` versioned and clean before migration branch. | Verified. |
| Existing scope bubble | `BubbleLevelScope.cs` maps root Euler Z directly to local X. | Legacy behavior identified. |
| Included mount | Build item packages BubbleLevel Black mount and its spawn/object entries. | Verified. |
| Shared source plan | Existing wrapper component classes/field names remain; common gravity implementation lives in BubbleLevel source. | Approved. |
| Shared controller source | Unity source commit `13dcaca` owns `GravityBubbleLevelController` in BubbleLevel; `BubbleLevel` and `BubbleLevelScope` inherit it. Existing NightForce prefab references remain named `baseObject`, `attachment`, and `level_bubble`. | Static source checks pass; Unity compilation pending. |
| Package boundary | NightForce profile declares BubbleLevelSet as a dependency but includes only `HLin_Mods.BubbleLevelScope`; shared controller types are supplied by BubbleLevelSet. | Static source check added; package inspection pending. |
| Unity runtime test | `NightForcePlusRuntimeTests` added for references, one-degree response, reverse mounting, travel limits, and exact MeatKit profile build. | Added; unrun. |
| Package | Profile is `NightForcePlus` `1.0.5`; README changelog ends at `1.0.4`. | Release metadata reconciliation pending. |
| Pipeline wrapper | Windows `h3vr-remote run Test` passed `85/85`; NightForcePlus descriptor and wrapper command are covered. Preflight reports generated source current. | Passed. |
| Windows Unity source | Unity project remains clean on `main`, but Unity is currently open. | Waiting for Unity to close before a safe feature-branch switch and batch run. |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Unity editor active | Close Unity before switching the Windows Unity worktree to the feature branch and starting batch tests. | Maintainer environment |
| New dependency release not created | Versioned BubbleLevelSet release containing shared base before NightForce package update. | Maintainer release decision |

## Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Migrate scope controller to shared gravity base and add source/runtime tests. | Shared source and wrapper inheritance are present; source checks and `.meta` identities are recorded. |
| `[>]` | Validate Unity/MeatKit package on Windows. | Runtime tests, wrapper test/build/package, and package contents pass. |
| `[ ]` | Perform H3VR VR acceptance. | Scope bubble, Black mount, reticles, zoom, turrets, and 180-degree mounting work without log errors. |
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

- [ ] Current Windows source and managed DLL status checked.
- [ ] Automated checks pass.
- [ ] Package payload/version verified.
- [ ] Deployment receipt and BepInEx log checked.
- [ ] Required VR interaction completed.
