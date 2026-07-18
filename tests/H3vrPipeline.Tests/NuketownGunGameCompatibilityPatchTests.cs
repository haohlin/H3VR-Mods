using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class NuketownGunGameCompatibilityPatchTests
{
    [Fact]
    public void Compatibility_plugin_is_startup_only_and_targets_only_original_nuketown_bundle()
    {
        var pluginPath = Path.Combine(
            RepositoryRoot,
            "NuketownGunGameCompatibilityPatch",
            "src",
            "Plugin.cs");

        Assert.True(File.Exists(pluginPath), "Compatibility plugin source must exist.");

        var plugin = File.ReadAllText(pluginPath);
        Assert.Contains("localpcnerd-NuketownGunGame", plugin, StringComparison.Ordinal);
        Assert.Contains("nuketown", plugin, StringComparison.Ordinal);
        Assert.DoesNotContain("void Update(", plugin, StringComparison.Ordinal);
        Assert.DoesNotContain("IEnumerator", plugin, StringComparison.Ordinal);
        Assert.DoesNotContain("Harmony", plugin, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly.Load", plugin, StringComparison.Ordinal);
    }

    [Fact]
    public void Compatibility_package_declares_original_mod_dependencies_and_only_compatibility_payload()
    {
        using var mods = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "build", "mods.json")));
        var registered = mods.RootElement.GetProperty("mods").TryGetProperty("NuketownGunGameCompatibilityPatch", out var descriptor);
        Assert.True(registered, "Compatibility package must be registered in build/mods.json.");

        Assert.Equal("dotnet", descriptor.GetProperty("kind").GetString());
        Assert.Equal("NuketownGunGameCompatibilityPatch.dll", descriptor.GetProperty("payload")[0].GetProperty("to").GetString());

        var packageRoot = Path.Combine(
            RepositoryRoot,
            "NuketownGunGameCompatibilityPatch",
            "Thunderstore",
            "HLin_Mods-Nuketown_GunGame_Compatibility_Patch");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, "manifest.json")));
        var dependencies = manifest.RootElement.GetProperty("dependencies").EnumerateArray().Select(item => item.GetString()).ToArray();

        Assert.Contains("localpcnerd-NuketownGunGame-2.1.6", dependencies);
        Assert.Contains("Kodeman-GunGame-1.0.2", dependencies);
        Assert.Contains("nrgill28-Atlas-1.0.0", dependencies);
        Assert.Equal(3, dependencies.Length);
        Assert.DoesNotContain(Directory.EnumerateFiles(packageRoot, "*", SearchOption.AllDirectories), file =>
            Path.GetFileName(file).Equals("nuketown", StringComparison.OrdinalIgnoreCase));
    }

    private static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "build", "mods.json")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
