# BubbleLevel Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[x]` | Version current MeatKit-Lite BubbleLevel project in `H3VR-unity-projects`. | In-place `Assets/Projects` Git root tracks scripts, assets, build profile, tests, and `.meta`; caches/bundles excluded. |
| `[x]` | Run licensed Unity checks, including 180° rail mount. | 1° movement `0.104174`; 4° saturates; limits/damping/nested/reverse-mount tests pass. |
| `[x]` | Add repeatable Unity/MeatKit build and deploy integration. | Exact profile package validated; deploy receipt created without manual file copy. |
| `[x]` | Validate release-critical H3VR behavior. | User confirmed deployed `2.0.3` works in-game, including corrected reverse/180° rail behavior. |
| `[x]` | Document BubbleLevelSet usage and release history. | Unity source `README.md` provides contents, behavior, use, and compatibility; `CHANGELOG.md` provides full version history without changing packaged `2.0.3`. |
| `[ ]` | Expand post-release H3VR regression coverage. | Offset rail, 30 mm mounts, nested chain, inversion, and settle behavior recorded when practical. |
| `[ ]` | Decide material candidate. | Explicit approval/rejection; approved path has applied-proof render. |

## Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | New geometry/material variants | Existing physical behavior and source control first. |
