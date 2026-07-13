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

    private static OpticMountRule CreateRule(string mountType)
    {
        if (string.Equals(mountType, "RMR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "Handgun", StringComparison.OrdinalIgnoreCase))
        {
            return new OpticMountRule(mountType, new[] { "Reflex" }, 20);
        }

        if (string.Equals(mountType, "Picatinny", StringComparison.OrdinalIgnoreCase))
        {
            // A verified Picatinny rail accepts both verified scopes and
            // reflex sights. The firearm role decides between those two after
            // this physical compatibility check; a proprietary mount still
            // wins because it has a lower priority value.
            return new OpticMountRule(mountType, new[] { "Scope", "Reflex" }, 100);
        }

        return IsScopeMount(mountType)
            ? new OpticMountRule(mountType, new[] { "Scope" }, 10)
            : null;
    }

    private static bool IsScopeMount(string mountType)
    {
        return string.Equals(mountType, "Russian", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "MAS4956Scope", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "SVTScope", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "M16HandleMount", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "M1GarandScope", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "M1CarbineScope", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "MP5RailMount", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "PythonScopeMount", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "FamasTopRail", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "Mini14TopRail", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mountType, "R1022TopRail", StringComparison.OrdinalIgnoreCase) ||
            mountType.StartsWith("Scope_", StringComparison.OrdinalIgnoreCase) ||
            mountType.EndsWith("Scope", StringComparison.OrdinalIgnoreCase);
    }
}
