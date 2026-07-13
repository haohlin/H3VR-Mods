using System;
using System.Reflection;
using UnityEngine;

namespace HLin.GunGameProgressions;

public static class GunGameSelectorLocator
{
    public static object Resolve(Type selectorType)
    {
        var singleton = ResolveSingleton(selectorType);
        if (IsAlive(singleton))
        {
            return singleton;
        }

        return selectorType == null ? null : UnityEngine.Object.FindObjectOfType(selectorType);
    }

    public static object ResolveSingleton(Type selectorType)
    {
        if (selectorType == null)
        {
            return null;
        }

        var instanceProperty = selectorType.GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        return instanceProperty == null || !instanceProperty.CanRead
            ? null
            : instanceProperty.GetValue(null, null);
    }

    private static bool IsAlive(object candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        var unityObject = candidate as UnityEngine.Object;
        return ReferenceEquals(unityObject, null) || unityObject != null;
    }
}
