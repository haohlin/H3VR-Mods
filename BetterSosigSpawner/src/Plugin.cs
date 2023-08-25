//using System.Reflection;
//using UnityEngine;
using BepInEx;
using HarmonyLib;
//using FistVR;
//using System;
//using RUST.Steamworks;

namespace BetterSosigSpawner
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-Better-SosigSpawner", PluginInfo.PLUGIN_NAME, "1.1.1")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly Harmony harmony = new Harmony("HLin-Better-SosigSpawner");

        public void Awake()
        {
            harmony.PatchAll();
            Logger.LogInfo("Loaded Better SosigSpawner Successfully!");
        }
    }
}
