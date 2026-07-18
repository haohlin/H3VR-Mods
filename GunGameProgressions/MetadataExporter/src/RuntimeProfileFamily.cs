namespace HLin.GunGameProgressions;

public static class RuntimeProfileFamily
{
    public static bool IsVanilla(string family)
    {
        return family == "01_Vanilla_Rot" || family == "03_Vanilla_Mixed_Enemy";
    }

    public static bool IsModded(string family)
    {
        return family == "02_Modded_Rot" || family == "04_Modded_Mixed_Enemy";
    }

    public static bool IsVanillaPoolFile(string fileName)
    {
        return IsPoolFileFor(fileName, IsVanilla);
    }

    public static bool IsModdedPoolFile(string fileName)
    {
        return IsPoolFileFor(fileName, IsModded);
    }

    public static bool IsCompatibilityProbe(string family)
    {
        return family == "05_Compatibility_Probe";
    }

    public static bool IsCompatibilityProbePoolName(string name)
    {
        return name == "Runtime 05 - Compatibility Probe";
    }

    public static bool IsCompatibilityProbePoolFile(string fileName)
    {
        return IsPoolFileFor(fileName, IsCompatibilityProbe);
    }

    private static bool IsPoolFileFor(string fileName, System.Func<string, bool> familyMatch)
    {
        const string prefix = "GunGameWeaponPool_Runtime_";
        const string suffix = ".json";
        if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith(prefix) || !fileName.EndsWith(suffix))
        {
            return false;
        }

        var poolName = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
        foreach (var family in new[]
            {
                "01_Vanilla_Rot",
                "02_Modded_Rot",
                "03_Vanilla_Mixed_Enemy",
                "04_Modded_Mixed_Enemy",
                "05_Compatibility_Probe",
            })
        {
            if (familyMatch(family) && poolName.StartsWith(family + "_"))
            {
                return true;
            }
        }

        return false;
    }
}
