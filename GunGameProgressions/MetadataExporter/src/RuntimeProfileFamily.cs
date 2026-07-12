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

    private static bool IsPoolFileFor(string fileName, System.Func<string, bool> familyMatch)
    {
        const string prefix = "GunGameWeaponPool_Runtime_";
        const string suffix = "_RW_Rot.json";
        if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith(prefix) || !fileName.EndsWith(suffix))
        {
            return false;
        }

        var family = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
        return familyMatch(family);
    }
}
