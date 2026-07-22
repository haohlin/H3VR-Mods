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
    private const string RandomGunMethodName = "BTN_TryToSpawnRandomGun";
    private const string RandomGunFieldName = "CurrentlySpawnedRandomGun";
    private const int RandomGunWaitFrames = 120;
    private const int MaxRandomSpawnAttempts = 3;

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
    private RandomSpawnAttempt activeRandomAttempt;
    private RandomSpawnAttempt abandonedRandomAttempt;
    private bool replacementQueued;
    private bool queuedTransition;
    private bool spawningRandomGun;
    private bool missingSpawnerLogged;
    private bool nativeFallbackPending;
    private bool nativeFallbackRequired;
    private bool randomGunRoutineHookInstalled;
    private bool weaponBufferSpawnHookInstalled;

    private void Awake()
    {
        instance = this;
        weaponBufferSpawnHookInstalled = InstallWeaponBufferSpawnHooks();
        randomGunRoutineHookInstalled = InstallRandomGunRoutineHook();
        if (!weaponBufferSpawnHookInstalled)
        {
            Logger.LogWarning("GunGame Cursed Random could not install WeaponBuffer.SpawnAsync hook; using post-spawn fallback.");
        }

        if (!randomGunRoutineHookInstalled)
        {
            Logger.LogError("GunGame Cursed Random could not install Item Spawner completion hook; using GunGame profile equipment.");
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
            harmony = harmony ?? new Harmony("HLin.GunGameCursedRandom");
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

    private bool InstallRandomGunRoutineHook()
    {
        var target = AccessTools.Method(
            typeof(ItemSpawnerV2),
            "SpawnRandomGunRoutine",
            new[] { typeof(FVRObject) });
        var postfix = AccessTools.Method(typeof(Plugin), "SpawnRandomGunRoutinePostfix");
        if (target == null || postfix == null)
        {
            return false;
        }

        try
        {
            harmony = harmony ?? new Harmony("HLin.GunGameCursedRandom");
            harmony.Patch(target, postfix: new HarmonyMethod(postfix) { priority = Priority.Last });
            var patchInfo = Harmony.GetPatchInfo(target);
            if (patchInfo == null || !patchInfo.Postfixes.Any(patch => patch.owner == harmony.Id))
            {
                return false;
            }

            Logger.LogInfo("GunGame Cursed Random installed ItemSpawnerV2.SpawnRandomGunRoutine completion hook.");
            return true;
        }
        catch (Exception exception)
        {
            Logger.LogWarning("GunGame Cursed Random could not patch Item Spawner random-gun routine: " + exception.GetType().Name + ".");
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

        if (plugin.nativeFallbackPending)
        {
            Trace("WeaponBuffer.SpawnAsync allowing one native fallback after exhausted random attempts.");
            return true;
        }

        if (plugin.spawningRandomGun)
        {
            __result = EmptyEnumerator();
            Trace("WeaponBuffer.SpawnAsync suppressing duplicate native placeholder during pending random spawn.");
            return false;
        }

        if (plugin.replacementQueued)
        {
            Trace("WeaponBuffer.SpawnAsync direct hook found queued post-spawn fallback; keeping native fallback.");
            return true;
        }

        var progression = FindProgressionForWeaponBuffer(__instance);
        if (progression == null || !plugin.TryStartRandomSpawn(progression, true))
        {
            Trace("WeaponBuffer.SpawnAsync direct hook could not start random spawn; keeping native fallback.");
            return true;
        }

        plugin.directTransitionProgressionType = progression.GetType();
        __result = EmptyEnumerator();
        Trace("WeaponBuffer.SpawnAsync suppressing native placeholder; vanilla random spawn started.");
        return false;
    }

    private static void SpawnRandomGunRoutinePostfix(ItemSpawnerV2 __instance, FVRObject o, ref IEnumerator __result)
    {
        var plugin = instance;
        var attempt = plugin == null ? null : plugin.activeRandomAttempt;
        if (attempt == null || attempt.RoutineObserved || __instance == null || attempt.Spawner != __instance || __result == null)
        {
            return;
        }

        attempt.RoutineObserved = true;
        attempt.ExpectedFirearm = o;
        __result = TrackRandomGunRoutine(__result, attempt);
    }

    private static IEnumerator TrackRandomGunRoutine(IEnumerator original, RandomSpawnAttempt attempt)
    {
        var disposable = original as IDisposable;
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = original.MoveNext();
                }
                catch (Exception exception)
                {
                    attempt.Error = exception.GetType().Name;
                    attempt.RoutineCompleted = true;
                    Trace("vanilla random routine failed; attempt=" + attempt.Number + "; reason=" + attempt.Error + ".");
                    yield break;
                }

                if (!hasNext)
                {
                    attempt.RoutineCompleted = true;
                    yield break;
                }

                yield return original.Current;
            }
        }
        finally
        {
            var plugin = instance;
            if (plugin != null)
            {
                plugin.OnRandomGunRoutineSettled(attempt);
            }

            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
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

        if (nativeFallbackPending)
        {
            nativeFallbackPending = false;
            directTransitionProgressionType = null;
            Trace("WeaponChangedEvent acknowledged native fallback after exhausted random attempts.");
            StartQueuedReplacement();
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

        if (!TryStartRandomSpawn(progression, false))
        {
            Logger.LogWarning(CursedProfileName + " random API unavailable; keeping current profile equipment.");
            StartQueuedReplacement();
        }
    }

    private void StartQueuedReplacement()
    {
        if (!queuedTransition || replacementQueued || nativeFallbackPending)
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

    private bool TryStartRandomSpawn(object progression, bool requireNativeFallback)
    {
        if (spawningRandomGun)
        {
            Logger.LogWarning("GunGame Cursed Random trace: random spawn already pending; suppressing duplicate GunGame profile spawn.");
            return true;
        }

        if (abandonedRandomAttempt != null)
        {
            Logger.LogWarning(
                "GunGame Cursed Random trace: native random routine is still finishing after a timeout; keeping GunGame profile equipment until it settles.");
            return false;
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

        if (!randomGunRoutineHookInstalled)
        {
            Logger.LogError("Item Spawner random-gun completion hook is unavailable; using GunGame profile weapon.");
            return false;
        }

        missingSpawnerLogged = false;
        nativeFallbackRequired = requireNativeFallback;
        spawningRandomGun = true;
        StartCoroutine(FinishRandomSpawn(progression, spawner));
        return true;
    }

    private IEnumerator FinishRandomSpawn(object progression, ItemSpawnerV2 spawner)
    {
        for (var number = 1; number <= MaxRandomSpawnAttempts; number++)
        {
            var attempt = StartRandomAttempt(spawner, number);
            if (attempt == null)
            {
                Logger.LogWarning(
                    "GunGame Cursed Random trace: random attempt " + number + "/" + MaxRandomSpawnAttempts +
                    " did not start; preserving native GunGame equipment.");
                FailRandomSpawn(progression, "native random API did not start");
                yield break;
            }

            var waitedFrames = 0;
            while (!attempt.RoutineCompleted && waitedFrames < RandomGunWaitFrames)
            {
                waitedFrames++;
                yield return null;
            }

            if (!attempt.RoutineCompleted)
            {
                abandonedRandomAttempt = attempt;
                Logger.LogWarning(
                    "GunGame Cursed Random trace: vanilla random routine timed out after " + waitedFrames +
                    " frames; preserving native GunGame equipment and not retrying an unfinished routine.");
                FailRandomSpawn(progression, "native random routine timed out");
                yield break;
            }

            FVRFireArm gun;
            string reason;
            if (!TryGetAttemptFirearm(spawner, attempt, out gun, out reason))
            {
                RejectRandomAttempt(attempt, reason);
                if (number < MaxRandomSpawnAttempts)
                {
                    yield return null;
                    continue;
                }

                break;
            }

            if (queuedTransition)
            {
                Trace("discarding completed random result for a newer Cursed transition.");
                RejectRandomAttempt(attempt, "superseded by a newer transition");
                CompletePendingRandomSpawn();
                yield break;
            }

            var generatedFeeds = FindAttemptFeeds(attempt, gun);
            FVRPhysicalObject loadedFeed;
            if (!TryLoadValidatedFeed(gun, generatedFeeds, out loadedFeed, out reason))
            {
                RejectRandomAttempt(attempt, reason);
                if (number < MaxRandomSpawnAttempts)
                {
                    yield return null;
                    continue;
                }

                break;
            }

            DestroyGunGameEquipment(progression);
            DestroyTrackedEquipment();
            yield return null;
            if (gun == null)
            {
                Logger.LogWarning("GunGame Cursed Random trace: accepted firearm disappeared during native cleanup; restoring GunGame equipment.");
                RejectRandomAttempt(attempt, "accepted firearm disappeared during native cleanup");
                FailRandomSpawn(progression, "accepted firearm disappeared during native cleanup");
                yield break;
            }

            activeRandomGun = gun.gameObject;
            EquipInGunGameHand(gun, progression == null ? null : progression.GetType().Assembly);
            MoveSpareFeedsToQuickbelt(loadedFeed, gun, progression == null ? null : progression.GetType().Assembly);
            LogRandomLoadout(gun, generatedFeeds.Concat(new[] { loadedFeed }).Distinct(), loadedFeed);
            CompletePendingRandomSpawn();
            yield break;
        }

        Logger.LogError(
            "GunGame Cursed Random trace: all " + MaxRandomSpawnAttempts +
            " completed random attempts were invalid; preserving native GunGame equipment.");
        FailRandomSpawn(progression, "all completed random attempts were invalid");
    }

    private RandomSpawnAttempt StartRandomAttempt(ItemSpawnerV2 spawner, int number)
    {
        try
        {
            var attempt = new RandomSpawnAttempt
            {
                Number = number,
                Spawner = spawner,
                PreviousGun = randomGunField.GetValue(spawner) as GameObject,
                BeforePhysicalObjectIds = CapturePhysicalObjects()
            };
            activeRandomAttempt = attempt;
            Logger.LogInfo(
                "GunGame Cursed Random trace: invoking vanilla random API; attempt=" + number + "/" +
                MaxRandomSpawnAttempts + "; previousGun=" + NameOf(attempt.PreviousGun) +
                "; physicalObjectsBefore=" + attempt.BeforePhysicalObjectIds.Count + ".");
            randomGunMethod.Invoke(spawner, null);
            if (!attempt.RoutineObserved)
            {
                attempt.RoutineCompleted = true;
                attempt.Error = "random routine was not started";
            }

            return attempt;
        }
        catch (Exception exception)
        {
            activeRandomAttempt = null;
            Logger.LogWarning("Could not call Item Spawner random-gun API: " + exception.GetType().Name + ".");
            return null;
        }
    }

    private bool TryGetAttemptFirearm(
        ItemSpawnerV2 spawner,
        RandomSpawnAttempt attempt,
        out FVRFireArm firearm,
        out string reason)
    {
        firearm = null;
        reason = attempt.Error;
        if (!string.IsNullOrEmpty(reason))
        {
            return false;
        }

        try
        {
            var randomGun = CaptureAttemptResult(spawner, attempt);
            if (randomGun == null || randomGun == attempt.PreviousGun)
            {
                reason = "no new random firearm result";
                return false;
            }

            firearm = randomGun.GetComponent<FVRFireArm>();
            if (firearm == null)
            {
                reason = "result is not an FVRFireArm";
                return false;
            }

            if (firearm.ObjectWrapper == null || !string.Equals(firearm.ObjectWrapper.Category.ToString(), "Firearm", StringComparison.Ordinal))
            {
                reason = "firearm wrapper/category is invalid";
                return false;
            }

            if (attempt.ExpectedFirearm != null &&
                !string.Equals(firearm.ObjectWrapper.ItemID, attempt.ExpectedFirearm.ItemID, StringComparison.Ordinal))
            {
                reason = "result firearm does not match the vanilla random selection";
                return false;
            }

            FVRObject registered;
            if (IM.OD == null || !IM.OD.TryGetValue(firearm.ObjectWrapper.ItemID, out registered) || registered == null)
            {
                reason = "firearm wrapper is not registered";
                return false;
            }
        }
        catch (Exception exception)
        {
            firearm = null;
            reason = "firearm validation failed: " + exception.GetType().Name;
            return false;
        }

        Logger.LogInfo(
            "GunGame Cursed Random trace: completed vanilla random result; attempt=" + attempt.Number + "/" +
            MaxRandomSpawnAttempts + "; gun=" + NameOf(firearm) + ".");
        return true;
    }

    private GameObject CaptureAttemptResult(ItemSpawnerV2 spawner, RandomSpawnAttempt attempt)
    {
        if (attempt.ResultGun != null)
        {
            return attempt.ResultGun;
        }

        var result = spawner == null || randomGunField == null ? null : randomGunField.GetValue(spawner) as GameObject;
        if (result == null || result == attempt.PreviousGun || !ResultBelongsToAttempt(result, attempt))
        {
            return result;
        }

        attempt.ResultGun = result;
        return result;
    }

    private static bool ResultBelongsToAttempt(GameObject result, RandomSpawnAttempt attempt)
    {
        var physicalObject = result == null ? null : result.GetComponent<FVRPhysicalObject>();
        return physicalObject != null && physicalObject.ObjectWrapper != null && attempt.ExpectedFirearm != null &&
               string.Equals(physicalObject.ObjectWrapper.ItemID, attempt.ExpectedFirearm.ItemID, StringComparison.Ordinal);
    }

    private static List<FVRPhysicalObject> FindAttemptFeeds(RandomSpawnAttempt attempt, FVRFireArm gun)
    {
        var resultRoot = attempt.ResultGun;
        foreach (var feed in CapturePhysicalObjectsList()
            .Where(item => item != null &&
                           item != gun &&
                           !attempt.BeforePhysicalObjectIds.Contains(item.GetInstanceID()) &&
                           IsNearSpawnerSmallPoint(attempt.Spawner, item) &&
                           (resultRoot == null || !item.transform.IsChildOf(resultRoot.transform)) &&
                           IsReusableFeed(item))
            .OrderBy(FeedSortOrder)
            .ToList())
        {
            if (!attempt.CandidateFeeds.Contains(feed))
            {
                attempt.CandidateFeeds.Add(feed);
            }
        }

        return attempt.CandidateFeeds;
    }

    private static bool IsNearSpawnerSmallPoint(ItemSpawnerV2 spawner, FVRPhysicalObject item)
    {
        if (spawner == null || item == null || spawner.SpawnPoints_Small == null)
        {
            return false;
        }

        return spawner.SpawnPoints_Small.Any(point =>
            point != null && (item.transform.position - point.position).sqrMagnitude <= 1f);
    }

    private bool TryLoadValidatedFeed(
        FVRFireArm gun,
        IEnumerable<FVRPhysicalObject> generatedFeeds,
        out FVRPhysicalObject loadedFeed,
        out string reason)
    {
        loadedFeed = null;
        reason = null;
        foreach (var feed in generatedFeeds)
        {
            if (TryLoadFeed(gun, feed, out reason))
            {
                loadedFeed = feed;
                return true;
            }
        }

        var attachedFeed = FindLoadedAttachedFeed(gun);
        if (attachedFeed != null)
        {
            loadedFeed = attachedFeed;
            reason = null;
            Trace("using vanilla-loaded attached feed=" + NameOf(attachedFeed) + "; gun=" + NameOf(gun) + ".");
            return true;
        }

        if (string.IsNullOrEmpty(reason))
        {
            reason = "native random routine created no compatible loaded magazine, clip, speedloader, or cartridge";
        }

        return false;
    }

    private static FVRPhysicalObject FindLoadedAttachedFeed(FVRFireArm gun)
    {
        return gun == null
            ? null
            : gun.GetComponentsInChildren<FVRPhysicalObject>()
                .FirstOrDefault(feed => IsReusableFeed(feed) && IsLoadedFeed(gun, feed));
    }

    private static bool TryLoadFeed(FVRFireArm gun, FVRPhysicalObject feed, out string reason)
    {
        reason = null;
        if (!IsReusableFeed(feed))
        {
            reason = "feed wrapper/category is invalid";
            return false;
        }

        try
        {
            var magazine = feed as FVRFireArmMagazine;
            if (magazine != null)
            {
                magazine.Load(gun);
                FillStandardFeed(magazine);
                if (magazine.FireArm == gun && gun.Magazine == magazine && magazine.m_numRounds > 0)
                {
                    Trace("loaded validated magazine=" + NameOf(magazine) + "; gun=" + NameOf(gun) + ".");
                    return true;
                }

                reason = "magazine did not load a usable round into firearm";
                return false;
            }

            var clip = feed as FVRFireArmClip;
            if (clip != null)
            {
                clip.Load(gun);
                FillStandardFeed(clip);
                if (clip.FireArm == gun && clip.m_numRounds > 0)
                {
                    Trace("loaded validated clip=" + NameOf(clip) + "; gun=" + NameOf(gun) + ".");
                    return true;
                }

                reason = "clip did not load a usable round into firearm";
                return false;
            }

            var speedloader = feed as Speedloader;
            var cylinder = gun.GetComponentInChildren<RevolverCylinder>();
            if (speedloader != null && cylinder != null)
            {
                FillStandardFeed(speedloader);
                cylinder.LoadFromSpeedLoader(speedloader);
                if (gun.GetChambers().Any(chamber => chamber != null && chamber.GetRound() != null))
                {
                    Trace("loaded validated speedloader=" + NameOf(speedloader) + "; gun=" + NameOf(gun) + ".");
                    return true;
                }

                reason = "speedloader did not load any chamber";
                return false;
            }

            var round = feed as FVRFireArmRound;
            if (round != null)
            {
                var chamber = gun.GetChambers().FirstOrDefault(item => item != null && item.GetRound() == null) ??
                              gun.GetChambers().FirstOrDefault(item => item != null);
                if (chamber == null)
                {
                    reason = "firearm has no usable chamber";
                    return false;
                }

                chamber.SetRound(round, false);
                if (chamber.GetRound() == round)
                {
                    Trace("loaded validated round=" + NameOf(round) + "; gun=" + NameOf(gun) + ".");
                    return true;
                }

                reason = "round did not load into firearm chamber";
                return false;
            }
        }
        catch (Exception exception)
        {
            reason = "feed load failed: " + exception.GetType().Name;
            return false;
        }

        reason = "unsupported feed type";
        return false;
    }

    private static bool IsLoadedFeed(FVRFireArm gun, FVRPhysicalObject feed)
    {
        var magazine = feed as FVRFireArmMagazine;
        if (magazine != null)
        {
            return magazine.FireArm == gun && gun.Magazine == magazine && magazine.m_numRounds > 0;
        }

        var clip = feed as FVRFireArmClip;
        if (clip != null)
        {
            return clip.FireArm == gun && clip.m_numRounds > 0;
        }

        var round = feed as FVRFireArmRound;
        return round != null && gun.GetChambers().Any(chamber => chamber != null && chamber.GetRound() == round);
    }

    private void RejectRandomAttempt(RandomSpawnAttempt attempt, string reason)
    {
        CaptureAttemptResult(attempt.Spawner, attempt);
        if (attempt.ResultGun != null)
        {
            FindAttemptFeeds(attempt, attempt.ResultGun.GetComponent<FVRFireArm>());
        }
        Logger.LogWarning(
            "GunGame Cursed Random trace: rejected random attempt " + attempt.Number + "/" +
            MaxRandomSpawnAttempts + "; gun=" + NameOf(attempt.ResultGun) + "; reason=" + reason + ".");
        var resultGun = attempt.ResultGun;
        foreach (var feed in attempt.CandidateFeeds)
        {
            if (feed != null && (resultGun == null || !feed.transform.IsChildOf(resultGun.transform)))
            {
                DestroyGeneratedFeed(feed);
            }
        }

        if (resultGun != null)
        {
            DestroyGeneratedObject(resultGun);
        }
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

    private static void DestroyGeneratedObject(GameObject item)
    {
        if (item == null)
        {
            return;
        }

        var physicalObject = item.GetComponent<FVRPhysicalObject>();
        if (physicalObject != null)
        {
            physicalObject.ForceBreakInteraction();
        }

        UnityEngine.Object.Destroy(item);
    }

    private static bool IsFeed(FVRPhysicalObject item)
    {
        return item is FVRFireArmMagazine || item is FVRFireArmClip || item is Speedloader || item is FVRFireArmRound;
    }

    private static bool IsReusableFeed(FVRPhysicalObject item)
    {
        return IsFeed(item) && item.ObjectWrapper != null && IsFeedCategory(item.ObjectWrapper.Category.ToString());
    }

    private static bool IsFeedCategory(string category)
    {
        return category == "Magazine" || category == "Clip" || category == "SpeedLoader" || category == "Cartridge";
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
            if (speedloader != null && speedloader.Chambers != null && speedloader.Chambers.Count > 0 &&
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
        if (!AM.SRoundDisplayDataDic.TryGetValue(roundType, out var display) || display.Classes == null || !display.Classes.Any())
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
            if (!IsReusableFeed(spare))
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

    private void MoveSpareFeedsToQuickbelt(
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
        Trace(
            "quickbelt: candidates=[" +
            string.Join(", ", candidateSlots.Select(candidate => candidate.name + "=" + NameOf(candidate.CurObject)).ToArray()) +
            "].");
        var slot = candidateSlots.FirstOrDefault(candidate => candidate.CurObject == null);
        var spare = slot == null ? null : SpawnQuickbeltSpare(loadedFeed, gun);

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

        Trace(
            "quickbelt: selectedSlot=" + (slot == null ? "none" : slot.name) +
            "; placed=" + placed +
            "; preservedNativeLooseFeeds=true.");
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
        try
        {
            GM.CurrentMovementManager.Hands[handIndex].RetrieveObject(gun);
        }
        catch (Exception exception)
        {
            Trace("hand equip failed: gun=" + NameOf(gun) + "; reason=" + exception.GetType().Name + ".");
        }
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
        try
        {
            var clearEquipment = progression == null
                ? null
                : progression.GetType().GetMethod("DestroyOldEq", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clearEquipment == null)
            {
                Trace("cleanup: native DestroyOldEq unavailable.");
                return;
            }

            clearEquipment.Invoke(progression, null);
            Trace("cleanup: native DestroyOldEq ran after a validated random loadout.");
        }
        catch (Exception exception)
        {
            Trace("cleanup: native DestroyOldEq failed; reason=" + exception.GetType().Name + ".");
        }
    }

    private void CompletePendingRandomSpawn()
    {
        spawningRandomGun = false;
        directTransitionProgressionType = null;
        activeRandomAttempt = null;
        nativeFallbackRequired = false;
        if (!nativeFallbackPending)
        {
            StartQueuedReplacement();
        }
    }

    private void FailRandomSpawn(object progression, string reason)
    {
        Trace("preserving GunGame equipment after random spawn failure; reason=" + reason + "; directFallback=" + nativeFallbackRequired + ".");
        if (nativeFallbackRequired)
        {
            RestoreNativeFallback(progression);
        }

        CompletePendingRandomSpawn();
    }

    private void OnRandomGunRoutineSettled(RandomSpawnAttempt attempt)
    {
        if (abandonedRandomAttempt != attempt)
        {
            return;
        }

        CaptureAttemptResult(attempt.Spawner, attempt);
        if (attempt.ResultGun != null)
        {
            FindAttemptFeeds(attempt, attempt.ResultGun.GetComponent<FVRFireArm>());
        }
        RejectRandomAttempt(attempt, "late native random routine result after timeout");
        abandonedRandomAttempt = null;
        Trace("late native random routine settled; random generation re-enabled for later GunGame transitions.");
        StartQueuedReplacement();
    }

    private bool RestoreNativeFallback(object progression)
    {
        try
        {
            var spawnAndEquip = progression == null
                ? null
                : progression.GetType().GetMethod(
                    "SpawnAndEquip",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool) },
                    null);
            if (spawnAndEquip == null)
            {
                Trace("native fallback unavailable after random spawn failure.");
                return false;
            }

            nativeFallbackPending = true;
            spawnAndEquip.Invoke(progression, new object[] { false });
            Trace("requested one native GunGame fallback after random spawn failure.");
            return true;
        }
        catch (Exception exception)
        {
            nativeFallbackPending = false;
            Trace("native fallback failed; reason=" + exception.GetType().Name + ".");
            return false;
        }
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

    private sealed class RandomSpawnAttempt
    {
        public readonly List<FVRPhysicalObject> CandidateFeeds = new List<FVRPhysicalObject>();
        public HashSet<int> BeforePhysicalObjectIds;
        public string Error;
        public FVRObject ExpectedFirearm;
        public int Number;
        public GameObject PreviousGun;
        public GameObject ResultGun;
        public bool RoutineCompleted;
        public bool RoutineObserved;
        public ItemSpawnerV2 Spawner;
    }

    private static void Trace(string message)
    {
        if (instance != null)
        {
            instance.Logger.LogInfo("GunGame Cursed Random trace: " + message);
        }
    }
}
