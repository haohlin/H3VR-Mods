# GunGame Progressions — Development Status

Read [DESIGN.md](DESIGN.md) for lifecycle and dated decisions. Read
[GENERATION_POLICY.md](GENERATION_POLICY.md) for loadout rules. This file is
cross-session handoff source of truth and records present state only.

## Status

Last verified: `2026-07-22`
State: `active`

Public release is `1.4.1`. This branch carries an unreleased candidate: fixed
30-minute refresh schedule, `HLin - ` profile labels, and bounded scan
diagnostics. Source edits made on 2026-11-12 passed portable checks at
`d39c630`. Nothing has run on the Windows host since the candidate recorded in
`a01ad95`.

### Verified now

| Area | Evidence | State |
| --- | --- | --- |
| Public release | `1.4.1` published from `main`. Windows `SourceStatus`, `Verify`, `Test` `98/98`, Release Build/Package/Deploy passed; package SHA-256 `21DBD53CCD6ED6F253716B2E4485247674429DE15DA1DCB21AA0BC6A6E8A8E75`; exact Thunderstore download returned HTTP `200`. | Verified |
| Packaged fallback | Policy `21`. Both tracked Vanilla fallback profiles hold `659` unique firearms and no empty `Extra`. Windows regenerated them from `4,331` Vanilla metadata entries and shared-builder verification passed. On this branch only their `Name` line changed, to the `HLin - ` label. | Verified |
| Lifecycle | Candidate requests a Modded refresh only at immediate startup and at 1, 5, 10, and 30 real-time minutes, under one idempotent owner in plugin `Awake()`. Thirty minutes is final; only ten minutes opens policy-version replacement. Selector-ready and GunGame teardown events restore saved choices and never request capture or build work. Dedicated Windows worktree: `SourceStatus` current, `Verify` passed, `Test` `101/101`, Release Build/Package/Deploy passed. | Verified on Windows; 30-minute runtime observation pending |
| Profile labels | Generated Vanilla, Modded, and local Debug profiles display `HLin - <profile name>`; Runtime file IDs are unchanged. A writable legacy persisted Modded or Debug profile is renamed once, atomically, when selector restore reads it. Dedicated Windows worktree: `SourceStatus` current, `Verify` passed, `Test` `103/103`, Release Build/Package/Deploy passed. | Verified on Windows; selector visual check pending |
| Scan diagnostics | Each actual Modded scan writes one Info-level start record and one outcome record with trigger, aggregate registry and Modded counts, capture/enemy/worker/total times, persistence result, pool count, and weapon count; no per-entry or item-ID logging. Dedicated Windows worktree: `SourceStatus` current, `Verify` passed, `Test` `101/101`, Release Build/Package/Deploy passed. | Verified on Windows; runtime log capture pending |
| Memory, prefab, and runtime-cost boundary | Capture reads the `FVRObject` catalog only, in two-millisecond slices; no `GetGameObject*` call is allowed. Spawn safety mounts only `Extra` objects that GunGame spawned and never instantiates an adapter or replacement optic. Merge, build, and write run on a below-normal worker. Source tests guard each point. A Windows A/B run eliminated the large prefab residency path, and the catalog-only run showed no runaway memory growth. | Verified |
| Spawn safety | Invalid pre-buffer data, immediate spawn errors, and iterator-time buffer errors clear the loadout, log gun/feed/optic IDs, and promote on the next frame. Windows `Test` `95/95`, `Verify`, `Build`, and `Package` passed. | Verified on Windows; VR validation pending |
| Runtime 05 boundary | Runtime 05 is local Debug-only. Release build and package neither generate nor restore it, and the Release ZIP holds `0` Runtime 05 files; Debug has isolated `-debug` artifacts and cannot publish. Remaining worklist: Airgun, Flaregun, MF_Medical180, Pocket1906, Quackenbush1886. Windows `Test` `97/97`, `Verify`, Release and Debug Build/Package, and the Debug publish guard passed. | Verified on Windows; worklist VR test pending |
| Candidate package | Last Windows-validated source is the profile-label candidate recorded in `a01ad95`. It reports version `1.4.1`, the same as the public release. It was deployed privately from the dedicated Windows worktree; nothing was published. Last recorded package SHA-256 is `94B8F102ADEF4132ACB82006E416A63C854E85BEF958EBD810871857EED309E9` (scan-diagnostics build); the hash of the later profile-label build was not recorded. | Deployed privately; not published |
| Last live run | Policy `20` build, outside GunGame. Startup generated all four player pools without selector interaction: Vanilla `659` each, Modded `60` each. Early capture `2` entries in `809 ms`; one-minute capture `1,124` entries in `1,033 ms`, then `pools ready`. Modded receipt: policy `20`, `1,124` Modded catalog entries, `60` eligible weapons; no exporter failure. | Verified; predates policy `21` and the candidate |
| Portable checks | Remote tip `d39c630`, pushed fast-forward from `a01ad95` on 2026-11-12. Hosted run `43206594414` (`Verify H3VR Portable Checks`) at `d39c630` concluded success; the `[WindowsH3vrFact]` tests are skipped there because `H3VR_METADATA_EXPORTER_TESTS` is not set. Isolated macOS clone, .NET SDK `8.0.428`: `103` passed in full mode with `H3VR_METADATA_EXPORTER_TESTS=1`; `42` passed and `61` skipped in CI mode. Mod builds equal the baseline. Not done: nothing ran on the Windows host; `tools/h3vr.ps1` was parsed, not executed; the `Kodeman-GunGame-1.0.2` plugin version check is still open because the package download did not complete. | Passed; not on the Windows host |
| Source edits (2026-11-12) | Selector and teardown guard rejects any `RequestModdedRefresh(` call, and every guard named in the policy regression matrix must exist as a test method. Scan `total` time starts after the wait for Vanilla generation, as in released `1.4.1`. Scheduled scans report `trigger=startup +60s`, `+300s`, `+600s`, or `+1800s` instead of the trace sentence. A persisted pool that a selector already lists under its legacy `Runtime 0N` label is not added again. Five unread `RuntimeGenerationReport` members, the unused `PoolHookUnavailable` message, and an always-true Harmony null guard are removed. | Portable checks passed at `d39c630`. Windows `Test`/`Verify`/`Build` pending. The deployed candidate predates these edits |
| Player guide edits (2026-11-12) | Changelog section points to the package page's Changelog tab; profile table sits under Choose a Pool; refresh timeline and optic rule are full plain sentences checked against source and policy. Three sentences that are also on the released page were corrected after review: Modded profiles build from content registered so far and at each rescan; a policy update can replace a larger saved pair once from the ten-minute rescan on; a gun from a mod disabled later is skipped at spawn and leaves the pool when a refresh replaces the saved pair. The `660` count, icon, and canonical description are unchanged. | Text only; ships with the next release. No version, changelog, or manifest change |

### Open blockers

| Blocker | Needed | Owner |
| --- | --- | --- |
| Source edits not validated on Windows | Windows `SourceStatus`, `Test`, `Verify`, and Release `Build` from the dedicated candidate worktree, then rebuild and redeploy the candidate. Portable checks passed at `d39c630`. | Agent; Windows host |
| Reported performance failure has no confirmed root cause | Hypothesis from the released `1.4.1` source audit: selector reload or session teardown could cause another complete capture/build; the Unity thread snapshots the whole registry before the two-millisecond yield budget, capture allocates per-entry lists, and merge/build runs even when the candidate later loses the persistence check. No allocation profile, OOM, or crash stack confirmed it. Capture bounded scan diagnostics before changing performance code. | Owner runtime log |
| Candidate shares version `1.4.1` with the public release | Choose the next version before any publish. | Owner |
| Listing counts differ | Icon artwork says `644`, canonical description `661`, player guide `660`, packaged fallback `659`. This is a product decision; the canonical description must not be reworded without approval. | Owner |

## Plan

Keep one item active.

| State | Item | Acceptance condition |
| --- | --- | --- |
| `[>]` | Validate the 2026-11-12 source edits on the Windows host. | Windows `SourceStatus`, `Test`, `Verify`, and Release `Build` pass from the dedicated candidate worktree. Portable checks already passed at `d39c630`. |
| `[ ]` | Rebuild, redeploy, and observe the fixed startup schedule. | Start H3VR, remain active through thirty minutes, then enter, reload, and exit GunGame. BepInEx shows immediate plus 1/5/10/30-minute scans only; no selector or map event starts a refresh after the final 30-minute callback; saved Modded pair is selectable; policy-version replacement is absent before ten minutes and eligible at ten minutes. |
| `[ ]` | Check `HLin` profile labels in the GunGame selector. | Vanilla and generated or persisted Modded profiles show `HLin - <profile name>`; Runtime filenames stay unchanged; no duplicate selector choice. |
| `[ ]` | Capture bounded scan diagnostics. | Two `GunGame Progressions debug: scan #` Info records plus the matching `modded scan` record for each observed scan; compare capture, worker, and total time before changing performance code. |
| `[ ]` | VR-test loadout rules. | G28/direct magazine with Picatinny scope; non-box shotgun shells; Modded handgun gets RMR; Picatinny rifle gets vanilla low-power/LPVO; Russian rail gets PSO-1 `MagnifierPSO1`; MP5 adapter route gets `Scope_G3SG1`; invalid mod is skipped. No wrong loose cartridge, wrong magazine, mount mismatch, duplicate optic, loose replacement, exception, or progression crash; progression feel is acceptable. |
| `[ ]` | VR-test the Runtime 05 worklist in a local Debug build. | Airgun, Flaregun, MF_Medical180, Pocket1906, and Quackenbush1886 each spawn, load, fire, and advance without error; PlungerLauncher is absent from every profile; a bad loadout advances instead of stalling or crashing. |
| `[ ]` | Converge remote wrapper routing. | The wrapper mechanism to converge on is the one on `codex/nightforce-runtime`: the `-EnvironmentConfigPath` parameter and its asset-lab prefix. When that wrapper reaches `main`, forward-revert `3dae011`, `37dbb22`, and `1cc6d55` here and keep only the HLin names in `Test-GunGamePools`. |

### Deferred

| Priority | Item | Reason |
| --- | --- | --- |
| P1 | Classify remaining Runtime 05 failures from the spawn-safety log. | Needs the worklist VR log. Add only confirmed broken IDs to the shared global blacklist, each with a focused regression. |
| P1 | Decide whether a saved Modded pair should drop guns when only some weapon mods are disabled. | Read from source, not observed in a live run: a smaller or equal candidate never replaces the saved pair, so those guns stay in it until a larger complete pair, a confirmed-empty snapshot, or a policy-version replacement; spawn safety skips each one. Changing this is a persistence lifecycle change and needs its own design and test. |
| P2 | Cap near-identical firearm variants, beginning with MP5/SP5. | Needs design approval: generic catalog-signature grouping or an explicit family rule; preserve two representative variants if requested. |

## Testing

### Automated

| Check | Command / entry point | Pass evidence |
| --- | --- | --- |
| Game API | `h3vr.ps1 -Action SourceStatus` | Current on the dedicated Windows worktree at the profile-label candidate. |
| Source / unit | `h3vr.ps1 -Action Test` | `103/103` at the profile-label candidate recorded in `a01ad95`. Not run on the Windows host since the 2026-11-12 source edits. |
| Harmony targets | `h3vr.ps1 -Action Verify -Mod GunGameProgressions` | Passed at the profile-label candidate; Kodeman targets resolve. |
| Build / package | `h3vr.ps1 -Action Build -Mod GunGameProgressions`; `Package`; `Deploy` | Release Build/Package/Deploy passed at the profile-label candidate. Not rebuilt on the Windows host since the 2026-11-12 source edits. |
| Runtime 05 release boundary | `Test`; default Release `Build`/`Package`/`Deploy`; `Build`/`Package -GunGameBuildConfiguration Debug`; Debug publish guard | Passed `97/97`: Release ZIP has `0` Runtime 05 files and the installed exporter reports disabled; Debug exporter reports enabled; Debug package is isolated and publish is rejected. |
| Offline fallback | `OfflineProfileGenerator`, then `--verify` | Passed on Windows for policy `21`; both tracked profiles hold `659` firearms. |
| Local Runtime 05 audit | `dotnet run --project GunGameProgressions\OfflineProfileGenerator\OfflineProfileGenerator.csproj -c Release -- --input GunGameProgressions\ObjectData.json --probe-output build\staging\runtime05-local-metadata-audit.json` | Passed via Windows test: Airgun present, global/runtime-blacklisted IDs absent, no empty `Extra`; never package this file. |
| Portable checks, macOS clone | `dotnet test tests/H3vrPipeline.Tests/H3vrPipeline.Tests.csproj` in an isolated macOS clone, .NET SDK `8.0.428` | At `d39c630`: `103` passed in full mode with `H3VR_METADATA_EXPORTER_TESTS=1`; `42` passed and `61` skipped in CI mode. Mod builds equal the baseline. Not on the Windows host. |
| Portable checks, hosted | `Verify H3VR Portable Checks` on push; the workflow branch filter lists `codex/**` | Run `43206594414` at `d39c630` concluded success. `[WindowsH3vrFact]` tests are skipped there because `H3VR_METADATA_EXPORTER_TESTS` is not set. |

### Manual H3VR acceptance

Launch the profile once before log monitoring: `Preflight` fails its log-path
check until a BepInEx log exists.

| Case | Expected result | Evidence |
| --- | --- | --- |
| Background generation outside GunGame | Runtime 02 and 04 files plus `runtime-generation-modded-receipt.json` appear; log reports `pools ready`, not `using packaged fallback pools.` | Passed on the policy `20` build; see Last live run. Not repeated for the candidate. |
| Fixed 30-minute lifecycle | Only immediate and 1/5/10/30-minute traces request Modded scans. Selector-ready events restore persisted choices only; no map transition or teardown starts a scan after 30 minutes. | Pending. Initial and one-minute scans were observed on the policy `20` build only. |
| Policy-version replacement gate | With a saved Modded receipt from an older policy, early snapshots keep an equal or smaller pair; the ten-minute scan may replace it once. | Pending runtime observation. Static and worker guards pass in `Test`. |
| Scan diagnostics | Exactly two aggregate diagnostic lines per scan plus the matching `modded scan` line; no per-item diagnostic output. | Pending. |
| Profile labels | Packaged Vanilla and saved Modded choices show `HLin - <profile name>`; no duplicate selector choice. | Pending. |
| Loadouts | G28; mod rifle with no direct feed; Russian/proprietary mount; pump/break shotgun; GunGame reload. Direct or exact gear only; unsafe object skipped; saved Modded pair persists. | Pending. |
| Runtime 05, Debug build | Exactly Airgun, Flaregun, MF_Medical180, Pocket1906, and Quackenbush1886 appear; each has nonempty `Extra`; a rejected loadout logs IDs and promotes without crash or stall. | Pending. |

### Release gate

The gate applies to the commit that ships, so nothing is checked for the
current branch tip.

- [ ] Current Windows source and managed DLL status checked.
- [ ] Automated checks pass.
- [ ] Package payload/version verified.
- [ ] Deployment receipt and BepInEx log checked.
- [ ] Required VR interaction completed.

Do not run H3VR tests/builds on macOS. Do not package `DESIGN.md`,
`DEV_STATUS.md`, or `GENERATION_POLICY.md`.
