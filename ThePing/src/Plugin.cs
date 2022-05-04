using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using BepInEx;
using HarmonyLib;
using FistVR;

namespace ThePing
{

    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-ThePing", PluginInfo.PLUGIN_NAME, "1.0.2")]
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
            customRolloffCurve = new AnimationCurve();
            customRolloffCurve.AddKey(new Keyframe(0, 1f));
            customRolloffCurve.AddKey(new Keyframe(50, 0.5f));
            customRolloffCurve.AddKey(new Keyframe(100, 0.3f));
            customRolloffCurve.AddKey(new Keyframe(1000f, 0.125f));
            customRolloffCurve.AddKey(new Keyframe(2500f, 0.1f));
            customRolloffCurve.SmoothTangents(1, -0.9f);
            customRolloffCurve.SmoothTangents(2, -0.9f);
            customRolloffCurve.SmoothTangents(3, -0.9f);
        }

        public static void Play_MyPatch(FVRPooledAudioSource __instance, AudioEvent audioEvent, Vector3 pos, Vector2 pitch, Vector2 volume, AudioMixerGroup mixerOverride = null)
        {
            __instance.Source.maxDistance = 2500f;
            __instance.Source.rolloffMode = AudioRolloffMode.Custom;
            __instance.Source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customRolloffCurve);
        }
    }
}
