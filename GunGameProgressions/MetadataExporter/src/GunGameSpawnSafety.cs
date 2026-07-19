using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FistVR;
using HarmonyLib;
using UnityEngine;

namespace HLin.GunGameProgressions;

public static class GunGameSpawnSafetyPolicy
{
    public static bool HasExpectedCategory(string role, string category)
    {
        if (role == "Gun")
        {
            return category == "Firearm";
        }

        if (role == "Feed")
        {
            return category == "Magazine" ||
                category == "Clip" ||
                category == "SpeedLoader" ||
                category == "Cartridge";
        }

        return role != "Extra" || category == "Attachment";
    }
}

public sealed class GunGameSpawnSafety
{
    private static GunGameSpawnSafety active;

    private readonly MonoBehaviour host;
    private readonly Action<string> trace;
    private bool skipQueued;
    private object queuedProgression;
    private int queuedWeaponId;

    public GunGameSpawnSafety(MonoBehaviour host, Action<string> trace)
    {
        this.host = host;
        this.trace = trace;
    }

    public bool Install(Harmony harmony)
    {
        var progressionType = AccessTools.TypeByName("GunGame.Scripts.Progression");
        var weaponBufferType = AccessTools.TypeByName("GunGame.Scripts.Weapons.WeaponBuffer");
        var spawnAndEquip = progressionType == null
            ? null
            : progressionType.GetMethod(
                "SpawnAndEquip",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null);
        var spawnAsync = weaponBufferType == null
            ? null
            : FindMethod(weaponBufferType, "SpawnAsync", 2);
        var spawnPrefix = AccessTools.Method(typeof(GunGameSpawnSafety), "SpawnAndEquipPrefix");
        var spawnPostfix = AccessTools.Method(typeof(GunGameSpawnSafety), "SpawnAndEquipPostfix");
        var spawnFinalizer = AccessTools.Method(typeof(GunGameSpawnSafety), "SpawnAndEquipFinalizer");
        var bufferPrefix = AccessTools.Method(typeof(GunGameSpawnSafety), "SpawnAsyncPrefix");
        var bufferPostfix = AccessTools.Method(typeof(GunGameSpawnSafety), "SpawnAsyncPostfix");
        var bufferFinalizer = AccessTools.Method(typeof(GunGameSpawnSafety), "SpawnAsyncFinalizer");
        if (spawnAndEquip == null || spawnAsync == null || spawnPrefix == null || spawnPostfix == null || spawnFinalizer == null || bufferPrefix == null || bufferPostfix == null || bufferFinalizer == null)
        {
            return false;
        }

        try
        {
            active = this;
            harmony.Patch(
                spawnAndEquip,
                prefix: new HarmonyMethod(spawnPrefix),
                postfix: new HarmonyMethod(spawnPostfix),
                finalizer: new HarmonyMethod(spawnFinalizer));
            harmony.Patch(
                spawnAsync,
                prefix: new HarmonyMethod(bufferPrefix),
                postfix: new HarmonyMethod(bufferPostfix),
                finalizer: new HarmonyMethod(bufferFinalizer));

            var spawnPatchInfo = Harmony.GetPatchInfo(spawnAndEquip);
            var bufferPatchInfo = Harmony.GetPatchInfo(spawnAsync);
            return spawnPatchInfo != null && bufferPatchInfo != null;
        }
        catch
        {
            active = null;
            return false;
        }
    }

    public static void Clear()
    {
        active = null;
    }

    private static bool SpawnAndEquipPrefix(object __instance)
    {
        if (active == null)
        {
            return true;
        }

        string reason;
        int weaponId;
        if (active.TryValidateCurrentLoadout(__instance, out weaponId, out reason))
        {
            return true;
        }

        active.QueueSkip(__instance, weaponId, reason);
        return false;
    }

    private static Exception SpawnAndEquipFinalizer(object __instance, Exception __exception)
    {
        if (__exception == null || active == null)
        {
            return __exception;
        }

        active.QueueSkip(__instance, ReadCurrentWeaponId(__instance), "spawn exception " + __exception.GetType().Name);
        return null;
    }

    private static void SpawnAndEquipPostfix(object __instance)
    {
        if (active != null)
        {
            active.TryMountGeneratedOptic(__instance);
        }
    }

    private static bool SpawnAsyncPrefix(object __instance, object __1, ref IEnumerator __result)
    {
        if (active == null)
        {
            return true;
        }

        string reason;
        if (active.TryValidateGunData(__1, out reason))
        {
            return true;
        }

        active.QueueSkipFromWeaponBuffer(__instance, "invalid pre-buffer: " + reason);
        active.trace("ignored invalid GunGame pre-buffer: " + reason);
        __result = EmptyEnumerator();
        return false;
    }

    private static void SpawnAsyncPostfix(object __instance, ref IEnumerator __result)
    {
        if (active != null && __result != null)
        {
            __result = GuardSpawnAsync(__instance, __result);
        }
    }

    private static Exception SpawnAsyncFinalizer(object __instance, Exception __exception, ref IEnumerator __result)
    {
        if (__exception == null || active == null)
        {
            return __exception;
        }

        active.QueueSkipFromWeaponBuffer(__instance, "buffer spawn exception " + __exception.GetType().Name);
        __result = EmptyEnumerator();
        return null;
    }

    private bool TryValidateCurrentLoadout(object progression, out int weaponId, out string reason)
    {
        weaponId = ReadCurrentWeaponId(progression);
        reason = null;
        try
        {
            var gameSettingsType = AccessTools.TypeByName("GunGame.Scripts.Options.GameSettings");
            var currentPool = gameSettingsType == null
                ? null
                : gameSettingsType.GetProperty(
                    "CurrentPool",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetValue(null, null);
            var getWeapon = currentPool == null ? null : FindMethod(currentPool.GetType(), "GetWeapon", 1);
            if (weaponId < 0 || currentPool == null || getWeapon == null)
            {
                return true;
            }

            var gunData = getWeapon.Invoke(currentPool, new object[] { weaponId });
            return TryValidateGunData(gunData, out reason);
        }
        catch
        {
            // Let GunGame retain its normal path when it is not yet initialized.
            return true;
        }
    }

    private bool TryValidateGunData(object gunData, out string reason)
    {
        reason = null;
        if (gunData == null)
        {
            reason = "missing gun data";
            return false;
        }

        Dictionary<string, FVRObject> objects;
        try
        {
            objects = IM.OD;
        }
        catch
        {
            return true;
        }

        if (objects == null)
        {
            return true;
        }

        if (!HasExpectedObject(objects, ReadField(gunData, "GunName"), "Gun", out reason))
        {
            return false;
        }

        var feedId = ReadField(gunData, "MagName");
        if (!string.IsNullOrEmpty(feedId) && !HasExpectedObject(objects, feedId, "Feed", out reason))
        {
            return false;
        }

        var extraId = ReadField(gunData, "Extra");
        return string.IsNullOrEmpty(extraId) || HasExpectedObject(objects, extraId, "Extra", out reason);
    }

    private void TryMountGeneratedOptic(object progression)
    {
        try
        {
            var weaponId = ReadCurrentWeaponId(progression);
            var currentPool = GetCurrentPool();
            if (weaponId < 0 || !IsGeneratedRuntimePool(currentPool))
            {
                return;
            }

            var gunData = GetCurrentGunData(currentPool, weaponId);
            var opticId = ReadField(gunData, "Extra");
            if (string.IsNullOrEmpty(opticId))
            {
                return;
            }

            var equipment = GetCurrentEquipment(progression);
            var firearm = FindFirearm(equipment, ReadField(gunData, "GunName"));
            var optic = FindAttachment(equipment, opticId);
            if (firearm == null || optic == null || optic.curMount != null)
            {
                return;
            }

            if (TryAttachOptic(firearm, optic))
            {
                return;
            }

            trace("could not mount generated optic for loadout " + weaponId + ".");
        }
        catch (Exception exception)
        {
            trace("could not mount generated optic: " + exception.GetType().Name);
        }
    }

    private static object GetCurrentPool()
    {
        try
        {
            var gameSettingsType = AccessTools.TypeByName("GunGame.Scripts.Options.GameSettings");
            var property = gameSettingsType == null
                ? null
                : gameSettingsType.GetProperty(
                    "CurrentPool",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? null : property.GetValue(null, null);
        }
        catch
        {
            return null;
        }
    }

    private static object GetCurrentGunData(object currentPool, int weaponId)
    {
        try
        {
            var getWeapon = currentPool == null ? null : FindMethod(currentPool.GetType(), "GetWeapon", 1);
            return getWeapon == null ? null : getWeapon.Invoke(currentPool, new object[] { weaponId });
        }
        catch
        {
            return null;
        }
    }

    private static bool IsGeneratedRuntimePool(object currentPool)
    {
        var name = ReadStringProperty(currentPool, "Name");
        return name.StartsWith("Runtime ", StringComparison.Ordinal);
    }

    private static string ReadStringProperty(object value, string propertyName)
    {
        try
        {
            var property = value == null
                ? null
                : value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? string.Empty : property.GetValue(value, null) as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IList GetCurrentEquipment(object progression)
    {
        var field = progression == null
            ? null
            : progression.GetType().GetField("_currentEquipment", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field == null ? null : field.GetValue(progression) as IList;
    }

    private static FVRFireArm FindFirearm(IList equipment, string firearmId)
    {
        foreach (var item in equipment ?? new object[0])
        {
            var gameObject = item as GameObject;
            var firearm = gameObject == null ? null : gameObject.GetComponent<FVRFireArm>();
            if (firearm != null && HasObjectId(firearm, firearmId))
            {
                return firearm;
            }
        }

        return null;
    }

    private static FVRFireArmAttachment FindAttachment(IList equipment, string opticId)
    {
        foreach (var item in equipment ?? new object[0])
        {
            var gameObject = item as GameObject;
            var attachment = gameObject == null ? null : gameObject.GetComponent<FVRFireArmAttachment>();
            if (attachment != null && HasObjectId(attachment, opticId))
            {
                return attachment;
            }
        }

        return null;
    }

    private static bool HasObjectId(FVRPhysicalObject item, string objectId)
    {
        return item != null &&
            item.ObjectWrapper != null &&
            string.Equals(item.ObjectWrapper.ItemID, objectId, StringComparison.Ordinal);
    }

    private static bool TryAttachOptic(FVRPhysicalObject parent, FVRFireArmAttachment optic)
    {
        var mount = FindCompatibleOpticMount(parent, optic);
        if (mount == null)
        {
            return false;
        }

        optic.ClearQuickbeltState();
        optic.AttachToMount(mount, playSound: false);
        return true;
    }

    private static FVRFireArmAttachmentMount FindCompatibleOpticMount(
        FVRPhysicalObject parent,
        FVRFireArmAttachment attachment)
    {
        if (parent == null || attachment == null || !IsOpticMountType(attachment.Type))
        {
            return null;
        }

        foreach (var mount in parent.AttachmentMounts ?? new List<FVRFireArmAttachmentMount>())
        {
            if (mount == null ||
                mount.Type != attachment.Type ||
                !IsOpticMountType(mount.Type) ||
                !IsTopSightingMount(parent, mount) ||
                !mount.isMountableOn(attachment))
            {
                continue;
            }

            return mount;
        }

        return null;
    }

    private static bool IsTopSightingMount(FVRPhysicalObject parent, FVRFireArmAttachmentMount mount)
    {
        if (!OpticMountPolicy.RequiresTopSightingOrientation(mount.Type.ToString()))
        {
            return true;
        }

        return Vector3.Angle(mount.transform.up, parent.transform.up) < 46f;
    }

    private static bool IsOpticMountType(FVRFireArmAttachementMountType mountType)
    {
        return OpticMountPolicy.IsOpticMountType(mountType.ToString());
    }

    private void QueueSkip(object progression, int weaponId, string reason)
    {
        if (progression == null || (skipQueued && ReferenceEquals(queuedProgression, progression)))
        {
            return;
        }

        skipQueued = true;
        queuedProgression = progression;
        queuedWeaponId = weaponId;
        ClearWeaponBuffer(progression);
        trace("skipping invalid GunGame loadout " + DescribeCurrentLoadout(progression, weaponId) + ": " + reason);
        host.StartCoroutine(AdvancePastInvalidWeapon());
    }

    private void QueueSkipFromWeaponBuffer(object weaponBuffer, string reason)
    {
        var progression = FindProgressionForWeaponBuffer(weaponBuffer);
        if (progression == null)
        {
            trace("could not advance invalid GunGame pre-buffer: " + reason);
            return;
        }

        QueueSkip(progression, ReadCurrentWeaponId(progression), reason);
    }

    private static object FindProgressionForWeaponBuffer(object weaponBuffer)
    {
        try
        {
            var progressionType = AccessTools.TypeByName("GunGame.Scripts.Progression");
            if (progressionType == null)
            {
                return null;
            }

            var bufferComponent = weaponBuffer as Component;
            var progression = bufferComponent == null
                ? null
                : bufferComponent.GetComponentInParent(progressionType);
            return progression ?? UnityEngine.Object.FindObjectOfType(progressionType);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerator GuardSpawnAsync(object weaponBuffer, IEnumerator original)
    {
        var disposable = original as IDisposable;
        try
        {
            while (true)
            {
                object current;
                Exception exception;
                if (!TryMoveNext(original, out current, out exception))
                {
                    if (exception != null && active != null)
                    {
                        active.QueueSkipFromWeaponBuffer(weaponBuffer, "buffer spawn exception " + exception.GetType().Name);
                    }

                    yield break;
                }

                yield return current;
            }
        }
        finally
        {
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }

    private static bool TryMoveNext(IEnumerator original, out object current, out Exception exception)
    {
        current = null;
        exception = null;
        try
        {
            if (original == null || !original.MoveNext())
            {
                return false;
            }

            current = original.Current;
            return true;
        }
        catch (Exception caught)
        {
            exception = caught;
            return false;
        }
    }

    private static string DescribeCurrentLoadout(object progression, int weaponId)
    {
        var gunData = GetCurrentGunData(GetCurrentPool(), weaponId);
        return "weapon " + weaponId + " (gun=" + ReadField(gunData, "GunName") +
            ", feed=" + ReadField(gunData, "MagName") +
            ", optic=" + ReadField(gunData, "Extra") + ")";
    }

    private IEnumerator AdvancePastInvalidWeapon()
    {
        yield return null;
        var progression = queuedProgression;
        var weaponId = queuedWeaponId;
        skipQueued = false;
        queuedProgression = null;

        if (progression == null || ReadCurrentWeaponId(progression) != weaponId)
        {
            yield break;
        }

        var promote = FindMethod(progression.GetType(), "Promote", 0);
        if (promote == null)
        {
            trace("could not advance past invalid GunGame loadout.");
            yield break;
        }

        try
        {
            promote.Invoke(progression, null);
        }
        catch (Exception exception)
        {
            trace("could not advance past invalid GunGame loadout: " + exception.GetType().Name);
        }
    }

    private static bool HasExpectedObject(
        IDictionary<string, FVRObject> objects,
        string objectId,
        string role,
        out string reason)
    {
        reason = null;
        FVRObject item;
        if (string.IsNullOrEmpty(objectId) || !objects.TryGetValue(objectId, out item) || item == null)
        {
            reason = role + " id is unavailable";
            return false;
        }

        if (!GunGameSpawnSafetyPolicy.HasExpectedCategory(role, item.Category.ToString()))
        {
            reason = role + " id has category " + item.Category;
            return false;
        }

        return true;
    }

    private static void ClearWeaponBuffer(object progression)
    {
        try
        {
            var component = progression as Component;
            var bufferType = AccessTools.TypeByName("GunGame.Scripts.Weapons.WeaponBuffer");
            var buffer = component == null || bufferType == null ? null : component.GetComponent(bufferType);
            var clearBuffer = buffer == null ? null : FindMethod(buffer.GetType(), "ClearBuffer", 0);
            if (clearBuffer != null)
            {
                clearBuffer.Invoke(buffer, null);
            }
        }
        catch
        {
            // A failed cleanup must not turn an invalid generated ID into a game crash.
        }
    }

    private static int ReadCurrentWeaponId(object progression)
    {
        try
        {
            var property = progression == null
                ? null
                : progression.GetType().GetProperty("CurrentWeaponId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? -1 : Convert.ToInt32(property.GetValue(progression, null));
        }
        catch
        {
            return -1;
        }
    }

    private static string ReadField(object value, string fieldName)
    {
        var field = value == null
            ? null
            : value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field == null ? string.Empty : field.GetValue(value) as string ?? string.Empty;
    }

    private static MethodInfo FindMethod(Type type, string methodName, int parameterCount)
    {
        if (type == null)
        {
            return null;
        }

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.Name == methodName && method.GetParameters().Length == parameterCount)
            {
                return method;
            }
        }

        return null;
    }

    private static IEnumerator EmptyEnumerator()
    {
        yield break;
    }
}
