namespace HLin.GunGameProgressions;

public static class RuntimeBuildFeatures
{
#if GUNGAME_COMPATIBILITY_PROBE
    public static bool CompatibilityProbeEnabled => true;
#else
    public static bool CompatibilityProbeEnabled => false;
#endif
}
