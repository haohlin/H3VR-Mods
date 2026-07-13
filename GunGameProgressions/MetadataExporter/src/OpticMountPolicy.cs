using System;
using System.Collections.Generic;
using System.Linq;

namespace HLin.GunGameProgressions;

public sealed class OpticMountRule
{
    public OpticMountRule(string mountType, IEnumerable<string> opticKinds, int priority)
    {
        MountType = mountType;
        OpticKinds = (opticKinds ?? Enumerable.Empty<string>())
            .Where(kind => !string.IsNullOrEmpty(kind))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Priority = priority;
    }

    public string MountType { get; private set; }
    public List<string> OpticKinds { get; private set; }
    public int Priority { get; private set; }

    public bool Accepts(string opticKind)
    {
        return OpticKinds.Any(kind => string.Equals(kind, opticKind, StringComparison.Ordinal));
    }
}

/// <summary>
/// Maps verified, physical firearm attachment mounts to the optic category they
/// accept. This deliberately has no firearm or attachment ID exceptions.
/// </summary>
public static class OpticMountPolicy
{
    // These are H3VR mount *types*, not firearm or attachment IDs. Keeping
    // this small taxonomy here gives profile selection and runtime mounting
    // one authoritative definition of an optic-capable mount.
    private static readonly HashSet<string> ReflexOnlyMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "RMR",
        "Handgun",
    };

    private static readonly HashSet<string> RailMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Picatinny",
        "MLokRail",
    };

    private static readonly HashSet<string> ScopeOnlyMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Russian",
        "MAS4956Scope",
        "SVTScope",
        "M16HandleMount",
        "M1GarandScope",
        "M1CarbineScope",
        "MP5RailMount",
        "PythonScopeMount",
        "FamasTopRail",
        "Mini14TopRail",
        "R1022TopRail",
    };

    public static IEnumerable<OpticMountRule> Rank(IEnumerable<string> physicalMountTypes)
    {
        return (physicalMountTypes ?? Enumerable.Empty<string>())
            .Select(MountResolution.Resolve)
            .Where(resolution => resolution.IsResolved)
            .Select(resolution => resolution.CanonicalMount)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(CreateRule)
            .Where(rule => rule != null)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.MountType, StringComparer.Ordinal);
    }

    public static bool IsOpticMountType(string rawMountType)
    {
        var resolution = MountResolution.Resolve(rawMountType);
        return resolution.IsResolved && GetOpticKinds(resolution.CanonicalMount) != null;
    }

    public static bool RequiresTopSightingOrientation(string rawMountType)
    {
        var resolution = MountResolution.Resolve(rawMountType);
        return resolution.IsResolved && RailMounts.Contains(resolution.CanonicalMount);
    }

    private static OpticMountRule CreateRule(string mountType)
    {
        var opticKinds = GetOpticKinds(mountType);
        if (opticKinds == null)
        {
            return null;
        }

        return new OpticMountRule(mountType, opticKinds, GetPriority(mountType));
    }

    private static IEnumerable<string> GetOpticKinds(string mountType)
    {
        if (ReflexOnlyMounts.Contains(mountType))
        {
            return new[] { "Reflex" };
        }

        if (RailMounts.Contains(mountType))
        {
            // Rail mounts accept a verified scope or reflex sight. The
            // firearm role ranks those physically compatible choices later.
            return new[] { "Scope", "Reflex" };
        }

        return IsScopeMount(mountType) ? new[] { "Scope" } : null;
    }

    private static int GetPriority(string mountType)
    {
        if (ReflexOnlyMounts.Contains(mountType))
        {
            return 20;
        }

        return RailMounts.Contains(mountType) ? 100 : 10;
    }

    private static bool IsScopeMount(string mountType)
    {
        return ScopeOnlyMounts.Contains(mountType) ||
            mountType.StartsWith("Scope_", StringComparison.OrdinalIgnoreCase) ||
            mountType.EndsWith("Scope", StringComparison.OrdinalIgnoreCase);
    }
}
