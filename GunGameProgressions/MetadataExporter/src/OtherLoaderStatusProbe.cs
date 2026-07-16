using System;
using System.Reflection;
using HarmonyLib;

namespace HLin.GunGameProgressions;

public sealed class OtherLoaderStatusProbe
{
    private bool initialized;
    private bool failureLogged;
    private MethodInfo progressMethod;
    private FieldInfo startTimeField;
    private PropertyInfo activeLoadersProperty;

    public ExternalContentLoadState Read(Action<string> logDebug)
    {
        Initialize();
        if (progressMethod == null)
        {
            return ExternalContentLoadState.Unavailable;
        }

        try
        {
            var progress = Convert.ToSingle(progressMethod.Invoke(null, null));
            var loadStartTime = startTimeField == null ? 0f : Convert.ToSingle(startTimeField.GetValue(null));
            var activeLoaders = activeLoadersProperty == null ? 0 : Convert.ToInt32(activeLoadersProperty.GetValue(null, null));
            if (progress >= 1f && activeLoaders <= 0 && (startTimeField != null || activeLoadersProperty != null))
            {
                return ExternalContentLoadState.Complete;
            }

            return loadStartTime > 0f || activeLoaders > 0
                ? ExternalContentLoadState.Loading
                : ExternalContentLoadState.Unavailable;
        }
        catch (Exception exception)
        {
            LogFailureOnce(logDebug, "Could not read OtherLoader load status: " + exception);
            return ExternalContentLoadState.Unavailable;
        }
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        var loaderStatusType = AccessTools.TypeByName("OtherLoader.LoaderStatus");
        if (loaderStatusType == null)
        {
            return;
        }

        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        progressMethod = loaderStatusType.GetMethod("GetLoaderProgress", flags);
        startTimeField = loaderStatusType.GetField("LoadStartTime", flags);
        activeLoadersProperty = loaderStatusType.GetProperty("NumActiveLoaders", flags);
    }

    private void LogFailureOnce(Action<string> logDebug, string message)
    {
        if (failureLogged)
        {
            return;
        }

        failureLogged = true;
        if (logDebug != null)
        {
            logDebug(message);
        }
    }
}
