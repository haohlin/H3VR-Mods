using BepInEx;
using HarmonyLib;

namespace BetterScopeControl
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-BetterScopeControl", PluginInfo.PLUGIN_NAME, "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly Harmony harmony = new Harmony("HLin-BetterScopeControl");

        public void Awake()
        {
            harmony.PatchAll();
            Logger.LogInfo("Loaded Better ScopeControl Successfully!");
        }
    }
}
