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
    private const string HarmonyId = "HLin.GunGameProgressionsMetadataExporter.KodemanRefresh";
    private const float GunGameSelectorPollSeconds = 0.25f;
    private const float ModdedRefreshTimeoutSeconds = 120f;
    private const float ModdedRefreshPollSeconds = 1f;

    private static Plugin instance;
    private readonly GunGameSelectorInstanceTracker selectorTracker = new GunGameSelectorInstanceTracker();
    private List<RuntimeMetadataEntry> vanillaMetadata;
    private bool vanillaGenerationFinished;
    private bool moddedRefreshRunning;
    private bool moddedRefreshRequested;
    private Harmony harmony;
    private Type weaponPoolLoaderType;
    private MethodInfo gameManagerOnDestroy;

    private void Awake()
    {
        instance = this;
        InstallGunGameRefreshHooks();
        Trace("plugin awake.");
        Logger.LogInfo(RuntimeStatusMessages.Ready);
    }

    private void Start()
    {
        Trace("starting vanilla generation and selector watch.");
        StartCoroutine(GenerateVanillaPoolsAtStartup());
        StartCoroutine(WatchForGunGamePoolLoader());
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

    private void InstallGunGameRefreshHooks()
    {
        var gameManagerType = AccessTools.TypeByName("GunGame.Scripts.GameManager");
        gameManagerOnDestroy = gameManagerType == null ? null : AccessTools.Method(gameManagerType, "OnDestroy");
        var exitPostfix = AccessTools.Method(typeof(Plugin), "GameManagerOnDestroyPostfix");
        if (gameManagerOnDestroy == null || exitPostfix == null)
        {
            Logger.LogError(RuntimeStatusMessages.PoolHookUnavailable);
            return;
        }

        harmony = new Harmony(HarmonyId);
        harmony.Patch(gameManagerOnDestroy, postfix: new HarmonyMethod(exitPostfix));
        var exitPatchInfo = Harmony.GetPatchInfo(gameManagerOnDestroy);
        if (exitPatchInfo == null || !exitPatchInfo.Postfixes.Any(patch => patch.owner == HarmonyId))
        {
            Logger.LogError(RuntimeStatusMessages.PoolHookUnavailable);
            return;
        }

        Trace("GunGame session-exit hook active.");
    }

    private static void GameManagerOnDestroyPostfix()
    {
        if (instance != null)
        {
            instance.Trace("GunGame session ended; background refresh requested.");
            instance.RequestModdedRefresh();
        }
    }

    private IEnumerator WatchForGunGamePoolLoader()
    {
        Trace("selector watch active.");
        while (true)
        {
            var loader = FindGunGamePoolLoader();
            if (selectorTracker.Observe(loader))
            {
                Trace("live selector detected.");
                yield return StartCoroutine(PrepareModdedProfilesForSelector(loader));
            }

            yield return new WaitForSeconds(GunGameSelectorPollSeconds);
        }
    }

    private object FindGunGamePoolLoader()
    {
        if (weaponPoolLoaderType == null)
        {
            weaponPoolLoaderType = AccessTools.TypeByName("GunGame.Scripts.Weapons.WeaponPoolLoader");
            if (weaponPoolLoaderType != null)
            {
                Trace("selector type resolved.");
            }
        }

        return GunGameSelectorLocator.Resolve(weaponPoolLoaderType);
    }

    private IEnumerator PrepareModdedProfilesForSelector(object weaponPoolLoader)
    {
        var loadingDisplay = CreateModdedProfileLoadingDisplay(weaponPoolLoader);
        Trace(loadingDisplay == null ? "selector status row unavailable." : "selector status row created.");
        while (!vanillaGenerationFinished)
        {
            UpdateModdedProfileLoadingDisplay(loadingDisplay, "Preparing vanilla profiles");
            yield return null;
        }

        Logger.LogInfo(RuntimeStatusMessages.Preparing);
        Trace("selector readiness wait started.");
        var readinessGate = new ModdedProfileReadinessGate();
        RuntimeMetadataCapture metadataCapture = null;
        do
        {
            Dictionary<string, FVRObject> objects;
            var externalLoadState = GetExternalContentLoadState();
            var now = Time.realtimeSinceStartup;
            if (TryGetObjectData(out objects) && objects.Count > 0)
            {
                readinessGate.Observe(now, objects.Count, externalLoadState);
                UpdateModdedProfileLoadingDisplay(
                    loadingDisplay,
                    ModdedProfileLoadingMessage(readinessGate, now, externalLoadState));
                if (readinessGate.IsReady(now, externalLoadState))
                {
                    Trace("mod content ready; capturing metadata.");
                    yield return StartCoroutine(CaptureRuntimeMetadata(
                        objects,
                        item => item.IsModContent,
                        capture => metadataCapture = capture));
                    break;
                }
            }
            else
            {
                UpdateModdedProfileLoadingDisplay(loadingDisplay, "Waiting for mod content: 5s");
            }

            yield return new WaitForSeconds(ModdedRefreshPollSeconds);
        }
        while (true);

        if (metadataCapture != null)
        {
            RuntimeGenerationReport report = null;
            UpdateModdedProfileLoadingDisplay(loadingDisplay, "Generating Modded profiles");
            yield return StartCoroutine(GenerateModdedPoolCandidate(
                metadataCapture,
                Stopwatch.StartNew(),
                generatedReport => report = generatedReport));
            if (report != null && report.PoolCount > 0)
            {
                var choicesAdded = AddGeneratedPoolChoices(weaponPoolLoader, report.PoolFileNames);
                Trace("selector insertion added " + choicesAdded + " of " + report.PoolCount + " modded profiles.");
                if (choicesAdded < report.PoolCount && !LoaderAlreadyHasGeneratedPools(weaponPoolLoader, report.PoolFileNames))
                {
                    Logger.LogWarning(RuntimeStatusMessages.ProfileUiUpdateFailed);
                }
            }
        }

        DestroyModdedProfileLoadingDisplay(loadingDisplay);
        Trace("selector preparation finished; background refresh requested.");
        RequestModdedRefresh();
    }

    private void Trace(string message)
    {
        Logger.LogInfo("GunGame Progressions trace: " + message);
    }

    private void RequestModdedRefresh()
    {
        moddedRefreshRequested = true;
        if (!moddedRefreshRunning)
        {
            StartCoroutine(RefreshModdedPoolsInBackground());
        }
    }

    private IEnumerator GenerateVanillaPoolsAtStartup()
    {
        Dictionary<string, FVRObject> objects;
        while (!TryGetObjectData(out objects) || objects.Count == 0)
        {
            yield return null;
        }

        RuntimeMetadataCapture metadataCapture = null;
        yield return StartCoroutine(CaptureRuntimeMetadata(
            objects,
            item => !item.IsModContent,
            capture => metadataCapture = capture));
        RuntimeEnemyCapture enemyCapture = null;
        yield return StartCoroutine(CaptureEnemyEntries(capture => enemyCapture = capture));

        if (metadataCapture == null)
        {
            vanillaGenerationFinished = true;
            Logger.LogWarning(RuntimeStatusMessages.FallbackPools);
            yield break;
        }

        vanillaMetadata = metadataCapture.Entries;
        var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var job = new RuntimeGenerationJob(
            packagePath,
            vanillaMetadata,
            enemyCapture == null ? new List<RuntimeEnemyEntry>() : enemyCapture.Entries,
            RuntimeGenerationPhase.Vanilla);
        job.Start();
        while (!job.IsCompleted)
        {
            yield return null;
        }

        vanillaGenerationFinished = true;
        if (job.Error != null)
        {
            Logger.LogWarning(RuntimeStatusMessages.FallbackPools);
            Logger.LogDebug("GunGame vanilla pool generation failed: " + job.Error);
            yield break;
        }

        Logger.LogInfo(RuntimeStatusMessages.VanillaPoolsReady);
        Logger.LogDebug(
            "GunGame vanilla pools: " + job.Report.PoolCount + " pools, " + job.Report.EligibleWeaponsPerPool +
            " items per pool; capture " + metadataCapture.ElapsedMilliseconds + "ms + background build/write " +
            job.Report.ElapsedMilliseconds + "ms.");
    }

    private IEnumerator RefreshModdedPoolsInBackground()
    {
        moddedRefreshRunning = true;
        while (moddedRefreshRequested)
        {
            moddedRefreshRequested = false;
            yield return StartCoroutine(GenerateModdedPoolsForRefresh());
        }

        moddedRefreshRunning = false;
    }

    private IEnumerator GenerateModdedPoolsForRefresh()
    {
        while (!vanillaGenerationFinished)
        {
            yield return null;
        }

        var totalTimer = Stopwatch.StartNew();
        Logger.LogInfo(RuntimeStatusMessages.Preparing);
        var readinessGate = new ModdedProfileReadinessGate();
        var waitStartTime = Time.realtimeSinceStartup;
        do
        {
            var elapsedSeconds = Time.realtimeSinceStartup - waitStartTime;
            var externalLoadState = GetExternalContentLoadState();
            Dictionary<string, FVRObject> objects;
            if (TryGetObjectData(out objects) && objects.Count > 0)
            {
                readinessGate.Observe(Time.realtimeSinceStartup, objects.Count, externalLoadState);
                if (readinessGate.IsReady(Time.realtimeSinceStartup, externalLoadState))
                {
                    RuntimeMetadataCapture metadataCapture = null;
                    yield return StartCoroutine(CaptureRuntimeMetadata(
                        objects,
                        item => item.IsModContent,
                        capture => metadataCapture = capture));
                    if (metadataCapture != null)
                    {
                        yield return StartCoroutine(GenerateModdedPoolCandidate(metadataCapture, totalTimer));
                    }

                    yield break;
                }
            }

            if (elapsedSeconds >= ModdedRefreshTimeoutSeconds)
            {
                Logger.LogDebug("GunGame modded refresh reached its background wait limit.");
                yield break;
            }

            yield return new WaitForSeconds(ModdedRefreshPollSeconds);
        }
        while (true);
    }

    private IEnumerator GenerateModdedPoolCandidate(
        RuntimeMetadataCapture metadataCapture,
        Stopwatch totalTimer,
        Action<RuntimeGenerationReport> complete = null)
    {
        RuntimeEnemyCapture enemyCapture = null;
        yield return StartCoroutine(CaptureEnemyEntries(capture => enemyCapture = capture));

        var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var combinedMetadata = (vanillaMetadata ?? new List<RuntimeMetadataEntry>())
            .Concat(metadataCapture.Entries)
            .GroupBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .ToList();
        var job = new RuntimeGenerationJob(
            packagePath,
            combinedMetadata,
            enemyCapture == null ? new List<RuntimeEnemyEntry>() : enemyCapture.Entries,
            RuntimeGenerationPhase.Modded);
        job.Start();
        while (!job.IsCompleted)
        {
            yield return null;
        }

        if (job.Error != null)
        {
            Logger.LogWarning(RuntimeStatusMessages.FallbackPools);
            Logger.LogDebug("GunGame runtime pool generation failed: " + job.Error);
            if (complete != null)
            {
                complete(null);
            }
            yield break;
        }

        var report = job.Report;
        if (report.PoolCount == 0)
        {
            Logger.LogInfo(RuntimeStatusMessages.NoModdedPools);
        }
        else if (report.WasWritten)
        {
            Logger.LogInfo(RuntimeStatusMessages.PoolsReady);
        }
        else
        {
            Logger.LogDebug("GunGame modded pools kept their larger existing gun count.");
        }
        Logger.LogDebug(
            "GunGame runtime pools: " + report.PoolCount + " pools, " + report.EntryCount +
            " items, " + report.EnemyCount + " Sosig types; capture " + metadataCapture.ElapsedMilliseconds +
            "ms + enemy capture " + (enemyCapture == null ? 0 : enemyCapture.ElapsedMilliseconds) +
            "ms + background build/write " + report.ElapsedMilliseconds + "ms, total " + totalTimer.ElapsedMilliseconds + "ms.");
        if (complete != null)
        {
            complete(report);
        }
    }

    private static string ModdedProfileLoadingMessage(
        ModdedProfileReadinessGate readinessGate,
        float now,
        ExternalContentLoadState externalLoadState)
    {
        if (externalLoadState == ExternalContentLoadState.Loading)
        {
            return "Loading mod content - waiting for completion";
        }

        return "Waiting for mod content: " + readinessGate.SecondsUntilQuiet(now, externalLoadState) + "s";
    }

    private static ModdedProfileLoadingDisplay CreateModdedProfileLoadingDisplay(object weaponPoolLoader)
    {
        try
        {
            var loaderType = weaponPoolLoader.GetType();
            var prefabField = loaderType.GetField("ChoicePrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var parentField = loaderType.GetField("ChoicesListParent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var prefab = prefabField == null ? null : prefabField.GetValue(weaponPoolLoader) as UnityEngine.Object;
            var parent = parentField == null ? null : parentField.GetValue(weaponPoolLoader) as Transform;
            if (prefab == null || parent == null)
            {
                return null;
            }

            var displayObject = UnityEngine.Object.Instantiate(prefab, parent);
            var root = DisplayRoot(displayObject);
            if (root == null)
            {
                UnityEngine.Object.Destroy(displayObject);
                return null;
            }

            root.name = "GunGameProgressionsModdedLoading";
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            var textTargets = new List<KeyValuePair<Component, PropertyInfo>>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                var textProperty = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (textProperty != null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
                {
                    textTargets.Add(new KeyValuePair<Component, PropertyInfo>(component, textProperty));
                }
            }

            var display = new ModdedProfileLoadingDisplay(root, textTargets);
            display.Update("Waiting for mod content: 5s");
            return display;
        }
        catch (Exception exception)
        {
            if (instance != null)
            {
                instance.Logger.LogDebug("GunGame modded profile loading display failed: " + exception);
            }

            return null;
        }
    }

    private static void UpdateModdedProfileLoadingDisplay(ModdedProfileLoadingDisplay display, string message)
    {
        if (display != null)
        {
            display.Update(message);
        }
    }

    private static void DestroyModdedProfileLoadingDisplay(ModdedProfileLoadingDisplay display)
    {
        if (display != null)
        {
            display.Destroy();
        }
    }

    private static GameObject DisplayRoot(UnityEngine.Object displayObject)
    {
        var gameObject = displayObject as GameObject;
        if (gameObject != null)
        {
            return gameObject;
        }

        var component = displayObject as Component;
        return component == null ? null : component.gameObject;
    }

    private static bool LoaderAlreadyHasGeneratedPools(object loader, IEnumerable<string> poolFileNames)
    {
        var poolsField = loader.GetType().GetField("_weaponPools", BindingFlags.Instance | BindingFlags.NonPublic);
        var pools = poolsField == null ? null : poolsField.GetValue(loader) as IList;
        if (pools == null)
        {
            return false;
        }

        var names = new HashSet<string>(
            pools.Cast<object>().Select(GunGamePoolName),
            StringComparer.Ordinal);
        return (poolFileNames ?? Enumerable.Empty<string>())
            .Select(RuntimePoolDisplayName)
            .All(name => !string.IsNullOrEmpty(name) && names.Contains(name));
    }

    private static string RuntimePoolDisplayName(string poolFileName)
    {
        if (poolFileName != null && poolFileName.IndexOf("_02_Modded_Rot_", StringComparison.Ordinal) >= 0)
        {
            return "Runtime 02 - Modded Rot";
        }

        return poolFileName != null && poolFileName.IndexOf("_04_Modded_Mixed_Enemy_", StringComparison.Ordinal) >= 0
            ? "Runtime 04 - Modded Mixed Enemy"
            : string.Empty;
    }

    private int AddGeneratedPoolChoices(object loader, IEnumerable<string> poolFileNames)
    {
        try
        {
            var loaderType = loader.GetType();
            var loadPool = loaderType.GetMethod("LoadWeaponPool", new[] { typeof(string) });
            var poolsField = loaderType.GetField("_weaponPools", BindingFlags.Instance | BindingFlags.NonPublic);
            var choicesField = loaderType.GetField("_choices", BindingFlags.Instance | BindingFlags.NonPublic);
            var prefabField = loaderType.GetField("ChoicePrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var parentField = loaderType.GetField("ChoicesListParent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var pools = poolsField == null ? null : poolsField.GetValue(loader) as IList;
            var choices = choicesField == null ? null : choicesField.GetValue(loader) as IList;
            var prefab = prefabField == null ? null : prefabField.GetValue(loader) as UnityEngine.Object;
            var parent = parentField == null ? null : parentField.GetValue(loader) as Transform;
            if (loadPool == null || pools == null || choices == null || prefab == null || parent == null)
            {
                return 0;
            }

            var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var added = 0;
            foreach (var poolFileName in poolFileNames ?? Enumerable.Empty<string>())
            {
                var pool = loadPool.Invoke(loader, new object[] { Path.Combine(packagePath, poolFileName) });
                if (pool == null)
                {
                    continue;
                }

                var choice = UnityEngine.Object.Instantiate(prefab, parent);
                var initialize = choice.GetType().GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (initialize == null)
                {
                    UnityEngine.Object.Destroy(choice);
                    continue;
                }

                initialize.Invoke(choice, new[] { pool });
                var insertionIndex = RuntimePoolInsertionIndex(pools, GunGamePoolName(pool));
                pools.Insert(insertionIndex, pool);
                choices.Insert(insertionIndex, choice);
                var component = choice as Component;
                if (component != null)
                {
                    component.transform.SetSiblingIndex(insertionIndex);
                }

                added++;
            }

            return added;
        }
        catch (Exception exception)
        {
            Logger.LogDebug("GunGame selector update failed: " + exception);
            return 0;
        }
    }

    private static int RuntimePoolInsertionIndex(IList pools, string generatedPoolName)
    {
        if (generatedPoolName.StartsWith("Runtime 02", StringComparison.Ordinal))
        {
            for (var index = 0; index < pools.Count; index++)
            {
                if (GunGamePoolName(pools[index]).StartsWith("Runtime 03", StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }

        return pools.Count;
    }

    private static string GunGamePoolName(object pool)
    {
        if (pool == null)
        {
            return string.Empty;
        }

        var getName = pool.GetType().GetMethod("GetName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return getName == null ? string.Empty : getName.Invoke(pool, null) as string ?? string.Empty;
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

    private ExternalContentLoadState GetExternalContentLoadState()
    {
        var loaderStatusType = AccessTools.TypeByName("OtherLoader.LoaderStatus");
        var progressMethod = loaderStatusType == null ? null : AccessTools.Method(loaderStatusType, "GetLoaderProgress");
        var startTimeField = loaderStatusType == null ? null : AccessTools.Field(loaderStatusType, "LoadStartTime");
        if (progressMethod == null || startTimeField == null)
        {
            return ExternalContentLoadState.Unavailable;
        }

        try
        {
            var progress = Convert.ToSingle(progressMethod.Invoke(null, null));
            var loadStartTime = Convert.ToSingle(startTimeField.GetValue(null));
            if (progress >= 1f)
            {
                return ExternalContentLoadState.Complete;
            }

            return loadStartTime > 0f ? ExternalContentLoadState.Loading : ExternalContentLoadState.Unavailable;
        }
        catch (Exception exception)
        {
            Logger.LogDebug("Could not read OtherLoader load status: " + exception);
            return ExternalContentLoadState.Unavailable;
        }
    }

    private IEnumerator CaptureRuntimeMetadata(
        Dictionary<string, FVRObject> objects,
        Func<FVRObject, bool> include,
        Action<RuntimeMetadataCapture> complete)
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
            if (item == null || string.IsNullOrEmpty(item.ItemID) || (include != null && !include(item)))
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
                FirearmAction = item.TagFirearmAction.ToString(),
                FirearmFeedOptions = item.TagFirearmFeedOption == null
                    ? new List<string>()
                    : item.TagFirearmFeedOption.Select(option => option.ToString()).ToList(),
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

    private static void RemoveStaleRuntimePools(
        string runtimePoolsPath,
        HashSet<string> expectedPoolFiles,
        Func<string, bool> isOwnedByPhase)
    {
        foreach (var existingPath in Directory.GetFiles(runtimePoolsPath, "GunGameWeaponPool_Runtime_*.json"))
        {
            var existingFileName = Path.GetFileName(existingPath);
            if (isOwnedByPhase(existingFileName) && !expectedPoolFiles.Contains(existingFileName))
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
            AppendJsonNamedString(json, "FirearmAction", item.FirearmAction);
            AppendJsonNamedStringArray(json, "FirearmFeedOptions", item.FirearmFeedOptions);
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
        List<RuntimeEnemyEntry> enemyEntries,
        RuntimeGenerationPhase phase)
    {
        var rules = ProfileRules.Load(packagePath);
        var profileEntries = phase == RuntimeGenerationPhase.Vanilla
            ? entries
            : entries.Where(entry => !entry.IsModContent || !rules.IsBlacklisted(entry)).ToList();
        var randomSeed = Guid.NewGuid().GetHashCode();
        var result = RuntimeProfileBuilder.BuildWithDiagnostics(profileEntries, enemyEntries, new System.Random(randomSeed));
        var phasePools = result.Pools
            .Where(pool => phase == RuntimeGenerationPhase.Vanilla
                ? RuntimeProfileFamily.IsVanilla(pool.Family)
                : RuntimeProfileFamily.IsModded(pool.Family))
            .ToList();
        var phaseResult = new RuntimeGenerationResult
        {
            Pools = phasePools,
            SkippedFirearms = result.SkippedFirearms,
            FirearmsWithoutOptics = result.FirearmsWithoutOptics,
        };
        var expectedPoolFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var runtimePool in phasePools)
        {
            var poolFileName = RuntimePoolFileName(runtimePool);
            expectedPoolFiles.Add(poolFileName);
        }

        var candidateGunCount = phasePools.Count == 0 ? 0 : phasePools.Min(pool => pool.Guns.Count);
        var existingGunCount = phase == RuntimeGenerationPhase.Modded
            ? GetExistingGunCount(packagePath, expectedPoolFiles)
            : -1;
        var shouldWrite = phase != RuntimeGenerationPhase.Modded ||
            (phasePools.Count > 0 && ModdedPoolReplacementPolicy.ShouldReplace(existingGunCount, candidateGunCount));
        if (!shouldWrite)
        {
            return new RuntimeGenerationReport(
                entries.Count,
                entries.Count(entry => entry.IsModContent),
                enemyEntries.Count,
                phasePools.Count,
                phasePools.Count == 0 ? 0 : phasePools[0].Guns.Count,
                result.SkippedFirearms.Count,
                expectedPoolFiles.OrderBy(fileName => fileName, StringComparer.Ordinal).ToList(),
                false,
                existingGunCount,
                candidateGunCount);
        }

        var metadataPath = Path.Combine(packagePath, "ObjectData.json");
        WriteTextAtomically(metadataPath, SerializeMetadata(entries));
        var runtimePoolsPath = Path.Combine(packagePath, "RuntimePools");
        Directory.CreateDirectory(runtimePoolsPath);
        foreach (var runtimePool in phasePools)
        {
            WriteTextAtomically(Path.Combine(packagePath, RuntimePoolFileName(runtimePool)), SerializePool(runtimePool));
        }

        RemoveStaleRuntimePools(
            packagePath,
            expectedPoolFiles,
            phase == RuntimeGenerationPhase.Vanilla
                ? RuntimeProfileFamily.IsVanillaPoolFile
                : RuntimeProfileFamily.IsModdedPoolFile);
        WriteTextAtomically(
            Path.Combine(runtimePoolsPath, "runtime-generation-" + phase.ToString().ToLowerInvariant() + "-receipt.json"),
            SerializeReceipt(entries, enemyEntries, phaseResult, randomSeed, phase.ToString().ToLowerInvariant()));
        WriteTextAtomically(Path.Combine(runtimePoolsPath, "enemy-catalog.json"), SerializeEnemyCatalog(enemyEntries));

        return new RuntimeGenerationReport(
            entries.Count,
            entries.Count(entry => entry.IsModContent),
            enemyEntries.Count,
            phasePools.Count,
            phasePools.Count == 0 ? 0 : phasePools[0].Guns.Count,
            result.SkippedFirearms.Count,
            expectedPoolFiles.OrderBy(fileName => fileName, StringComparer.Ordinal).ToList(),
            true,
            existingGunCount,
            candidateGunCount);
    }

    private static int GetExistingGunCount(string packagePath, IEnumerable<string> expectedPoolFiles)
    {
        var counts = new List<int>();
        foreach (var fileName in expectedPoolFiles)
        {
            var poolPath = Path.Combine(packagePath, fileName);
            if (!File.Exists(poolPath))
            {
                continue;
            }

            counts.Add(CountOccurrences(File.ReadAllText(poolPath), "\"GunName\":"));
        }

        return counts.Count == 0 ? -1 : counts.Max();
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private sealed class ModdedProfileLoadingDisplay
    {
        private readonly GameObject root;
        private readonly List<KeyValuePair<Component, PropertyInfo>> textTargets;

        public ModdedProfileLoadingDisplay(
            GameObject root,
            List<KeyValuePair<Component, PropertyInfo>> textTargets)
        {
            this.root = root;
            this.textTargets = textTargets;
        }

        public void Update(string message)
        {
            foreach (var target in textTargets)
            {
                target.Value.SetValue(target.Key, message, null);
            }
        }

        public void Destroy()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }
    }

    private enum RuntimeGenerationPhase
    {
        Vanilla,
        Modded,
    }

    private sealed class RuntimeGenerationJob
    {
        private readonly object sync = new object();
        private readonly string packagePath;
        private readonly List<RuntimeMetadataEntry> entries;
        private readonly List<RuntimeEnemyEntry> enemyEntries;
        private readonly RuntimeGenerationPhase phase;
        private bool isCompleted;
        private Exception error;
        private RuntimeGenerationReport report;

        public RuntimeGenerationJob(
            string packagePath,
            List<RuntimeMetadataEntry> entries,
            List<RuntimeEnemyEntry> enemyEntries,
            RuntimeGenerationPhase phase)
        {
            this.packagePath = packagePath;
            this.entries = entries;
            this.enemyEntries = enemyEntries;
            this.phase = phase;
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
                generated = GenerateRuntimeFiles(packagePath, entries, enemyEntries, phase);
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
            int skippedFirearmCount,
            List<string> poolFileNames,
            bool wasWritten,
            int existingGunCount,
            int candidateGunCount)
        {
            EntryCount = entryCount;
            ModdedEntryCount = moddedEntryCount;
            EnemyCount = enemyCount;
            PoolCount = poolCount;
            EligibleWeaponsPerPool = eligibleWeaponsPerPool;
            SkippedFirearmCount = skippedFirearmCount;
            PoolFileNames = poolFileNames;
            WasWritten = wasWritten;
            ExistingGunCount = existingGunCount;
            CandidateGunCount = candidateGunCount;
        }

        public int EntryCount { get; private set; }
        public int ModdedEntryCount { get; private set; }
        public int EnemyCount { get; private set; }
        public int PoolCount { get; private set; }
        public int EligibleWeaponsPerPool { get; private set; }
        public int SkippedFirearmCount { get; private set; }
        public List<string> PoolFileNames { get; private set; }
        public bool WasWritten { get; private set; }
        public int ExistingGunCount { get; private set; }
        public int CandidateGunCount { get; private set; }
        public long ElapsedMilliseconds { get; set; }
    }
}
