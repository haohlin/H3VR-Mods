# GunGame Progressions — Design

> **Purpose:** keep GunGame playable immediately, then safely add the enabled
> modded content without freezing H3VR or editing GunGame maps.

This is the maintainer-level lifecycle and architecture reference.  The exact
weapon/feed/optic rules live in [GENERATION_POLICY.md](GENERATION_POLICY.md).
The approved store wording lives in [BRANDING.md](BRANDING.md).

## Product contract

| Must do | Must not do |
| --- | --- |
| Vanilla pools are usable as soon as the registry is ready. | Freeze startup or a GunGame map while mods load. |
| Keep the last complete Modded pair through an H3VR restart. | Guess an ID, feed, or mount from an item name. |
| Generate Modded pools off the play path and add them when safe. | Edit individual GunGame maps or require their authors to change code. |
| Skip a broken loadout and continue the progression. | Let a missing/wrong object crash or stall a run. |

## Runtime shape

```text
H3VR registry ──> catalog capture ──> shared builder ──> Runtime 01 + 03
      │
      └─> Modded refresh request ──> immediate catalog snapshot ──> shared builder ──> Runtime 02 + 04 + 05
                                             │                                  │
                                             │                                  └─> atomic persisted files
                                             └─> no registry-wide prefab loading

GunGame WeaponPoolLoader event ──> restore saved Modded choices
                                └─> request background refresh if needed

GunGame session end ──> request another non-blocking Modded refresh
```

The plugin is a BepInEx/Harmony overlay. It listens to Kodeman GunGame's shared
`WeaponPoolLoader` event, so every GunGame map using that loader receives the
same integration; no map-specific patch is the normal design.

## Pool contract

| Order | Pool | Weapons | Enemies |
| ---: | --- | --- | --- |
| 01 | Vanilla Rot | Live vanilla firearms | Rotwieners |
| 02 | Modded Rot | Active mod firearms | Rotwieners |
| 03 | Vanilla Mixed Enemy | Live vanilla firearms | Vanilla Sosigs |
| 04 | Modded Mixed Enemy | Active mod firearms | Vanilla + custom Sosigs |
| 05 | Compatibility Probe | Configured test candidates; direct-safe feeds plus explicit diagnostic overrides only | Rotwieners |

The names and order are compatibility contracts. The two tracked offline
Vanilla files are the safe packaged fallback; user-specific Modded files are
created only at runtime and are never published in a package.

## Lifecycle

| Moment | Required behavior |
| --- | --- |
| Plugin start | Start Vanilla capture, request a Modded refresh, and schedule non-blocking Modded rescans at one, five, and ten real-time minutes. |
| Modded refresh request | Capture current catalog once, then build/write on a background worker. Never wait or poll on selector path. |
| Loader status | Only authorizes deletion after a confirmed-empty snapshot. Never vetoes a non-empty current snapshot. |
| GunGame selector opens or reloads | Keep Vanilla usable, restore saved Modded and Compatibility Probe choices once, request fresh background snapshot. |
| New larger Modded pair | Persist it atomically for next selector load. Reloading GunGame shows it. Probe refresh is independent. |
| One, five, and ten minutes after plugin start | Request additional background rescans for late-loading content. Each complete candidate follows ordinary persistence replacement rules. |
| GunGame closes | Request another background refresh for late-loading mods. |
| Registry unavailable | Stop that attempt immediately; a later selector/session/retry event starts another. |

There is no Modded loading row or live-insertion path. Selector restore clones
at most one existing GunGame choice per already-persisted runtime pool (02, 04,
or 05), once per selector instance. It never captures metadata, builds pools,
or loads an asset prefab. Reloading GunGame is reliable player path after a
fresh pair is written.

## Persistence and replacement

```text
saved complete Modded pair
        │
        ├─ selector start ──> make it selectable immediately
        │
fresh snapshot ──> build candidate off-path ──> complete? ── no ──> keep saved pair
                                             │
                                            yes
                                             │
                                             └─> atomic files + receipt/fingerprint ──> use on next selector
```

Receipts fingerprint the captured items, enemies, and generation-policy
version. Changes therefore trigger a rebuild, while partial or failed captures
leave the last complete pair intact. A confirmed empty compatible-mod snapshot
removes the saved Modded pair so disabled IDs cannot survive indefinitely.

### Replacement rule

First complete non-empty pair writes immediately. Later Modded candidates
replace a saved pair only when eligible weapon count is strictly larger. A new
complete generation-policy version also replaces once, even if it safely removes
weapons. Equal, smaller, malformed, and unknown-count candidates retain saved
pair. Only an explicit loader-complete empty snapshot removes stale pools.

## Compatibility and runtime safety

One `RuntimeProfileBuilder` serves runtime Vanilla, runtime Modded, and the
offline Vanilla fallback. Runtime capture uses the lightweight `FVRObject`
catalog only, then follows the shared feed and optic policy.

```text
catalog-proven firearm + verified compatible feed + exact optic route
    or safe Modded fallback
    -> progression entry
anything unproven / malformed
    -> skip it safely
spawn-time mismatch or exception
    -> clear bad entry, advance next frame, keep the run alive
```

See the policy's regression matrix for the required negative cases: Slingshot,
rail objects mislabeled as guns, G28 magazine preference, bows without arrows,
shotgun feed classes, scope mount classes, proprietary Russian optics, and
invalid spawn IDs. Add every new playtest failure there with both positive and
negative coverage.

## Integration boundaries

| Boundary | Rule |
| --- | --- |
| GunGame | Harmony/reflection overlay; minimize the patched surface. |
| Maps | Support the shared loader; do not fork or edit each map. |
| OtherLoader | Its status is loader-local, not a global enabled-mod count. Treat it as a readiness hint only. |
| Unity | This is a code/data mod. No Unity GUI or MeatKit workflow is required for generator changes. |
| Game source | Read-only API/Harmony-target discovery only. |

The spawn-safety overlay protects GunGame progression spawning and its weapon
buffer. It validates generated categories and attachment compatibility before
letting a bad loadout derail a session.

## Observability and performance

| Signal | Meaning |
| --- | --- |
| `vanilla pools ready` | Live Vanilla generation completed. |
| `preparing pools` | A Modded background attempt is waiting/capturing. |
| `pools ready` | A Modded candidate was written. |
| `no modded pools available` | No compatible active Modded firearms were found. |
| `modded capture complete` | One current catalog snapshot is ready for background generation. |
| `modded scan <time>ms` | One Modded snapshot/build completed; reports wall-clock duration and catalog entry count. |
| `compatibility probe updated` | Runtime 05 wrote configured compatibility candidates for manual testing. |
| `spawn safety unavailable` | API drift disabled the protection; investigate before release. |

Capture yields after a two-millisecond frame budget. Metadata merge,
generation, serialization, and disk writes run on below-normal background
workers. A Modded build retains vanilla entries only as feed/optic lookup data;
it does not rebuild Vanilla weapon lists. Each request has one cached loader
status read and one catalog snapshot; it never waits for a global loader
completion signal or polls registry. If GunGame's selector type arrives late,
its event subscription retries only every ten seconds and stops after success.
Startup does one immediate snapshot plus one-, five-, and ten-minute rescans.
The event log records each completed scan's wall-clock duration for live
measurement. Design goal is responsive game, not a fixed artificial loading
delay.

### Prefab-materialization boundary

`FVRObject` catalog metadata is the only profile-capture source. Capture must
never call `GetGameObject()` or `GetGameObjectAsync()`: either can materialize
an Anvil/OtherLoader prefab and retain its bundle. Catalog tags and direct
compatibility lists provide all required pool metadata. Missing or conflicting
metadata means skip the item. Modded fallback uses curated vanilla
RMR/red-dot/low-power/LPVO optics but does not claim a missing catalog mount
exists. GunGame alone materializes the selected loadout at normal gameplay
spawn time. Spawn safety only attaches that already-spawned selected optic; it
never materializes or instantiates a repair adapter or replacement optic. The
small selector choice clone uses GunGame's existing UI prefab only after a
saved profile is found; it is not an item/attachment/prefab load.

Logging is event-based: request, capture, write, or retained candidate. Never
emit an exception or status log in a poll loop; repeated formatting and disk
writes turn a transient loader problem into a stutter.

## Source and release assets

| Artifact | Role |
| --- | --- |
| `ObjectData.json` | Versioned vanilla-only metadata snapshot. |
| `OfflineProfileGenerator` | Rebuilds/verifies the two tracked Vanilla fallback pools; `--probe-output <file>` emits a temporary metadata-only Runtime 05 audit for maintainer review. |
| `GENERATION_POLICY.md` | Compatibility rules and regressions. |
| `BRANDING.md` | Canonical Thunderstore short description. |
| `profile-rules.json` | Global offline blacklist (`Slingshot`), `runtimeFirearmBlacklist` for Runtime 02/04/05 only, Runtime 05 candidates, and five explicit Runtime 05-only unsafe-test overrides. |
| Package README | Player-facing installation and use guidance. |

Never package a player's captured Modded pool, private paths, credentials,
machine details, or game assemblies.

## Verification and change protocol

```text
playtest report
   -> record rule + failure in GENERATION_POLICY
   -> add focused positive and negative test
   -> bump GenerationPolicyVersion if behavior changes
   -> refresh offline Vanilla fallback when policy affects it
   -> Windows Verify / Test / Build / Package
   -> deploy + inspect BepInEx log + VR test
```

## Backlog and known limits

| Priority | Item | Definition of done |
| --- | --- | --- |
| P0 | Validate background Modded coordination. | No selector-owned coroutine/UI clone/live insertion; low-mod idle/reload test shows stable memory and no periodic stutter. |
| P0 | Verify catalog-only release candidate. | Windows runtime restores golden vanilla weapons and includes catalog-proven Modded firearms without prefab materialization. |
| P1 | Classify Runtime 05 results. | Record each former blacklist candidate as works, lacks feed proof, lacks physical optic mount, or crashes. Keep only confirmed-broken entries excluded. |
| P2 | Discover a trustworthy global mod-completion signal if one becomes available. | Replace the loader-local/quiet heuristic only with verified behavior. |
