using System;

namespace HLin.GunGameProgressions;

public sealed class RuntimeMetadataStabilityGate
{
    private readonly int requiredStableSamples;
    private int initialCount = -1;
    private int previousCount = -1;
    private int stableSamples;
    private bool observedLateGrowth;

    public RuntimeMetadataStabilityGate(int requiredStableSamples)
    {
        if (requiredStableSamples < 1)
        {
            throw new ArgumentOutOfRangeException("requiredStableSamples");
        }

        this.requiredStableSamples = requiredStableSamples;
    }

    public bool Observe(int itemCount)
    {
        if (itemCount <= 0)
        {
            return false;
        }

        if (initialCount < 0)
        {
            initialCount = itemCount;
        }

        if (itemCount != previousCount)
        {
            observedLateGrowth = observedLateGrowth || itemCount > initialCount;
            previousCount = itemCount;
            stableSamples = 0;
            return false;
        }

        stableSamples++;
        return observedLateGrowth && stableSamples >= requiredStableSamples;
    }
}
