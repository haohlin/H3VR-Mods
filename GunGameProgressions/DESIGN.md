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
| Keep the last complete Modded pair through an H3VR restart. | Guess an ID, feed, scope, or mount from an item name. |
| Generate Modded pools off the play path and add them when safe. | Edit individual GunGame maps or require their authors to change code. |
| Skip a broken loadout and continue the progression. | Let a missing/wrong object crash or stall a run. |

## Runtime shape

```text
H3VR registry ──> Vanilla capture ──> shared builder ──> Runtime 01 + 03
      │
      └─> mod-content readiness ──> Modded capture ──> shared builder ──> Runtime 02 + 04
                                      │                                │
                                      │                                └─> atomic persisted pair
                                      └─> prefab reconciliation

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

The names and order are compatibility contracts. The two tracked offline
Vanilla files are the safe packaged fallback; user-specific Modded files are
created only at runtime and are never published in a package.

## Lifecycle

| Moment | Required behavior |
| --- | --- |
| Plugin start | Start Vanilla capture; separately request a Modded refresh. |
| Registry changes | Observe it without blocking the main thread. |
| Loader reports complete | Capture Modded metadata immediately. |
| No usable loader-complete signal | Capture after five seconds of registry quiet. |
| Loader stays `Loading` but registry stops growing | Capture the stable snapshot after 30 seconds. |
| GunGame selector opens | Keep Vanilla usable and restore any saved Modded pair once. It owns no polling, UI clone, capture, or build work. |
| New complete Modded pair | Persist it atomically for the next selector load. Reloading GunGame shows it. |
| GunGame closes | Request another background refresh for late-loading mods. |
| Registry never appears | Stop that background attempt after 30 seconds; a later selector/session event retries. |

There is no Modded loading row or live-insertion path. This removes selector
UI ownership and prevents selector reloads from retaining polling coroutines or
cloned UI components. Reloading GunGame is the reliable player path after a
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

### Open correctness gap — do not treat as solved

The agreed product rule is: **do not replace a saved complete Modded pair with
a smaller fresh candidate; replace only when the candidate has more eligible
weapons, except that a confirmed empty snapshot removes stale pools.**

Current code does **not** enforce this count comparison. It accepts any
complete non-empty two-pool candidate whose fingerprint changed. The public
README's “same or fewer” wording is the target rule, not the current verified
implementation. Fix `RuntimePoolPersistence` and add a focused regression test
before claiming this requirement is met.

### Verified root cause — 2026-07-15

The Modded receipt already records `eligibleWeaponsPerPool`, but
`ShouldPromoteModdedCandidate` receives only candidate pool count and candidate
weapon count. It never reads or compares the saved count. Therefore any full,
non-empty candidate can replace a larger saved pair after fingerprint change.

Resume with one count-aware persistence gate. Preserve an existing pair when
its count is unknown or candidate count is equal/smaller. Permit first complete
pair, strictly larger replacement, and confirmed-empty removal only.

## Compatibility and runtime safety

One `RuntimeProfileBuilder` serves runtime Vanilla, runtime Modded, and the
offline Vanilla fallback. It uses real prefab components to reconcile imperfect
catalog metadata, then follows the shared feed and optic policy.

```text
verified firearm + verified compatible feed + optional verified optic
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
| `modded refresh stopped; object registry did not become available` | This bounded attempt ended; a later selector or session exit retries. |
| `spawn safety unavailable` | API drift disabled the protection; investigate before release. |

Capture yields after a two-millisecond frame budget. Building/writing happens
in a background job; readiness polls once per second. The design goal is a
responsive game, not a fixed artificial loading delay.

Readiness logging is event-based: attempt start, ready, timeout/cancel, and
completion. Never emit an exception or status log on every poll; repeated log
formatting and disk writes turn a transient loader problem into a stutter.

## Source and release assets

| Artifact | Role |
| --- | --- |
| `ObjectData.json` | Versioned vanilla-only metadata snapshot. |
| `OfflineProfileGenerator` | Rebuilds/verifies the two tracked Vanilla fallback pools. |
| `GENERATION_POLICY.md` | Compatibility rules and regressions. |
| `BRANDING.md` | Canonical Thunderstore short description. |
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
| P0 | Enforce “strictly larger or confirmed empty” Modded replacement. | Smaller complete candidate cannot replace a larger saved pair; regression test proves it. |
| P0 | Validate bounded background Modded coordination. | No selector-owned coroutine/UI clone/live insertion; low-mod idle/reload test shows stable memory and no periodic stutter. |
| P1 | Improve compatibility evidence for incomplete mod metadata. | General prefab/catalog reconciliation improves coverage without per-weapon hard-codes. |
| P2 | Discover a trustworthy global mod-completion signal if one becomes available. | Replace the loader-local/quiet heuristic only with verified behavior. |
