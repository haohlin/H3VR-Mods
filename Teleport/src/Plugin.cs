using System.Reflection;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using FistVR;

namespace Teleport
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-Teleport", PluginInfo.PLUGIN_NAME, "1.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Logger.LogInfo("Loaded Teleport Successfully!");
            Harmony harmony = new Harmony("HLin-Teleport");

            MethodInfo original = AccessTools.Method(typeof(FVRMovementManager), "FindValidPointCurved");
            MethodInfo patch = AccessTools.Method(typeof(Plugin), "FindValidPointCurved_MyPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
        }

        public static bool FindValidPointCurved_MyPatch(FVRMovementManager __instance, ref Vector3 __result, Vector3 castOrigin, Vector3 castDir, float initialVel)
        {
            float max_dist = 5000f;
            __instance.m_hasValidPoint = false;
            __result = Vector3.zero;
            Vector3 vector = castDir * initialVel * max_dist;
            float d = 1f / GM.CurrentPlayerBody.transform.localScale.x;
            Physics.Raycast(castOrigin, vector, out __instance.m_hit_ray, max_dist, __instance.LM_TeleCast);

            if (!__instance.m_hit_ray.transform.gameObject.CompareTag("NoTeleport"))
            {
                __instance.m_hasValidPoint = true;
                __result = __instance.m_hit_ray.point;

            }

            for (int i = 0; i < 3; i++)
            {
                __instance.Cylinders[i].gameObject.SetActive(true);
                __instance.Cylinders[i].position = castOrigin;
                __instance.Cylinders[i].rotation = Quaternion.LookRotation(vector);
                __instance.Cylinders[i].localScale = new Vector3(0.1f, 0.1f, __instance.m_hit_ray.distance) * d;
            }

            return false;
        }
    }
}
