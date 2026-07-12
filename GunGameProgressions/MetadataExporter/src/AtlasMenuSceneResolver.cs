using System.Reflection;

namespace HLin.GunGameProgressions;

public static class AtlasMenuSceneResolver
{
    public static object GetSceneInfo(object menuScreen)
    {
        return ReadMember(ReadMember(menuScreen, "m_def"), "CustomSceneInfo");
    }

    public static bool IsGunGameSelection(object menuScreen)
    {
        return GunGameSceneIdentity.IsMatch(ReadMember(GetSceneInfo(menuScreen), "Identifier") as string);
    }

    private static object ReadMember(object instance, string memberName)
    {
        if (instance == null)
        {
            return null;
        }

        var type = instance.GetType();
        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            return field.GetValue(instance);
        }

        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property == null ? null : property.GetValue(instance, null);
    }
}
