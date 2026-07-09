using System;

namespace HLin.GunGameProgressions;

public sealed class MountResolution
{
    private MountResolution(string rawMount, string canonicalMount, bool isResolved)
    {
        RawMount = rawMount;
        CanonicalMount = canonicalMount;
        IsResolved = isResolved;
    }

    public string RawMount { get; private set; }
    public string CanonicalMount { get; private set; }
    public bool IsResolved { get; private set; }

    public static MountResolution Resolve(string rawMount)
    {
        if (string.IsNullOrEmpty(rawMount) || rawMount.Trim().Length == 0)
        {
            return new MountResolution(string.Empty, string.Empty, false);
        }

        var trimmed = rawMount.Trim();
        if (string.Equals(trimmed, "99", StringComparison.Ordinal))
        {
            return new MountResolution(trimmed, "RMR", true);
        }

        int ignored;
        if (int.TryParse(trimmed, out ignored))
        {
            return new MountResolution(trimmed, string.Empty, false);
        }

        return new MountResolution(trimmed, trimmed, true);
    }
}
