using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using FistVR;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HLin.GunGameCursedRandom;

[BepInPlugin("HLin.GunGameCursedRandom", "GunGame Cursed Random", "1.0.0")]
[BepInDependency("Kodeman.GunGame", BepInDependency.DependencyFlags.HardDependency)]
[BepInProcess("h3vr.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    private const string HarmonyId = "HLin.GunGameCursedRandom";
    private const string RandomGunMethodName = "BTN_TryToSpawnRandomGun";
    private const string RandomGunFieldName = "CurrentlySpawnedRandomGun";
    private const int RandomGunWaitFrames = 120;
    private const int StartupToggleWaitFrames = 120;

    private static Plugin instance;
    private readonly List<GameObject> activeRandomEquipment = new List<GameObject>();
    private ConfigEntry<bool> randomGunsEnabled;
    private ConfigEntry<bool> randomGunDefaultInitialized;
    private MethodInfo randomGunMethod;
    private FieldInfo randomGunField;
    private bool spawningRandomGun;
    private bool missingSpawnerLogged;

    private void Awake()
    {
        instance = this;
        randomGunsEnabled = Config.Bind(
            "General",
            "EnableRandomCursedGuns",
            true,
            "Use H3VR Item Spawner random guns instead of GunGame profile weapons. Forced on when H3VR starts; startup UI may change it for the current session.");
        randomGunDefaultInitialized = Config.Bind(
            "General",
            "RandomGunDefaultInitialized",
            false,
            "Internal one-time default migration.");
        var persistedRandomGunSetting = randomGunsEnabled.Value;
        randomGunsEnabled.Value = true;
        randomGunDefaultInitialized.Value = true;
        randomGunsEnabled.SettingChanged += (sender, args) => RefreshOptionVisual();

        var harmony = new Harmony(HarmonyId);
        Patch(harmony, "GunGame.Scripts.Options.GameSettings", "Start", "GameSettingsStartPostfix", false);
        Patch(harmony, "GunGame.Scripts.GameManager", "StartGame", "GameManagerStartGameTracePrefix", false);
        Patch(harmony, "GunGame.Scripts.Progression", "SpawnAndEquip", "SpawnAndEquipPrefix", true);
        Patch(harmony, "GunGame.Scripts.Progression", "Promote", "ProgressionPromoteTracePrefix", false);
        Patch(harmony, "GunGame.Scripts.Progression", "Demote", "ProgressionDemoteTracePrefix", false);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Logger.LogInfo("GunGame Cursed Random ready. Random progression is force-enabled for this H3VR session.");
        Logger.LogInfo("GunGame Cursed Random trace: persisted random setting=" + persistedRandomGunSetting + "; effective startup setting=true.");
    }

    private void Start()
    {
        StartCoroutine(AddStartupToggleWhenReady());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DestroyTrackedEquipment();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Patch(Harmony harmony, string typeName, string methodName, string patchName, bool hasBooleanParameter)
    {
        var type = AccessTools.TypeByName(typeName);
        var original = type == null
            ? null
            : type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                hasBooleanParameter ? new[] { typeof(bool) } : Type.EmptyTypes,
                null);
        var patch = AccessTools.Method(typeof(Plugin), patchName);
        if (original == null || patch == null)
        {
            Logger.LogError("GunGame API unavailable: " + typeName + "." + methodName);
            return;
        }

        if (patchName.EndsWith("Prefix", StringComparison.Ordinal))
        {
            var prefix = new HarmonyMethod(patch);
            if (patchName == "SpawnAndEquipPrefix" || patchName.EndsWith("TracePrefix", StringComparison.Ordinal))
            {
                prefix.priority = Priority.First;
            }

            harmony.Patch(original, prefix: prefix);
            if (patchName == "SpawnAndEquipPrefix")
            {
                LogSpawnAndEquipPatchInfo(original);
            }
        }
        else
        {
            harmony.Patch(original, postfix: new HarmonyMethod(patch));
        }
    }

    private void LogSpawnAndEquipPatchInfo(MethodBase original)
    {
        Logger.LogInfo(
            "GunGame Cursed Random trace: SpawnAndEquip hook installed; target=" + original +
            "; prefixes=[" + DescribePrefixOwners(original) + "].");
    }

    private static void GameSettingsStartPostfix(object __instance)
    {
        if (instance != null)
        {
            instance.AddStartupToggle(__instance);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(AddStartupToggleWhenReady());
        StartCoroutine(TraceGunGameSceneAfterLoad());
    }

    private IEnumerator TraceGunGameSceneAfterLoad()
    {
        for (var frame = 0; frame < 120; frame++)
        {
            yield return null;
        }

        TraceRuntimeMethod("GameManager.StartGame", "GunGame.Scripts.GameManager", "StartGame", false);
        TraceRuntimeMethod("Progression.SpawnAndEquip", "GunGame.Scripts.Progression", "SpawnAndEquip", true);
        TraceRuntimeMethod("Progression.Promote", "GunGame.Scripts.Progression", "Promote", false);
        TraceRuntimeMethod("Progression.Demote", "GunGame.Scripts.Progression", "Demote", false);
    }

    private void TraceRuntimeMethod(string label, string typeName, string methodName, bool hasBooleanParameter)
    {
        var type = AccessTools.TypeByName(typeName);
        var component = type == null ? null : UnityEngine.Object.FindObjectOfType(type) as Component;
        var method = type == null
            ? null
            : type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                hasBooleanParameter ? new[] { typeof(bool) } : Type.EmptyTypes,
                null);
        Logger.LogInfo(
            "GunGame Cursed Random trace: runtime method probe; label=" + label +
            "; typeFound=" + (type != null) +
            "; componentFound=" + (component != null) +
            "; componentAssembly=" + (component == null ? "none" : component.GetType().Assembly.FullName) +
            "; method=" + (method == null ? "none" : method.ToString()) +
            "; prefixes=[" + DescribePrefixOwners(method) + "].");
    }

    private IEnumerator AddStartupToggleWhenReady()
    {
        var settingsType = AccessTools.TypeByName("GunGame.Scripts.Options.GameSettings");
        for (var frame = 0; frame < StartupToggleWaitFrames; frame++)
        {
            var settings = settingsType == null
                ? null
                : UnityEngine.Object.FindObjectOfType(settingsType) as Component;
            if (settings != null)
            {
                AddStartupToggle(settings);
                yield break;
            }

            yield return null;
        }

        Logger.LogWarning("GunGame settings were not available for RANDOM CURSED GUNS.");
    }

    private static bool SpawnAndEquipPrefix(object __instance, bool __0)
    {
        if (instance == null)
        {
            return true;
        }

        instance.Logger.LogInfo(
            "GunGame Cursed Random trace: SpawnAndEquip entered; demotion=" + __0 +
            "; randomEnabled=" + instance.randomGunsEnabled.Value +
            "; progression=" + (__instance == null ? "null" : __instance.GetType().FullName) + ".");
        if (!instance.randomGunsEnabled.Value)
        {
            instance.Logger.LogWarning("GunGame Cursed Random trace: random override bypassed because current-session setting is false.");
            return true;
        }

        var started = instance.TryStartRandomSpawn(__instance);
        instance.Logger.LogInfo("GunGame Cursed Random trace: SpawnAndEquip override started=" + started + "; original profile spawn=" + (!started) + ".");
        return !started;
    }

    private static void GameManagerStartGameTracePrefix(object __instance)
    {
        Trace("GameManager.StartGame entered; manager=" + (__instance == null ? "null" : __instance.GetType().Assembly.FullName) + ".");
    }

    private static void ProgressionPromoteTracePrefix(object __instance)
    {
        Trace("Progression.Promote entered; progression=" + (__instance == null ? "null" : __instance.GetType().Assembly.FullName) + ".");
    }

    private static void ProgressionDemoteTracePrefix(object __instance)
    {
        Trace("Progression.Demote entered; progression=" + (__instance == null ? "null" : __instance.GetType().Assembly.FullName) + ".");
    }

    private bool TryStartRandomSpawn(object progression)
    {
        if (spawningRandomGun)
        {
            Logger.LogWarning("GunGame Cursed Random trace: random spawn already pending; suppressing duplicate GunGame profile spawn.");
            return true;
        }

        var spawners = UnityEngine.Object.FindObjectsOfType<ItemSpawnerV2>();
        var spawner = spawners.FirstOrDefault();
        Logger.LogInfo(
            "GunGame Cursed Random trace: random spawn request; spawnerCount=" + spawners.Length +
            "; selectedSpawner=" + (spawner == null ? "none" : spawner.name) +
            "; trackedRandomEquipment=" + activeRandomEquipment.Count + ".");
        if (spawner == null)
        {
            if (!missingSpawnerLogged)
            {
                missingSpawnerLogged = true;
                Logger.LogWarning("Random Gun mode needs an active vanilla Item Spawner V2; using GunGame profile weapon until one is available.");
            }

            return false;
        }

        randomGunMethod = randomGunMethod ?? spawner.GetType().GetMethod(
            RandomGunMethodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        randomGunField = randomGunField ?? spawner.GetType().GetField(
            RandomGunFieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (randomGunMethod == null || randomGunField == null)
        {
            Logger.LogError("Item Spawner random-gun API is unavailable; using GunGame profile weapon.");
            return false;
        }

        missingSpawnerLogged = false;
        spawningRandomGun = true;
        var previousGun = randomGunField.GetValue(spawner) as GameObject;
        var before = CapturePhysicalObjects();
        Logger.LogInfo(
            "GunGame Cursed Random trace: invoking vanilla random API; method=" + randomGunMethod +
            "; resultField=" + randomGunField.Name +
            "; previousGun=" + NameOf(previousGun) +
            "; physicalObjectsBefore=" + before.Count + ".");
        try
        {
            randomGunMethod.Invoke(spawner, null);
            Logger.LogInfo("GunGame Cursed Random trace: vanilla random API invoked; resultImmediately=" + NameOf(randomGunField.GetValue(spawner) as GameObject) + ".");
            StartCoroutine(FinishRandomSpawn(progression, spawner, previousGun, before));
            return true;
        }
        catch (Exception exception)
        {
            spawningRandomGun = false;
            Logger.LogWarning("Could not call Item Spawner random-gun API: " + exception.GetType().Name);
            return false;
        }
    }

    private IEnumerator FinishRandomSpawn(
        object progression,
        ItemSpawnerV2 spawner,
        GameObject previousGun,
        HashSet<int> before)
    {
        GameObject randomGun = null;
        var waitedFrames = 0;
        for (var frame = 0; frame < RandomGunWaitFrames; frame++)
        {
            waitedFrames = frame + 1;
            randomGun = randomGunField.GetValue(spawner) as GameObject;
            if (randomGun != null && randomGun != previousGun)
            {
                break;
            }

            yield return null;
        }

        spawningRandomGun = false;
        var gun = randomGun == null ? null : randomGun.GetComponent<FVRPhysicalObject>();
        if (gun == null)
        {
            Logger.LogWarning(
                "GunGame Cursed Random trace: vanilla random API produced no usable firearm after " +
                waitedFrames + " frames; keeping current GunGame weapon.");
            yield break;
        }

        var spawned = CapturePhysicalObjectsList()
            .Where(item => item != null && !before.Contains(item.GetInstanceID()))
            .ToList();
        if (!spawned.Contains(gun))
        {
            spawned.Add(gun);
        }

        Logger.LogInfo(
            "GunGame Cursed Random trace: vanilla random result; waitFrames=" + waitedFrames +
            "; gun=" + NameOf(gun) +
            "; newPhysicalObjects=" + spawned.Count + ".");

        DestroyGunGameEquipment(progression);
        DestroyTrackedEquipment();
        activeRandomEquipment.AddRange(spawned.Select(item => item.gameObject));

        var looseFeeds = spawned
            .Where(item => item != gun && !item.transform.IsChildOf(gun.transform) && IsFeed(item))
            .OrderBy(FeedSortOrder)
            .ToList();
        var loadedFeed = LoadFirstCompatibleFeed(gun, looseFeeds);
        MoveSpareFeedsToQuickbelt(looseFeeds, loadedFeed);
        EquipInGunGameHand(gun);
        NotifyWeaponChanged(progression);
        LogRandomLoadout(gun, looseFeeds, loadedFeed);
    }

    private static HashSet<int> CapturePhysicalObjects()
    {
        return new HashSet<int>(UnityEngine.Object.FindObjectsOfType<FVRPhysicalObject>()
            .Where(item => item != null)
            .Select(item => item.GetInstanceID()));
    }

    private static List<FVRPhysicalObject> CapturePhysicalObjectsList()
    {
        return UnityEngine.Object.FindObjectsOfType<FVRPhysicalObject>()
            .Where(item => item != null)
            .ToList();
    }

    private static bool IsFeed(FVRPhysicalObject item)
    {
        return item is FVRFireArmMagazine || item is Speedloader || item is FVRFireArmRound;
    }

    private static FVRPhysicalObject LoadFirstCompatibleFeed(FVRPhysicalObject gun, IEnumerable<FVRPhysicalObject> feeds)
    {
        foreach (var feed in feeds)
        {
            try
            {
                var firearm = gun as FVRFireArm;
                var magazine = feed as FVRFireArmMagazine;
                if (firearm != null && magazine != null)
                {
                    magazine.Load(firearm);
                    magazine.ReloadMagWithType(AM.SRoundDisplayDataDic[magazine.RoundType].Classes[0].Class);
                    Trace("loaded magazine=" + NameOf(magazine) + "; gun=" + NameOf(gun) + ".");
                    return feed;
                }

                var cylinder = gun.GetComponentInChildren<RevolverCylinder>();
                var speedloader = feed as Speedloader;
                if (cylinder != null && speedloader != null)
                {
                    cylinder.LoadFromSpeedLoader(speedloader);
                    Trace("loaded speedloader=" + NameOf(speedloader) + "; gun=" + NameOf(gun) + ".");
                    return feed;
                }

                var round = feed as FVRFireArmRound;
                if (firearm != null && round != null && firearm.GetChambers().Count > 0)
                {
                    firearm.GetChambers()[0].SetRound(round, false);
                    Trace("loaded round=" + NameOf(round) + "; gun=" + NameOf(gun) + ".");
                    return feed;
                }
            }
            catch (Exception exception)
            {
                Trace("feed rejected=" + NameOf(feed) + "; reason=" + exception.GetType().Name + ".");
            }
        }

        Trace("no compatible generated feed for gun=" + NameOf(gun) + ".");
        return null;
    }

    private static int FeedSortOrder(FVRPhysicalObject feed)
    {
        if (feed is FVRFireArmMagazine)
        {
            return 0;
        }

        if (feed is Speedloader)
        {
            return 1;
        }

        return 2;
    }

    private static void MoveSpareFeedsToQuickbelt(
        IEnumerable<FVRPhysicalObject> feeds,
        FVRPhysicalObject loadedFeed)
    {
        var spares = feeds.Where(feed => feed != null && feed != loadedFeed).ToList();
        var candidateSlots = new[]
            {
                GetQuickbeltSlot("AmmoQuickbeltSlot", 0),
                GetQuickbeltSlot("ExtraQuickbeltSlot", 1)
            }
            .Where(slot => slot != null)
            .Distinct()
            .ToList();
        Trace(
            "quickbelt: spares=" + spares.Count +
            "; slots=[" + string.Join(", ", candidateSlots.Select(slot => slot.name + "=" + NameOf(slot.CurObject)).ToArray()) + "].");
        var emptySlots = candidateSlots.Where(slot => slot.CurObject == null).ToList();
        for (var index = 0; index < spares.Count && index < emptySlots.Count; index++)
        {
            spares[index].ForceObjectIntoInventorySlot(emptySlots[index]);
            spares[index].m_isSpawnLock = true;
            Trace("quickbelt: placed=" + NameOf(spares[index]) + "; slot=" + emptySlots[index].name + ".");
        }
    }

    private static FVRQuickBeltSlot GetQuickbeltSlot(string fieldName, int fallbackIndex)
    {
        var optionType = AccessTools.TypeByName("GunGame.Scripts.Options.QuickbeltOption");
        var field = optionType == null
            ? null
            : optionType.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var slot = field == null ? null : field.GetValue(null) as FVRQuickBeltSlot;
        if (slot != null)
        {
            return slot;
        }

        return GetInternalQuickbeltSlots().Skip(fallbackIndex).FirstOrDefault();
    }

    private static IEnumerable<FVRQuickBeltSlot> GetInternalQuickbeltSlots()
    {
        var body = GM.CurrentPlayerBody;
        var field = body == null
            ? null
            : body.GetType().GetField(
                "QBSlots_Internal",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var slots = field == null ? null : field.GetValue(body) as IEnumerable;
        if (slots == null)
        {
            yield break;
        }

        foreach (var value in slots)
        {
            var slot = value as FVRQuickBeltSlot;
            if (slot != null)
            {
                yield return slot;
            }
        }
    }

    private static void EquipInGunGameHand(FVRPhysicalObject gun)
    {
        var leftHand = ReadStaticBool("GunGame.Scripts.Options.LeftHandOption", "LeftHandModeEnabled");
        var handIndex = leftHand ? 0 : 1;
        if (GM.CurrentMovementManager == null || GM.CurrentMovementManager.Hands == null || GM.CurrentMovementManager.Hands.Length <= handIndex)
        {
            Trace("hand equip failed: movement manager or selected hand unavailable; handIndex=" + handIndex + ".");
            return;
        }

        Trace("hand equip: gun=" + NameOf(gun) + "; handIndex=" + handIndex + "; leftHandMode=" + leftHand + ".");
        GM.CurrentMovementManager.Hands[handIndex].RetrieveObject(gun);
    }

    private static bool ReadStaticBool(string typeName, string fieldName)
    {
        var type = AccessTools.TypeByName(typeName);
        var field = type == null
            ? null
            : type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null && field.GetValue(null) is bool && (bool)field.GetValue(null);
    }

    private static void DestroyGunGameEquipment(object progression)
    {
        var destroyedOldEquipment = false;
        var clearedWeaponBuffer = false;
        try
        {
            var clearEquipment = progression == null
                ? null
                : progression.GetType().GetMethod("DestroyOldEq", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clearEquipment != null)
            {
                clearEquipment.Invoke(progression, null);
                destroyedOldEquipment = true;
            }

            var component = progression as Component;
            var bufferType = AccessTools.TypeByName("GunGame.Scripts.Weapons.WeaponBuffer");
            var buffer = component == null || bufferType == null ? null : component.GetComponent(bufferType);
            var clearBuffer = buffer == null ? null : AccessTools.Method(buffer.GetType(), "ClearBuffer");
            if (clearBuffer != null)
            {
                clearBuffer.Invoke(buffer, null);
                clearedWeaponBuffer = true;
            }

            Trace("cleanup: DestroyOldEq=" + destroyedOldEquipment + "; ClearBuffer=" + clearedWeaponBuffer + ".");
        }
        catch (Exception exception)
        {
            Trace("cleanup failed: " + exception.GetType().Name + ".");
        }
    }

    private void DestroyTrackedEquipment()
    {
        var destroyed = 0;
        foreach (var item in activeRandomEquipment)
        {
            if (item != null)
            {
                var physicalObject = item.GetComponent<FVRPhysicalObject>();
                if (physicalObject != null)
                {
                    physicalObject.ForceBreakInteraction();
                }

                UnityEngine.Object.Destroy(item);
                destroyed++;
            }
        }

        activeRandomEquipment.Clear();
        if (destroyed > 0)
        {
            Logger.LogInfo("GunGame Cursed Random trace: destroyed prior tracked random equipment=" + destroyed + ".");
        }
    }

    private static void NotifyWeaponChanged(object progression)
    {
        var eventField = progression == null
            ? null
            : progression.GetType().GetField("WeaponChangedEvent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var callback = eventField == null ? null : eventField.GetValue(null) as Action;
        if (callback != null)
        {
            callback();
            Trace("WeaponChangedEvent invoked.");
            return;
        }

        Trace("WeaponChangedEvent was unavailable.");
    }

    private void AddStartupToggle(object settings)
    {
        var component = settings as Component;
        if (component == null || component.GetComponentsInChildren<RandomToggleMarker>(true).Length > 0)
        {
            return;
        }

        var sourceImage = settings.GetType().GetField(
            "DisabledAutoLoadingImage",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var image = sourceImage == null ? null : sourceImage.GetValue(settings) as Image;
        var sourceButton = image == null ? null : image.GetComponentInParent<Button>();
        if (sourceButton == null)
        {
            Logger.LogWarning("Could not add RANDOM CURSED GUNS startup toggle.");
            return;
        }

        var clone = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
        clone.name = "HLin_RandomCursedGuns";
        clone.transform.SetSiblingIndex(sourceButton.transform.GetSiblingIndex() + 1);
        var button = clone.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(new UnityAction(ToggleRandomGuns));
        foreach (var text in clone.GetComponentsInChildren<Text>(true))
        {
            text.text = "RANDOM CURSED GUNS";
        }

        var markerPath = GetRelativePath(sourceButton.transform, image.transform);
        var markerTransform = string.IsNullOrEmpty(markerPath) ? clone.transform : clone.transform.Find(markerPath);
        var marker = markerTransform == null ? null : markerTransform.GetComponent<Image>();
        clone.AddComponent<RandomToggleMarker>().EnabledImage = marker;
        RefreshOptionVisual();
        Logger.LogInfo("Added RANDOM CURSED GUNS startup toggle.");
    }

    private static string GetRelativePath(Transform root, Transform child)
    {
        var parts = new List<string>();
        while (child != null && child != root)
        {
            parts.Add(child.name);
            child = child.parent;
        }

        if (child != root)
        {
            return string.Empty;
        }

        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    private void ToggleRandomGuns()
    {
        randomGunsEnabled.Value = !randomGunsEnabled.Value;
        Logger.LogInfo("Random cursed GunGame progression " + (randomGunsEnabled.Value ? "enabled." : "disabled."));
    }

    private void RefreshOptionVisual()
    {
        foreach (var marker in UnityEngine.Object.FindObjectsOfType<RandomToggleMarker>())
        {
            if (marker.EnabledImage != null)
            {
                marker.EnabledImage.enabled = randomGunsEnabled != null && randomGunsEnabled.Value;
            }
        }
    }

    private void LogRandomLoadout(
        FVRPhysicalObject gun,
        IEnumerable<FVRPhysicalObject> feeds,
        FVRPhysicalObject loadedFeed)
    {
        var attachments = gun.GetComponentsInChildren<FVRFireArmAttachment>()
            .Select(NameOf)
            .Distinct()
            .ToArray();
        var feedNames = feeds.Select(NameOf).Distinct().ToArray();
        Logger.LogInfo(
            "Cursed random GunGame spawn: gun=" + NameOf(gun) +
            ", loaded=" + (loadedFeed == null ? "none" : NameOf(loadedFeed)) +
            ", feeds=[" + string.Join(", ", feedNames) +
            "], attachments=[" + string.Join(", ", attachments) + "].");
    }

    private static string NameOf(FVRPhysicalObject item)
    {
        return item == null || item.ObjectWrapper == null ? "unknown" : item.ObjectWrapper.DisplayName;
    }

    private static string NameOf(GameObject item)
    {
        return item == null ? "none" : item.name;
    }

    private static string DescribePrefixOwners(MethodBase method)
    {
        var patchInfo = method == null ? null : Harmony.GetPatchInfo(method);
        return patchInfo == null
            ? string.Empty
            : string.Join(
                ", ",
                patchInfo.Prefixes.Select(prefix => prefix.owner + "@" + prefix.priority).ToArray());
    }

    private static void Trace(string message)
    {
        if (instance != null)
        {
            instance.Logger.LogInfo("GunGame Cursed Random trace: " + message);
        }
    }
}

public sealed class RandomToggleMarker : MonoBehaviour
{
    public Image EnabledImage;
}
