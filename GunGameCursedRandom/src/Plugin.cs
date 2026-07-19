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

    private static Plugin instance;
    private readonly List<GameObject> activeRandomEquipment = new List<GameObject>();
    private ConfigEntry<bool> randomGunsEnabled;
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
            false,
            "Use H3VR Item Spawner random guns instead of GunGame profile weapons.");
        randomGunsEnabled.SettingChanged += (sender, args) => RefreshOptionVisual();

        var harmony = new Harmony(HarmonyId);
        Patch(harmony, "GunGame.Scripts.Options.GameSettings", "Start", "GameSettingsStartPostfix", false);
        Patch(harmony, "GunGame.Scripts.Progression", "SpawnAndEquip", "SpawnAndEquipPrefix", true);
        Logger.LogInfo("GunGame Cursed Random ready. Toggle RANDOM CURSED GUNS in GunGame settings.");
    }

    private void OnDestroy()
    {
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
            harmony.Patch(original, prefix: new HarmonyMethod(patch));
        }
        else
        {
            harmony.Patch(original, postfix: new HarmonyMethod(patch));
        }
    }

    private static void GameSettingsStartPostfix(object __instance)
    {
        if (instance != null)
        {
            instance.AddStartupToggle(__instance);
        }
    }

    private static bool SpawnAndEquipPrefix(object __instance)
    {
        return instance == null || !instance.randomGunsEnabled.Value || !instance.TryStartRandomSpawn(__instance);
    }

    private bool TryStartRandomSpawn(object progression)
    {
        if (spawningRandomGun)
        {
            return true;
        }

        var spawner = UnityEngine.Object.FindObjectOfType<ItemSpawnerV2>();
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
        try
        {
            randomGunMethod.Invoke(spawner, null);
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
        for (var frame = 0; frame < RandomGunWaitFrames; frame++)
        {
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
            Logger.LogWarning("Item Spawner random-gun API did not produce a usable firearm; keeping current GunGame weapon.");
            yield break;
        }

        var spawned = CapturePhysicalObjectsList()
            .Where(item => item != null && !before.Contains(item.GetInstanceID()))
            .ToList();
        if (!spawned.Contains(gun))
        {
            spawned.Add(gun);
        }

        DestroyGunGameEquipment(progression);
        DestroyTrackedEquipment();
        activeRandomEquipment.AddRange(spawned.Select(item => item.gameObject));

        var looseFeeds = spawned
            .Where(item => item != gun && !item.transform.IsChildOf(gun.transform) && IsFeed(item))
            .ToList();
        var loadedFeed = LoadFirstCompatibleFeed(gun, looseFeeds);
        MoveSpareFeedToQuickbelt(looseFeeds, loadedFeed);
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
                    return feed;
                }

                var cylinder = gun.GetComponentInChildren<RevolverCylinder>();
                var speedloader = feed as Speedloader;
                if (cylinder != null && speedloader != null)
                {
                    cylinder.LoadFromSpeedLoader(speedloader);
                    return feed;
                }

                var round = feed as FVRFireArmRound;
                if (firearm != null && round != null && firearm.GetChambers().Count > 0)
                {
                    firearm.GetChambers()[0].SetRound(round, false);
                    return feed;
                }
            }
            catch
            {
                // Item Spawner may generate a legal but non-interchangeable feed.
            }
        }

        return null;
    }

    private static void MoveSpareFeedToQuickbelt(
        IEnumerable<FVRPhysicalObject> feeds,
        FVRPhysicalObject loadedFeed)
    {
        var spare = feeds.FirstOrDefault(feed => feed != null && feed != loadedFeed);
        var slot = GetQuickbeltSlot("AmmoQuickbeltSlot", 0);
        if (spare == null || slot == null)
        {
            return;
        }

        if (slot.CurObject != null)
        {
            slot.CurObject.ClearQuickbeltState();
        }

        spare.ForceObjectIntoInventorySlot(slot);
        spare.m_isSpawnLock = true;
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
        GM.CurrentMovementManager.Hands[leftHand ? 0 : 1].RetrieveObject(gun);
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
        try
        {
            var clearEquipment = progression == null
                ? null
                : progression.GetType().GetMethod("DestroyOldEq", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clearEquipment != null)
            {
                clearEquipment.Invoke(progression, null);
            }

            var component = progression as Component;
            var bufferType = AccessTools.TypeByName("GunGame.Scripts.Weapons.WeaponBuffer");
            var buffer = component == null || bufferType == null ? null : component.GetComponent(bufferType);
            var clearBuffer = buffer == null ? null : AccessTools.Method(buffer.GetType(), "ClearBuffer");
            if (clearBuffer != null)
            {
                clearBuffer.Invoke(buffer, null);
            }
        }
        catch
        {
            // A cleanup issue must not prevent delivery of a successful random gun.
        }
    }

    private void DestroyTrackedEquipment()
    {
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
            }
        }

        activeRandomEquipment.Clear();
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
        }
    }

    private void AddStartupToggle(object settings)
    {
        var component = settings as Component;
        if (component == null || component.transform.Find("HLin_RandomCursedGuns") != null)
        {
            return;
        }

        var sourceImage = settings.GetType().GetField(
            "DisabledAutoLoadingImage",
            BindingFlags.Instance | BindingFlags.NonPublic);
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
}

public sealed class RandomToggleMarker : MonoBehaviour
{
    public Image EnabledImage;
}
