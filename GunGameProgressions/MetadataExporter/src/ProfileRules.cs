using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HLin.GunGameProgressions;

public sealed class ProfileRules
{
    public string[] FirearmBlacklist { get; set; }
    public string[] RuntimeFirearmBlacklist { get; set; }
    public string[] FeedBlacklist { get; set; }
    public string[] CompatibilityProbeFirearms { get; set; }

    public static ProfileRules Load(string packageDirectory)
    {
        var path = Path.Combine(packageDirectory, "profile-rules.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("GunGame profile rules are missing.", path);
        }

        var json = File.ReadAllText(path);
        var firearmBlacklist = ReadStringArray(json, "firearmBlacklist");
        var runtimeFirearmBlacklist = ReadStringArray(json, "runtimeFirearmBlacklist");
        return new ProfileRules
        {
            FirearmBlacklist = firearmBlacklist,
            RuntimeFirearmBlacklist = runtimeFirearmBlacklist.Length == 0
                ? firearmBlacklist
                : runtimeFirearmBlacklist,
            FeedBlacklist = ReadStringArray(json, "feedBlacklist"),
            CompatibilityProbeFirearms = ReadStringArray(json, "compatibilityProbeFirearms"),
        };
    }

    public bool IsBlacklisted(RuntimeMetadataEntry entry)
    {
        return IsGloballyBlacklisted(entry) ||
            (entry.Category == "Firearm" && Contains(RuntimeFirearmBlacklist, entry.ObjectID));
    }

    public bool IsGloballyBlacklisted(RuntimeMetadataEntry entry)
    {
        return entry.Category == "Firearm"
            ? Contains(FirearmBlacklist, entry.ObjectID)
            : Contains(FeedBlacklist, entry.ObjectID);
    }

    private static bool Contains(IEnumerable<string> values, string value)
    {
        foreach (var item in values)
        {
            if (string.Equals(item, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] ReadStringArray(string json, string propertyName)
    {
        var propertyPattern = "\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*\\[(?<items>.*?)\\]";
        var property = Regex.Match(json, propertyPattern, RegexOptions.Singleline);
        if (!property.Success)
        {
            return new string[0];
        }

        var values = new List<string>();
        foreach (Match item in Regex.Matches(property.Groups["items"].Value, "\\\"(?<value>(?:\\\\.|[^\\\"\\\\])*)\\\""))
        {
            values.Add(item.Groups["value"].Value.Replace("\\\\\"", "\"").Replace("\\\\\\\\", "\\"));
        }

        return values.ToArray();
    }
}
