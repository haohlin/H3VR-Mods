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
    private const float LoadingStableSeconds = 30f;

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

        if (!hasObservedRegistry)
        {
            return false;
        }

        var stableSeconds = now - lastRegistryChangeTime;
        return externalLoadState == ExternalContentLoadState.Loading
            ? stableSeconds >= LoadingStableSeconds
            : externalLoadState == ExternalContentLoadState.Unavailable && stableSeconds >= QuietSeconds;
    }
}
