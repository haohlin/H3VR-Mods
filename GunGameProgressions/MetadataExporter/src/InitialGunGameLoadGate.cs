namespace HLin.GunGameProgressions;

public enum InitialGunGameLoadAction
{
    BeginPreparation,
    KeepWaiting,
    AllowOriginalLoad,
}

public sealed class InitialGunGameLoadGate
{
    private bool preparationStarted;
    private bool originalLoadPending;
    private bool firstLoadCompleted;

    public InitialGunGameLoadAction NextAction()
    {
        if (originalLoadPending)
        {
            originalLoadPending = false;
            firstLoadCompleted = true;
            return InitialGunGameLoadAction.AllowOriginalLoad;
        }

        if (firstLoadCompleted)
        {
            return InitialGunGameLoadAction.AllowOriginalLoad;
        }

        if (!preparationStarted)
        {
            preparationStarted = true;
            return InitialGunGameLoadAction.BeginPreparation;
        }

        return InitialGunGameLoadAction.KeepWaiting;
    }

    public void CompletePreparation()
    {
        if (preparationStarted && !firstLoadCompleted)
        {
            originalLoadPending = true;
        }
    }
}
