using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace HLin.GunGameProgressions;

[BepInPlugin("HLin.GunGameProgressionsMetadataExporter", "GunGame Progressions Metadata Exporter", "1.3.6")]
[BepInDependency("Kodeman.GunGame", BepInDependency.DependencyFlags.HardDependency)]
[BepInProcess("h3vr.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    private const long CaptureFrameBudgetMilliseconds = 2;
    private const string HarmonyId = "HLin.GunGameProgressionsMetadataExporter.OnDemandPoolGeneration";
    private static Plugin instance;

    private readonly object sceneLoadGateLock = new object();
    private readonly OnDemandGenerationGate sceneLoadGate = new OnDemandGenerationGate();
    private Harmony harmony;
    private MethodInfo atlasMainMenuScreenOnLoadScene;

    private void Awake()
    {
        instance = this;
        InstallGunGameSceneLoadGate();
    }

    private void OnDestroy()
    {
        if (harmony != null)
        {
            harmony.UnpatchSelf();
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void InstallGunGameSceneLoadGate()
    {
        var atlasPluginType = AccessTools.TypeByName("Atlas.AtlasPlugin");
        atlasMainMenuScreenOnLoadScene = atlasPluginType == null
            ? null
            : AccessTools.Method(atlasPluginType, "MainMenuScreenOnLoadScene");
        var prefix = AccessTools.Method(typeof(Plugin), "AtlasMainMenuScreenOnLoadScenePrefix");
        if (atlasMainMenuScreenOnLoadScene == null || prefix == null)
        {
            Logger.LogError(RuntimeStatusMessages.PoolHookUnavailable);
            return;
        }

        harmony = new Harmony(HarmonyId);
        harmony.Patch(atlasMainMenuScreenOnLoadScene, prefix: new HarmonyMethod(prefix));
        var patchInfo = Harmony.GetPatchInfo(atlasMainMenuScreenOnLoadScene);
        if (patchInfo == null || !patchInfo.Prefixes.Any(patch => patch.owner == HarmonyId))
        {
            Logger.LogError(RuntimeStatusMessages.PoolHookUnavailable);
            return;
        }

        Logger.LogInfo(RuntimeStatusMessages.Ready);
    }

    private static bool AtlasMainMenuScreenOnLoadScenePrefix(object __instance, object __0, object __1)
    {
        return instance == null || instance.HandleAtlasMainMenuScreenOnLoadScene(__instance, __0, __1);
    }

    private bool HandleAtlasMainMenuScreenOnLoadScene(object atlasPlugin, object originalLoad, object menuScreen)
    {
        var sceneInfo = AtlasMenuSceneResolver.GetSceneInfo(menuScreen);
        if (!GunGameSceneIdentity.IsMatch(ReadSceneIdentifier(sceneInfo)))
        {
            return true;
        }

        lock (sceneLoadGateLock)
        {
            if (sceneLoadGate.ConsumeOriginalLoadPermission())
            {
                return true;
            }

            if (!sceneLoadGate.TryBeginPreparation())
            {
                return false;
            }
        }

        StartCoroutine(PreparePoolsThenLoadGunGameScene(atlasPlugin, originalLoad, menuScreen));
        return false;
    }

    private static string ReadSceneIdentifier(object sceneInfo)
    {
        if (sceneInfo == null)
        {
            return string.Empty;
        }

        try
        {
            var property = AccessTools.Property(sceneInfo.GetType(), "Identifier");
            return property == null ? string.Empty : property.GetValue(sceneInfo, null) as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private IEnumerator PreparePoolsThenLoadGunGameScene(object atlasPlugin, object originalLoad, object menuScreen)
    {
        var totalTimer = Stopwatch.StartNew();
        Logger.LogInfo(RuntimeStatusMessages.Preparing);
        Dictionary<string, FVRObject> objects;
        if (!TryGetObjectData(out objects) || objects.Count == 0)
        {
            Logger.LogWarning(RuntimeStatusMessages.FallbackPools);
            ResumeGunGameSceneLoad(atlasPlugin, originalLoad, menuScreen);
            yield break;
        }

        RuntimeMetadataCapture metadataCapture = null;
        yield return StartCoroutine(CaptureRuntimeMetadata(objects, capture => metadataCapture = capture));

        RuntimeEnemyCapture enemyCapture = null;
        yield return StartCoroutine(CaptureEnemyEntries(capture => enemyCapture = capture));

        if (metadataCapture == null)
        {
            Logger.LogWarning(RuntimeStatusMessages.FallbackPools);
            ResumeGunGameSceneLoad(atlasPlugin, originalLoad, menuScreen);
            yield break;
        }

        var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var job = new RuntimeGenerationJob(packagePath, metadataCapture.Entries, enemyCapture == null ? new List<RuntimeEnemyEntry>() : enemyCapture.Entries);
        job.Start();
        while (!job.IsCompleted)
        {
            yield return null;
        }

        if (job.Error != null)
        {
            Logger.LogWarning(RuntimeStatusMessages.FallbackPools);
            Logger.LogDebug("GunGame runtime pool generation failed: " + job.Error);
            ResumeGunGameSceneLoad(atlasPlugin, originalLoad, menuScreen);
            yield break;
        }

        var report = job.Report;
        Logger.LogInfo(RuntimeStatusMessages.PoolsReady);
        Logger.LogDebug(
            "GunGame runtime pools: " + report.PoolCount + " pools, " + report.EntryCount +
            " items, " + report.EnemyCount + " Sosig types; capture " + metadataCapture.ElapsedMilliseconds +
            "ms + enemy capture " + (enemyCapture == null ? 0 : enemyCapture.ElapsedMilliseconds) +
            "ms + background build/write " + report.ElapsedMilliseconds + "ms, total " + totalTimer.ElapsedMilliseconds + "ms.");
        ResumeGunGameSceneLoad(atlasPlugin, originalLoad, menuScreen);
    }

    private void ResumeGunGameSceneLoad(object atlasPlugin, object originalLoad, object menuScreen)
    {
        lock (sceneLoadGateLock)
        {
            sceneLoadGate.ReleaseOriginalLoad();
        }

        try
        {
            atlasMainMenuScreenOnLoadScene.Invoke(atlasPlugin, new[] { originalLoad, menuScreen });
        }
        catch (TargetInvocationException exception)
        {
            Logger.LogError(RuntimeStatusMessages.PoolLoadFailed);
            Logger.LogDebug("GunGame scene load could not resume: " + (exception.InnerException ?? exception));
        }
        catch (Exception exception)
        {
            Logger.LogError(RuntimeStatusMessages.PoolLoadFailed);
            Logger.LogDebug("GunGame scene load could not resume: " + exception);
        }
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
            Logger.LogDebug("H3VR object data is not ready: " + exception);
            return false;
        }
    }

    private IEnumerator CaptureRuntimeMetadata(Dictionary<string, FVRObject> objects, Action<RuntimeMetadataCapture> complete)
    {
        var timer = Stopwatch.StartNew();
        var entries = new List<RuntimeMetadataEntry>();
        List<FVRObject> snapshot;
        try
        {
            snapshot = objects.Values.Where(item => item != null).ToList();
        }
        catch (Exception exception)
        {
            Logger.LogDebug("Could not snapshot H3VR object data for GunGame: " + exception);
            complete(null);
            yield break;
        }

        var frameStart = Stopwatch.GetTimestamp();
        for (var index = 0; index < snapshot.Count; index++)
        {
            var item = snapshot[index];
            if (item == null || string.IsNullOrEmpty(item.ItemID))
            {
                continue;
            }

            var declaredCategory = item.Category.ToString();
            entries.Add(new RuntimeMetadataEntry
            {
                ObjectID = item.ItemID,
                Category = declaredCategory,
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
                OpticKind = GetOpticKind(item),
                PhysicalMountTypes = GetDeclaredMountTypes(item, declaredCategory),
            });

            if (HasExceededCaptureBudget(frameStart))
            {
                yield return null;
                frameStart = Stopwatch.GetTimestamp();
            }
        }

        entries.Sort((left, right) => string.CompareOrdinal(left.ObjectID, right.ObjectID));
        complete(new RuntimeMetadataCapture(entries, timer.ElapsedMilliseconds));
    }

    private static bool HasExceededCaptureBudget(long frameStart)
    {
        return (Stopwatch.GetTimestamp() - frameStart) * 1000L >= Stopwatch.Frequency * CaptureFrameBudgetMilliseconds;
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

    private static List<string> GetDeclaredMountTypes(FVRObject item, string category)
    {
        if (category == "Firearm")
        {
            return item.TagFirearmMounts == null
                ? new List<string>()
                : item.TagFirearmMounts
                    .Select(mount => mount.ToString())
                    .Where(mount => !string.IsNullOrEmpty(mount) && mount != "None")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(mount => mount, StringComparer.Ordinal)
                    .ToList();
        }

        if (category == "Attachment")
        {
            var mount = item.TagAttachmentMount.ToString();
            return string.IsNullOrEmpty(mount) || mount == "None"
                ? new List<string>()
                : new List<string> { mount };
        }

        return new List<string>();
    }

    private IEnumerator CaptureEnemyEntries(Action<RuntimeEnemyCapture> complete)
    {
        var timer = Stopwatch.StartNew();
        var entries = new List<RuntimeEnemyEntry>();
        var itemManager = ManagerSingleton<IM>.Instance;
        if (itemManager == null || itemManager.odicSosigObjsByID == null)
        {
            complete(new RuntimeEnemyCapture(entries, timer.ElapsedMilliseconds));
            yield break;
        }

        var snapshot = itemManager.odicSosigObjsByID.ToList();
        var frameStart = Stopwatch.GetTimestamp();
        foreach (var pair in snapshot)
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

            if (HasExceededCaptureBudget(frameStart))
            {
                yield return null;
                frameStart = Stopwatch.GetTimestamp();
            }
        }

        complete(new RuntimeEnemyCapture(
            entries
                .OrderBy(entry => entry.DifficultyScore)
                .ThenBy(entry => entry.EnemyNameString, StringComparer.Ordinal)
                .ToList(),
            timer.ElapsedMilliseconds));
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

        return PipScopeOpticClassifier.ClassifyFromMetadata(item.ItemID, item.TagAttachmentFeature.ToString());
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

    private static RuntimeGenerationReport GenerateRuntimeFiles(
        string packagePath,
        List<RuntimeMetadataEntry> entries,
        List<RuntimeEnemyEntry> enemyEntries)
    {
        var metadataPath = Path.Combine(packagePath, "ObjectData.json");
        WriteTextAtomically(metadataPath, SerializeMetadata(entries));

        var rules = ProfileRules.Load(packagePath);
        var profileEntries = entries.Where(entry => !rules.IsBlacklisted(entry)).ToList();
        var randomSeed = Guid.NewGuid().GetHashCode();
        var result = RuntimeProfileBuilder.BuildWithDiagnostics(profileEntries, enemyEntries, new System.Random(randomSeed));
        var runtimePoolsPath = Path.Combine(packagePath, "RuntimePools");
        Directory.CreateDirectory(runtimePoolsPath);
        var expectedPoolFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var runtimePool in result.Pools)
        {
            var poolFileName = RuntimePoolFileName(runtimePool);
            expectedPoolFiles.Add(poolFileName);
            WriteTextAtomically(Path.Combine(packagePath, poolFileName), SerializePool(runtimePool));
        }

        RemoveStaleRuntimePools(packagePath, expectedPoolFiles);
        RemoveStaleRuntimePools(runtimePoolsPath, new HashSet<string>(StringComparer.Ordinal));
        WriteTextAtomically(
            Path.Combine(runtimePoolsPath, "runtime-generation-receipt.json"),
            SerializeReceipt(entries, enemyEntries, result, randomSeed, "on-demand-gungame-load"));
        WriteTextAtomically(Path.Combine(runtimePoolsPath, "enemy-catalog.json"), SerializeEnemyCatalog(enemyEntries));

        return new RuntimeGenerationReport(
            entries.Count,
            entries.Count(entry => entry.IsModContent),
            enemyEntries.Count,
            result.Pools.Count,
            result.Pools.Count == 0 ? 0 : result.Pools[0].Guns.Count,
            result.SkippedFirearms.Count);
    }

    private sealed class RuntimeGenerationJob
    {
        private readonly object sync = new object();
        private readonly string packagePath;
        private readonly List<RuntimeMetadataEntry> entries;
        private readonly List<RuntimeEnemyEntry> enemyEntries;
        private bool isCompleted;
        private Exception error;
        private RuntimeGenerationReport report;

        public RuntimeGenerationJob(
            string packagePath,
            List<RuntimeMetadataEntry> entries,
            List<RuntimeEnemyEntry> enemyEntries)
        {
            this.packagePath = packagePath;
            this.entries = entries;
            this.enemyEntries = enemyEntries;
        }

        public bool IsCompleted
        {
            get
            {
                lock (sync)
                {
                    return isCompleted;
                }
            }
        }

        public Exception Error
        {
            get
            {
                lock (sync)
                {
                    return error;
                }
            }
        }

        public RuntimeGenerationReport Report
        {
            get
            {
                lock (sync)
                {
                    return report;
                }
            }
        }

        public void Start()
        {
            var worker = new Thread(Generate)
            {
                IsBackground = true,
            };
            worker.Priority = System.Threading.ThreadPriority.BelowNormal;
            worker.Start();
        }

        private void Generate()
        {
            var timer = Stopwatch.StartNew();
            RuntimeGenerationReport generated = null;
            Exception failure = null;
            try
            {
                generated = GenerateRuntimeFiles(packagePath, entries, enemyEntries);
                generated.ElapsedMilliseconds = timer.ElapsedMilliseconds;
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            lock (sync)
            {
                report = generated;
                error = failure;
                isCompleted = true;
            }
        }
    }

    private sealed class RuntimeMetadataCapture
    {
        public RuntimeMetadataCapture(List<RuntimeMetadataEntry> entries, long elapsedMilliseconds)
        {
            Entries = entries;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public List<RuntimeMetadataEntry> Entries { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
    }

    private sealed class RuntimeEnemyCapture
    {
        public RuntimeEnemyCapture(List<RuntimeEnemyEntry> entries, long elapsedMilliseconds)
        {
            Entries = entries;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public List<RuntimeEnemyEntry> Entries { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
    }

    private sealed class RuntimeGenerationReport
    {
        public RuntimeGenerationReport(
            int entryCount,
            int moddedEntryCount,
            int enemyCount,
            int poolCount,
            int eligibleWeaponsPerPool,
            int skippedFirearmCount)
        {
            EntryCount = entryCount;
            ModdedEntryCount = moddedEntryCount;
            EnemyCount = enemyCount;
            PoolCount = poolCount;
            EligibleWeaponsPerPool = eligibleWeaponsPerPool;
            SkippedFirearmCount = skippedFirearmCount;
        }

        public int EntryCount { get; private set; }
        public int ModdedEntryCount { get; private set; }
        public int EnemyCount { get; private set; }
        public int PoolCount { get; private set; }
        public int EligibleWeaponsPerPool { get; private set; }
        public int SkippedFirearmCount { get; private set; }
        public long ElapsedMilliseconds { get; set; }
    }
}
