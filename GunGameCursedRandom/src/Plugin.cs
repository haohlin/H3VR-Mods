using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace HLin.GunGameCursedRandom;

[BepInPlugin("HLin.GunGameCursedRandom", "GunGame Cursed Random", "1.0.0")]
[BepInDependency("Kodeman.GunGame", BepInDependency.DependencyFlags.HardDependency)]
[BepInProcess("h3vr.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    private const string CursedProfileName = "HLin-Random Cursed";
    private const string PlaceholderMagazineItemId = "MagazineG17Standard";
    private const string RandomGunMethodName = "BTN_TryToSpawnRandomGun";
    private const string RandomGunFieldName = "CurrentlySpawnedRandomGun";
    private const int RandomGunWaitFrames = 120;

    private static Plugin instance;
    private readonly List<ManagedQuickbeltFeed> managedQuickbeltFeeds = new List<ManagedQuickbeltFeed>();
    private readonly List<WeaponChangedSubscription> weaponChangedSubscriptions = new List<WeaponChangedSubscription>();
    private GameObject activeRandomGun;
    private Harmony harmony;
    private MethodInfo randomGunMethod;
    private FieldInfo randomGunField;
    private Type randomSpawnerType;
    private Component queuedProgression;
    private Type directTransitionProgressionType;
    private bool replacementQueued;
    private bool queuedTransition;
    private bool spawningRandomGun;
    private bool missingSpawnerLogged;
    private bool weaponBufferSpawnHookInstalled;

    private void Awake()
    {
        instance = this;
        weaponBufferSpawnHookInstalled = InstallWeaponBufferSpawnHooks();
        if (!weaponBufferSpawnHookInstalled)
        {
            Logger.LogWarning("GunGame Cursed Random could not install WeaponBuffer.SpawnAsync hook; using post-spawn fallback.");
        }

        if (SubscribeWeaponChangedEvents() == 0)
        {
            Logger.LogError("GunGame Cursed Random could not subscribe to GunGame Progression.WeaponChangedEvent.");
        }

        Logger.LogInfo("GunGame Cursed Random ready. Select " + CursedProfileName + " in GunGame progression choices to enable random weapons.");
    }

    private void OnDestroy()
    {
        UnsubscribeWeaponChangedEvents();
        if (harmony != null)
        {
            harmony.UnpatchSelf();
        }

        DestroyTrackedEquipment();
        if (instance == this)
        {
            instance = null;
        }
    }

    private int SubscribeWeaponChangedEvents()
    {
        var added = 0;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var progressionType = assembly.GetType("GunGame.Scripts.Progression", false);
            var changedEvent = progressionType == null
                ? null
                : progressionType.GetField(
                    "WeaponChangedEvent",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (changedEvent == null || changedEvent.FieldType != typeof(Action) ||
                weaponChangedSubscriptions.Any(subscription => subscription.EventField == changedEvent))
            {
                continue;
            }

            var subscription = new WeaponChangedSubscription
            {
                ProgressionType = progressionType,
                EventField = changedEvent
            };
            subscription.Handler = delegate { OnNativeWeaponChanged(subscription.ProgressionType); };
            changedEvent.SetValue(null, (Action)Delegate.Combine(changedEvent.GetValue(null) as Action, subscription.Handler));
            weaponChangedSubscriptions.Add(subscription);
            added++;
            Logger.LogInfo("GunGame Cursed Random subscribed to " + progressionType.FullName + " in " + assembly.GetName().Name + ".");
        }

        return added;
    }

    private bool InstallWeaponBufferSpawnHooks()
    {
        var prefix = AccessTools.Method(typeof(Plugin), "WeaponBufferSpawnAsyncPrefix");
        if (prefix == null)
        {
            return false;
        }

        var patched = 0;
        try
        {
            harmony = new Harmony("HLin.GunGameCursedRandom");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType("GunGame.Scripts.Progression", false) == null)
                {
                    continue;
                }

                var weaponBufferType = assembly.GetType("GunGame.Scripts.Weapons.WeaponBuffer", false);
                if (weaponBufferType == null)
                {
                    continue;
                }

                foreach (var spawnAsync in weaponBufferType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             .Where(method => method.Name == "SpawnAsync" &&
                                              typeof(IEnumerator).IsAssignableFrom(method.ReturnType) &&
                                              method.GetParameters().Length == 2))
                {
                    harmony.Patch(spawnAsync, prefix: new HarmonyMethod(prefix) { priority = Priority.First });
                    var patchInfo = Harmony.GetPatchInfo(spawnAsync);
                    if (patchInfo != null && patchInfo.Prefixes.Any(patch => patch.owner == harmony.Id))
                    {
                        patched++;
                        Logger.LogInfo(
                            "GunGame Cursed Random installed WeaponBuffer.SpawnAsync direct hook in " +
                            assembly.GetName().Name + ".");
                    }
                }
            }

            return patched > 0;
        }
        catch (Exception exception)
        {
            Logger.LogWarning("GunGame Cursed Random could not patch active WeaponBuffer.SpawnAsync: " + exception.GetType().Name + ".");
            return false;
        }
    }

    private void UnsubscribeWeaponChangedEvents()
    {
        foreach (var subscription in weaponChangedSubscriptions)
        {
            Unsubscribe(subscription);
        }

        weaponChangedSubscriptions.Clear();
    }

    private static void Unsubscribe(IEnumerable<StaticActionSubscription> subscriptions)
    {
        foreach (var subscription in subscriptions)
        {
            Unsubscribe(subscription);
        }
    }

    private static void Unsubscribe(StaticActionSubscription subscription)
    {
        var current = subscription.EventField.GetValue(null) as Action;
        subscription.EventField.SetValue(null, (Action)Delegate.Remove(current, subscription.Handler));
    }

    private static bool WeaponBufferSpawnAsyncPrefix(object __instance, object __1, ref IEnumerator __result)
    {
        var plugin = instance;
        if (plugin == null || __instance == null || !IsCursedProfileSelected(__instance.GetType().Assembly))
        {
            return true;
        }

        if (plugin.spawningRandomGun || plugin.replacementQueued)
        {
            Trace("WeaponBuffer.SpawnAsync direct hook found a pending random spawn; keeping native fallback.");
            return true;
        }

        var progression = FindProgressionForWeaponBuffer(__instance);
        if (progression == null || !plugin.TryStartRandomSpawn(progression, false))
        {
            Trace("WeaponBuffer.SpawnAsync direct hook could not start random spawn; keeping native fallback.");
            return true;
        }

        plugin.directTransitionProgressionType = progression.GetType();
        __result = EmptyEnumerator();
        Trace("WeaponBuffer.SpawnAsync suppressing native placeholder; vanilla random spawn started.");
        return false;
    }

    private void OnNativeWeaponChanged(Type progressionType)
    {
        if (!IsCursedProfileSelected(progressionType.Assembly))
        {
            return;
        }

        var progression = FindLiveProgression(progressionType);
        if (progression == null)
        {
            Logger.LogWarning(CursedProfileName + " selected, but its live Progression instance was unavailable.");
            return;
        }

        if (directTransitionProgressionType == progressionType && spawningRandomGun)
        {
            directTransitionProgressionType = null;
            Trace("WeaponChangedEvent acknowledged direct Cursed transition; no fallback needed.");
            return;
        }

        directTransitionProgressionType = null;
        if (spawningRandomGun || replacementQueued)
        {
            queuedProgression = progression;
            queuedTransition = true;
            Trace("WeaponChangedEvent queued while a Cursed transition is pending.");
            return;
        }

        Trace("WeaponChangedEvent found no pending direct random spawn; using post-spawn fallback.");
        replacementQueued = true;
        Logger.LogInfo(CursedProfileName + " selected: native GunGame weapon transition observed; replacing profile equipment.");
        StartCoroutine(ReplaceNativeEquipment(progression));
    }

    private IEnumerator ReplaceNativeEquipment(object progression)
    {
        yield return null;
        replacementQueued = false;
        if (!IsCursedProfileSelected(progression.GetType().Assembly) || spawningRandomGun)
        {
            yield break;
        }

        if (!TryStartRandomSpawn(progression, true))
        {
            Logger.LogWarning(CursedProfileName + " random API unavailable; keeping current profile equipment.");
            StartQueuedReplacement();
        }
    }

    private void StartQueuedReplacement()
    {
        if (!queuedTransition || replacementQueued)
        {
            return;
        }

        var progression = queuedProgression;
        queuedProgression = null;
        queuedTransition = false;
        if (progression == null)
        {
            return;
        }

        replacementQueued = true;
        Trace("starting latest queued Cursed transition.");
        StartCoroutine(ReplaceNativeEquipment(progression));
    }

    private static Component FindLiveProgression(Type progressionType)
    {
        Component fallback = null;
        foreach (var candidate in Resources.FindObjectsOfTypeAll(progressionType))
        {
            var component = candidate as Component;
            if (component == null)
            {
                continue;
            }

            if (component.gameObject.activeInHierarchy)
            {
                return component;
            }

            fallback = component;
        }

        return fallback;
    }

    private static Component FindProgressionForWeaponBuffer(object weaponBuffer)
    {
        var buffer = weaponBuffer as Component;
        var progressionType = buffer == null
            ? null
            : buffer.GetType().Assembly.GetType("GunGame.Scripts.Progression", false);
        if (progressionType == null)
        {
            return null;
        }

        var progression = buffer == null
            ? null
            : buffer.GetComponentInParent(progressionType) as Component;
        return progression ?? FindLiveProgression(progressionType);
    }

    private static IEnumerator EmptyEnumerator()
    {
        yield break;
    }

    private static bool IsCursedProfileSelected(Assembly assembly)
    {
        try
        {
            var settingsType = assembly == null ? null : assembly.GetType("GunGame.Scripts.Options.GameSettings", false);
            var currentPoolProperty = settingsType == null
                ? null
                : settingsType.GetProperty(
                    "CurrentPool",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var currentPool = currentPoolProperty == null ? null : currentPoolProperty.GetValue(null, null);
            var getName = currentPool == null
                ? null
                : currentPool.GetType().GetMethod("GetName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var name = getName == null ? null : getName.Invoke(currentPool, null) as string;
            return string.Equals(name, CursedProfileName, StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            Trace("profile check failed: " + exception.GetType().Name + ".");
            return false;
        }
    }

    private bool TryStartRandomSpawn(object progression, bool removeNativeEquipment)
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
            "; trackedRandomGun=" + NameOf(activeRandomGun) + ".");
        if (spawner == null)
        {
            if (!missingSpawnerLogged)
            {
                missingSpawnerLogged = true;
                Logger.LogWarning("Random Gun mode needs an active vanilla Item Spawner V2; using GunGame profile weapon until one is available.");
            }

            return false;
        }

        if (randomSpawnerType != spawner.GetType())
        {
            randomSpawnerType = spawner.GetType();
            randomGunMethod = randomSpawnerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == RandomGunMethodName && method.GetParameters().Length == 0);
            randomGunField = randomSpawnerType.GetField(
                RandomGunFieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (randomGunMethod == null || randomGunField == null)
        {
            Logger.LogError("Item Spawner random-gun API is unavailable; using GunGame profile weapon.");
            return false;
        }

        missingSpawnerLogged = false;
        try
        {
            spawningRandomGun = true;
            var previousGun = randomGunField.GetValue(spawner) as GameObject;
            var before = CapturePhysicalObjects();
            Logger.LogInfo(
                "GunGame Cursed Random trace: invoking vanilla random API; method=" + randomGunMethod +
                "; resultField=" + randomGunField.Name +
                "; previousGun=" + NameOf(previousGun) +
                "; physicalObjectsBefore=" + before.Count + ".");
            randomGunMethod.Invoke(spawner, null);
            Logger.LogInfo("GunGame Cursed Random trace: vanilla random API invoked; resultImmediately=" + NameOf(randomGunField.GetValue(spawner) as GameObject) + ".");
            StartCoroutine(FinishRandomSpawn(progression, spawner, previousGun, before, removeNativeEquipment));
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
        HashSet<int> before,
        bool removeNativeEquipment)
    {
        GameObject randomGun = null;
        var waitedFrames = 0;
        for (var frame = 0; frame < RandomGunWaitFrames; frame++)
        {
            waitedFrames = frame + 1;
            try
            {
                randomGun = randomGunField.GetValue(spawner) as GameObject;
            }
            catch (Exception exception)
            {
                spawningRandomGun = false;
                Logger.LogWarning("GunGame Cursed Random trace: random result read failed: " + exception.GetType().Name + ".");
                StartQueuedReplacement();
                yield break;
            }

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
            StartQueuedReplacement();
            yield break;
        }

        directTransitionProgressionType = null;

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

        if (queuedTransition)
        {
            Trace("discarding stale random result for a newer Cursed transition.");
            foreach (var item in spawned)
            {
                DestroyGeneratedFeed(item);
            }

            StartQueuedReplacement();
            yield break;
        }

        if (removeNativeEquipment)
        {
            DestroyGunGameEquipment(progression);
            ClearNativePlaceholderFeed(progression);
        }

        DestroyTrackedEquipment();
        yield return null;
        activeRandomGun = gun.gameObject;

        var allFeeds = spawned
            .Where(item => item != gun && IsFeed(item))
            .OrderBy(FeedSortOrder)
            .ToList();
        var looseFeeds = allFeeds
            .Where(item => !item.transform.IsChildOf(gun.transform))
            .ToList();
        var loadedFeed = allFeeds.FirstOrDefault(item => item.transform.IsChildOf(gun.transform));
        if (loadedFeed != null)
        {
            FillStandardFeed(loadedFeed);
            Trace("retained loaded generated feed=" + NameOf(loadedFeed) + "; gun=" + NameOf(gun) + ".");
        }

        loadedFeed = loadedFeed ?? LoadFirstCompatibleFeed(gun, looseFeeds);
        MoveSpareFeedsToQuickbelt(looseFeeds, loadedFeed, gun, progression == null ? null : progression.GetType().Assembly);
        EquipInGunGameHand(gun, progression == null ? null : progression.GetType().Assembly);
        LogRandomLoadout(gun, allFeeds, loadedFeed);
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

    private static void DestroyGeneratedFeed(FVRPhysicalObject item)
    {
        if (item == null)
        {
            return;
        }

        item.ForceBreakInteraction();
        UnityEngine.Object.Destroy(item.gameObject);
    }

    private static bool IsFeed(FVRPhysicalObject item)
    {
        return item is FVRFireArmMagazine || item is FVRFireArmClip || item is Speedloader || item is FVRFireArmRound;
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
                    FillStandardFeed(magazine);
                    Trace("loaded magazine=" + NameOf(magazine) + "; gun=" + NameOf(gun) + ".");
                    return feed;
                }

                var clip = feed as FVRFireArmClip;
                if (firearm != null && clip != null)
                {
                    firearm.LoadClip(clip);
                    FillStandardFeed(clip);
                    Trace("loaded clip=" + NameOf(clip) + "; gun=" + NameOf(gun) + ".");
                    return feed;
                }

                var cylinder = gun.GetComponentInChildren<RevolverCylinder>();
                var speedloader = feed as Speedloader;
                if (cylinder != null && speedloader != null)
                {
                    FillStandardFeed(speedloader);
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

        if (feed is FVRFireArmClip)
        {
            return 1;
        }

        if (feed is Speedloader)
        {
            return 2;
        }

        return 3;
    }

    private static void FillStandardFeed(FVRPhysicalObject feed)
    {
        try
        {
            var magazine = feed as FVRFireArmMagazine;
            if (magazine != null && TryGetDefaultRoundClass(magazine.RoundType, out var magazineRoundClass))
            {
                magazine.ReloadMagWithType(magazineRoundClass);
                return;
            }

            var clip = feed as FVRFireArmClip;
            if (clip != null && TryGetDefaultRoundClass(clip.RoundType, out var clipRoundClass))
            {
                clip.ReloadClipWithType(clipRoundClass);
                return;
            }

            var speedloader = feed as Speedloader;
            if (speedloader != null && speedloader.Chambers != null && speedloader.Chambers.Length > 0 &&
                TryGetDefaultRoundClass(speedloader.Chambers[0].Type, out var speedloaderRoundClass))
            {
                speedloader.ReloadClipWithType(speedloaderRoundClass);
            }
        }
        catch (Exception exception)
        {
            Trace("feed fill skipped=" + NameOf(feed) + "; reason=" + exception.GetType().Name + ".");
        }
    }

    private static bool TryGetDefaultRoundClass(FireArmRoundType roundType, out FireArmRoundClass roundClass)
    {
        roundClass = default(FireArmRoundClass);
        if (!AM.SRoundDisplayDataDic.TryGetValue(roundType, out var display) || display.Classes == null || display.Classes.Count == 0)
        {
            return false;
        }

        roundClass = display.Classes[0].Class;
        return true;
    }

    private static FVRPhysicalObject SpawnQuickbeltSpare(FVRPhysicalObject loadedFeed, FVRPhysicalObject gun)
    {
        var feedObject = loadedFeed == null ? null : loadedFeed.ObjectWrapper;
        if (feedObject == null || gun == null)
        {
            Trace("quickbelt: no compatible loaded feed available for spare.");
            return null;
        }

        GameObject spawnedObject = null;
        try
        {
            spawnedObject = UnityEngine.Object.Instantiate(
                feedObject.GetGameObject(),
                gun.transform.position + Vector3.up * 0.2f,
                gun.transform.rotation);
            var spare = spawnedObject == null ? null : spawnedObject.GetComponent<FVRPhysicalObject>();
            if (!IsFeed(spare))
            {
                if (spawnedObject != null)
                {
                    UnityEngine.Object.Destroy(spawnedObject);
                }

                Trace("quickbelt: spawned object was not a supported feed.");
                return null;
            }

            FillStandardFeed(spare);
            Trace("quickbelt: spawned compatible spare=" + NameOf(spare) + "; source=loaded feed.");
            return spare;
        }
        catch (Exception exception)
        {
            if (spawnedObject != null)
            {
                UnityEngine.Object.Destroy(spawnedObject);
            }

            Trace("quickbelt: spare spawn failed=" + exception.GetType().Name + ".");
            return null;
        }
    }

    private static bool SameFeedType(FVRPhysicalObject left, FVRPhysicalObject right)
    {
        return left != null && right != null && left.ObjectWrapper != null && right.ObjectWrapper != null &&
               string.Equals(left.ObjectWrapper.ItemID, right.ObjectWrapper.ItemID, StringComparison.Ordinal);
    }

    private void MoveSpareFeedsToQuickbelt(
        IEnumerable<FVRPhysicalObject> feeds,
        FVRPhysicalObject loadedFeed,
        FVRPhysicalObject gun,
        Assembly assembly)
    {
        var candidateSlots = new[]
            {
                GetQuickbeltSlot(assembly, "AmmoQuickbeltSlot", 0),
                GetQuickbeltSlot(assembly, "ExtraQuickbeltSlot", 1)
            }
            .Where(slot => slot != null)
            .Distinct()
            .ToList();
        var slot = candidateSlots.FirstOrDefault(candidate => candidate.CurObject == null);
        var generatedFeeds = feeds.Where(feed => feed != null && feed != loadedFeed).ToList();
        var spare = generatedFeeds.FirstOrDefault(feed => SameFeedType(feed, loadedFeed));
        if (spare == null && slot != null)
        {
            spare = SpawnQuickbeltSpare(loadedFeed, gun);
        }

        var placed = false;
        if (spare != null && slot != null)
        {
            spare.m_isSpawnLock = true;
            spare.ForceObjectIntoInventorySlot(slot);
            if (slot.CurObject == spare)
            {
                managedQuickbeltFeeds.Add(new ManagedQuickbeltFeed { Slot = slot, Object = spare });
                placed = true;
                Trace("quickbelt: placed=" + NameOf(spare) + "; slot=" + slot.name + ".");
            }
            else
            {
                DestroyGeneratedFeed(spare);
                spare = null;
                Trace("quickbelt: selected slot rejected compatible spare.");
            }
        }

        var discarded = 0;
        foreach (var feed in generatedFeeds)
        {
            if (feed == spare)
            {
                continue;
            }

            DestroyGeneratedFeed(feed);
            discarded++;
        }

        Trace(
            "quickbelt: selectedSlot=" + (slot == null ? "none" : slot.name) +
            "; placed=" + placed +
            "; discardedGeneratedFeeds=" + discarded + ".");
    }

    private static FVRQuickBeltSlot GetQuickbeltSlot(Assembly assembly, string fieldName, int fallbackIndex)
    {
        var optionType = assembly == null
            ? AccessTools.TypeByName("GunGame.Scripts.Options.QuickbeltOption")
            : assembly.GetType("GunGame.Scripts.Options.QuickbeltOption", false);
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

    private static void EquipInGunGameHand(FVRPhysicalObject gun, Assembly assembly)
    {
        var leftHand = ReadStaticBool(assembly, "GunGame.Scripts.Options.LeftHandOption", "LeftHandModeEnabled");
        var handIndex = leftHand ? 0 : 1;
        if (GM.CurrentMovementManager == null || GM.CurrentMovementManager.Hands == null || GM.CurrentMovementManager.Hands.Length <= handIndex)
        {
            Trace("hand equip failed: movement manager or selected hand unavailable; handIndex=" + handIndex + ".");
            return;
        }

        Trace("hand equip: gun=" + NameOf(gun) + "; handIndex=" + handIndex + "; leftHandMode=" + leftHand + ".");
        GM.CurrentMovementManager.Hands[handIndex].RetrieveObject(gun);
    }

    private static bool ReadStaticBool(Assembly assembly, string typeName, string fieldName)
    {
        var type = assembly == null ? null : assembly.GetType(typeName, false);
        var field = type == null
            ? null
            : type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null && field.GetValue(null) is bool && (bool)field.GetValue(null);
    }

    private static void DestroyGunGameEquipment(object progression)
    {
        var destroyedOldEquipment = false;
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

            Trace("cleanup: DestroyOldEq=" + destroyedOldEquipment + "; preserved native next-weapon buffer.");
        }
        catch (Exception exception)
        {
            Trace("cleanup failed: " + exception.GetType().Name + ".");
        }
    }

    private static void ClearNativePlaceholderFeed(object progression)
    {
        var assembly = progression == null ? null : progression.GetType().Assembly;
        var slot = GetQuickbeltSlot(assembly, "AmmoQuickbeltSlot", 0);
        var feed = slot == null ? null : slot.CurObject;
        if (feed == null || feed.ObjectWrapper == null ||
            !string.Equals(feed.ObjectWrapper.ItemID, PlaceholderMagazineItemId, StringComparison.Ordinal) ||
            !feed.m_isSpawnLock)
        {
            return;
        }

        feed.ForceBreakInteraction();
        UnityEngine.Object.Destroy(feed.gameObject);
        Trace("cleanup: removed spawn-locked native placeholder feed from Ammo slot.");
    }

    private void DestroyTrackedEquipment()
    {
        var destroyedGun = false;
        if (activeRandomGun != null)
        {
            var physicalObject = activeRandomGun.GetComponent<FVRPhysicalObject>();
            if (physicalObject != null)
            {
                physicalObject.ForceBreakInteraction();
            }

            UnityEngine.Object.Destroy(activeRandomGun);
            destroyedGun = true;
        }

        activeRandomGun = null;
        var clearedManagedSlots = 0;
        foreach (var managedFeed in managedQuickbeltFeeds)
        {
            if (managedFeed.Slot == null || managedFeed.Object == null ||
                managedFeed.Slot.CurObject != managedFeed.Object)
            {
                continue;
            }

            managedFeed.Object.ForceBreakInteraction();
            UnityEngine.Object.Destroy(managedFeed.Object.gameObject);
            clearedManagedSlots++;
        }

        managedQuickbeltFeeds.Clear();
        if (destroyedGun || clearedManagedSlots > 0)
        {
            Logger.LogInfo(
                "GunGame Cursed Random trace: cleanup: destroyedRandomGun=" + destroyedGun +
                "; clearedManagedSpareSlots=" + clearedManagedSlots +
                "; preserved all other quickbelt items.");
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

    private class StaticActionSubscription
    {
        public FieldInfo EventField;
        public Action Handler;
    }

    private sealed class WeaponChangedSubscription : StaticActionSubscription
    {
        public Type ProgressionType;
    }

    private sealed class ManagedQuickbeltFeed
    {
        public FVRQuickBeltSlot Slot;
        public FVRPhysicalObject Object;
    }

    private static void Trace(string message)
    {
        if (instance != null)
        {
            instance.Logger.LogInfo("GunGame Cursed Random trace: " + message);
        }
    }
}
