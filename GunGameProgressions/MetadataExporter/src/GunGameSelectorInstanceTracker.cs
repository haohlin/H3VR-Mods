namespace HLin.GunGameProgressions;

public sealed class GunGameSelectorInstanceTracker
{
    private object activeSelector;

    public bool Observe(object selector)
    {
        if (selector == null)
        {
            activeSelector = null;
            return false;
        }

        if (ReferenceEquals(activeSelector, selector))
        {
            return false;
        }

        activeSelector = selector;
        return true;
    }
}
