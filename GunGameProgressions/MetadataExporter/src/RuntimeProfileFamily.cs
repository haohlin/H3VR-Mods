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
}
