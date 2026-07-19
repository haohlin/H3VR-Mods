# GunGame profile-generation policy

For the runtime lifecycle, pool architecture, persistence contract, and known
limitations, see [DESIGN.md](DESIGN.md). This document owns compatibility rules
and their regression tests.

Vanilla, Modded, and offline Vanilla call one `RuntimeProfileBuilder`. The
resolver has one ordered feed/optic algorithm. Its only source-confidence rule:
the versioned Vanilla catalog may use its established exact-`RoundType`
cartridge fallback; Modded content may use only direct cartridges because a
shared caliber does not prove a mod firearm's chamber or magazine geometry.

The two tracked **offline Vanilla fallback** pools are generated from the same
`RuntimeProfileBuilder` source by `OfflineProfileGenerator`. They are a
vanilla-only safe starting point, not a substitute for runtime Modded capture.
The versioned `ObjectData.json` vanilla snapshot is required generator input and
is packaged beside those two profiles for repeatable offline validation.

> **Compatibility rule:** when metadata cannot prove a safe firearm or feed,
> omit it. Never guess firearm/feed IDs from a name or shared caliber. Modded
> magnified scopes are never selected. When no exact optic route exists, use
> only curated vanilla RMR/reflex/low-power/LPVO fallback optics; runtime still
> requires a real compatible top sight mount.

> **Catalog-only capture rule:** runtime profile capture reads `FVRObject`
> fields and never materializes a prefab. A firearm needs valid GunGame
> round-display data plus catalog proof: firearm identity tags, a direct
> compatible feed, exact nonzero magazine/clip interface, or the explicit
> feedless `GravitonBeamer` exception. Missing/conflicting metadata is skipped,
> never repaired by scanning prefabs.

> **Runtime-cost rule:** capture reads plain catalog metadata in two-millisecond
> frame slices. Merge, resolver work, serialization, and file writes run on a
> below-normal worker. No `Update`/`FixedUpdate` loop may scan registry,
> instantiate weapon assets, or rebuild Vanilla weapon lists during a Modded
> refresh.

## Safety gate

| Metadata state | Result |
| --- | --- |
| Not a supported firearm | Skip |
| Firearm lacks catalog proof or GunGame round-display data | Skip |
| No verified compatible feed | Skip |
| Modded exact route | Prefer compatible proprietary/Russian/RMR/Picatinny optic; permit Modded reflex/RMR, never Modded magnified scope |
| No Modded exact optic route | Handgun: vanilla RMR reflex; CQC: vanilla Picatinny reflex/low-power; rifle/carbine/unknown: vanilla LPVO/low-power/reflex |
| Curated fallback missing | Spawn firearm without optic |
| Actual GunGame spawn rejects or throws for a generated loadout | Clear it, skip it, and advance safely |

Skipping an ambiguous weapon is intentional. A missing progression entry is safer than a wrong object ID, a malformed loadout, or a game crash.

## Feed resolver

```text
firearm
|
+-- shotgun
|   |
|   +-- BoxMag ──> direct magazine -> exact MagazineType -> direct clip
|   |                -> exact ClipType -> direct speedloader -> otherwise skip
|   |
|   +-- Revolver + direct speedloader ──> that direct speedloader
|   |
|   `-- all other shotguns ──> direct shell -> Vanilla exact RoundType shell -> otherwise skip
|
`-- other firearm
    `-- direct magazine -> exact MagazineType -> direct clip -> exact ClipType
        -> direct speedloader -> direct cartridge -> Vanilla exact RoundType cartridge -> otherwise skip
```

Rules:

| Rule | Reason |
| --- | --- |
| A compatible-object list is authoritative. | It is the game’s explicit relationship. |
| Magazine/clip type is an exact fallback. | Those types encode their matching interface. |
| Modded loose cartridges require a direct compatible-object entry. | Shared `RoundType` does not prove a mod firearm accepts that cartridge. |
| Vanilla may use its versioned exact-`RoundType` cartridge fallback. | Preserves the versioned offline baseline. |
| A speedloader is never inferred from round type. | Equal caliber does not prove compatible geometry. |
| A non-box shotgun gets shells, not a generic magazine or rotary loader. | Prevents P6-12/Jackhammer-style cross-assignment. |
| A revolver shotgun keeps only its direct loader. | Preserves real compatible rotary/revolver feeds. |
| A box-fed shotgun without a verified loader is skipped. | It must not fall back to loose shells or another shotgun’s loader. |

## Optic resolver

```text
firearm
|
+-- direct verified `BespokeAttachments` optic -> use it
|
+-- supported physical mount
|   |
|   +-- proprietary scope mount -> matching scope
|   +-- RMR / handgun mount    -> matching reflex sight
|   +-- Picatinny / M-Lok rail -> matching scope or reflex sight
|   `-- verified adapter       -> supported mount exposed by adapter
|
+-- explicit compatible adapter with a declared optic-capable mount
|   `-- matching vanilla scope/reflex (for example `MP5RailMount` -> `Scope_G3SG1`)
|
`-- no verified route -> deterministic random catalog Picatinny scope fallback
                           from curated vanilla-safe candidates
                           (or no optic if unavailable)

compatible optics
|
+-- CQC / pistol / SMG / shotgun -> reflex -> low-power scope
+-- handgun without metadata route -> vanilla RMR reflex
+-- rifle / carbine / unknown route -> vanilla LPVO -> low-power -> reflex
`-- proprietary Russian route -> matching vanilla Russian scope first
```

Candidate gate: object must be `Attachment` classified as `Scope` or `Reflex`; magnifiers and every other attachment type are excluded. For Modded profiles, a Modded candidate may only be `Reflex`; every magnified candidate must be a curated vanilla low-power/LPVO scope.

Mount matching uses lightweight catalog metadata (`TagFirearmMounts`,
`TagAttachmentMount`, `BespokeAttachments`, `AttachmentFeature`, and
`PhysicalMountTypes`), not firearm or scope name lists. A direct compatible
adapter may supply an explicit optic-capable mount type; this covers the stock
MP5 adapter route without reading its prefab. `ProvidedMountTypes` is used only
when the catalog already declares it. The small hard-coded list in
`OpticMountPolicy` is only the H3VR **mount-type taxonomy** needed to identify
sight-capable interfaces; it contains no individual weapon or attachment IDs.
Runtime mounting reuses that same taxonomy, checks the exact mount type, and
permits rail mounts only when oriented as a top sighting rail. Muzzle, stock,
grip, and side/bottom rail positions are rejected.

An M4-style carbine is selected by metadata, never by Object ID: it must be `Carbine` size, use a rifle-caliber round class, and have Picatinny as its only recognized optic route. A direct/bespoke or proprietary route wins before role ranking. Pistol-caliber carbines remain CQC and prefer a reflex sight.

### Fallback optic rule

Fallback is last, deterministic, and Modded-only. It uses a small maintained
vanilla ID set because lightweight `FVRObject` metadata does not expose
magnification values: RMR reflexes, Picatinny reflexes, fixed low-power scopes,
LPVOs, `Scope_G3SG1` for catalog-declared MP5 rails, and `MagnifierPSO1` for a
catalog-declared Russian rail. `MagnifierPSO1` is a legacy object ID for H3VR's
real PSO-1 4x scope; classifier normalizes only that exact stock ID before
excluding every other magnifier. `Scope_M76` remains secondary Russian fallback.
These are mount-class fallbacks, not Modded-firearm exceptions. Sniper scopes
and all Modded magnified scopes are excluded.

Direct bespoke optics, proprietary mounts, RMR, and exact Picatinny matches
always win before fallback. If a Modded entry omits mount tags, profile capture
does not inspect its prefab and runtime does not materialize/instantiate an
adapter or replacement optic. The normal GunGame loadout objects are the only
objects materialized during play. Fallback never changes feed selection.

### Compatibility Probe

`firearmBlacklist` excludes known broken content from every generated profile:
offline Vanilla, Runtime 01/03, Runtime 02/04, and Runtime 05. It currently
contains `Slingshot`, `BrownBess`, `Degle`, `JunkyardFlameThrower`,
`LaserPistol`, `MF_Flamethrower`, and `Stinger`. `Slingshot` can freeze a run;
the others either fail GunGame spawn safety or have a live damage failure.

`runtimeFirearmBlacklist` is additional Runtime 02/04/05-only curation:
`GrappleGun`, `M224Mortar`, `LadiesPepperbox`, `M6Survival`, `MF_LongShot`,
`PlungerLauncher`, `PotatoGun`, `SustenanceCrossbow`, and the 19 listed
MP5/SP5 variants. It does not change the versioned offline fallback.

`compatibilityProbeFirearms` is a test-candidate list, not a bypass. It builds
**Runtime 05 - Compatibility Probe** only from entries that pass the same
firearm, feed, blacklist, and optic gates as Modded profiles. No force-include
mechanism exists.

## Runtime availability and recovery

| Situation | Required behavior |
| --- | --- |
| Startup | Keep the last complete Modded profiles from the prior run selectable immediately; generate Vanilla pools, request a fresh Modded capture, and schedule non-blocking Modded rescans at one, five, and ten real-time minutes. |
| Any Modded refresh request | Snapshot currently registered Modded catalog immediately; build/write on background worker. |
| Loader reports complete | Allows confirmed-empty snapshot to remove stale saved profiles. |
| Loader remains `Loading` or unavailable | Does not veto a usable non-empty snapshot. |
| Selector opens while Modded profiles are pending | Restore only the saved complete Modded pair. Keep Vanilla choices usable; no selector-side polling, UI clone, capture, or build work. |
| Complete Modded replacement | Build candidate off play path. Replace saved pair only when both generated Modded pools contain eligible weapons **and** candidate count is strictly greater than saved count. Equal/smaller or unproven saved count keeps saved pair. |
| Confirmed empty Modded snapshot | Remove the saved Modded pair. This prevents disabled mods from leaving stale object IDs behind. |
| Partial capture, capture failure, or build failure | Keep the previous saved Modded pair unchanged and request a later background refresh. |
| Generated ID is missing, has the wrong category, or throws while spawning | Clear the bad buffer, skip that loadout, and promote to the next weapon on the following frame. Do not crash or freeze the session. |

The loader signal is authoritative only for content exposing it. Its reflection
metadata is read once per background attempt and failures log once. It never
polls. One-, five-, and ten-minute rescans, selector entries/reloads, and GunGame
session exits request another background snapshot. Modded files and receipt stay in the
installed plugin folder across H3VR restarts: the selector restores that last
complete pair first, then a completed fresh candidate replaces it for a later
selector load.

## Offline fallback contract

```text
policy change
    |
    +-- shared RuntimeProfileBuilder test: positive + negative case
    |
    +-- OfflineProfileGenerator --verify
    |       |
    |       `-- tracked Vanilla fallback matches shared resolver
    |
    `-- Build / Package accepts the release payload
```

The packaged fallback contains the versioned vanilla metadata snapshot and two
Vanilla profiles. It must never contain a user-specific Modded profile. When a
policy change affects a loadout, refresh the tracked Vanilla fallback with the
offline generator and commit its JSON output in the same change. The package
build and GitHub workflow run `--verify` and reject a missing, modded, or stale
baseline.

## Playtest regression matrix

Every entry below came from a reported or observed playtest failure. The named
test is the regression guard; do not remove or weaken one without replacing it
with coverage for the same condition.

| Never reintroduce | Required outcome | Regression test |
| --- | --- | --- |
| `Slingshot` | Explicitly blacklist it; firing it can freeze GunGame. It must never enter a progression. | `Runtime_profile_builder_skips_explicitly_blacklisted_slingshot`; `Production_profile_rules_keep_requested_runtime_and_global_exclusions` |
| Known broken content | Exclude BrownBess, Degle, JunkyardFlameThrower, LaserPistol, MF_Flamethrower, and Stinger from every generated profile. | `Production_profile_rules_keep_requested_runtime_and_global_exclusions` |
| Requested Runtime 05 removals | Exclude GrappleGun, M224Mortar, LadiesPepperbox, M6Survival, MF_LongShot, PlungerLauncher, PotatoGun, SustenanceCrossbow, and 19 MP5/SP5 variants from Runtime 02/04 and 05. Keep tracked Vanilla unchanged. | `Production_profile_rules_keep_requested_runtime_and_global_exclusions` |
| Unsafe test candidate | Runtime 05 never bypasses firearm/feed safety gates. | `Runtime_compatibility_probe_never_bypasses_ordinary_safety_gates` |
| Airgun | Its direct pellet cartridge is a catalog-proven feed, so it stays eligible for Runtime 05. | `Offline_generator_emits_a_metadata_only_runtime_05_scope_audit` |
| Firearm without catalog proof or verified feed | Skip it; never guess magazine, round, or arrow from name/model. This covers malformed `CompoundBow`, MCX rail objects, and incomplete G28 variants. Sole feedless exception: `GravitonBeamer`. | `Runtime_profile_builder_skips_unproven_modded_cartridge_guesses_and_bad_feedless_objects` |
| Mod firearm or feed has incomplete catalog metadata | Skip it; profile capture must not materialize prefabs to repair it. Actual spawn failures also skip and advance safely. | `Runtime_catalog_capture_never_materializes_the_prefab_registry`; `GunGame_spawn_safety_skips_unavailable_or_mismatched_objects_without_leaking_exceptions` |
| Firearm lacking GunGame round-display data | Skip it. | `Runtime_profile_builder_skips_firearms_without_gungame_round_display_data` |
| G28-style magazine-fed firearm is skipped because optional identity tags are absent | Accept direct/exact catalog feed proof, keep its magazine, then use exact mount route or safe Modded fallback. | `Runtime_profile_builder_uses_catalog_proven_modded_magazines_and_exact_mount_scopes` |
| Mod firearm receives loose cartridge from `RoundType` alone | Skip it unless it declares direct cartridge compatibility. | `Runtime_profile_builder_skips_unproven_modded_cartridge_guesses_and_bad_feedless_objects` |
| Same-caliber but unrelated speedloader | Never infer a speedloader from `RoundType`. | `Runtime_profile_builder_does_not_infer_a_speedloader_from_round_type` |
| Tube, internal, or break-action shotgun receives P6-12/Jackhammer-style rotary feed | Use a verified shell; never a generic magazine or rotary loader. | `Runtime_profile_builder_uses_shells_for_non_box_shotguns_in_both_profile_families` |
| Real revolver shotgun loses its direct loader | Retain only its explicitly compatible speedloader. | `Runtime_profile_builder_keeps_a_revolver_shotguns_direct_speedloader` |
| Box-fed shotgun has no verified box loader | Skip it; do not fall back to shells or generic loaders. | `Runtime_profile_builder_skips_a_box_fed_shotgun_without_a_compatible_loader` |
| Missing ID, wrong ID category, pre-buffer error, or spawn iterator exception | Clear it, log gun/feed/optic IDs, skip, then promote next frame; no crash or stuck progression. | `GunGame_spawn_safety_skips_unavailable_or_mismatched_objects_without_leaking_exceptions` |
| Verified RMR or Picatinny sight mount receives no optic | Use a verified compatible reflex or scope when one exists. | `Runtime_profile_builder_selects_only_exact_mount_verified_optics` |
| Proprietary mount is replaced by a generic Picatinny optic | Direct/proprietary verified scope wins. | `Runtime_profile_builder_prefers_a_proprietary_scope_mount_over_picatinny` |
| Russian side rail receives a generic/pistol optic | Use its compatible Russian scope. | `Runtime_profile_builder_prefers_a_russian_side_rail_scope_over_other_shared_mounts` |
| CQC, rifle, and sniper receive indiscriminate optic power | Modded profiles permit vanilla red-dot/low-power/LPVO choices only; sniper scopes are excluded. | `Runtime_profile_builder_matches_verified_picatinny_optics_to_firearm_role` |
| M4-style Picatinny-only rifle carbine receives a reflex sight | Prefer a compatible variable scope; retain reflex priority for pistol-caliber carbines. | `Runtime_profile_builder_assigns_variable_scope_to_picatinny_only_rifle_carbines` |
| Modded magnified scope is offered as a universal fallback | Never select it. Exact Modded reflex/RMR remains valid; magnified fallback comes from curated vanilla low-power/LPVO IDs. | `Runtime_profile_builder_uses_vanilla_low_power_and_rmr_fallbacks_for_modded_firearms` |
| Otherwise-valid Modded firearm has no direct/proprietary/exact-mount optic | Handgun gets vanilla RMR reflex. CQC gets vanilla Picatinny reflex/low-power. Rifle/carbine/unknown gets vanilla LPVO/low-power/reflex. | `Runtime_profile_builder_uses_picatinny_scope_fallback_when_no_verified_optic_route_exists`; `Runtime_profile_builder_assigns_picatinny_scope_fallback_to_otherwise_valid_firearms`; `Runtime_profile_builder_uses_vanilla_low_power_and_rmr_fallbacks_for_modded_firearms` |
| Catalog omits physical mount tags | Capture and runtime remain prefab-free. Use only direct/declared adapter metadata or normal fallback; never instantiate a repair adapter/optic. | `Runtime_catalog_capture_never_materializes_the_prefab_registry`; `GunGame_spawn_safety_wraps_the_single_upstream_spawn_boundary` |
| MP5 exposes only a declared compatible adapter | Read adapter `PhysicalMountTypes`; select its exact vanilla mount-matched scope without loading any prefab. | `Runtime_profile_builder_uses_a_declared_mp5_adapter_mount_without_prefab_materialization` |
| Russian mount has no Modded scope | Use vanilla PSO-1 `MagnifierPSO1`; normalize only this legacy ID to `Scope`, and exclude every other magnifier. `Scope_M76` is secondary fallback. | `Runtime_profile_builder_uses_the_default_pso1_scope_when_no_modded_scope_is_available`; `Optic_classifier_excludes_generic_magnifier_ids_but_normalizes_pso1_scope` |
| Compatibility test candidate | Runtime 05 uses same feed and safe Modded optic resolver; global/runtime exclusions remain absent and no candidate bypasses proof. | `Runtime_compatibility_probe_uses_verified_feed_and_global_picatinny_scope_fallback`; `Runtime_compatibility_probe_never_bypasses_ordinary_safety_gates` |
| Generic magnifier is treated as a scope | Exclude it; only legacy `MagnifierPSO1` normalizes to H3VR's real PSO-1 scope. | `Optic_classifier_excludes_generic_magnifier_ids_but_normalizes_pso1_scope` |
| Vanilla and Modded pool rules diverge | Use the same feed and optic resolver. | `Runtime_profile_builder_applies_one_magazine_first_policy_to_vanilla_and_modded_profiles`; `Runtime_profile_builder_applies_one_optic_policy_to_vanilla_and_modded_profiles` |
| Mods are still loading or loader state unavailable | Vanilla remains usable; each request captures current catalog once, generates in background, and keeps larger saved pair. Further rescans start one, five, and ten real-time minutes after plugin start. | `Runtime_captures_each_modded_snapshot_without_waiting_for_loader_readiness`; `Runtime_keeps_vanilla_profiles_playable_while_modded_profiles_refresh_off_selector_path`; `Runtime_schedules_nonblocking_one_five_and_ten_minute_startup_modded_rescans` |
| Runtime scan regresses into a hot loop or main-thread resolver | Retry selector-event reflection at most every ten seconds until subscribed; keep merge/build/write on below-normal worker; no registry scan in `Update`/`FixedUpdate`. | `Runtime_modded_refresh_keeps_heavy_work_off_the_unity_thread` |
| Packaged local metadata produces a scope-less emitted gun | Both tracked Vanilla pools and every generated local Runtime 05 candidate must have nonempty `Extra`. | `Local_metadata_compatibility_probe_assigns_an_optic_to_every_generated_firearm` |
| Existing generated profiles are stale, deleted, or content changes | Rebuild from the current fingerprinted snapshot. | `Runtime_pool_persistence_rebuilds_when_active_content_changes_or_files_are_missing` |
| H3VR restarts while Modded refresh is pending | Restore the last complete Modded pair; do not replace it with a partial candidate. | `Runtime_modded_profiles_keep_the_last_complete_set_until_a_complete_replacement_is_ready` |

## Change checklist

When changing this algorithm:

1. Record the rule and its intended outcome in this file; bump `GenerationPolicyVersion` when behavior changes.
2. Add a focused positive and negative unit test, covering the reported case and the nearest incompatible case.
3. Keep one shared resolver for Vanilla, Modded, and offline Vanilla fallback profiles; do not duplicate policy in a generator script.
4. Keep the versioned `ObjectData.json` snapshot vanilla-only; refresh it from a current runtime capture when the game data/schema changes.
5. Regenerate the tracked offline Vanilla fallback with `OfflineProfileGenerator`, then run it with `--verify`.
6. Run the Windows pipeline: `Verify`, `Test`, `Build`, `Package`.
7. Audit generated pools for invalid IDs and feed-category mismatches before deployment.

The policy version is recorded in the persistence receipt and fingerprint. A
complete candidate from a newer version replaces existing Modded pools once,
even when a safety rule makes it smaller; partial/empty candidates still cannot
replace a usable pair.
