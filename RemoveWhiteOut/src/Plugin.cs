using System.Reflection;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using FistVR;

namespace RemoveWhiteOut
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-RemoveWhiteOut", PluginInfo.PLUGIN_NAME, "1.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            MethodInfo original1 = AccessTools.Method(typeof(WW_TeleportMaster), "UpdateWhiteOut");
            MethodInfo original2 = AccessTools.Method(typeof(WW_StandaloneWeatherSystem), "UpdateWhiteOut");
            MethodInfo patch1 = AccessTools.Method(typeof(Plugin), "UpdateWhiteOut_MyPatch");
            MethodInfo patch2 = AccessTools.Method(typeof(Plugin), "TNH_UpdateWhiteOut_MyPatch");
            harmony.Patch(original1, new HarmonyMethod(patch1));
            harmony.Patch(original2, new HarmonyMethod(patch2));
        }
        public static bool UpdateWhiteOut_MyPatch(WW_TeleportMaster __instance)
        {
            //__instance.WhiteOutThreshold = 0f;
            //__instance.SkyboxMat.SetFloat("_WhiteoutAmount", __instance.WhiteOutThreshold);
            //RenderSettings.fogDensity = Mathf.Lerp(__instance.WhiteOutFogVals.x, __instance.WhiteOutFogVals.y, __instance.WhiteOutThreshold);
            //float t = Mathf.InverseLerp(__instance.RefHeights.x, __instance.RefHeights.y, GM.CurrentPlayerBody.Head.position.y);
            //float value = Mathf.Lerp(0.15f, Mathf.Lerp(__instance.HorizonHeights.x, __instance.HorizonHeights.y, t), __instance.WhiteOutThreshold);
            //float value2 = Mathf.Lerp(0.4f, Mathf.Lerp(__instance.HorizonBlends.x, __instance.HorizonBlends.y, t), __instance.WhiteOutThreshold);
            //__instance.SkyboxMat.SetFloat("_HorizonHeight", value);
            //__instance.SkyboxMat.SetFloat("_HorizonBlend", value2);
            return false;
        }
        public static bool TNH_UpdateWhiteOut_MyPatch(WW_StandaloneWeatherSystem __instance)
        {
            __instance.WhiteOutThreshold = 0f;
            __instance.SkyboxMat.SetFloat("_WhiteoutAmount", __instance.WhiteOutThreshold);
            RenderSettings.fogDensity = Mathf.Lerp(__instance.WhiteOutFogVals.x, __instance.WhiteOutFogVals.y, __instance.WhiteOutThreshold);
            float t = Mathf.InverseLerp(__instance.RefHeights.x, __instance.RefHeights.y, GM.CurrentPlayerBody.Head.position.y);
            float value = Mathf.Lerp(0.15f, Mathf.Lerp(__instance.HorizonHeights.x, __instance.HorizonHeights.y, t), __instance.WhiteOutThreshold);
            float value2 = Mathf.Lerp(0.4f, Mathf.Lerp(__instance.HorizonBlends.x, __instance.HorizonBlends.y, t), __instance.WhiteOutThreshold);
            __instance.SkyboxMat.SetFloat("_HorizonHeight", value);
            __instance.SkyboxMat.SetFloat("_HorizonBlend", value2);
            return false;
        }
    }
}