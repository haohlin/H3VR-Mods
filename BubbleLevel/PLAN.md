# BubbleLevel Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[>]` | Version current MeatKit-Lite BubbleLevel project in `H3VR-unity-projects`. | Prefabs, scripts, tests, build assets, docs, art, and `.meta` files tracked; caches/bundles excluded. |
| `[ ]` | Tune sniper sensitivity. | 1° movement `>= 0.1`; 4° saturates; limits/damping/nested tests pass. |
| `[ ]` | Add repeatable Unity/MeatKit build and deploy integration. | Exact build profile/package checked; deploy receipt created without manual file copy. |
| `[ ]` | Run real H3VR test. | Rail, offset rail, 30 mm mounts, nested chain, inversion, and settle behavior pass in-game. |
| `[ ]` | Decide material candidate. | Explicit approval/rejection; approved path has applied-proof render. |

## Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P2 | New geometry/material variants | Existing physical behavior and source control first. |
