using System;

namespace HLin.GunGameProgressions;

public static class GunGameSceneIdentity
{
    public const string Identifier = "GunGame";

    public static bool IsMatch(string sceneIdentifier)
    {
        return string.Equals(sceneIdentifier, Identifier, StringComparison.OrdinalIgnoreCase);
    }
}
