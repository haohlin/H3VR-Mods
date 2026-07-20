using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class GunGameCursedRandomTests
{
    [Fact]
    public void Cursed_random_mod_keeps_one_narrow_progression_hook_and_vanilla_random_api()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "GunGameCursedRandom", "src", "Plugin.cs"));

        Assert.Contains("BTN_TryToSpawnRandomGun", source);
        Assert.Contains("AmmoQuickbeltSlot", source);
        Assert.Contains("ExtraQuickbeltSlot", source);
        Assert.Contains("GunGame.Scripts.Progression", source);
        Assert.Contains("SpawnAndEquip", source);
        Assert.Contains("GameSettingsStartPostfix", source);
        Assert.Contains("AddStartupToggleWhenReady", source);
        Assert.Contains("RandomGunDefaultInitialized", source);
        Assert.Contains("Cursed random GunGame spawn:", source);
        Assert.DoesNotContain("private void Update(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private void FixedUpdate(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Cursed_random_mod_is_registered_with_package_and_external_harmony_targets()
    {
        var root = FindRepositoryRoot();
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "build", "mods.json")));
        var mod = config.RootElement.GetProperty("mods").GetProperty("GunGameCursedRandom");

        Assert.Equal("dotnet", mod.GetProperty("kind").GetString());
        Assert.Equal("GunGameCursedRandom\\GunGameCursedRandom.csproj", mod.GetProperty("csproj").GetString());
        Assert.Contains(
            mod.GetProperty("externalPatchTargets").EnumerateArray(),
            target => target.GetProperty("type").GetString() == "GunGame.Scripts.Progression" &&
                target.GetProperty("method").GetString() == "SpawnAndEquip");
        Assert.Contains(
            mod.GetProperty("externalPatchTargets").EnumerateArray(),
            target => target.GetProperty("type").GetString() == "GunGame.Scripts.Options.GameSettings" &&
                target.GetProperty("method").GetString() == "Start");

        var package = Path.Combine(root, "GunGameCursedRandom", "Thunderstore", "HLin_Mods-GunGame_Cursed_Random");
        Assert.True(File.Exists(Path.Combine(package, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(package, "README.md")));
        Assert.True(File.Exists(Path.Combine(package, "CHANGELOG.md")));
        Assert.True(File.Exists(Path.Combine(package, "icon.png")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "build", "mods.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the H3VR-Mods repository root.");
    }
}
