namespace HLin.GunGameProgressions;

public sealed class OnDemandGenerationGate
{
    private bool preparing;
    private bool originalLoadReleased;

    public bool TryBeginPreparation()
    {
        if (preparing || originalLoadReleased)
        {
            return false;
        }

        preparing = true;
        return true;
    }

    public void ReleaseOriginalLoad()
    {
        originalLoadReleased = true;
    }

    public bool ConsumeOriginalLoadPermission()
    {
        if (!originalLoadReleased)
        {
            return false;
        }

        originalLoadReleased = false;
        preparing = false;
        return true;
    }
}
