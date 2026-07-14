# BubbleLevel Testing

## Automated Unity / MeatKit

| Check | Entry point | Pass evidence |
| --- | --- | --- |
| Controller and prefab checks | `HLin Mods > BubbleLevel > Run all runtime tests` | Log contains `[BubbleLevelRuntime] PASS`. |
| Sensitivity calibration | `HLin Mods > BubbleLevel > Calibrate rail prefab for sniper sensitivity` | 1° and 4° assertions pass. |
| Candidate art preview | `HLin Mods > BubbleLevel > Visual review > Render CC0 candidate previews` | Two preview PNGs; production material signatures unchanged. |
| Package | `HLin Mods > BubbleLevel > Build BubbleLevel package` | Exact profile builds expected ZIP and MeatKit reports completion. |

## H3VR acceptance

| Case | Expected result |
| --- | --- |
| Level rail, left/right cant | Bubble follows gravity, centers level, stops at travel limits. |
| Nested rail/attachment chain | Same gravity direction after each mounting layer. |
| Upside-down / rapid handling | Bubble stays in tube; no jitter, escape, or exception. |
| 30 mm black and tan mounts | Bubble and cosine indicator behave without controller conflict. |
| Spawn/attach/reload scene | Every listed item spawns, mounts, detaches, and survives scene lifecycle. |

## Evidence rule

Record package version/hash, deployment receipt, BepInEx log result, and VR
outcome in `STATUS.md`. Unity editor checks do not prove H3VR runtime behavior.
