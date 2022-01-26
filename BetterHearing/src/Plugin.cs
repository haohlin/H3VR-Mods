using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using BepInEx;
using HarmonyLib;
using FistVR;

namespace BetterHearing
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogInfo("Loaded ReleaseMag Successfully!");
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            MethodInfo original = AccessTools.Method(typeof(ClosedBoltWeapon), "ReleaseMag");
            MethodInfo patch = AccessTools.Method(typeof(Plugin), "ReleaseMag_MyPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
        }

        //[HarmonyPatch(typeof(ClosedBoltWeapon), "ReleaseMag")]
        //[HarmonyPrefix]
        public static void ReleaseMag_MyPatch(ClosedBoltWeapon __instance)
        {
            Debug.LogWarning("ReleasingMag_MyPatch");
            bool has_mag = __instance.Magazine != null;
            bool mag_not_full = __instance.Magazine.m_numRounds < __instance.Magazine.m_capacity;
            //Debug.Log("Initial Bolt.CurPos = " + __instance.Bolt.CurPos);
            __instance.Bolt.m_boltZ_current = __instance.Bolt.m_boltZ_lock;
            __instance.Bolt.CurPos = ClosedBolt.BoltPos.Locked;
            __instance.Bolt.Weapon.IsBoltCatchButtonHeld = true;
            //Debug.Log("After mod Bolt.CurPos = " + __instance.Bolt.CurPos);
            //FVRFireArmRound round =  __instance.Chamber.GetRound();
            //__instance.Bolt.BoltEvent_EjectRound();

            Patch ejectround
            FVRFireArmRound round = __result
            if this is ClosedBolt and releasemag pressed:
                mag.addround
                round.destroy

            if (mag_not_full && round != null && has_mag)
            {
                Debug.Log("Chembered round going back into mag");
                __instance.Magazine.AddRound(round, true, true, false);
                Debug.Log(" round before destroy: " + round);
                UnityEngine.Object.Destroy(round.gameObject);
                Debug.Log(" round after destroy: " + round);
            }
            //Debug.Log("EjectRound Bolt.CurPos = " + __instance.Bolt.CurPos);
            __instance.Bolt.BoltEvent_BoltCaught();
            //Debug.Log("BoltCaught Bolt.CurPos = " + __instance.Bolt.CurPos);
            __instance.Bolt.LockBolt();
            //Debug.Log("LockBolt Bolt.CurPos = " + __instance.Bolt.CurPos);
        }
    }
}
