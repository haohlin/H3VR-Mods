using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HLin.GunGameProgressions;

public static class RuntimePoolPersistence
{
    // Bump when a generation rule changes so persisted runtime pools cannot
    // retain an obsolete compatibility decision after a plugin update.
    public const string CurrentGenerationPolicyVersion = "19";
    private const int ExpectedModdedPoolCount = 2;

    public static string CreateFingerprint(
        IEnumerable<RuntimeMetadataEntry> entries,
        IEnumerable<RuntimeEnemyEntry> enemies)
    {
        var content = new StringBuilder();
        content.Append("generationPolicy|");
        AppendValue(content, CurrentGenerationPolicyVersion);
        foreach (var entry in (entries ?? Enumerable.Empty<RuntimeMetadataEntry>())
                     .Where(entry => entry != null)
                     .OrderBy(entry => entry.ObjectID ?? string.Empty, StringComparer.Ordinal))
        {
            content.Append("item|");
            AppendEntry(content, entry);
        }

        foreach (var enemy in (enemies ?? Enumerable.Empty<RuntimeEnemyEntry>())
                     .Where(enemy => enemy != null)
                     .OrderBy(enemy => enemy.EnemyNameString ?? string.Empty, StringComparer.Ordinal))
        {
            content.Append("enemy|");
            AppendValue(content, enemy.EnemyNameString);
            AppendValue(content, enemy.DisplayName);
            AppendValue(content, enemy.IsModContent ? "1" : "0");
            AppendValue(content, enemy.IsSpawnable ? "1" : "0");
            AppendValue(content, enemy.DifficultyScore.ToString(CultureInfo.InvariantCulture));
            AppendValue(content, enemy.HealthScore.ToString(CultureInfo.InvariantCulture));
            AppendValue(content, enemy.ArmorScore.ToString(CultureInfo.InvariantCulture));
            AppendValue(content, enemy.WeaponThreatScore.ToString(CultureInfo.InvariantCulture));
            AppendValue(content, enemy.SpecialThreatScore.ToString(CultureInfo.InvariantCulture));
        }

        using (var hash = new SHA256Managed())
        {
            var fingerprint = new StringBuilder();
            foreach (var value in hash.ComputeHash(Encoding.UTF8.GetBytes(content.ToString())))
            {
                fingerprint.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return fingerprint.ToString();
        }
    }

    public static int CreateStableSeed(string fingerprint)
    {
        unchecked
        {
            var seed = 17;
            foreach (var character in fingerprint ?? string.Empty)
            {
                seed = (seed * 31) + character;
            }

            return seed & int.MaxValue;
        }
    }

    public static bool ShouldWrite(string storedFingerprint, string candidateFingerprint, bool poolFilesMatch)
    {
        return !poolFilesMatch ||
            string.IsNullOrEmpty(storedFingerprint) ||
            !string.Equals(storedFingerprint, candidateFingerprint, StringComparison.Ordinal);
    }

    // A Modded capture can create the first complete pair immediately, even
    // while another loader still reports Loading. Later captures may replace
    // that pair only when they contain strictly more eligible weapons. An empty
    // snapshot clears saved profiles only after the loader confirms completion.
    public static bool ShouldPromoteModdedCandidate(
        int candidatePoolCount,
        int eligibleWeaponsPerPool,
        int? persistedEligibleWeaponsPerPool,
        bool hasPersistedPair,
        bool confirmedEmptySnapshot)
    {
        return ShouldPromoteModdedCandidate(
            candidatePoolCount,
            eligibleWeaponsPerPool,
            persistedEligibleWeaponsPerPool,
            hasPersistedPair,
            confirmedEmptySnapshot,
            false);
    }

    // A complete generation-policy upgrade replaces a saved pair even when
    // its safe candidate is smaller. Count growth still governs normal scans.
    public static bool ShouldPromoteModdedCandidate(
        int candidatePoolCount,
        int eligibleWeaponsPerPool,
        int? persistedEligibleWeaponsPerPool,
        bool hasPersistedPair,
        bool confirmedEmptySnapshot,
        bool generationPolicyChanged)
    {
        if (candidatePoolCount == 0)
        {
            return hasPersistedPair && confirmedEmptySnapshot;
        }

        if (candidatePoolCount != ExpectedModdedPoolCount || eligibleWeaponsPerPool <= 0)
        {
            return false;
        }

        if (!hasPersistedPair)
        {
            return true;
        }

        if (generationPolicyChanged)
        {
            return true;
        }

        return persistedEligibleWeaponsPerPool.HasValue &&
            eligibleWeaponsPerPool > persistedEligibleWeaponsPerPool.Value;
    }

    public static bool HasCompleteModdedPoolFiles(string packagePath)
    {
        if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
        {
            return false;
        }

        return Directory.GetFiles(packagePath, "GunGameWeaponPool_Runtime_*.json")
            .Select(Path.GetFileName)
            .Count(RuntimeProfileFamily.IsModdedPoolFile) == ExpectedModdedPoolCount;
    }

    public static string ReadFingerprint(string receiptPath)
    {
        return ReadReceiptString(receiptPath, "contentFingerprint");
    }

    public static string ReadGenerationPolicyVersion(string receiptPath)
    {
        return ReadReceiptString(receiptPath, "generationPolicyVersion");
    }

    private static string ReadReceiptString(string receiptPath, string propertyName)
    {
        if (string.IsNullOrEmpty(receiptPath) || !File.Exists(receiptPath))
        {
            return null;
        }

        var receipt = File.ReadAllText(receiptPath);
        var marker = "\"" + propertyName + "\"";
        var markerIndex = receipt.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var separatorIndex = receipt.IndexOf(':', markerIndex);
        if (separatorIndex < 0)
        {
            return null;
        }

        var valueStart = receipt.IndexOf('"', separatorIndex + 1);
        if (valueStart < 0)
        {
            return null;
        }

        var valueEnd = receipt.IndexOf('"', valueStart + 1);
        return valueEnd < 0 ? null : receipt.Substring(valueStart + 1, valueEnd - valueStart - 1);
    }

    public static int? ReadEligibleWeaponsPerPool(string receiptPath)
    {
        if (string.IsNullOrEmpty(receiptPath) || !File.Exists(receiptPath))
        {
            return null;
        }

        var receipt = File.ReadAllText(receiptPath);
        const string marker = "\"eligibleWeaponsPerPool\"";
        var markerIndex = receipt.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var separatorIndex = receipt.IndexOf(':', markerIndex);
        if (separatorIndex < 0)
        {
            return null;
        }

        var valueStart = separatorIndex + 1;
        while (valueStart < receipt.Length && char.IsWhiteSpace(receipt[valueStart]))
        {
            valueStart++;
        }

        var valueEnd = valueStart;
        while (valueEnd < receipt.Length && char.IsDigit(receipt[valueEnd]))
        {
            valueEnd++;
        }

        if (valueStart == valueEnd)
        {
            return null;
        }

        int result;
        return int.TryParse(
            receipt.Substring(valueStart, valueEnd - valueStart),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out result)
            ? result
            : (int?)null;
    }

    public static bool HasExpectedPoolFiles(
        string packagePath,
        IEnumerable<string> expectedPoolFiles,
        Func<string, bool> isOwnedByPhase)
    {
        var expected = new HashSet<string>(
            (expectedPoolFiles ?? Enumerable.Empty<string>()).Where(fileName => !string.IsNullOrEmpty(fileName)),
            StringComparer.Ordinal);
        if (!Directory.Exists(packagePath))
        {
            return false;
        }

        var existing = new HashSet<string>(
            Directory.GetFiles(packagePath, "GunGameWeaponPool_Runtime_*.json")
                .Select(Path.GetFileName)
                .Where(fileName => isOwnedByPhase(fileName)),
            StringComparer.Ordinal);
        return expected.SetEquals(existing);
    }

    private static void AppendEntry(StringBuilder content, RuntimeMetadataEntry entry)
    {
        AppendValue(content, entry.ObjectID);
        AppendValue(content, entry.Category);
        AppendValue(content, entry.IsModContent ? "1" : "0");
        AppendValue(content, entry.MagazineType.ToString(CultureInfo.InvariantCulture));
        AppendValue(content, entry.ClipType.ToString(CultureInfo.InvariantCulture));
        AppendValue(content, entry.RoundType.ToString(CultureInfo.InvariantCulture));
        AppendList(content, entry.CompatibleMagazines);
        AppendList(content, entry.CompatibleClips);
        AppendList(content, entry.CompatibleSpeedLoaders);
        AppendList(content, entry.CompatibleSingleRounds);
        AppendList(content, entry.BespokeAttachments);
        AppendValue(content, entry.FirearmSize);
        AppendValue(content, entry.FirearmRoundPower);
        AppendValue(content, entry.FirearmAction);
        AppendList(content, entry.FirearmFeedOptions);
        AppendList(content, entry.FirearmMounts);
        AppendValue(content, entry.AttachmentMount);
        AppendValue(content, entry.AttachmentFeature);
        AppendValue(content, entry.OpticKind);
        AppendList(content, entry.PhysicalMountTypes);
        AppendList(content, entry.ProvidedMountTypes);
        AppendValue(content, entry.OpticMinMagnification.ToString("R", CultureInfo.InvariantCulture));
        AppendValue(content, entry.OpticMaxMagnification.ToString("R", CultureInfo.InvariantCulture));
        AppendValue(content, entry.IsVariableMagnification ? "1" : "0");
        AppendValue(content, entry.IsGunGameRoundDisplaySupported ? "1" : "0");
        AppendValue(content, entry.IsVerifiedFirearmPrefab ? "1" : "0");
    }

    private static void AppendList(StringBuilder content, IEnumerable<string> values)
    {
        content.Append('[');
        foreach (var value in (values ?? Enumerable.Empty<string>())
                     .OrderBy(value => value ?? string.Empty, StringComparer.Ordinal))
        {
            AppendValue(content, value);
        }

        content.Append(']');
    }

    private static void AppendValue(StringBuilder content, string value)
    {
        value = value ?? string.Empty;
        content.Append(value.Length);
        content.Append(':');
        content.Append(value);
        content.Append('|');
    }
}
