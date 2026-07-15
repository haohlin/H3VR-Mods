# <ModName> Development Status

`DEV_STATUS.md` is the cross-session handoff file. Keep these three sections
current with verified evidence; do not duplicate this state in separate files.

## Status

Last verified: `YYYY-MM-DD`
State: `planned | active | blocked | released | retired`

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Source | Commit / tracked project | |
| Automated checks | Command + result | |
| Package | Version + artifact | |
| Deploy / VR | Receipt or log evidence | |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| | | |

## Plan

Keep one item active.

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[ ]` | | |
| `[>]` | | |
| `[x]` | | |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| | | |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Source / unit | | |
| Build / package | | |

### Manual H3VR acceptance

| Case | Expected result | Evidence |
| --- | --- | --- |
| | | |

### Release gate

- [ ] Current Windows source and managed DLL status checked.
- [ ] Automated checks pass.
- [ ] Package payload/version verified.
- [ ] Deployment receipt and BepInEx log checked.
- [ ] Required VR interaction completed.
