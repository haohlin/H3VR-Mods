using System.Reflection;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using FistVR;
//using RUST.Steamworks;

namespace BetterSosigSpawner
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-Better-SosigSpawner", PluginInfo.PLUGIN_NAME, "1.0.3")]
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

        private static bool UpdateInteraction_SpawnSosig_MyPatch(SosigSpawner __instance, FVRViveHand hand)
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
            Vector3 position = __instance.gameObject.transform.position;
            if (__instance.m_hasTriggeredUpSinceBegin && hand.Input.TriggerDown)
            {
                if (__instance.m_canSpawn_Sosig)
                {
                    Vector3 a = __instance.m_sosigSpawn_Point - position;
                    a.y = 0f;
                    SM.PlayGenericSound(__instance.AudEvent_Spawn, position);
                    if (__instance.SpawnerGroups[__instance.m_spawn_group].IsFurniture)
                    {
                        UnityEngine.Object.Instantiate<GameObject>(__instance.SpawnerGroups[__instance.m_spawn_group].Furnitures[__instance.m_spawn_template], __instance.m_sosigSpawn_Point + Vector3.up * 0.25f, Quaternion.LookRotation(-a, Vector3.up));
                        return false;
                    }
                    SosigEnemyTemplate template = __instance.SpawnerGroups[__instance.m_spawn_group].Templates[__instance.m_spawn_template];
                    __instance.SpawnSosigWithTemplate(template, __instance.m_sosigSpawn_Point, -a);
                    return false;
                }
                else
                {
                    SM.PlayGenericSound(__instance.AudEvent_Fail, position);
                }
            }
            return false;
        }
    }
}
