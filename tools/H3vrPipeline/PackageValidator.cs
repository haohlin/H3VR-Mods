using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace H3vrPipeline;

public sealed class PackageValidationResult
{
    public PackageValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}

public static class PackageValidator
{
    private static readonly Regex PackageNamePattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
    private static readonly Regex VersionPattern = new("^\\d+\\.\\d+\\.\\d+$", RegexOptions.Compiled);
    private static readonly HashSet<string> MetadataFiles = new(StringComparer.Ordinal)
    {
        "manifest.json",
        "README.md",
        "icon.png",
        "CHANGELOG.md"
    };

    public static PackageValidationResult Validate(string packagePath, string layout)
    {
        var errors = new List<string>();

        if (!File.Exists(packagePath))
        {
            errors.Add($"Package does not exist: {packagePath}");
            return new PackageValidationResult(errors);
        }

        using var archive = ZipFile.OpenRead(packagePath);
        var files = archive.Entries.Where(entry => !entry.FullName.EndsWith('/')).ToList();
        var rootFiles = files.Where(entry => !entry.FullName.Contains('/')).ToDictionary(entry => entry.FullName, StringComparer.Ordinal);

        foreach (var requiredFile in new[] { "manifest.json", "README.md", "icon.png" })
        {
            if (!rootFiles.ContainsKey(requiredFile))
            {
                errors.Add($"Missing required root file: {requiredFile}");
            }
        }

        if (rootFiles.TryGetValue("manifest.json", out var manifest))
        {
            ValidateManifest(manifest, errors);
        }

        if (rootFiles.TryGetValue("icon.png", out var icon))
        {
            ValidateIcon(icon, errors);
        }

        ValidatePayloadLayout(files, layout, errors);
        return new PackageValidationResult(errors);
    }

    private static void ValidateManifest(ZipArchiveEntry manifest, List<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(manifest.Open());
            var root = document.RootElement;

            ValidateString(root, "name", value => PackageNamePattern.IsMatch(value), "must contain only letters, numbers, and underscores", errors);
            ValidateString(root, "version_number", value => VersionPattern.IsMatch(value), "must be Major.Minor.Patch", errors);
            ValidateString(root, "description", value => value.Length is > 0 and <= 250, "must be 1 to 250 characters", errors);
            ValidateString(root, "website_url", _ => true, "must be a string", errors);

            if (!root.TryGetProperty("dependencies", out var dependencies) || dependencies.ValueKind != JsonValueKind.Array || dependencies.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            {
                errors.Add("manifest.json dependencies must be an array of strings");
            }
        }
        catch (JsonException exception)
        {
            errors.Add($"manifest.json is invalid JSON: {exception.Message}");
        }
    }

    private static void ValidateString(JsonElement root, string propertyName, Func<string, bool> rule, string requirement, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || !rule(property.GetString()!))
        {
            errors.Add($"manifest.json {propertyName} {requirement}");
        }
    }

    private static void ValidateIcon(ZipArchiveEntry icon, List<string> errors)
    {
        using var stream = icon.Open();
        var header = new byte[24];
        if (stream.Read(header, 0, header.Length) != header.Length ||
            !header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !header.Skip(12).Take(4).SequenceEqual("IHDR"u8.ToArray()))
        {
            errors.Add("icon.png is not a PNG file");
            return;
        }

        var width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        var height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        if (width != 256 || height != 256)
        {
            errors.Add("icon.png must be 256x256");
        }
    }

    private static void ValidatePayloadLayout(IReadOnlyList<ZipArchiveEntry> files, string layout, List<string> errors)
    {
        if (layout == "legacy-flat")
        {
            if (!files.Any(entry => !entry.FullName.Contains('/') && !MetadataFiles.Contains(entry.FullName)))
            {
                errors.Add("legacy-flat package must contain at least one payload file at the ZIP root");
            }

            return;
        }

        if (layout == "bepinex")
        {
            if (!files.Any(entry => entry.FullName.StartsWith("BepInEx/plugins/", StringComparison.Ordinal) && entry.FullName.Length > "BepInEx/plugins/".Length))
            {
                errors.Add("bepinex package must contain payload under BepInEx/plugins");
            }

            return;
        }

        errors.Add($"Unsupported package layout: {layout}");
    }
}
