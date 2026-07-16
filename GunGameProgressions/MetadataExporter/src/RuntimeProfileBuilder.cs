using System;
using System.Collections.Generic;
using System.Linq;

namespace HLin.GunGameProgressions;

public sealed class RuntimeMetadataEntry
{
    public string ObjectID { get; set; }
    public string Category { get; set; }
    public bool IsModContent { get; set; }
    public int MagazineType { get; set; }
    public int ClipType { get; set; }
    public int RoundType { get; set; }
    public List<string> CompatibleMagazines { get; set; }
    public List<string> CompatibleClips { get; set; }
    public List<string> CompatibleSpeedLoaders { get; set; }
    public List<string> CompatibleSingleRounds { get; set; }
    public List<string> BespokeAttachments { get; set; }
    public string FirearmSize { get; set; }
    public string FirearmRoundPower { get; set; }
    public string FirearmAction { get; set; }
    public List<string> FirearmFeedOptions { get; set; }
    public List<string> FirearmMounts { get; set; }
    public string AttachmentMount { get; set; }
    public string AttachmentFeature { get; set; }
    public string OpticKind { get; set; }
    public List<string> PhysicalMountTypes { get; set; }
    public List<string> ProvidedMountTypes { get; set; }
    public float OpticMinMagnification { get; set; }
    public float OpticMaxMagnification { get; set; }
    public bool IsVariableMagnification { get; set; }
    public bool IsGunGameRoundDisplaySupported { get; set; } = true;
    // Runtime capture accepts only catalog entries with firearm identity. The
    // spawn-safety layer validates IDs/categories and isolates spawn failures.
    public bool IsVerifiedFirearmPrefab { get; set; } = true;
}

public sealed class RuntimeEnemyEntry
{
    public string EnemyNameString { get; set; }
    public string DisplayName { get; set; }
    public bool IsModContent { get; set; }
    public bool IsSpawnable { get; set; }
    public int DifficultyScore { get; set; }
    public int HealthScore { get; set; }
    public int ArmorScore { get; set; }
    public int WeaponThreatScore { get; set; }
    public int SpecialThreatScore { get; set; }
}

public sealed class RuntimeGun
{
    public string GunName { get; set; }
    public string MagName { get; set; }
    public List<string> MagNames { get; set; }
    public string Extra { get; set; }
    public int CategoryID { get; set; }
}

public sealed class RuntimeEnemy
{
    public string EnemyNameString { get; set; }
    public int Value { get; set; }
}

public sealed class RuntimeWeaponPool
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int OrderType { get; set; }
    public string WeaponPoolType { get; set; }
    public int EnemyProgressionType { get; set; }
    public string Family { get; set; }
    public string EnemyType { get; set; }
    public List<RuntimeEnemy> Enemies { get; set; }
    public List<RuntimeGun> Guns { get; set; }
}

public sealed class RuntimeGenerationResult
{
    public List<RuntimeWeaponPool> Pools { get; set; }
    public List<string> SkippedFirearms { get; set; }
    public List<string> FirearmsWithoutOptics { get; set; }
}

public static class RuntimeProfileBuilder
{
    // The one deliberate object-ID exclusion. Slingshot can freeze a GunGame
    // progression when fired, so it must never be emitted into any pool.
    private static readonly HashSet<string> ExplicitFirearmBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Slingshot",
    };

    public static List<RuntimeWeaponPool> Build(IEnumerable<RuntimeMetadataEntry> sourceEntries, Random random)
    {
        return BuildWithDiagnostics(sourceEntries, DefaultEnemies(), random).Pools;
    }

    public static List<RuntimeWeaponPool> Build(
        IEnumerable<RuntimeMetadataEntry> sourceEntries,
        IEnumerable<RuntimeEnemyEntry> sourceEnemies,
        Random random)
    {
        return BuildWithDiagnostics(sourceEntries, sourceEnemies, random).Pools;
    }

    public static RuntimeGenerationResult BuildWithDiagnostics(IEnumerable<RuntimeMetadataEntry> sourceEntries, Random random)
    {
        return BuildWithDiagnostics(sourceEntries, DefaultEnemies(), random);
    }

    public static RuntimeGenerationResult BuildWithDiagnostics(
        IEnumerable<RuntimeMetadataEntry> sourceEntries,
        IEnumerable<RuntimeEnemyEntry> sourceEnemies,
        Random random)
    {
        if (sourceEntries == null)
        {
            throw new ArgumentNullException("sourceEntries");
        }

        if (random == null)
        {
            throw new ArgumentNullException("random");
        }

        if (sourceEnemies == null)
        {
            throw new ArgumentNullException("sourceEnemies");
        }

        var entries = sourceEntries
            .Where(entry => entry != null && !string.IsNullOrEmpty(entry.ObjectID))
            .OrderBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .ToList();
        var entriesById = entries
            .GroupBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var firearms = entries
            .Where(entry => entry.Category == "Firearm")
            .GroupBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var vanillaEntries = entries.Where(entry => !entry.IsModContent).ToList();
        var vanillaEntriesById = vanillaEntries
            .GroupBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var vanillaFirearms = firearms.Where(entry => !entry.IsModContent).ToList();
        var moddedFirearms = firearms.Where(entry => entry.IsModContent).ToList();
        var enemyEntries = sourceEnemies
            .Where(enemy => enemy != null && enemy.IsSpawnable && !string.IsNullOrEmpty(enemy.EnemyNameString))
            .GroupBy(enemy => enemy.EnemyNameString, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(enemy => enemy.DifficultyScore).First())
            .ToList();

        var skipped = new List<string>();
        var noOptic = new List<string>();
        var vanillaIndex = new RuntimeProfileIndex(vanillaEntries, vanillaEntriesById);
        var moddedIndex = new RuntimeProfileIndex(entries, entriesById);
        var vanillaWeapons = BuildWeapons(vanillaFirearms, vanillaIndex, random, skipped, noOptic);
        var moddedWeapons = BuildWeapons(moddedFirearms, moddedIndex, random, skipped, noOptic);
        var rot = FindRot(enemyEntries);
        var vanillaMixed = BuildMixedEnemies(enemyEntries.Where(enemy => !enemy.IsModContent));
        var allActiveMixed = BuildMixedEnemies(enemyEntries);

        var pools = new List<RuntimeWeaponPool>
        {
            CreateScenarioPool(
                "01_Vanilla_Rot",
                "Runtime 01 - Vanilla Rot",
                "A Rot-only random progression using active vanilla firearms.",
                1,
                0,
                vanillaWeapons,
                rot),
            CreateScenarioPool(
                "03_Vanilla_Mixed_Enemy",
                "Runtime 03 - Vanilla Mixed Enemy",
                "A weighted mixed-enemy progression using active vanilla firearms.",
                1,
                0,
                vanillaWeapons,
                vanillaMixed.ToArray()),
        };
        if (moddedWeapons.Count > 0)
        {
            pools.Insert(1, CreateScenarioPool(
                "02_Modded_Rot",
                "Runtime 02 - Modded Rot",
                "A Rot-only random progression using active modded firearms and compatible active feeds.",
                1,
                0,
                moddedWeapons,
                rot));
            pools.Add(CreateScenarioPool(
                "04_Modded_Mixed_Enemy",
                "Runtime 04 - Modded Mixed Enemy",
                "A weighted mixed-enemy progression using active modded firearms and active Sosigs.",
                1,
                0,
                moddedWeapons,
                allActiveMixed.ToArray()));
        }

        return new RuntimeGenerationResult
        {
            Pools = pools,
            SkippedFirearms = skipped,
            FirearmsWithoutOptics = noOptic,
        };
    }

    private static List<RuntimeGun> BuildWeapons(
        IEnumerable<RuntimeMetadataEntry> firearms,
        RuntimeProfileIndex index,
        Random random,
        List<string> skipped,
        List<string> noOptic)
    {
        var weapons = new List<RuntimeGun>();
        foreach (var firearm in firearms)
        {
            if (!IsSupportedGunGameFirearm(firearm))
            {
                skipped.Add(firearm.ObjectID);
                continue;
            }

            var feeds = GetCompatibleFeeds(index, firearm);
            if (feeds.Count == 0 && !AllowsFeedlessLoadout(firearm))
            {
                skipped.Add(firearm.ObjectID);
                continue;
            }

            var extra = SelectOptic(index, firearm, random);
            if (string.IsNullOrEmpty(extra))
            {
                noOptic.Add(firearm.ObjectID);
            }

            weapons.Add(new RuntimeGun
            {
                GunName = firearm.ObjectID,
                MagName = feeds.Count == 0 ? string.Empty : feeds[0].ObjectID,
                MagNames = feeds.Select(feed => feed.ObjectID).ToList(),
                Extra = extra ?? string.Empty,
                CategoryID = feeds.Count == 0 ? 0 : feeds[0].CategoryID,
            });
        }

        return weapons;
    }

    private static RuntimeEnemy FindRot(IEnumerable<RuntimeEnemyEntry> enemies)
    {
        var rot = enemies.FirstOrDefault(enemy => string.Equals(enemy.EnemyNameString, "RW_Rot", StringComparison.OrdinalIgnoreCase));
        return new RuntimeEnemy
        {
            EnemyNameString = rot == null ? "RW_Rot" : rot.EnemyNameString,
            Value = 1,
        };
    }

    private static List<RuntimeEnemy> BuildMixedEnemies(IEnumerable<RuntimeEnemyEntry> enemies)
    {
        var sorted = enemies
            .OrderBy(enemy => Math.Max(0, enemy.DifficultyScore))
            .ThenBy(enemy => enemy.EnemyNameString, StringComparer.Ordinal)
            .ToList();
        if (sorted.Count == 0)
        {
            return new List<RuntimeEnemy> { FindRot(sorted) };
        }

        var weighted = new List<RuntimeEnemy>();
        foreach (var enemy in sorted)
        {
            var spawnWeight = EnemyWeightPolicy.Resolve(enemy);
            for (var copy = 0; copy < spawnWeight.Multiplicity; copy++)
            {
                weighted.Add(new RuntimeEnemy
                {
                    EnemyNameString = enemy.EnemyNameString,
                    Value = spawnWeight.Value,
                });
            }
        }

        return weighted;
    }

    private static RuntimeWeaponPool CreateScenarioPool(
        string family,
        string name,
        string description,
        int orderType,
        int enemyProgressionType,
        IEnumerable<RuntimeGun> weapons,
        params RuntimeEnemy[] enemies)
    {
        return new RuntimeWeaponPool
        {
            Name = name,
            Description = description,
            OrderType = orderType,
            WeaponPoolType = "Advanced",
            EnemyProgressionType = enemyProgressionType,
            Family = family,
            EnemyType = enemies[0].EnemyNameString,
            Enemies = enemies.Select(CloneEnemy).ToList(),
            Guns = weapons.Select(CloneGun).ToList(),
        };
    }

    private static RuntimeEnemy CloneEnemy(RuntimeEnemy enemy)
    {
        return new RuntimeEnemy
        {
            EnemyNameString = enemy.EnemyNameString,
            Value = enemy.Value,
        };
    }

    private static RuntimeGun CloneGun(RuntimeGun gun)
    {
        return new RuntimeGun
        {
            GunName = gun.GunName,
            MagName = gun.MagName,
            MagNames = new List<string>(gun.MagNames),
            Extra = gun.Extra,
            CategoryID = gun.CategoryID,
        };
    }

    private static List<FeedCandidate> GetCompatibleFeeds(
        RuntimeProfileIndex index,
        RuntimeMetadataEntry firearm)
    {
        if (firearm.FirearmRoundPower == "Shotgun")
        {
            return GetCompatibleShotgunFeeds(index, firearm);
        }

        return GetStandardFeeds(index, firearm, true);
    }

    private static List<FeedCandidate> GetCompatibleShotgunFeeds(
        RuntimeProfileIndex index,
        RuntimeMetadataEntry firearm)
    {
        if (!IsShellFedShotgun(firearm))
        {
            // A detachable or rotary shotgun may only use a loader the
            // firearm explicitly supports (or a magazine with its exact
            // MagazineType). Do not infer a rotary loader from round type.
            return GetStandardFeeds(index, firearm, false);
        }

        var directSpeedLoaders = GetDirectFeeds(
            firearm.CompatibleSpeedLoaders,
            index.EntriesById,
            "SpeedLoader");
        if (UsesExplicitRevolverSpeedLoader(firearm, directSpeedLoaders))
        {
            return directSpeedLoaders;
        }

        var shells = GetCompatibleShells(index, firearm);
        if (shells.Count > 0)
        {
            return shells;
        }

        // A shell-fed shotgun without a compatible shell is safer to exclude
        // than to equip with a same-round-type rotary loader.
        return new List<FeedCandidate>();
    }

    private static List<FeedCandidate> GetStandardFeeds(
        RuntimeProfileIndex index,
        RuntimeMetadataEntry firearm,
        bool allowCartridges)
    {
        return FirstAvailableFeeds(
            GetDirectFeeds(firearm.CompatibleMagazines, index.EntriesById, "Magazine"),
            index.GetFeeds("Magazine", firearm.MagazineType),
            GetDirectFeeds(firearm.CompatibleClips, index.EntriesById, "Clip"),
            index.GetFeeds("Clip", firearm.ClipType),
            GetDirectFeeds(firearm.CompatibleSpeedLoaders, index.EntriesById, "SpeedLoader"),
            allowCartridges
                ? GetDirectFeeds(firearm.CompatibleSingleRounds, index.EntriesById, "Cartridge")
                : new List<FeedCandidate>(),
            allowCartridges
                ? index.GetFeeds("Cartridge", firearm.RoundType)
                : new List<FeedCandidate>());
    }

    private static bool IsSupportedGunGameFirearm(RuntimeMetadataEntry firearm)
    {
        return firearm.IsGunGameRoundDisplaySupported &&
            firearm.IsVerifiedFirearmPrefab &&
            !ExplicitFirearmBlacklist.Contains(firearm.ObjectID ?? string.Empty);
    }

    private static bool AllowsFeedlessLoadout(RuntimeMetadataEntry firearm)
    {
        // GravitonBeamer is self-contained. Every other progression firearm
        // needs a verified magazine, clip, speedloader, or cartridge so a
        // bad mod metadata entry cannot produce an unusable loadout.
        return string.Equals(firearm.ObjectID, "GravitonBeamer", StringComparison.OrdinalIgnoreCase);
    }

    private static List<FeedCandidate> FirstAvailableFeeds(params List<FeedCandidate>[] groups)
    {
        return (groups ?? new List<FeedCandidate>[0])
            .FirstOrDefault(group => group != null && group.Count > 0)
            ?? new List<FeedCandidate>();
    }

    private static bool IsShellFedShotgun(RuntimeMetadataEntry firearm)
    {
        if (firearm.FirearmRoundPower != "Shotgun")
        {
            return false;
        }

        var options = firearm.FirearmFeedOptions ?? new List<string>();
        return !options.Contains("BoxMag");
    }

    private static bool UsesExplicitRevolverSpeedLoader(
        RuntimeMetadataEntry firearm,
        List<FeedCandidate> directSpeedLoaders)
    {
        return string.Equals(firearm.FirearmAction, "Revolver", StringComparison.Ordinal) &&
            directSpeedLoaders != null &&
            directSpeedLoaders.Count > 0;
    }

    private static List<FeedCandidate> GetCompatibleShells(
        RuntimeProfileIndex index,
        RuntimeMetadataEntry firearm)
    {
        return FirstAvailableFeeds(
            GetDirectFeeds(firearm.CompatibleSingleRounds, index.EntriesById, "Cartridge"),
            index.GetFeeds("Cartridge", firearm.RoundType));
    }

    private static List<FeedCandidate> GetDirectFeeds(
        List<string> objectIds,
        IDictionary<string, RuntimeMetadataEntry> entriesById,
        string category)
    {
        var candidates = new List<FeedCandidate>();
        foreach (var objectId in DistinctIds(objectIds))
        {
            RuntimeMetadataEntry feed;
            if (entriesById.TryGetValue(objectId, out feed) && feed.Category == category)
            {
                AddFeedCandidate(candidates, feed);
            }
        }

        return candidates;
    }

    private static void AddFeedCandidate(List<FeedCandidate> candidates, RuntimeMetadataEntry feed)
    {
        var categoryId = FeedCategoryId(feed.Category);
        if (categoryId < 0 || candidates.Any(candidate => candidate.ObjectID == feed.ObjectID))
        {
            return;
        }

        candidates.Add(new FeedCandidate(feed.ObjectID, categoryId));
    }

    private static string SelectOptic(
        RuntimeProfileIndex index,
        RuntimeMetadataEntry firearm,
        Random random)
    {
        var directCandidates = GetDirectOptics(index, firearm);
        if (directCandidates.Count > 0)
        {
            // BespokeAttachments is the game's direct compatibility list. A
            // verified optic there is the authoritative proprietary choice.
            return SelectPreferredOptic(directCandidates, firearm, random);
        }

        foreach (var rule in OpticMountPolicy.Rank(firearm.PhysicalMountTypes))
        {
            var candidates = index.GetOptics(rule.OpticKinds)
                .Where(attachment => IsCompatibleWithRule(attachment, rule))
                .GroupBy(attachment => attachment.ObjectID, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            if (candidates.Count > 0)
            {
                return SelectPreferredOptic(candidates, firearm, random);
            }

            var adapterCandidates = GetAdapterCompatibleOptics(index, rule);
            if (adapterCandidates.Count > 0)
            {
                return SelectPreferredOptic(adapterCandidates, firearm, random);
            }
        }

        return null;
    }

    private static List<RuntimeMetadataEntry> GetAdapterCompatibleOptics(
        RuntimeProfileIndex index,
        OpticMountRule firearmRule)
    {
        return index.EntriesById.Values
            .Where(adapter => adapter.Category == "Attachment" &&
                adapter.AttachmentFeature == "Adapter" &&
                HasMount(adapter.PhysicalMountTypes, firearmRule.MountType))
            .SelectMany(adapter => OpticMountPolicy.Rank(adapter.ProvidedMountTypes))
            .SelectMany(rule => index.GetOptics(rule.OpticKinds)
                .Where(attachment => IsCompatibleWithRule(attachment, rule)))
            .GroupBy(attachment => attachment.ObjectID, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static List<RuntimeMetadataEntry> GetDirectOptics(
        RuntimeProfileIndex index,
        RuntimeMetadataEntry firearm)
    {
        var candidates = new List<RuntimeMetadataEntry>();
        foreach (var objectId in firearm.BespokeAttachments ?? new List<string>())
        {
            RuntimeMetadataEntry attachment;
            if (index.EntriesById.TryGetValue(objectId, out attachment) && IsVerifiedOptic(attachment))
            {
                candidates.Add(attachment);
            }
        }

        return candidates;
    }

    private static bool IsCompatibleWithRule(RuntimeMetadataEntry attachment, OpticMountRule rule)
    {
        return IsVerifiedOptic(attachment) &&
            rule.Accepts(attachment.OpticKind) &&
            HasMount(attachment.PhysicalMountTypes, rule.MountType);
    }

    private static string SelectPreferredOptic(
        IEnumerable<RuntimeMetadataEntry> candidates,
        RuntimeMetadataEntry firearm,
        Random random)
    {
        var candidateList = candidates
            .Where(candidate => candidate != null)
            .ToList();
        if (candidateList.Count == 0)
        {
            return null;
        }

        var bestRank = candidateList.Min(candidate => OpticFitRank(firearm, candidate));
        var bestCandidates = candidateList
            .Where(candidate => OpticFitRank(firearm, candidate) == bestRank)
            .OrderBy(candidate => candidate.ObjectID, StringComparer.Ordinal)
            .ToList();
        return bestCandidates[random.Next(bestCandidates.Count)].ObjectID;
    }

    // This is a selection preference only. Physical mount verification and
    // proprietary-mount priority are enforced before it is called.
    private static int OpticFitRank(RuntimeMetadataEntry firearm, RuntimeMetadataEntry optic)
    {
        if (IsPicatinnyOnlyRifleCarbine(firearm))
        {
            return VariableScopeFirstRank(optic);
        }

        if (IsCloseRangeFirearm(firearm))
        {
            if (optic.OpticKind == "Reflex")
            {
                return 0;
            }

            return optic.OpticMaxMagnification > 0f && optic.OpticMaxMagnification <= 4f ? 10 : 20;
        }

        if (IsHighPowerFirearm(firearm))
        {
            if (optic.OpticKind == "Scope")
            {
                if (optic.OpticMaxMagnification >= 8f)
                {
                    return 0;
                }

                return optic.OpticMaxMagnification >= 4f ? 10 : 20;
            }

            return 100;
        }

        if (optic.OpticKind == "Scope")
        {
            return VariableScopeFirstRank(optic);
        }

        return 30;
    }

    private static int VariableScopeFirstRank(RuntimeMetadataEntry optic)
    {
        if (optic.OpticKind != "Scope")
        {
            return 30;
        }

        return optic.IsVariableMagnification && optic.OpticMinMagnification <= 3f && optic.OpticMaxMagnification >= 4f
            ? 0
            : 10;
    }

    private static bool IsCloseRangeFirearm(RuntimeMetadataEntry firearm)
    {
        return firearm.FirearmRoundPower == "Tiny" ||
            firearm.FirearmRoundPower == "Pistol" ||
            firearm.FirearmRoundPower == "Shotgun" ||
            firearm.FirearmSize == "Pocket" ||
            firearm.FirearmSize == "Pistol" ||
            (firearm.FirearmAction == "Automatic" &&
                firearm.FirearmSize == "Compact");
    }

    private static bool IsPicatinnyOnlyRifleCarbine(RuntimeMetadataEntry firearm)
    {
        if (firearm.FirearmSize != "Carbine" ||
            firearm.FirearmRoundPower == "Tiny" ||
            firearm.FirearmRoundPower == "Pistol" ||
            firearm.FirearmRoundPower == "Shotgun")
        {
            return false;
        }

        var opticMounts = OpticMountPolicy.Rank(firearm.PhysicalMountTypes).ToList();
        return opticMounts.Count == 1 &&
            string.Equals(opticMounts[0].MountType, "Picatinny", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHighPowerFirearm(RuntimeMetadataEntry firearm)
    {
        return firearm.FirearmRoundPower == "AntiMaterial" ||
            (firearm.FirearmAction == "BoltAction" &&
                (firearm.FirearmRoundPower == "FullPower" || firearm.FirearmRoundPower == "Exotic"));
    }

    private static bool IsVerifiedOptic(RuntimeMetadataEntry attachment)
    {
        return attachment.Category == "Attachment" &&
            (attachment.OpticKind == "Scope" || attachment.OpticKind == "Reflex");
    }

    private static bool HasMount(IEnumerable<string> mounts, string expectedMount)
    {
        return (mounts ?? Enumerable.Empty<string>())
            .Select(MountResolution.Resolve)
            .Where(resolution => resolution.IsResolved)
            .Any(resolution => string.Equals(resolution.CanonicalMount, expectedMount, StringComparison.OrdinalIgnoreCase));
    }

    private static RuntimeEnemyEntry[] DefaultEnemies()
    {
        return new[]
        {
            new RuntimeEnemyEntry
            {
                EnemyNameString = "RW_Rot",
                DisplayName = "Rot",
                IsSpawnable = true,
                DifficultyScore = 1,
            },
        };
    }

    private static List<string> DistinctIds(params List<string>[] lists)
    {
        return lists
            .Where(list => list != null)
            .SelectMany(list => list)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static int FeedCategoryId(string category)
    {
        switch (category)
        {
            case "Magazine": return 0;
            case "Clip": return 1;
            case "SpeedLoader":
            case "Cartridge": return 2;
            default: return -1;
        }
    }

    private static int RoundPowerRank(string roundPower)
    {
        switch (roundPower)
        {
            case "Tiny": return 1;
            case "Pistol": return 2;
            case "Shotgun": return 3;
            case "Intermediate": return 4;
            case "FullPower": return 5;
            case "AntiMaterial": return 6;
            case "Ordnance": return 7;
            case "Exotic": return 8;
            case "Fire": return 9;
            default: return 10;
        }
    }

    private static int SizeRank(string firearmSize)
    {
        switch (firearmSize)
        {
            case "Pocket": return 1;
            case "Pistol": return 2;
            case "Compact": return 3;
            case "Carbine": return 4;
            case "FullSize": return 5;
            case "Bulky": return 6;
            case "Oversize": return 7;
            default: return 8;
        }
    }

    private sealed class RuntimeProfileIndex
    {
        private readonly Dictionary<string, Dictionary<int, List<FeedCandidate>>> feedsByCategory =
            new Dictionary<string, Dictionary<int, List<FeedCandidate>>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<RuntimeMetadataEntry>> opticsByKind =
            new Dictionary<string, List<RuntimeMetadataEntry>>(StringComparer.Ordinal);

        public RuntimeProfileIndex(
            IEnumerable<RuntimeMetadataEntry> entries,
            IDictionary<string, RuntimeMetadataEntry> entriesById)
        {
            EntriesById = entriesById;
            foreach (var entry in entries)
            {
                IndexFeed(entry);
                IndexOptic(entry);
            }
        }

        public IDictionary<string, RuntimeMetadataEntry> EntriesById { get; private set; }

        public List<FeedCandidate> GetFeeds(string category, int type)
        {
            Dictionary<int, List<FeedCandidate>> byType;
            List<FeedCandidate> candidates;
            return type != 0 &&
                feedsByCategory.TryGetValue(category, out byType) &&
                byType.TryGetValue(type, out candidates)
                ? new List<FeedCandidate>(candidates)
                : new List<FeedCandidate>();
        }

        public List<RuntimeMetadataEntry> GetOptics(IEnumerable<string> opticKinds)
        {
            return (opticKinds ?? Enumerable.Empty<string>())
                .Where(kind => !string.IsNullOrEmpty(kind))
                .Distinct(StringComparer.Ordinal)
                .SelectMany(GetOptics)
                .GroupBy(entry => entry.ObjectID, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private IEnumerable<RuntimeMetadataEntry> GetOptics(string opticKind)
        {
            List<RuntimeMetadataEntry> candidates;
            return opticsByKind.TryGetValue(opticKind, out candidates)
                ? candidates
                : Enumerable.Empty<RuntimeMetadataEntry>();
        }

        private void IndexFeed(RuntimeMetadataEntry entry)
        {
            var categoryId = FeedCategoryId(entry.Category);
            if (categoryId < 0)
            {
                return;
            }

            var type = entry.Category == "Magazine"
                ? entry.MagazineType
                : entry.Category == "Clip"
                    ? entry.ClipType
                    : entry.RoundType;
            if (type == 0)
            {
                return;
            }

            Dictionary<int, List<FeedCandidate>> byType;
            if (!feedsByCategory.TryGetValue(entry.Category, out byType))
            {
                byType = new Dictionary<int, List<FeedCandidate>>();
                feedsByCategory.Add(entry.Category, byType);
            }

            List<FeedCandidate> candidates;
            if (!byType.TryGetValue(type, out candidates))
            {
                candidates = new List<FeedCandidate>();
                byType.Add(type, candidates);
            }

            AddFeedCandidate(candidates, entry);
        }

        private void IndexOptic(RuntimeMetadataEntry entry)
        {
            if (entry.Category != "Attachment" || string.IsNullOrEmpty(entry.OpticKind))
            {
                return;
            }

            List<RuntimeMetadataEntry> candidates;
            if (!opticsByKind.TryGetValue(entry.OpticKind, out candidates))
            {
                candidates = new List<RuntimeMetadataEntry>();
                opticsByKind.Add(entry.OpticKind, candidates);
            }

            if (!candidates.Any(candidate => candidate.ObjectID == entry.ObjectID))
            {
                candidates.Add(entry);
            }
        }
    }

    private sealed class FeedCandidate
    {
        public FeedCandidate(string objectId, int categoryId)
        {
            ObjectID = objectId;
            CategoryID = categoryId;
        }

        public string ObjectID { get; private set; }
        public int CategoryID { get; private set; }
    }
}
