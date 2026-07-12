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

    public ModContentReadinessGate(float timeoutSeconds)
    {
        this.timeoutSeconds = timeoutSeconds;
    }

    public bool IsReady(float elapsedSeconds, ExternalContentLoadState externalLoadState)
    {
        if (HasTimedOut(elapsedSeconds) || externalLoadState == ExternalContentLoadState.Complete)
        {
            return true;
        }

        return false;
    }

    public bool HasTimedOut(float elapsedSeconds)
    {
        return elapsedSeconds >= timeoutSeconds;
    }
}
