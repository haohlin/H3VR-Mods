using System.Reflection;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using FistVR;


namespace LongRangeDrone
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("nrgill28.RemoteC4Drone", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("Loaded ReleaseMag Successfully!");
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            MethodInfo original = AccessTools.Method(typeof(RemoteControlDroneController), "UpdateStatus");
            MethodInfo patch = AccessTools.Method(typeof(Plugin), "UpdateStatus_MyPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
            MethodInfo original2 = AccessTools.Method(typeof(RemoteControlDroneController), "Update");
            MethodInfo patch2 = AccessTools.Method(typeof(Plugin), "Update_MyPatch");
            harmony.Patch(original2, new HarmonyMethod(patch2));
        }

        public static bool UpdateStatus_MyPatch(RemoteControlDroneController __instance)
        {
            Vector3 b = Vector3.zero;
            string text = "  <color=red>FAIL</color>";
            string text2 = "  <color=red>FAIL</color>";
            string text3 = "  <color=red>FAIL</color>";
            string text4 = "";
            int num = 0;
            if (__instance._connectedDrone)
            {
                b = __instance._connectedDrone.transform.position;
                text3 = ((!__instance._connectedDrone.IsCameraDamaged) ? "  <color=green>OK  </color>" : "  <color=red>FAIL</color>");
                int numRotorsDamaged = __instance._connectedDrone.NumRotorsDamaged;
                if (numRotorsDamaged != 0)
                {
                    if (numRotorsDamaged != 1)
                    {
                        text = "  <color=red>FAIL</color>";
                    }
                    else
                    {
                        text = "  <color=yellow>WARN</color>";
                    }
                }
                else
                {
                    text = "  <color=green>OK  </color>";
                }
                __instance._flashedLastTime = !__instance._flashedLastTime;
                text2 = "  <color=green>OK  </color>";
            }
            __instance.ScreenText.text = string.Format("Position: (X {0:0.##}, Z {1:0.##})\nAltitude: {2:0.##}\nDistance: NA {4}\n\nStatus\n{5} Connection\n{6} Camera\n{7} Rotors\n\nInputs\nL (X {8:0.##}, Y {9:0.##})\nR (X {10:0.##}, Y {11:0.##})", new object[]
            {
            b.x,
            b.z,
            b.y,
            num,
            text4,
            text2,
            text3,
            text,
            __instance._lAxes.x,
            __instance._lAxes.y,
            __instance._rAxes.x,
            __instance._rAxes.y
            });
            return false;
        }


        public static bool Update_MyPatch(RemoteControlDroneController __instance)
        {
            __instance._elapsed += Time.deltaTime;
            if (__instance._elapsed > __instance.StatusUpdateDelay)
            {
                __instance._elapsed = 0f;
                __instance.UpdateStatus();
            }
            return false;
        }
    }
}
