# Nuketown GunGame Compatibility Patch Development Status

## Status

Last verified: `2026-07-18`
State: `active`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | New compatibility-mod records created on `feat/nuketown-gungame-compat`. | Active |
| Runtime cause | Default-profile log skips Nuketown's `GunGame 1.0.3` because core `GunGame 1.0.4` owns same plugin ID; package contains `nuketown` while skipped DLL targets `gungame`. | Verified |
| Package | Owner package files, `nuketown` bundle, descriptor, and pools are present. | Verified |
| Deploy / VR | Not started. | Pending |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Full map gameplay acceptance | Runtime load and GunGame progression test in H3VR. | Codex + player VR acceptance |

## Plan

Keep one item active.

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[>]` | Implement, package, deploy, and launch compatibility plugin. | BepInEx logs one compatibility registration; map appears without replacing owner files. |
| `[ ]` | Perform manual VR gameplay acceptance. | Start, progression, death, respawn, and exit work normally. |
| `[ ]` | Publish with explicit user approval. | Versioned package passes release checks. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| Medium | Additional upstream Nuketown versions | Only 2.1.6 package layout is verified. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Source / unit | `h3vr.ps1 -Action Test` | Pending |
| Source API | `h3vr.ps1 -Action SourceStatus` | Current before implementation. |
| Build / package | `Build`, `Package`, `Deploy` through wrapper | Pending |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| Startup | Compatibility plugin loads once and Atlas registers BO1 Nuketown. | Pending |
| Map selector | BO1 Nuketown appears and loads. | Pending |
| Gameplay | Start, kill promotion, death demotion, respawn, and exit work. | Pending |

### Release gate

- [x] Current Windows source and managed DLL status checked.
- [ ] Automated checks pass.
- [ ] Package payload/version verified.
- [ ] Deployment receipt and BepInEx log checked.
- [ ] Required VR interaction completed.
