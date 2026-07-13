# GunGame profile-generation policy

This policy applies identically to **Vanilla** and **Modded** runtime pools.
They both call `RuntimeProfileBuilder`; neither pool may have a separate feed or optic rule.

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
`-- rifle / battle rifle -> variable scope -> other scope -> reflex
```

Candidate gate: the object must be an `Attachment` classified as `Scope` or `Reflex`; magnifiers and every other attachment type are excluded.

Mount matching uses captured prefab metadata (`PhysicalMountTypes`, direct compatibility, and an adapter's `ProvidedMountTypes`), not firearm or scope name lists. The small hard-coded list in `OpticMountPolicy` is only the H3VR **mount-type taxonomy** needed to identify sight-capable interfaces; it contains no individual weapon or attachment IDs. Runtime mounting reuses that same taxonomy, checks the exact mount type, and permits rail mounts only when oriented as a top sighting rail. Muzzle, stock, grip, and side/bottom rail positions are rejected.

## Change checklist

When changing this algorithm:

1. Update this policy and bump `GenerationPolicyVersion`.
2. Add one positive and one negative unit test for the new rule.
3. Keep the resolver shared by Vanilla and Modded profiles.
4. Run the Windows pipeline: `Verify`, `Test`, `Build`, `Package`.
5. Audit generated pools for invalid IDs and feed-category mismatches before deployment.

The policy version is included in the persistence fingerprint. A rule change therefore regenerates existing runtime pools instead of preserving old compatibility decisions.
