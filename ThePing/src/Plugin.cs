using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using BepInEx;
using HarmonyLib;
using FistVR;

namespace ThePing
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-ThePing", PluginInfo.PLUGIN_NAME, "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony harmony = new Harmony("HLin-ThePing");
            MethodInfo original = AccessTools.Method(typeof(FVRPooledAudioSource), "Play");
            MethodInfo patch = AccessTools.Method(typeof(Plugin), "Play_MyPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
        }

        public static void Play_MyPatch(FVRPooledAudioSource __instance, AudioEvent audioEvent, Vector3 pos, Vector2 pitch, Vector2 volume, AudioMixerGroup mixerOverride = null)
        {
            __instance.Source.maxDistance = 2000f;
        }
    }
}
