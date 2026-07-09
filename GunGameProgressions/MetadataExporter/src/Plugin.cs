using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using FistVR;
using UnityEngine;

namespace HLin.GunGameProgressions;

[BepInPlugin("HLin.GunGameProgressionsMetadataExporter", "GunGame Progressions Metadata Exporter", "1.3.5")]
[BepInProcess("h3vr.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    private const float InitialSnapshotDeadlineSeconds = 5f;
    private const float PollIntervalSeconds = 0.25f;
    private const int StableSamplesRequired = 3;
    private const int MaximumSamples = 120;
    private const int MetadataEntriesPerFrame = 64;

    private void Start()
    {
        StartCoroutine(ExportLifecycle());
    }

    private IEnumerator ExportLifecycle()
    {
        var deadline = Time.realtimeSinceStartup + InitialSnapshotDeadlineSeconds;
        var earlySnapshotWritten = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            Dictionary<string, FVRObject> objects;
            if (TryGetObjectData(out objects) && objects.Count > 0)
            {
                yield return StartCoroutine(WriteRuntimeMetadata(objects, "startup"));
                earlySnapshotWritten = true;
                break;
            }

            yield return new WaitForSeconds(PollIntervalSeconds);
        }

        if (!earlySnapshotWritten)
        {
            Logger.LogWarning("H3VR object metadata was not ready within five seconds; packaged vanilla fallback profiles remain available for this launch.");
        }

        yield return StartCoroutine(ExportWhenObjectDataIsStable());
    }

    private IEnumerator ExportWhenObjectDataIsStable()
    {
        var previousCount = -1;
        var stableSamples = 0;
        for (var sample = 0; sample < MaximumSamples; sample++)
        {
            Dictionary<string, FVRObject> objects;
            if (!TryGetObjectData(out objects) || objects.Count == 0)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            stableSamples = objects.Count == previousCount ? stableSamples + 1 : 0;
            previousCount = objects.Count;
            if (stableSamples >= StableSamplesRequired)
            {
                yield return StartCoroutine(WriteRuntimeMetadata(objects, "stable-refresh"));
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }

        Logger.LogWarning("Timed out waiting for H3VR object metadata to stabilize; keeping the most recent startup snapshot and packaged fallback profiles.");
    }

    private bool TryGetObjectData(out Dictionary<string, FVRObject> objects)
    {
        objects = null;
        try
        {
            objects = IM.OD;
            return objects != null;
        }
        catch (Exception exception)
        {
            Logger.LogWarning("H3VR object data is not ready: " + exception.Message);
            return false;
        }
    }

    private IEnumerator WriteRuntimeMetadata(Dictionary<string, FVRObject> objects, string phase)
    {
        var entries = new List<RuntimeMetadataEntry>();
        var skipped = 0;
        var snapshot = objects.Values.Where(item => item != null).ToList();
        for (var index = 0; index < snapshot.Count; index++)
        {
            var item = snapshot[index];
            if (item == null || string.IsNullOrEmpty(item.ItemID))
            {
                skipped++;
                continue;
            }

            var category = item.Category.ToString();
            var opticKind = GetOpticKind(item);

            entries.Add(new RuntimeMetadataEntry
            {
                ObjectID = item.ItemID,
                Category = category,
                IsModContent = item.IsModContent,
                MagazineType = (int)item.MagazineType,
                ClipType = (int)item.ClipType,
                RoundType = (int)item.RoundType,
                CompatibleMagazines = ObjectIds(item.CompatibleMagazines),
                CompatibleClips = ObjectIds(item.CompatibleClips),
                CompatibleSpeedLoaders = ObjectIds(item.CompatibleSpeedLoaders),
                CompatibleSingleRounds = ObjectIds(item.CompatibleSingleRounds),
                BespokeAttachments = ObjectIds(item.BespokeAttachments),
                FirearmSize = item.TagFirearmSize.ToString(),
                FirearmRoundPower = item.TagFirearmRoundPower.ToString(),
                FirearmMounts = item.TagFirearmMounts == null
                    ? new List<string>()
                    : item.TagFirearmMounts.Select(mount => mount.ToString()).ToList(),
                AttachmentMount = item.TagAttachmentMount.ToString(),
                AttachmentFeature = item.TagAttachmentFeature.ToString(),
                OpticKind = opticKind,
                PhysicalMountTypes = GetPhysicalMountTypes(item, category, opticKind),
            });

            if (index > 0 && index % MetadataEntriesPerFrame == 0)
            {
                yield return null;
            }
        }

        entries.Sort((left, right) => string.CompareOrdinal(left.ObjectID, right.ObjectID));
        var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var metadataPath = Path.Combine(packagePath, "ObjectData.json");
        WriteTextAtomically(metadataPath, SerializeMetadata(entries));

        ProfileRules rules;
        try
        {
            rules = ProfileRules.Load(packagePath);
        }
        catch (Exception exception)
        {
            Logger.LogError("Unable to load GunGame profile rules: " + exception.Message);
            yield break;
        }

        var profileEntries = entries.Where(entry => !rules.IsBlacklisted(entry)).ToList();
        var randomSeed = Guid.NewGuid().GetHashCode();
        var enemyEntries = BuildEnemyEntries();
        var result = RuntimeProfileBuilder.BuildWithDiagnostics(profileEntries, enemyEntries, new System.Random(randomSeed));
        var runtimePoolsPath = Path.Combine(packagePath, "RuntimePools");
        Directory.CreateDirectory(runtimePoolsPath);
        var expectedPoolFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var runtimePool in result.Pools)
        {
            var poolFileName = RuntimePoolFileName(runtimePool);
            expectedPoolFiles.Add(poolFileName);
            var poolPath = Path.Combine(packagePath, poolFileName);
            WriteTextAtomically(poolPath, SerializePool(runtimePool));
        }
        RemoveStaleRuntimePools(packagePath, expectedPoolFiles);
        RemoveStaleRuntimePools(runtimePoolsPath, new HashSet<string>(StringComparer.Ordinal));

        var receiptPath = Path.Combine(runtimePoolsPath, "runtime-generation-receipt.json");
        WriteTextAtomically(receiptPath, SerializeReceipt(entries, enemyEntries, result, randomSeed, phase));
        WriteTextAtomically(Path.Combine(runtimePoolsPath, "enemy-catalog.json"), SerializeEnemyCatalog(enemyEntries));

        var activeModdedItems = entries.Count(entry => entry.IsModContent);
        var eligibleWeapons = result.Pools.Count == 0 ? 0 : result.Pools[0].Guns.Count;
        Logger.LogInfo(
            "Exported " + entries.Count + " active metadata entries (" + activeModdedItems + " modded) to " + metadataPath +
            "; generated " + result.Pools.Count + " advanced runtime GunGame pools with " + eligibleWeapons +
            " firearms and " + enemyEntries.Count + " active Sosig types during " + phase +
            "; skipped " + result.SkippedFirearms.Count + " without a compatible feed.");
    }

    private static List<string> ObjectIds(IEnumerable<FVRObject> objects)
    {
        if (objects == null)
        {
            return new List<string>();
        }

        return objects
            .Where(item => item != null && !string.IsNullOrEmpty(item.ItemID))
            .Select(item => item.ItemID)
            .Distinct()
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> GetPhysicalMountTypes(FVRObject item, string category, string opticKind)
    {
        var componentTypeName = string.Empty;
        if (category == "Firearm")
        {
            componentTypeName = "FistVR.FVRFireArmAttachmentMount";
        }
        else if (category == "Attachment" && !string.IsNullOrEmpty(opticKind))
        {
            componentTypeName = "FistVR.FVRFireArmAttachment";
        }

        if (string.IsNullOrEmpty(componentTypeName))
        {
            return new List<string>();
        }

        try
        {
            var prefab = item.GetGameObject();
            var componentType = typeof(FVRObject).Assembly.GetType(componentTypeName, false);
            if (prefab == null || componentType == null)
            {
                return new List<string>();
            }

            var typeField = componentType.GetField("Type", BindingFlags.Public | BindingFlags.Instance);
            if (typeField == null)
            {
                return new List<string>();
            }

            return prefab
                .GetComponentsInChildren(componentType, true)
                .Select(component => typeField.GetValue(component))
                .Where(value => value != null)
                .Select(ResolveRuntimeMountName)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception)
        {
            // Unsupported or malformed mod prefabs are never used for automatic optic selection.
            return new List<string>();
        }
    }

    private static string ResolveRuntimeMountName(object value)
    {
        var valueType = value.GetType();
        if (!valueType.IsEnum || !Enum.IsDefined(valueType, value))
        {
            return value.ToString();
        }

        return Enum.GetName(valueType, value) ?? value.ToString();
    }

    private static List<RuntimeEnemyEntry> BuildEnemyEntries()
    {
        var entries = new List<RuntimeEnemyEntry>();
        var itemManager = ManagerSingleton<IM>.Instance;
        if (itemManager == null || itemManager.odicSosigObjsByID == null)
        {
            return entries;
        }

        foreach (var pair in itemManager.odicSosigObjsByID)
        {
            var template = pair.Value;
            if (template == null || (int)pair.Key == 0)
            {
                continue;
            }

            var isKnownVanillaId = Enum.IsDefined(typeof(SosigEnemyID), pair.Key);
            var name = isKnownVanillaId
                ? pair.Key.ToString()
                : Convert.ToInt32(pair.Key).ToString();
            var healthScore = EnemyHealthScore(template);
            var armorScore = EnemyArmorScore(template);
            var weaponThreatScore = EnemyWeaponThreatScore(template);
            var specialThreatScore = EnemySpecialThreatScore(template);
            entries.Add(new RuntimeEnemyEntry
            {
                EnemyNameString = name,
                DisplayName = string.IsNullOrEmpty(template.DisplayName) ? name : template.DisplayName,
                IsModContent = !isKnownVanillaId,
                IsSpawnable = true,
                HealthScore = healthScore,
                ArmorScore = armorScore,
                WeaponThreatScore = weaponThreatScore,
                SpecialThreatScore = specialThreatScore,
                DifficultyScore = Math.Max(1, healthScore + armorScore + weaponThreatScore + specialThreatScore),
            });
        }

        return entries
            .OrderBy(entry => entry.DifficultyScore)
            .ThenBy(entry => entry.EnemyNameString, StringComparer.Ordinal)
            .ToList();
    }

    private static int EnemyHealthScore(SosigEnemyTemplate template)
    {
        var configs = EnemyConfigs(template).ToList();
        if (configs.Count == 0)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Round(configs.Average(config => config.TotalMustard) / 25f));
    }

    private static int EnemyArmorScore(SosigEnemyTemplate template)
    {
        var score = 0f;
        foreach (var outfit in template.OutfitConfig ?? new List<SosigOutfitConfig>())
        {
            score += OutfitSlotScore(outfit.Headwear, outfit.Chance_Headwear);
            score += OutfitSlotScore(outfit.Eyewear, outfit.Chance_Eyewear) * 0.5f;
            score += OutfitSlotScore(outfit.Facewear, outfit.Chance_Facewear) * 0.75f;
            score += OutfitSlotScore(outfit.Torsowear, outfit.Chance_Torsowear) * 1.5f;
            score += OutfitSlotScore(outfit.Pantswear, outfit.Chance_Pantswear) * 0.5f;
        }

        return (int)Math.Round(score * 3f);
    }

    private static float OutfitSlotScore(List<FVRObject> items, float chance)
    {
        if (items == null || items.Count == 0)
        {
            return 0f;
        }

        return chance > 0f ? chance : 1f;
    }

    private static int EnemyWeaponThreatScore(SosigEnemyTemplate template)
    {
        var weapons = new List<FVRObject>();
        weapons.AddRange(template.WeaponOptions ?? new List<FVRObject>());
        weapons.AddRange(template.WeaponOptions_Secondary ?? new List<FVRObject>());
        weapons.AddRange(template.WeaponOptions_Tertiary ?? new List<FVRObject>());
        var strongestWeapon = weapons.Where(weapon => weapon != null).Select(WeaponThreatScore).DefaultIfEmpty(0).Max();
        var equipmentScore = Math.Min(10, weapons.Count(weapon => weapon != null));
        return strongestWeapon + equipmentScore + (int)Math.Round((template.SecondaryChance + template.TertiaryChance) * 4f);
    }

    private static int WeaponThreatScore(FVRObject weapon)
    {
        if (weapon.Category.ToString() != "Firearm")
        {
            return 1;
        }

        switch (weapon.TagFirearmRoundPower.ToString())
        {
            case "Tiny": return 2;
            case "Pistol": return 4;
            case "Shotgun": return 6;
            case "Intermediate": return 7;
            case "FullPower": return 9;
            case "AntiMaterial": return 12;
            case "Ordnance": return 14;
            case "Exotic": return 12;
            default: return 5;
        }
    }

    private static int EnemySpecialThreatScore(SosigEnemyTemplate template)
    {
        var score = 0f;
        foreach (var config in EnemyConfigs(template))
        {
            score = Math.Max(score, Math.Max(0f, config.RunSpeed - 3f) * 2f);
            score = Math.Max(score, Math.Max(0f, config.ViewDistance - 150f) / 100f);
            score = Math.Max(score, HasBooleanProperty(config, "HasNightVision") ? 4f : 0f);
            score = Math.Max(score, config.AppliesDamageResistToIntegrityLoss ? 3f : 0f);
            score = Math.Max(score, !config.CanBeGrabbed || !config.CanBeSevered || !config.CanBeStabbed ? 3f : 0f);
        }

        return (int)Math.Ceiling(score);
    }

    private static IEnumerable<SosigConfigTemplate> EnemyConfigs(SosigEnemyTemplate template)
    {
        return (template.ConfigTemplates ?? new List<SosigConfigTemplate>())
            .Concat(template.ConfigTemplates_Easy ?? new List<SosigConfigTemplate>())
            .Where(config => config != null);
    }

    private static bool HasBooleanProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(value, null);
    }

    private static string GetOpticKind(FVRObject item)
    {
        if (item.Category.ToString() != "Attachment")
        {
            return string.Empty;
        }

        var feature = item.TagAttachmentFeature.ToString();
        if (feature != "Reflex" && feature != "Magnification")
        {
            return string.Empty;
        }

        try
        {
            var prefab = item.GetGameObject();
            if (prefab == null)
            {
                return string.Empty;
            }

            if (HasComponent(prefab, "FistVR.PIPScopeController"))
            {
                return "Scope";
            }

            if (HasComponent(prefab, "FistVR.ReflexSightController"))
            {
                return "Reflex";
            }
        }
        catch (Exception)
        {
            // A failed or unsupported mod prefab is not safe to auto-equip.
        }

        return string.Empty;
    }

    private static bool HasComponent(GameObject prefab, string typeName)
    {
        var componentType = typeof(FVRObject).Assembly.GetType(typeName, false);
        return componentType != null && prefab.GetComponentInChildren(componentType, true) != null;
    }

    private static string RuntimePoolFileName(RuntimeWeaponPool pool)
    {
        return "GunGameWeaponPool_Runtime_" + pool.Family + "_" + pool.EnemyType + ".json";
    }

    private static void RemoveStaleRuntimePools(string runtimePoolsPath, HashSet<string> expectedPoolFiles)
    {
        foreach (var existingPath in Directory.GetFiles(runtimePoolsPath, "GunGameWeaponPool_Runtime_*.json"))
        {
            if (!expectedPoolFiles.Contains(Path.GetFileName(existingPath)))
            {
                File.Delete(existingPath);
            }
        }
    }

    private static void WriteTextAtomically(string outputPath, string contents)
    {
        var temporaryPath = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));

        try
        {
            if (File.Exists(outputPath))
            {
                File.Replace(temporaryPath, outputPath, null);
            }
            else
            {
                File.Move(temporaryPath, outputPath);
            }
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(temporaryPath, outputPath, true);
            File.Delete(temporaryPath);
        }
    }

    private static string SerializeMetadata(List<RuntimeMetadataEntry> entries)
    {
        var json = new StringBuilder(entries.Count * 256);
        json.Append("[\n");
        for (var index = 0; index < entries.Count; index++)
        {
            var item = entries[index];
            json.Append("  {\"ObjectID\":\"");
            AppendJsonString(json, item.ObjectID);
            json.Append("\",\"Category\":\"");
            AppendJsonString(json, item.Category);
            json.Append("\",\"IsModContent\":");
            json.Append(item.IsModContent ? "true" : "false");
            json.Append(",\"MagazineType\":");
            json.Append(item.MagazineType);
            json.Append(",\"ClipType\":");
            json.Append(item.ClipType);
            json.Append(",\"RoundType\":");
            json.Append(item.RoundType);
            AppendJsonNamedStringArray(json, "CompatibleMagazines", item.CompatibleMagazines);
            AppendJsonNamedStringArray(json, "CompatibleClips", item.CompatibleClips);
            AppendJsonNamedStringArray(json, "CompatibleSpeedLoaders", item.CompatibleSpeedLoaders);
            AppendJsonNamedStringArray(json, "CompatibleSingleRounds", item.CompatibleSingleRounds);
            AppendJsonNamedStringArray(json, "BespokeAttachments", item.BespokeAttachments);
            AppendJsonNamedString(json, "FirearmSize", item.FirearmSize);
            AppendJsonNamedString(json, "FirearmRoundPower", item.FirearmRoundPower);
            AppendJsonNamedStringArray(json, "FirearmMounts", item.FirearmMounts);
            AppendJsonNamedString(json, "AttachmentMount", item.AttachmentMount);
            AppendJsonNamedString(json, "AttachmentFeature", item.AttachmentFeature);
            AppendJsonNamedString(json, "OpticKind", item.OpticKind);
            AppendJsonNamedStringArray(json, "PhysicalMountTypes", item.PhysicalMountTypes);
            json.Append('}');
            if (index < entries.Count - 1)
            {
                json.Append(',');
            }

            json.Append('\n');
        }

        json.Append(']');
        return json.ToString();
    }

    private static string SerializePool(RuntimeWeaponPool pool)
    {
        var json = new StringBuilder(pool.Guns.Count * 128);
        json.Append("{\n  \"WeaponPoolType\": \"Advanced\",\n  \"Description\": \"");
        AppendJsonString(json, pool.Description);
        json.Append("\",\n  \"EnemyProgressionType\": ");
        json.Append(pool.EnemyProgressionType);
        json.Append(",\n  \"Enemies\": [");
        for (var index = 0; index < pool.Enemies.Count; index++)
        {
            if (index > 0)
            {
                json.Append(',');
            }

            json.Append("{\"EnemyName\":0,\"EnemyNameString\":\"");
            AppendJsonString(json, pool.Enemies[index].EnemyNameString);
            json.Append("\",\"Value\":");
            json.Append(pool.Enemies[index].Value);
            json.Append('}');
        }

        json.Append("],\n  \"Guns\": [");
        for (var index = 0; index < pool.Guns.Count; index++)
        {
            if (index > 0)
            {
                json.Append(',');
            }

            var gun = pool.Guns[index];
            json.Append("{\"GunName\":\"");
            AppendJsonString(json, gun.GunName);
            json.Append("\",\"MagName\":\"");
            AppendJsonString(json, gun.MagName);
            json.Append("\",\"MagNames\":");
            AppendJsonStringArray(json, gun.MagNames);
            json.Append(",\"CategoryID\":");
            json.Append(gun.CategoryID);
            json.Append(",\"Extra\":\"");
            AppendJsonString(json, gun.Extra);
            json.Append("\"}");
        }

        json.Append("],\n  \"Name\": \"");
        AppendJsonString(json, pool.Name);
        json.Append("\",\n  \"OrderType\": ");
        json.Append(pool.OrderType);
        json.Append("\n}");
        return json.ToString();
    }

    private static string SerializeReceipt(
        List<RuntimeMetadataEntry> entries,
        List<RuntimeEnemyEntry> enemies,
        RuntimeGenerationResult result,
        int randomSeed,
        string phase)
    {
        var json = new StringBuilder();
        json.Append("{\n  \"generatedAtUtc\": \"");
        AppendJsonString(json, DateTime.UtcNow.ToString("o"));
        json.Append("\",\n  \"randomSeed\": ");
        json.Append(randomSeed);
        json.Append(",\n  \"phase\": \"");
        AppendJsonString(json, phase);
        json.Append('"');
        json.Append(",\n  \"activeItems\": ");
        json.Append(entries.Count);
        json.Append(",\n  \"activeModdedItems\": ");
        json.Append(entries.Count(entry => entry.IsModContent));
        json.Append(",\n  \"activeSosigTypes\": ");
        json.Append(enemies.Count);
        json.Append(",\n  \"activeModdedSosigTypes\": ");
        json.Append(enemies.Count(enemy => enemy.IsModContent));
        json.Append(",\n  \"eligibleWeaponsPerPool\": ");
        json.Append(result.Pools.Count == 0 ? 0 : result.Pools[0].Guns.Count);
        json.Append(",\n  \"skippedFirearms\": ");
        AppendJsonStringArray(json, result.SkippedFirearms);
        json.Append(",\n  \"firearmsWithoutOptics\": ");
        AppendJsonStringArray(json, result.FirearmsWithoutOptics);
        json.Append("\n}");
        return json.ToString();
    }

    private static string SerializeEnemyCatalog(List<RuntimeEnemyEntry> enemies)
    {
        var json = new StringBuilder(enemies.Count * 192);
        json.Append("[\n");
        for (var index = 0; index < enemies.Count; index++)
        {
            var enemy = enemies[index];
            json.Append("  {\"EnemyNameString\":\"");
            AppendJsonString(json, enemy.EnemyNameString);
            json.Append("\",\"DisplayName\":\"");
            AppendJsonString(json, enemy.DisplayName);
            json.Append("\",\"IsModContent\":");
            json.Append(enemy.IsModContent ? "true" : "false");
            json.Append(",\"IsSpawnable\":");
            json.Append(enemy.IsSpawnable ? "true" : "false");
            json.Append(",\"DifficultyScore\":");
            json.Append(enemy.DifficultyScore);
            json.Append(",\"HealthScore\":");
            json.Append(enemy.HealthScore);
            json.Append(",\"ArmorScore\":");
            json.Append(enemy.ArmorScore);
            json.Append(",\"WeaponThreatScore\":");
            json.Append(enemy.WeaponThreatScore);
            json.Append(",\"SpecialThreatScore\":");
            json.Append(enemy.SpecialThreatScore);
            json.Append('}');
            if (index < enemies.Count - 1)
            {
                json.Append(',');
            }

            json.Append('\n');
        }

        json.Append(']');
        return json.ToString();
    }

    private static void AppendJsonNamedString(StringBuilder json, string name, string value)
    {
        json.Append(",\"");
        AppendJsonString(json, name);
        json.Append("\":\"");
        AppendJsonString(json, value ?? string.Empty);
        json.Append('"');
    }

    private static void AppendJsonNamedStringArray(StringBuilder json, string name, List<string> values)
    {
        json.Append(",\"");
        AppendJsonString(json, name);
        json.Append("\":");
        AppendJsonStringArray(json, values);
    }

    private static void AppendJsonStringArray(StringBuilder json, List<string> values)
    {
        values = values ?? new List<string>();
        json.Append('[');
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                json.Append(',');
            }

            json.Append('"');
            AppendJsonString(json, values[index]);
            json.Append('"');
        }

        json.Append(']');
    }

    private static void AppendJsonString(StringBuilder json, string value)
    {
        foreach (var character in value ?? string.Empty)
        {
            switch (character)
            {
                case '\\': json.Append("\\\\"); break;
                case '"': json.Append("\\\""); break;
                case '\n': json.Append("\\n"); break;
                case '\r': json.Append("\\r"); break;
                case '\t': json.Append("\\t"); break;
                default:
                    if (character < ' ')
                    {
                        json.Append("\\u");
                        json.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        json.Append(character);
                    }

                    break;
            }
        }
    }
}
