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

[BepInPlugin("HLin.GunGameProgressionsMetadataExporter", "GunGame Progressions Metadata Exporter", "1.4.1")]
[BepInDependency("Kodeman.GunGame", BepInDependency.DependencyFlags.HardDependency)]
[BepInProcess("h3vr.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    private const long CaptureFrameBudgetMilliseconds = 2;
    private const float SelectorSubscriptionRetrySeconds = 10f;
    private const string HarmonyId = "HLin.GunGameProgressionsMetadataExporter.KodemanRefresh";
    private static Plugin instance;
    private readonly GunGameSelectorInstanceTracker selectorTracker = new GunGameSelectorInstanceTracker();
    private readonly OtherLoaderStatusProbe otherLoaderStatusProbe = new OtherLoaderStatusProbe();
    private List<RuntimeMetadataEntry> vanillaMetadata;
    private bool vanillaGenerationFinished;
    private bool moddedRefreshRunning;
    private bool moddedRefreshRequested;
    private bool policyReplacementEligible;
    private bool startupWarmupScheduled;
    private Harmony harmony;
    private GunGameSpawnSafety spawnSafety;
    private Type weaponPoolLoaderType;
    private MethodInfo gameManagerOnDestroy;
    private EventInfo weaponPoolLoadedEvent;
    private Delegate weaponPoolLoadedHandler;
    private bool selectorSubscriptionWaitingLogged;
    private bool objectDataUnavailableLogged;

    private void Awake()
    {
        instance = this;
        InstallGunGameRefreshHooks();
        InstallGunGameSpawnSafety();
        ScheduleStartupProfileWarmup();
        StartCoroutine(WaitForWeaponPoolLoaderReadyEvent());
        Trace("plugin awake.");
        if (RuntimeBuildFeatures.CompatibilityProbeEnabled)
        {
            Trace("local Debug build: Runtime 05 compatibility probe enabled.");
        }
        Logger.LogInfo(RuntimeStatusMessages.Ready);
    }

    private void Start()
    {
        ScheduleStartupProfileWarmup();
    }

    private void ScheduleStartupProfileWarmup()
    {
        if (startupWarmupScheduled)
        {
            return;
        }

        startupWarmupScheduled = true;
        Trace("starting vanilla and modded profile warmup.");
        StartCoroutine(GenerateVanillaPoolsAtStartup());
        RequestModdedRefresh();
        StartCoroutine(RequestStartupModdedRescan(60f, "startup 1-minute rescan requested.", false));
        StartCoroutine(RequestStartupModdedRescan(300f, "startup 5-minute rescan requested.", false));
        StartCoroutine(RequestStartupModdedRescan(600f, "startup 10-minute rescan requested; policy replacement eligible.", true));
    }

    private void OnDestroy()
    {
        if (harmony != null)
        {
            harmony.UnpatchSelf();
        }

        GunGameSpawnSafety.Clear();

        if (weaponPoolLoadedEvent != null && weaponPoolLoadedHandler != null)
        {
            weaponPoolLoadedEvent.RemoveEventHandler(null, weaponPoolLoadedHandler);
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

    private void InstallGunGameSpawnSafety()
    {
        if (harmony == null)
        {
            harmony = new Harmony(HarmonyId);
        }

        spawnSafety = new GunGameSpawnSafety(this, Trace);
        if (spawnSafety.Install(harmony))
        {
            Trace("GunGame invalid-loadout safety active.");
            return;
        }

        Logger.LogError(RuntimeStatusMessages.SpawnSafetyUnavailable);
    }

    private static void GameManagerOnDestroyPostfix()
    {
        if (instance != null)
        {
            instance.Trace("GunGame session ended; background refresh requested.");
            instance.RequestModdedRefresh();
        }
    }

    private IEnumerator WaitForWeaponPoolLoaderReadyEvent()
    {
        while (weaponPoolLoadedHandler == null)
        {
            if (TrySubscribeToWeaponPoolLoaderReadyEvent())
            {
                yield break;
            }

            if (!selectorSubscriptionWaitingLogged)
            {
                selectorSubscriptionWaitingLogged = true;
                Trace("waiting for GunGame selector event.");
            }

            // GunGame is a hard dependency, so this normally succeeds during
            // Awake. If an unusual loader order delays its type, retry slowly.
            // This never captures metadata or touches a prefab.
            yield return new WaitForSecondsRealtime(SelectorSubscriptionRetrySeconds);
        }
    }

    private bool TrySubscribeToWeaponPoolLoaderReadyEvent()
    {
        weaponPoolLoaderType = AccessTools.TypeByName("GunGame.Scripts.Weapons.WeaponPoolLoader");
        var callback = AccessTools.Method(typeof(Plugin), "WeaponPoolLoaderReady");
        weaponPoolLoadedEvent = weaponPoolLoaderType == null
            ? null
            : weaponPoolLoaderType.GetEvent(
                "WeaponLoadedEvent",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (weaponPoolLoadedEvent == null || callback == null || weaponPoolLoadedEvent.EventHandlerType == null)
        {
            return false;
        }

        try
        {
            weaponPoolLoadedHandler = Delegate.CreateDelegate(weaponPoolLoadedEvent.EventHandlerType, callback);
            weaponPoolLoadedEvent.AddEventHandler(null, weaponPoolLoadedHandler);
            Trace("GunGame selector ready event subscribed.");
            return true;
        }
        catch (Exception exception)
        {
            Logger.LogDebug("Could not subscribe to GunGame selector ready event: " + exception);
            return false;
        }
    }

    private static void WeaponPoolLoaderReady()
    {
        if (instance == null)
        {
            return;
        }

        var loader = instance.FindGunGamePoolLoader();
        if (instance.selectorTracker.Observe(loader))
        {
            instance.Trace("live selector ready event received.");
            instance.RestorePersistedRuntimeProfilesForSelector(loader);
        }

        // A loader can fire more than once for the same selector when its map
        // reloads. Restore only once, but always request a fresh catalog
        // snapshot so late-loaded content can promote a larger saved pair.
        instance.RequestModdedRefresh();
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

    private void RestorePersistedRuntimeProfilesForSelector(object weaponPoolLoader)
    {
        var persistedChoicesAdded = AddPersistedRuntimePoolChoices(weaponPoolLoader);
        if (persistedChoicesAdded > 0)
        {
            Trace("selector restored " + persistedChoicesAdded + " persisted runtime profiles.");
        }
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

    private IEnumerator RequestStartupModdedRescan(
        float delaySeconds,
        string traceMessage,
        bool enablePolicyReplacement)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        if (enablePolicyReplacement)
        {
            policyReplacementEligible = true;
        }

        Trace(traceMessage);
        RequestModdedRefresh();
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
            RuntimeGenerationPhase.Vanilla,
            false,
            false);
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
            var allowPolicyReplacement = policyReplacementEligible;
            yield return StartCoroutine(GenerateModdedPoolsForRefresh(allowPolicyReplacement));
        }

        moddedRefreshRunning = false;
    }

    private IEnumerator GenerateModdedPoolsForRefresh(bool allowPolicyReplacement)
    {
        while (!vanillaGenerationFinished)
        {
            yield return null;
        }

        var totalTimer = Stopwatch.StartNew();
        Logger.LogInfo(RuntimeStatusMessages.Preparing);
        Dictionary<string, FVRObject> objects;
        if (!TryGetObjectData(out objects) || objects.Count == 0)
        {
            Trace("modded capture skipped; object registry unavailable.");
            yield break;
        }

        // Loader state is only evidence for safe empty-pool cleanup. It never
        // vetoes a usable current snapshot: available mod weapons can be
        // generated now, then a later trigger can promote a larger pair.
        var externalLoadState = otherLoaderStatusProbe.Read(Logger.LogDebug);
        RuntimeMetadataCapture metadataCapture = null;
        Trace("modded capture started.");
        yield return StartCoroutine(CaptureRuntimeMetadata(
            objects,
            item => item.IsModContent,
            capture => metadataCapture = capture));
        if (metadataCapture == null)
        {
            Trace("modded capture failed.");
            yield break;
        }

        Trace("modded capture complete: " + metadataCapture.Entries.Count + " entries.");
        yield return StartCoroutine(GenerateModdedPoolCandidate(
            metadataCapture,
            totalTimer,
            externalLoadState == ExternalContentLoadState.Complete,
            allowPolicyReplacement));
    }

    private IEnumerator GenerateModdedPoolCandidate(
        RuntimeMetadataCapture metadataCapture,
        Stopwatch totalTimer,
        bool confirmedEmptySnapshot,
        bool allowPolicyReplacement,
        Action<RuntimeGenerationReport> complete = null)
    {
        RuntimeEnemyCapture enemyCapture = null;
        yield return StartCoroutine(CaptureEnemyEntries(capture => enemyCapture = capture));

        var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var combinedMetadata = MergeRuntimeMetadata(
            vanillaMetadata ?? new List<RuntimeMetadataEntry>(),
            metadataCapture.Entries);
        var job = new RuntimeGenerationJob(
            packagePath,
            combinedMetadata,
            enemyCapture == null ? new List<RuntimeEnemyEntry>() : enemyCapture.Entries,
            RuntimeGenerationPhase.Modded,
            confirmedEmptySnapshot,
            allowPolicyReplacement);
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
#if GUNGAME_COMPATIBILITY_PROBE
        yield return StartCoroutine(GenerateCompatibilityProbe(
            packagePath,
            combinedMetadata,
            enemyCapture == null ? new List<RuntimeEnemyEntry>() : enemyCapture.Entries));
#endif
        if (report.PoolCount == 0 && report.WasWritten)
        {
            Logger.LogInfo(RuntimeStatusMessages.NoModdedPools);
        }
        else if (report.WasWritten)
        {
            Logger.LogInfo(RuntimeStatusMessages.PoolsReady);
        }
        else if (report.PoolCount == 0)
        {
            Logger.LogDebug("GunGame Modded snapshot was empty but not confirmed complete; saved profiles were retained.");
        }
        else
        {
            Logger.LogDebug("GunGame Modded candidate did not exceed the saved profile count.");
        }
        Logger.LogDebug(
            "GunGame runtime pools: " + report.PoolCount + " pools, " + report.EntryCount +
            " items, " + report.EnemyCount + " Sosig types; capture " + metadataCapture.ElapsedMilliseconds +
            "ms + enemy capture " + (enemyCapture == null ? 0 : enemyCapture.ElapsedMilliseconds) +
            "ms + background build/write " + report.ElapsedMilliseconds + "ms, total " + totalTimer.ElapsedMilliseconds + "ms.");
        Logger.LogInfo(RuntimeStatusMessages.ModdedScanCompleted(
            totalTimer.ElapsedMilliseconds,
            metadataCapture.Entries.Count));
        if (complete != null)
        {
            complete(report);
        }
    }

#if GUNGAME_COMPATIBILITY_PROBE
    private IEnumerator GenerateCompatibilityProbe(
        string packagePath,
        List<RuntimeMetadataEntry> metadata,
        List<RuntimeEnemyEntry> enemyEntries)
    {
        var probeJob = new RuntimeGenerationJob(
            packagePath,
            metadata,
            enemyEntries,
            RuntimeGenerationPhase.CompatibilityProbe,
            false,
            false);
        probeJob.Start();
        while (!probeJob.IsCompleted)
        {
            yield return null;
        }

        if (probeJob.Error != null)
        {
            Logger.LogDebug("GunGame compatibility probe generation failed: " + probeJob.Error);
        }
        else if (probeJob.Report.WasWritten)
        {
            Trace("compatibility probe updated: " + probeJob.Report.EligibleWeaponsPerPool + " test firearms.");
        }
    }
#endif

    private static string RuntimePoolDisplayName(string poolFileName)
    {
        if (poolFileName != null && poolFileName.IndexOf("_02_Modded_Rot_", StringComparison.Ordinal) >= 0)
        {
            return "Runtime 02 - Modded Rot";
        }

        if (poolFileName != null && poolFileName.IndexOf("_04_Modded_Mixed_Enemy_", StringComparison.Ordinal) >= 0)
        {
            return "Runtime 04 - Modded Mixed Enemy";
        }

#if GUNGAME_COMPATIBILITY_PROBE
        return poolFileName != null && poolFileName.IndexOf("_05_Compatibility_Probe_", StringComparison.Ordinal) >= 0
            ? "Runtime 05 - Compatibility Probe"
            : string.Empty;
#else
        return string.Empty;
#endif
    }

    private int AddPersistedRuntimePoolChoices(object loader)
    {
        var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
        {
            return 0;
        }

        var poolFileNames = Directory.GetFiles(packagePath, "GunGameWeaponPool_Runtime_*.json")
            .Select(Path.GetFileName)
            .Where(fileName => RuntimeProfileFamily.IsModdedPoolFile(fileName)
#if GUNGAME_COMPATIBILITY_PROBE
                || RuntimeProfileFamily.IsCompatibilityProbePoolFile(fileName)
#endif
            )
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToList();
        return AddPoolChoices(loader, poolFileNames);
    }

    private int AddPoolChoices(object loader, IEnumerable<string> poolFileNames)
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
            var existingNames = new HashSet<string>(
                pools.Cast<object>().Select(GunGamePoolName),
                StringComparer.Ordinal);
            var added = 0;
            foreach (var poolFileName in poolFileNames ?? Enumerable.Empty<string>())
            {
                var poolName = RuntimePoolDisplayName(poolFileName);
                if (!string.IsNullOrEmpty(poolName) && existingNames.Contains(poolName))
                {
                    continue;
                }

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

                existingNames.Add(GunGamePoolName(pool));
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
            if (!objectDataUnavailableLogged)
            {
                objectDataUnavailableLogged = true;
                Logger.LogDebug("H3VR object data is not ready: " + exception);
            }
            return false;
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
            var roundType = (int)item.RoundType;
            var capturedEntry = new RuntimeMetadataEntry
            {
                ObjectID = item.ItemID,
                Category = declaredCategory,
                IsModContent = item.IsModContent,
                MagazineType = (int)item.MagazineType,
                ClipType = (int)item.ClipType,
                RoundType = roundType,
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
                OpticKind = CatalogOpticKind(item, declaredCategory),
                PhysicalMountTypes = CatalogPhysicalMountTypes(item, declaredCategory),
                ProvidedMountTypes = new List<string>(),
                IsGunGameRoundDisplaySupported = declaredCategory != "Firearm" || HasGunGameRoundDisplayData(roundType),
                // Legacy property name retained for serialized runtime metadata.
                // It now means the lightweight catalog proves this is a usable
                // firearm; capture must never resolve the prefab to find out.
                IsVerifiedFirearmPrefab = declaredCategory != "Firearm" || HasCatalogFirearmProof(item),
            };
            entries.Add(capturedEntry);

            if (HasExceededCaptureBudget(frameStart))
            {
                yield return null;
                frameStart = Stopwatch.GetTimestamp();
            }
        }

        entries.Sort((left, right) => string.CompareOrdinal(left.ObjectID, right.ObjectID));
        complete(new RuntimeMetadataCapture(entries, timer.ElapsedMilliseconds));
    }

    private static bool HasGunGameRoundDisplayData(int roundType)
    {
        try
        {
            return AM.SRoundDisplayDataDic != null &&
                AM.SRoundDisplayDataDic.ContainsKey((FireArmRoundType)roundType);
        }
        catch
        {
            // Missing display data is unsafe for GunGame's promotion path.
            return false;
        }
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

    private static bool HasCatalogFirearmIdentity(FVRObject item)
    {
        return item != null &&
            item.TagFirearmSize.ToString() != "None" &&
            item.TagFirearmRoundPower.ToString() != "None";
    }

    private static bool HasCatalogFirearmProof(FVRObject item)
    {
        if (HasCatalogFirearmIdentity(item))
        {
            return true;
        }

        if (item == null)
        {
            return false;
        }

        // These lists and exact interfaces are FVRObject metadata, not a
        // prefab scan. They prove a real feed path even when a mod omits the
        // optional firearm size/round-power tags (for example G28 variants).
        if (HasDeclaredCompatibleFeed(item) ||
            (int)item.MagazineType != 0 ||
            (int)item.ClipType != 0)
        {
            return true;
        }

        // H3VR's self-contained gravity gun is intentionally feedless. Do
        // not extend this exception to arbitrary mod entries labelled as a
        // firearm: malformed rails and bows must stay out of a progression.
        return string.Equals(item.ItemID, "GravitonBeamer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDeclaredCompatibleFeed(FVRObject item)
    {
        return HasObjectId(item.CompatibleMagazines) ||
            HasObjectId(item.CompatibleClips) ||
            HasObjectId(item.CompatibleSpeedLoaders) ||
            HasObjectId(item.CompatibleSingleRounds);
    }

    private static bool HasObjectId(IEnumerable<FVRObject> objects)
    {
        return objects != null && objects.Any(candidate =>
            candidate != null && !string.IsNullOrEmpty(candidate.ItemID));
    }

    private static string CatalogOpticKind(FVRObject item, string category)
    {
        return category == "Attachment"
            ? PipScopeOpticClassifier.ClassifyFromMetadata(item.ItemID, item.TagAttachmentFeature.ToString())
            : string.Empty;
    }

    private static List<string> CatalogPhysicalMountTypes(FVRObject item, string category)
    {
        IEnumerable<string> mounts;
        if (category == "Firearm")
        {
            mounts = item.TagFirearmMounts == null
                ? Enumerable.Empty<string>()
                : item.TagFirearmMounts.Select(mount => mount.ToString());
        }
        else if (category == "Attachment")
        {
            mounts = new[] { item.TagAttachmentMount.ToString() };
        }
        else
        {
            mounts = Enumerable.Empty<string>();
        }

        return mounts
            .Where(mount => !string.IsNullOrEmpty(mount) && mount != "None")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(mount => mount, StringComparer.Ordinal)
            .ToList();
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
            json.Append(",\"IsGunGameRoundDisplaySupported\":");
            json.Append(item.IsGunGameRoundDisplaySupported ? "true" : "false");
            json.Append(",\"IsVerifiedFirearmPrefab\":");
            json.Append(item.IsVerifiedFirearmPrefab ? "true" : "false");
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
            AppendJsonNamedStringArray(json, "ProvidedMountTypes", item.ProvidedMountTypes);
            json.Append(",\"OpticMinMagnification\":");
            json.Append(item.OpticMinMagnification.ToString(System.Globalization.CultureInfo.InvariantCulture));
            json.Append(",\"OpticMaxMagnification\":");
            json.Append(item.OpticMaxMagnification.ToString(System.Globalization.CultureInfo.InvariantCulture));
            json.Append(",\"IsVariableMagnification\":");
            json.Append(item.IsVariableMagnification ? "true" : "false");
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
        string phase,
        string contentFingerprint)
    {
        var json = new StringBuilder();
        json.Append("{\n  \"generatedAtUtc\": \"");
        AppendJsonString(json, DateTime.UtcNow.ToString("o"));
        json.Append("\",\n  \"randomSeed\": ");
        json.Append(randomSeed);
        json.Append(",\n  \"phase\": \"");
        AppendJsonString(json, phase);
        json.Append('"');
        json.Append(",\n  \"contentFingerprint\": \"");
        AppendJsonString(json, contentFingerprint);
        json.Append('"');
        json.Append(",\n  \"generationPolicyVersion\": \"");
        AppendJsonString(json, RuntimePoolPersistence.CurrentGenerationPolicyVersion);
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
        RuntimeGenerationPhase phase,
        bool confirmedEmptySnapshot,
        bool allowPolicyReplacement)
    {
        var rules = ProfileRules.Load(packagePath);
        var profileEntries = phase == RuntimeGenerationPhase.Vanilla
            ? entries.Where(entry => !rules.IsGloballyBlacklisted(entry)).ToList()
            : entries.Where(entry => !rules.IsBlacklisted(entry)).ToList();
        var phaseName = phase.ToString().ToLowerInvariant();
        var contentFingerprint = phase == RuntimeGenerationPhase.CompatibilityProbe
            ? RuntimePoolPersistence.CreateFingerprint(
                profileEntries,
                enemyEntries,
                rules.CompatibilityProbeFirearms)
            : RuntimePoolPersistence.CreateFingerprint(profileEntries, enemyEntries);
        var randomSeed = RuntimePoolPersistence.CreateStableSeed(contentFingerprint);
        var result = phase == RuntimeGenerationPhase.CompatibilityProbe
            ? RuntimeProfileBuilder.BuildCompatibilityProbe(
                profileEntries,
                enemyEntries,
                rules.CompatibilityProbeFirearms,
                new System.Random(randomSeed))
            : RuntimeProfileBuilder.BuildWithDiagnostics(
                profileEntries,
                enemyEntries,
                new System.Random(randomSeed));
        var phasePools = result.Pools
            .Where(pool => phase == RuntimeGenerationPhase.Vanilla
                ? RuntimeProfileFamily.IsVanilla(pool.Family)
                : phase == RuntimeGenerationPhase.Modded
                    ? RuntimeProfileFamily.IsModded(pool.Family)
                    : RuntimeProfileFamily.IsCompatibilityProbe(pool.Family))
            .ToList();
        var phaseResult = new RuntimeGenerationResult
        {
            Pools = phasePools,
            SkippedFirearms = result.SkippedFirearms,
            FirearmsWithoutOptics = result.FirearmsWithoutOptics,
        };
        var eligibleWeaponsPerPool = phasePools.Count == 0 ? 0 : phasePools[0].Guns.Count;
        var runtimePoolsPath = Path.Combine(packagePath, "RuntimePools");
        var receiptPath = Path.Combine(runtimePoolsPath, "runtime-generation-" + phaseName + "-receipt.json");
        if (phase == RuntimeGenerationPhase.Modded &&
            !RuntimePoolPersistence.ShouldPromoteModdedCandidate(
                phasePools.Count,
                eligibleWeaponsPerPool,
                RuntimePoolPersistence.ReadEligibleWeaponsPerPool(receiptPath),
                RuntimePoolPersistence.HasCompleteModdedPoolFiles(packagePath),
                confirmedEmptySnapshot,
                !string.Equals(
                    RuntimePoolPersistence.ReadGenerationPolicyVersion(receiptPath),
                    RuntimePoolPersistence.CurrentGenerationPolicyVersion,
                    StringComparison.Ordinal),
                allowPolicyReplacement))
        {
            // Preserve a complete saved pair until a larger current snapshot,
            // or the scheduled ten-minute policy-upgrade window. An empty
            // snapshot can clear it only after loader completion is explicit.
            return new RuntimeGenerationReport(
                profileEntries.Count,
                profileEntries.Count(entry => entry.IsModContent),
                enemyEntries.Count,
                phasePools.Count,
                eligibleWeaponsPerPool,
                result.SkippedFirearms.Count,
                new List<string>(),
                false);
        }

        var expectedPoolFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var runtimePool in phasePools)
        {
            var poolFileName = RuntimePoolFileName(runtimePool);
            expectedPoolFiles.Add(poolFileName);
        }

        Func<string, bool> isOwnedByPhase = phase == RuntimeGenerationPhase.Vanilla
            ? RuntimeProfileFamily.IsVanillaPoolFile
            : phase == RuntimeGenerationPhase.Modded
                ? RuntimeProfileFamily.IsModdedPoolFile
                : RuntimeProfileFamily.IsCompatibilityProbePoolFile;
        var storedFingerprint = RuntimePoolPersistence.ReadFingerprint(receiptPath);
        var poolFilesMatch = RuntimePoolPersistence.HasExpectedPoolFiles(packagePath, expectedPoolFiles, isOwnedByPhase);
        var shouldWrite = RuntimePoolPersistence.ShouldWrite(storedFingerprint, contentFingerprint, poolFilesMatch);
        if (!shouldWrite)
        {
            return new RuntimeGenerationReport(
                profileEntries.Count,
                profileEntries.Count(entry => entry.IsModContent),
                enemyEntries.Count,
                phasePools.Count,
                eligibleWeaponsPerPool,
                result.SkippedFirearms.Count,
                expectedPoolFiles.OrderBy(fileName => fileName, StringComparer.Ordinal).ToList(),
                false);
        }

        if (phase != RuntimeGenerationPhase.CompatibilityProbe)
        {
            var metadataPath = Path.Combine(packagePath, "ObjectData.json");
            WriteTextAtomically(metadataPath, SerializeMetadata(profileEntries));
        }
        Directory.CreateDirectory(runtimePoolsPath);
        foreach (var runtimePool in phasePools)
        {
            WriteTextAtomically(Path.Combine(packagePath, RuntimePoolFileName(runtimePool)), SerializePool(runtimePool));
        }

        RemoveStaleRuntimePools(
            packagePath,
            expectedPoolFiles,
            isOwnedByPhase);
        WriteTextAtomically(
            receiptPath,
            SerializeReceipt(profileEntries, enemyEntries, phaseResult, randomSeed, phaseName, contentFingerprint));
        WriteTextAtomically(Path.Combine(runtimePoolsPath, "enemy-catalog.json"), SerializeEnemyCatalog(enemyEntries));

        return new RuntimeGenerationReport(
            profileEntries.Count,
            profileEntries.Count(entry => entry.IsModContent),
            enemyEntries.Count,
            phasePools.Count,
            eligibleWeaponsPerPool,
            result.SkippedFirearms.Count,
            expectedPoolFiles.OrderBy(fileName => fileName, StringComparer.Ordinal).ToList(),
            true);
    }

    private enum RuntimeGenerationPhase
    {
        Vanilla,
        Modded,
        CompatibilityProbe,
    }

    private sealed class RuntimeGenerationJob
    {
        private readonly object sync = new object();
        private readonly string packagePath;
        private readonly List<RuntimeMetadataEntry> entries;
        private readonly List<RuntimeEnemyEntry> enemyEntries;
        private readonly RuntimeGenerationPhase phase;
        private readonly bool confirmedEmptySnapshot;
        private readonly bool allowPolicyReplacement;
        private bool isCompleted;
        private Exception error;
        private RuntimeGenerationReport report;

        public RuntimeGenerationJob(
            string packagePath,
            List<RuntimeMetadataEntry> entries,
            List<RuntimeEnemyEntry> enemyEntries,
            RuntimeGenerationPhase phase,
            bool confirmedEmptySnapshot,
            bool allowPolicyReplacement)
        {
            this.packagePath = packagePath;
            this.entries = entries;
            this.enemyEntries = enemyEntries;
            this.phase = phase;
            this.confirmedEmptySnapshot = confirmedEmptySnapshot;
            this.allowPolicyReplacement = allowPolicyReplacement;
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
                generated = GenerateRuntimeFiles(
                    packagePath,
                    entries,
                    enemyEntries,
                    phase,
                    confirmedEmptySnapshot,
                    allowPolicyReplacement);
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

    private static List<RuntimeMetadataEntry> MergeRuntimeMetadata(
        IEnumerable<RuntimeMetadataEntry> first,
        IEnumerable<RuntimeMetadataEntry> second)
    {
        // This is a small plain-metadata merge performed once per completed
        // Modded capture. It restores the 1.4.0 input route for the shared
        // builder without materializing any prefab.
        return (first ?? Enumerable.Empty<RuntimeMetadataEntry>())
            .Concat(second ?? Enumerable.Empty<RuntimeMetadataEntry>())
            .Where(entry => entry != null && !string.IsNullOrEmpty(entry.ObjectID))
            .GroupBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.ObjectID, StringComparer.Ordinal)
            .ToList();
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
            bool wasWritten)
        {
            EntryCount = entryCount;
            ModdedEntryCount = moddedEntryCount;
            EnemyCount = enemyCount;
            PoolCount = poolCount;
            EligibleWeaponsPerPool = eligibleWeaponsPerPool;
            SkippedFirearmCount = skippedFirearmCount;
            PoolFileNames = poolFileNames;
            WasWritten = wasWritten;
        }

        public int EntryCount { get; private set; }
        public int ModdedEntryCount { get; private set; }
        public int EnemyCount { get; private set; }
        public int PoolCount { get; private set; }
        public int EligibleWeaponsPerPool { get; private set; }
        public int SkippedFirearmCount { get; private set; }
        public List<string> PoolFileNames { get; private set; }
        public bool WasWritten { get; private set; }
        public long ElapsedMilliseconds { get; set; }
    }
}
