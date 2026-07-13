using System;

namespace HLin.GunGameProgressions;

public enum ExternalContentLoadState
{
    Unavailable,
    Loading,
    Complete,
}

public sealed class ModdedProfileReadinessGate
{
    private const float QuietSeconds = 5f;

    private bool hasObservedRegistry;
    private int lastRegistryCount;
    private float lastRegistryChangeTime;

    public void Observe(float now, int registryCount, ExternalContentLoadState externalLoadState)
    {
        if (!hasObservedRegistry || registryCount != lastRegistryCount)
        {
            hasObservedRegistry = true;
            lastRegistryCount = registryCount;
            lastRegistryChangeTime = now;
        }
    }

    public bool IsReady(float now, ExternalContentLoadState externalLoadState)
    {
        if (externalLoadState == ExternalContentLoadState.Complete)
        {
            return true;
        }

        return externalLoadState == ExternalContentLoadState.Unavailable &&
            hasObservedRegistry &&
            now - lastRegistryChangeTime >= QuietSeconds;
    }

    public int SecondsUntilQuiet(float now, ExternalContentLoadState externalLoadState)
    {
        if (externalLoadState == ExternalContentLoadState.Complete)
        {
            return 0;
        }

        if (externalLoadState == ExternalContentLoadState.Loading || !hasObservedRegistry)
        {
            return (int)QuietSeconds;
        }

        return Math.Max(0, (int)Math.Ceiling(QuietSeconds - (now - lastRegistryChangeTime)));
    }
}
