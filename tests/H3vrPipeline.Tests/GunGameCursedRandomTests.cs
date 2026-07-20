using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class GunGameCursedRandomTests
{
    [Fact]
    public void Cursed_random_mod_uses_native_weapon_changed_event_and_vanilla_random_api()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "GunGameCursedRandom", "src", "Plugin.cs"));

        Assert.Contains("BTN_TryToSpawnRandomGun", source);
        Assert.Contains("AmmoQuickbeltSlot", source);
        Assert.Contains("ExtraQuickbeltSlot", source);
        Assert.Contains("GunGame.Scripts.Progression", source);
        Assert.Contains("WeaponChangedEvent", source);
        Assert.Contains("CursedProfileName", source);
        Assert.Contains("IsCursedProfileSelected", source);
        Assert.Contains("if (!IsCursedProfileSelected(progressionType.Assembly))", source);
        Assert.Contains("Resources.FindObjectsOfTypeAll", source);
        Assert.Contains("ReplaceNativeEquipment", source);
        Assert.Contains("spawnerCount=", source);
        Assert.Contains("vanilla random result", source);
        Assert.Contains("quickbelt: spares=", source);
        Assert.Contains("slot.CurObject == null", source);
        Assert.Contains("Cursed random GunGame spawn:", source);
        Assert.DoesNotContain("SpawnAndEquipPrefix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddStartupToggle", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RandomToggleMarker", source, StringComparison.Ordinal);
        Assert.DoesNotContain("randomGunsEnabled", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private void Update(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private void FixedUpdate(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Cursed_random_mod_packages_selectable_profile_without_harmony_targets()
    {
        var root = FindRepositoryRoot();
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "build", "mods.json")));
        var mod = config.RootElement.GetProperty("mods").GetProperty("GunGameCursedRandom");

        Assert.Equal("dotnet", mod.GetProperty("kind").GetString());
        Assert.Equal("GunGameCursedRandom\\GunGameCursedRandom.csproj", mod.GetProperty("csproj").GetString());
        Assert.True(mod.GetProperty("registerWithR2modman").GetBoolean());
        Assert.Equal(0, mod.GetProperty("externalPatchTargets").GetArrayLength());
        Assert.Contains(
            mod.GetProperty("payload").EnumerateArray(),
            payload => payload.GetProperty("to").GetString() == "GunGameWeaponPool_Cursed_Random.json");

        var profilePath = Path.Combine(
            root,
            "GunGameCursedRandom",
            "profiles",
            "GunGameWeaponPool_Cursed_Random.json");
        using var profile = JsonDocument.Parse(File.ReadAllText(profilePath));
        Assert.Equal("Cursed Random", profile.RootElement.GetProperty("Name").GetString());
        Assert.Equal("Advanced", profile.RootElement.GetProperty("WeaponPoolType").GetString());
        Assert.True(profile.RootElement.GetProperty("Guns").GetArrayLength() >= 64);

        var package = Path.Combine(root, "GunGameCursedRandom", "Thunderstore", "HLin_Mods-GunGame_Cursed_Random");
        Assert.True(File.Exists(Path.Combine(package, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(package, "README.md")));
        Assert.True(File.Exists(Path.Combine(package, "CHANGELOG.md")));
        Assert.True(File.Exists(Path.Combine(package, "icon.png")));

        var pipeline = File.ReadAllText(Path.Combine(root, "tools", "h3vr.ps1"));
        Assert.Contains("Register-R2modmanLocalPackage", pipeline);
        Assert.Contains("mm_v2_manifest.json", pipeline);
        Assert.Contains("registerWithR2modman", pipeline);
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
