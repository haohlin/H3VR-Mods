using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using BepInEx;
using HarmonyLib;
using FistVR;

namespace ThePing
{

    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-ThePing", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static AnimationCurve customRolloffCurve;
        private void Awake()
        {
            Harmony harmony = new Harmony("HLin-ThePing");
            MethodInfo original = AccessTools.Method(typeof(FVRPooledAudioSource), "Play");
            MethodInfo patch = AccessTools.Method(typeof(Plugin), "Play_MyPatch");
            harmony.Patch(original, new HarmonyMethod(patch));
            customRolloffCurve = new AnimationCurve();
            foreach (AudioRolloffPoint point in AudioRolloffPolicy.Points)
            {
                customRolloffCurve.AddKey(new Keyframe(point.Distance, point.Volume));
            }
            customRolloffCurve.SmoothTangents(1, -0.9f);
            customRolloffCurve.SmoothTangents(2, -0.9f);
            customRolloffCurve.SmoothTangents(3, -0.9f);
        }

        public static void Play_MyPatch(FVRPooledAudioSource __instance, AudioEvent audioEvent, Vector3 pos, Vector2 pitch, Vector2 volume, AudioMixerGroup mixerOverride = null)
        {
            __instance.Source.maxDistance = AudioRolloffPolicy.MaxDistance;
            __instance.Source.rolloffMode = AudioRolloffMode.Custom;
            __instance.Source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customRolloffCurve);
        }
    }
}
