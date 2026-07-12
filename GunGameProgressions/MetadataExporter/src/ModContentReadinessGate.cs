namespace HLin.GunGameProgressions;

public enum ExternalContentLoadState
{
    Unavailable,
    Loading,
    Complete,
}

public sealed class ModContentReadinessGate
{
    private readonly float timeoutSeconds;
    private readonly float registryQuietSeconds;
    private bool hasRegistrySnapshot;
    private int lastModdedEntryCount;
    private float lastRegistryChangeSeconds;

    public ModContentReadinessGate(float timeoutSeconds, float registryQuietSeconds)
    {
        this.timeoutSeconds = timeoutSeconds;
        this.registryQuietSeconds = registryQuietSeconds;
    }

    public bool IsReady(float elapsedSeconds, int moddedEntryCount, ExternalContentLoadState externalLoadState)
    {
        if (HasTimedOut(elapsedSeconds) || externalLoadState == ExternalContentLoadState.Complete)
        {
            return true;
        }

        if (externalLoadState == ExternalContentLoadState.Loading)
        {
            return false;
        }

        if (!hasRegistrySnapshot || moddedEntryCount != lastModdedEntryCount)
        {
            hasRegistrySnapshot = true;
            lastModdedEntryCount = moddedEntryCount;
            lastRegistryChangeSeconds = elapsedSeconds;
            return false;
        }

        return elapsedSeconds - lastRegistryChangeSeconds >= registryQuietSeconds;
    }

    public bool HasTimedOut(float elapsedSeconds)
    {
        return elapsedSeconds >= timeoutSeconds;
    }
}
