using BepInEx;
using HarmonyLib;

namespace BetterGasTank
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-BetterGasTank", MyPluginInfo.PLUGIN_NAME, "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly Harmony harmony = new Harmony("HLin-BetterGasTank");

        public void Awake()
        {
            harmony.PatchAll();
            Logger.LogInfo("Loaded BetterGasTank Successfully!");
        }
    }
}
