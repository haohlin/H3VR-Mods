using System.Reflection;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using FistVR;

namespace BetterSosigSpawner
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-Better-SosigSpawner", PluginInfo.PLUGIN_NAME, "1.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogInfo("Loaded Better SosigSpawner Successfully!");
            Harmony harmony = new Harmony("HLin-Better-SosigSpawner");

            MethodInfo original1 = AccessTools.Method(typeof(SosigSpawner), "PageUpdate_SpawnSosig");
            MethodInfo patch1 = AccessTools.Method(typeof(Plugin), "PageUpdate_SpawnSosig_MyPatch");
            harmony.Patch(original1, new HarmonyMethod(patch1));

            MethodInfo original2 = AccessTools.Method(typeof(SosigSpawner), "UpdateInteraction_SpawnSosig");
            MethodInfo patch2 = AccessTools.Method(typeof(Plugin), "UpdateInteraction_SpawnSosig_MyPatch");
            harmony.Patch(original2, new HarmonyMethod(patch2));
        }

        public static bool PageUpdate_SpawnSosig_MyPatch(SosigSpawner __instance)
        {
            Vector3 zero = Vector3.zero;
            Physics.Raycast(__instance.Muzzle.position, __instance.Muzzle.forward, out __instance.m_hit, __instance.Range_PlacementBeam, __instance.LM_PlacementBeam, QueryTriggerInteraction.Ignore);
            __instance.PlacementBeam1.gameObject.SetActive(true);
            __instance.PlacementBeam2.gameObject.SetActive(false);
            __instance.PlacementBeam1.localScale = new Vector3(0.005f, 0.005f, __instance.m_hit.distance);
            __instance.PlacementReticle.gameObject.SetActive(true);
            __instance.PlacementReticle_Valid.SetActive(true);
            __instance.PlacementReticle_Invalid.SetActive(false);
            __instance.m_canSpawn_Sosig = true;
            __instance.m_sosigSpawn_Point = __instance.m_hit.point;
            __instance.PlacementReticle.position = __instance.m_hit.point + Vector3.up * 0.01f;
            return false;
        }

        private static void UpdateInteraction_SpawnSosig_MyPatch(SosigSpawner __instance, FVRViveHand hand)
        {
            Physics.Raycast(__instance.Muzzle.position, __instance.Muzzle.forward, out __instance.m_hit, 3000f, __instance.LM_PlacementBeam, QueryTriggerInteraction.Ignore);            
            __instance.PlacementReticle.gameObject.SetActive(true);
            __instance.PlacementReticle_Valid.SetActive(true);
            __instance.PlacementReticle_Invalid.SetActive(false);
            __instance.PlacementBeam1.gameObject.SetActive(true);
            __instance.PlacementBeam2.gameObject.SetActive(false);
            __instance.PlacementBeam1.localScale = new Vector3(0.005f, 0.005f, __instance.m_hit.distance);
            __instance.m_canSpawn_Sosig = true;
            __instance.m_sosigSpawn_Point = __instance.m_hit.point;
            __instance.PlacementReticle.position = __instance.m_hit.point + Vector3.up * 0.01f;
        }
    }
}
