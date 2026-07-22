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
        Assert.Contains("CursedProfileName = \"HLin-Random Cursed\"", source);
        Assert.Contains("IsCursedProfileSelected", source);
        Assert.Contains("if (!IsCursedProfileSelected(progressionType.Assembly))", source);
        Assert.Contains("Resources.FindObjectsOfTypeAll", source);
        Assert.Contains("ReplaceNativeEquipment", source);
        Assert.Contains("spawnerCount=", source);
        Assert.Contains("vanilla random result", source);
        Assert.Contains("quickbelt: selectedSlot=", source);
        Assert.Contains("candidate.CurObject == null", source);
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
        Assert.Contains(
            mod.GetProperty("externalPatchTargets").EnumerateArray(),
            target => target.GetProperty("type").GetString() == "GunGame.Scripts.Weapons.WeaponBuffer" &&
                target.GetProperty("method").GetString() == "SpawnAsync");
        Assert.Contains(
            mod.GetProperty("payload").EnumerateArray(),
            payload => payload.GetProperty("to").GetString() == "GunGameWeaponPool_Cursed_Random.json");

        var profilePath = Path.Combine(
            root,
            "GunGameCursedRandom",
            "profiles",
            "GunGameWeaponPool_Cursed_Random.json");
        using var profile = JsonDocument.Parse(File.ReadAllText(profilePath));
        Assert.Equal("HLin-Random Cursed", profile.RootElement.GetProperty("Name").GetString());
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

    [Fact]
    public void Cursed_random_profile_uses_valid_g17_placeholder_feed_for_every_tier()
    {
        var root = FindRepositoryRoot();
        var profilePath = Path.Combine(
            root,
            "GunGameCursedRandom",
            "profiles",
            "GunGameWeaponPool_Cursed_Random.json");
        using var profile = JsonDocument.Parse(File.ReadAllText(profilePath));
        var guns = profile.RootElement.GetProperty("Guns").EnumerateArray().ToArray();

        Assert.Equal(64, guns.Length);
        foreach (var gun in guns)
        {
            Assert.Equal("G17", gun.GetProperty("GunName").GetString());
            Assert.Equal("MagazineG17Standard", gun.GetProperty("MagName").GetString());
            Assert.Equal(
                new[] { "MagazineG17Standard" },
                gun.GetProperty("MagNames").EnumerateArray().Select(value => value.GetString()).ToArray());
        }
    }

    [Fact]
    public void Cursed_random_mod_intercepts_weapon_buffer_and_preserves_unmanaged_quickbelt_items()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "GunGameCursedRandom", "src", "Plugin.cs"));
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "build", "mods.json")));
        var mod = config.RootElement.GetProperty("mods").GetProperty("GunGameCursedRandom");

        Assert.Contains("WeaponBufferSpawnAsyncPrefix(object __instance, object __1, ref IEnumerator __result)", source);
        Assert.Contains("suppressing native placeholder", source);
        Assert.Contains("suppressing duplicate native placeholder during pending random spawn", source);
        Assert.Contains("queued post-spawn fallback; keeping native fallback", source);
        Assert.Contains("WeaponChangedEvent found no pending direct random spawn; using post-spawn fallback.", source);
        Assert.Contains("SpawnQuickbeltSpare", source);
        Assert.Contains("feedObject.GetGameObject()", source);
        Assert.Contains("FVRFireArmClip", source);
        Assert.Contains("ReloadClipWithType", source);
        Assert.Contains("SameFeedType(feed, loadedFeed)", source);
        Assert.Contains("DestroyTrackedEquipment();\n        yield return null;", source.Replace("\r\n", "\n"));
        Assert.Contains("ManagedQuickbeltFeed", source);
        Assert.Contains("managedQuickbeltFeeds.Add", source);
        Assert.Contains("managedFeed.Slot.CurObject != managedFeed.Object", source);
        Assert.DoesNotContain("activeRandomEquipment.AddRange", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BeforeGameStartedEvent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (weaponBufferSpawnHookInstalled)", source, StringComparison.Ordinal);
        Assert.Contains(
            mod.GetProperty("externalPatchTargets").EnumerateArray(),
            target => target.GetProperty("type").GetString() == "GunGame.Scripts.Weapons.WeaponBuffer" &&
                target.GetProperty("method").GetString() == "SpawnAsync");
    }

    [Fact]
    public void Cursed_random_mod_hardens_active_gungame_and_feed_transitions()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "GunGameCursedRandom", "src", "Plugin.cs"));

        Assert.Contains("InstallWeaponBufferSpawnHooks", source);
        Assert.DoesNotContain("AccessTools.TypeByName(\"GunGame.Scripts.Weapons.WeaponBuffer\")", source, StringComparison.Ordinal);
        Assert.Contains("ClearNativePlaceholderFeed", source);
        Assert.Contains("SameFeedType", source);
        Assert.Contains("DestroyGeneratedFeed", source);
        Assert.Contains("queuedProgression", source);
        Assert.Contains("directTransitionProgressionType", source);
        Assert.Contains("WeaponChangedEvent acknowledged direct Cursed transition", source);
        Assert.Contains("buffer.GetType().Assembly.GetType(\"GunGame.Scripts.Progression\", false)", source);
        Assert.Contains("randomSpawnerType", source);
        Assert.Contains("random result read failed", source);
        Assert.Contains("speedloader.ReloadClipWithType", source);
        Assert.Contains("retained loaded generated feed", source);
        Assert.Contains("ReadStaticBool(assembly", source);
        Assert.Contains("slot.CurObject == spare", source);
        Assert.Contains("TryGetValue", source);
        Assert.Contains("CompletePendingRandomSpawn", source);
        Assert.Contains("generated firearm disappeared during cleanup", source);
        Assert.Contains("loadout setup failed after firearm handoff; keeping gun", source);
        Assert.Contains("DestroyGunGameEquipment", source);
        Assert.Contains("DestroyOldEq", source);
        Assert.Contains("native DestroyOldEq ran after vanilla random API start", source);
        Assert.Contains("native DestroyOldEq skipped because random firearm materialized synchronously", source);
        Assert.Contains("IsReusableFeed", source);
        Assert.Contains("feed setup: ignored wrapperless generated feeds", source);
        Assert.Contains("retained attached generated feed", source);
        Assert.DoesNotContain("allFeeds.FirstOrDefault(item => item.transform.IsChildOf(gun.transform))", source, StringComparison.Ordinal);
        Assert.Contains("quickbelt: candidates=[", source);

        var randomApiCall = source.IndexOf("randomGunMethod.Invoke(spawner, null);", StringComparison.Ordinal);
        var nativeCleanup = source.IndexOf("DestroyGunGameEquipment(progression);", StringComparison.Ordinal);
        var waitCoroutine = source.IndexOf("StartCoroutine(FinishRandomSpawn", StringComparison.Ordinal);
        Assert.True(randomApiCall >= 0 && randomApiCall < nativeCleanup && nativeCleanup < waitCoroutine);

        var looseFeedLoad = source.IndexOf("var loadedFeed = LoadFirstCompatibleFeed(gun, looseFeeds);", StringComparison.Ordinal);
        var attachedFeedFallback = source.IndexOf("loadedFeed = attachedFeeds.FirstOrDefault();", StringComparison.Ordinal);
        Assert.True(looseFeedLoad >= 0 && looseFeedLoad < attachedFeedFallback);
    }

    [Fact]
    public void Local_r2modman_registration_has_an_executable_yaml_regression_check()
    {
        var root = FindRepositoryRoot();
        var pipeline = File.ReadAllText(Path.Combine(root, "tools", "h3vr.ps1"));

        Assert.Contains("function New-R2modmanLocalPackageYamlEntry", pipeline);
        Assert.Contains("function Test-R2modmanLocalPackageYamlEntryIsValid", pipeline);
        Assert.Contains("function Test-R2modmanLocalPackageYamlEntry", pipeline);
        Assert.Contains("Test-R2modmanLocalPackageYamlEntry", pipeline);
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
