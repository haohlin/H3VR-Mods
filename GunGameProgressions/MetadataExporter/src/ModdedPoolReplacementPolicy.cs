namespace HLin.GunGameProgressions;

public static class ModdedPoolReplacementPolicy
{
    public static bool ShouldReplace(int existingGunCount, int candidateGunCount)
    {
        return candidateGunCount > existingGunCount;
    }
}
