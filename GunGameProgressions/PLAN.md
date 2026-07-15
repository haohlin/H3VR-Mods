# GunGame Progressions Plan

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[ ]` | Enforce count-aware Modded replacement. | Missing pair accepts first complete candidate; smaller/equal candidate keeps saved pair; strictly larger candidate replaces it; confirmed-empty still removes stale pair; focused test passes. |
| `[ ]` | Re-validate selector loading row in VR. | Concise status appears without blocking Vanilla choices. |
| `[ ]` | Improve general incomplete-metadata reconciliation. | Better valid mod coverage without weapon-specific hard-codes. |

## Deferred

| Priority | Item | Reason |
| --- | --- |
| P2 | Global mod completion signal | Current loader signal is loader-local; replace only with verified API. |

## Current blocker

Windows H3VR build/runtime environment was unavailable on 2026-07-15. Keep
this task at documentation and test-design stage until Windows returns; do not
claim a source fix, package, deployment, or VR result.
