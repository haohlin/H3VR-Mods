# GunGame profile-generation policy

This policy applies identically to **Vanilla** and **Modded** runtime pools.
They both call `RuntimeProfileBuilder`; neither pool may have a separate feed or optic rule.

The two tracked **offline Vanilla fallback** pools are generated from the same
`RuntimeProfileBuilder` source by `OfflineProfileGenerator`. They are a
vanilla-only safe starting point, not a substitute for runtime Modded capture.
The versioned `ObjectData.json` vanilla snapshot is required generator input and
is packaged beside those two profiles for repeatable offline validation.

> **Compatibility rule:** when metadata cannot prove a safe loadout, omit that
> item or attachment. Never guess an object ID from a name or shared caliber.

## Safety gate

| Metadata state | Result |
| --- | --- |
| Not a supported firearm | Skip |
| No action or no round-power classification | Skip |
| No verified compatible feed | Skip |
| No compatible optic | Spawn the firearm without an optic |

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
|   `-- all other shotguns ──> direct shell -> exact RoundType shell -> otherwise skip
|
`-- other firearm
    `-- direct magazine -> exact MagazineType -> direct clip -> exact ClipType
        -> direct speedloader -> direct cartridge -> exact RoundType cartridge -> otherwise skip
```

Rules:

| Rule | Reason |
| --- | --- |
| A compatible-object list is authoritative. | It is the game’s explicit relationship. |
| Magazine/clip type is an exact fallback. | Those types encode their matching interface. |
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
`-- no verified route -> no optic

compatible optics
|
+-- CQC / pistol / SMG / shotgun -> reflex -> low-power scope
+-- bolt-action full-power / anti-materiel -> high-power scope
+-- rifle-caliber carbine with Picatinny as its only valid optic route
|   -> variable scope -> other scope -> reflex
`-- rifle / battle rifle -> variable scope -> other scope -> reflex
```

Candidate gate: the object must be an `Attachment` classified as `Scope` or `Reflex`; magnifiers and every other attachment type are excluded.

Mount matching uses captured prefab metadata (`PhysicalMountTypes`, direct compatibility, and an adapter's `ProvidedMountTypes`), not firearm or scope name lists. The small hard-coded list in `OpticMountPolicy` is only the H3VR **mount-type taxonomy** needed to identify sight-capable interfaces; it contains no individual weapon or attachment IDs. Runtime mounting reuses that same taxonomy, checks the exact mount type, and permits rail mounts only when oriented as a top sighting rail. Muzzle, stock, grip, and side/bottom rail positions are rejected.

An M4-style carbine is selected by metadata, never by Object ID: it must be `Carbine` size, use a rifle-caliber round class, and have Picatinny as its only recognized optic route. A direct/bespoke or proprietary route wins before role ranking. Pistol-caliber carbines remain CQC and prefer a reflex sight.

## Runtime availability and recovery

| Situation | Required behavior |
| --- | --- |
| Startup | Generate Vanilla pools as soon as the object registry is available; request Modded generation in the background. |
| Mod loader is complete | Capture Modded metadata immediately. |
| Loader has no complete signal | Wait for five seconds with no registry-count change, then capture. |
| Selector opens while Modded profiles are pending | Keep Vanilla choices usable and display a concise Modded-loading status. |
| A Modded snapshot changes | Persist a new fingerprinted pool set and remove stale generated entries from that completed snapshot. No fixed firearm count is assumed. |
| Generated ID is missing, has the wrong category, or throws while spawning | Clear the bad buffer, skip that loadout, and promote to the next weapon on the following frame. Do not crash or freeze the session. |

The loader signal is authoritative only for the content that exposes it. The five-second quiet fallback is deliberately non-blocking; later selector entries and GunGame-session exits request another background refresh.

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
| `Slingshot` | Explicitly blacklist it; it can freeze GunGame when fired. It must never enter a progression. | `Runtime_profile_builder_skips_explicitly_blacklisted_slingshot` |
| Firearm with no verified compatible feed | Skip it; never guess a magazine, round, or arrow from its name or model. This covers malformed mod entries such as `CompoundBow`, MCX rail objects mislabeled as firearms, and incomplete G28 variants. The sole exception is the self-contained `GravitonBeamer`, which intentionally uses an empty feed. | `Runtime_profile_builder_allows_only_graviton_to_be_feedless` |
| Firearm lacking GunGame round-display data | Skip it. | `Runtime_profile_builder_skips_firearms_without_gungame_round_display_data` |
| G28-style magazine-fed firearm receives a loose round | Prefer its direct/exact magazine in both pool families. | `Runtime_profile_builder_applies_one_magazine_first_policy_to_vanilla_and_modded_profiles` |
| Same-caliber but unrelated speedloader | Never infer a speedloader from `RoundType`. | `Runtime_profile_builder_does_not_infer_a_speedloader_from_round_type` |
| Tube, internal, or break-action shotgun receives P6-12/Jackhammer-style rotary feed | Use a verified shell; never a generic magazine or rotary loader. | `Runtime_profile_builder_uses_shells_for_non_box_shotguns_in_both_profile_families` |
| Real revolver shotgun loses its direct loader | Retain only its explicitly compatible speedloader. | `Runtime_profile_builder_keeps_a_revolver_shotguns_direct_speedloader` |
| Box-fed shotgun has no verified box loader | Skip it; do not fall back to shells or generic loaders. | `Runtime_profile_builder_skips_a_box_fed_shotgun_without_a_compatible_loader` |
| Missing ID, wrong ID category, or spawn exception | Skip and promote; no crash or stuck progression. | `GunGame_spawn_safety_skips_unavailable_or_mismatched_objects_without_leaking_exceptions` |
| Verified RMR or Picatinny sight mount receives no optic | Use a verified compatible reflex or scope when one exists. | `Runtime_profile_builder_selects_only_exact_mount_verified_optics` |
| Proprietary mount is replaced by a generic Picatinny optic | Direct/proprietary verified scope wins. | `Runtime_profile_builder_prefers_a_proprietary_scope_mount_over_picatinny` |
| Russian side rail receives a generic/pistol optic | Use its compatible Russian scope. | `Runtime_profile_builder_prefers_a_russian_side_rail_scope_over_other_shared_mounts` |
| CQC, rifle, and sniper receive indiscriminate optic power | Rank verified compatible optics by firearm role. | `Runtime_profile_builder_matches_verified_picatinny_optics_to_firearm_role` |
| M4-style Picatinny-only rifle carbine receives a reflex sight | Prefer a compatible variable scope; retain reflex priority for pistol-caliber carbines. | `Runtime_profile_builder_assigns_variable_scope_to_picatinny_only_rifle_carbines` |
| Scope is assigned to muzzle, stock, grip, or a generic side mount | Emit no optic for a non-sighting mount. | `Runtime_profile_builder_never_assigns_optic_to_non_sighting_mounts` |
| Firearm has no verified sight-capable mount but receives an optic | Emit no optic; do not guess a mount. | `Runtime_profile_builder_ignores_unrecognized_non_optic_mounts` |
| Magnifier is treated as a scope | Exclude it from optic candidates. | `Optic_classifier_excludes_magnifier_object_ids_case_insensitively` |
| Vanilla and Modded pool rules diverge | Use the same feed and optic resolver. | `Runtime_profile_builder_applies_one_magazine_first_policy_to_vanilla_and_modded_profiles`; `Runtime_profile_builder_applies_one_optic_policy_to_vanilla_and_modded_profiles` |
| Mods are still loading or loader state is unavailable | Vanilla remains usable; Modded refresh waits in the background. | `Modded_profile_readiness_waits_for_loader_completion_or_five_seconds_of_registry_quiet`; `Runtime_keeps_vanilla_profiles_playable_while_modded_profiles_load_into_the_active_selector` |
| Existing generated profiles are stale, deleted, or content changes | Rebuild from the current fingerprinted snapshot. | `Runtime_pool_persistence_rebuilds_when_active_content_changes_or_files_are_missing` |

## Change checklist

When changing this algorithm:

1. Record the rule and its intended outcome in this file; bump `GenerationPolicyVersion` when behavior changes.
2. Add a focused positive and negative unit test, covering the reported case and the nearest incompatible case.
3. Keep one shared resolver for Vanilla, Modded, and offline Vanilla fallback profiles; do not duplicate policy in a generator script.
4. Keep the versioned `ObjectData.json` snapshot vanilla-only; refresh it from a current runtime capture when the game data/schema changes.
5. Regenerate the tracked offline Vanilla fallback with `OfflineProfileGenerator`, then run it with `--verify`.
6. Run the Windows pipeline: `Verify`, `Test`, `Build`, `Package`.
7. Audit generated pools for invalid IDs and feed-category mismatches before deployment.

The policy version is included in the persistence fingerprint. A rule change therefore regenerates existing runtime pools instead of preserving old compatibility decisions.
