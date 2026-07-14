# BubbleLevel Testing

## Automated Unity / MeatKit

| Check | Entry point | Pass evidence |
| --- | --- | --- |
| Controller and prefab checks | `HLin Mods > BubbleLevel > Run all runtime tests` | Log contains `[BubbleLevelRuntime] PASS`. |
| Sensitivity calibration | `HLin Mods > BubbleLevel > Calibrate rail prefab for sniper sensitivity` | 1° and 4° assertions pass. |
| Candidate art preview | `HLin Mods > BubbleLevel > Visual review > Render CC0 candidate previews` | Two preview PNGs; production material signatures unchanged. |
| Package | `HLin Mods > BubbleLevel > Build BubbleLevel package` | Exact profile builds expected ZIP and MeatKit reports completion. |
| Fresh batch build | `h3vr.ps1 -Action Build -Mod BubbleLevel` | Wrapper deletes prior ZIP, sees MeatKit completion marker, retries once only after script import. |

## H3VR acceptance

| Case | Expected result |
| --- | --- |
| Level rail, left/right cant | Bubble follows gravity, centers level, stops at travel limits. |
| Nested rail/attachment chain | Same gravity direction after each mounting layer. |
| Bidirectional rail, same world cant | Normal and 180° mount settle on same world uphill side; local-X signs are opposite. |
| Upside-down / rapid handling | Bubble stays in tube; no jitter, escape, or exception. |
| 30 mm black and tan mounts | Bubble and cosine indicator behave without controller conflict. |
| Spawn/attach/reload scene | Every listed item spawns, mounts, detaches, and survives scene lifecycle. |

## Evidence rule

Record package version/hash, deployment receipt, BepInEx log result, and VR
outcome in `STATUS.md`. Unity editor checks do not prove H3VR runtime behavior.
Use `-ReuseExistingUnityPackage` only to deploy a previously validated package;
normal `Build`, `Package`, and `Deploy` invoke Unity batch mode.

## Release evidence

`2.0.3` was freshly packaged through MeatKit, published to Thunderstore, and
confirmed in H3VR by the user. The reverse/180° rail gravity response is the
verified release-critical regression case.
