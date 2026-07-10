namespace HLin.GunGameProgressions;

public static class RuntimeItemRole
{
    public static string Resolve(
        string declaredCategory,
        bool hasFirearm,
        bool hasMagazine,
        bool hasClip,
        bool hasSpeedloader,
        bool hasRound)
    {
        if (declaredCategory != "Firearm")
        {
            return declaredCategory;
        }

        if (hasFirearm)
        {
            return "Firearm";
        }

        if (hasMagazine)
        {
            return "Magazine";
        }

        if (hasClip)
        {
            return "Clip";
        }

        if (hasSpeedloader)
        {
            return "SpeedLoader";
        }

        if (hasRound)
        {
            return "Cartridge";
        }

        return "Unknown";
    }
}
