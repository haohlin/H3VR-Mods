using System;
using System.Collections.Generic;
using System.Linq;

namespace HLin.GunGameProgressions;

public sealed class OpticMountRule
{
    public OpticMountRule(string mountType, string opticKind, int priority)
    {
        MountType = mountType;
        OpticKind = opticKind;
        Priority = priority;
    }

    public string MountType { get; private set; }
    public string OpticKind { get; private set; }
    public int Priority { get; private set; }
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
            return new OpticMountRule(mountType, "Reflex", 20);
        }

        if (string.Equals(mountType, "Picatinny", StringComparison.OrdinalIgnoreCase))
        {
            // A real Picatinny rail is the universal default: give it a
            // magnified Picatinny scope unless a more specific optic mount is
            // present on the same firearm.
            return new OpticMountRule(mountType, "Scope", 100);
        }

        return IsScopeMount(mountType)
            ? new OpticMountRule(mountType, "Scope", 10)
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
