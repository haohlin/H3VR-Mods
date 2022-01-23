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
            Logger.LogInfo("Loaded Better Hearing Successfully!");
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            MethodInfo original = AccessTools.Method(typeof(FVRPooledAudioSource), "Play");
            MethodInfo patch = AccessTools.Method(typeof(Plugin), "Play_MyPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
        }

        public static void Play_MyPatch(FVRPooledAudioSource __instance, AudioEvent audioEvent, Vector3 pos, Vector2 pitch, Vector2 volume, AudioMixerGroup mixerOverride = null)
        {
            __instance.Source.maxDistance = 3000f;
        }
    }
}
