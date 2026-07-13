using System.Diagnostics;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class GunGameGeneratorTests
{
    [Fact]
    public void GunGame_package_includes_the_independent_runtime_metadata_exporter()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ModsConfigPath));
        var gunGame = document.RootElement
            .GetProperty("mods")
            .GetProperty("GunGameProgressions");

        Assert.Equal(
            "GunGameProgressions\\MetadataExporter\\GunGameProgressionsMetadataExporter.csproj",
            gunGame.GetProperty("metadataExporterCsproj").GetString());
        Assert.Equal("GunGameProgressions", gunGame.GetProperty("profileSource").GetString());
        Assert.Equal(
            "GunGameProgressions\\Thunderstore\\HLin_Mods-GunGameProgression",
            gunGame.GetProperty("packageSource").GetString());

        Assert.Contains(
            gunGame.GetProperty("payload").EnumerateArray(),
            payload => payload.GetProperty("to").GetString() == "GunGameProgressionsMetadataExporter.dll");
        var externalTargets = gunGame.GetProperty("externalPatchTargets").EnumerateArray().ToArray();
        Assert.Contains(
            externalTargets,
            target => target.GetProperty("type").GetString() == "GunGame.Scripts.Progression" &&
                target.GetProperty("method").GetString() == "SpawnAndEquip");
        Assert.Contains(
            externalTargets,
            target => target.GetProperty("type").GetString() == "GunGame.Scripts.Weapons.WeaponBuffer" &&
                target.GetProperty("method").GetString() == "SpawnAsync");
    }

    [Fact]
    public void GunGame_release_metadata_describes_count_mode_and_includes_a_changelog()
    {
        var packageSource = Path.Combine(
            Path.GetDirectoryName(GeneratorPath)!,
            "Thunderstore",
            "HLin_Mods-GunGameProgression");
        var readmePath = Path.Combine(packageSource, "README.md");
        var changelogPath = Path.Combine(packageSource, "CHANGELOG.md");

        Assert.True(File.Exists(changelogPath));
        Assert.Contains("Count mode", File.ReadAllText(readmePath), StringComparison.Ordinal);
        Assert.Contains("## 1.3.3", File.ReadAllText(changelogPath), StringComparison.Ordinal);
        Assert.Contains("'CHANGELOG.md'", File.ReadAllText(H3vrScriptPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Thunderstore_publish_requires_explicit_approval_without_a_vr_receipt_gate()
    {
        var pipeline = File.ReadAllText(H3vrScriptPath);
        var publishStart = pipeline.IndexOf("function Invoke-Publish", StringComparison.Ordinal);
        var publishEnd = pipeline.IndexOf("switch ($Action)", StringComparison.Ordinal);
        var publishFunction = pipeline[publishStart..publishEnd];

        Assert.Contains("if (-not $Publish -or -not $VrApproved)", publishFunction, StringComparison.Ordinal);
        Assert.DoesNotContain("A matching VR receipt marked Result: PASS is required before publishing.", publishFunction, StringComparison.Ordinal);
        Assert.DoesNotContain("$vrReceipt =", publishFunction, StringComparison.Ordinal);
    }

    [Fact]
    public void GunGame_metadata_exporter_builds_for_the_runtime()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{MetadataExporterProjectPath}\" -c Release --no-restore",
            WorkingDirectory = Path.GetDirectoryName(MetadataExporterProjectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process!.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"Metadata exporter build failed with exit code {process.ExitCode}:{Environment.NewLine}{standardOutput}{standardError}");
    }

    [Fact]
    public void GunGame_offline_baselines_provide_vanilla_rot_and_mixed_enemy_profiles()
    {
        var profileDirectory = Path.GetDirectoryName(GeneratorPath)!;
        var profilePaths = Directory
            .EnumerateFiles(profileDirectory, "GunGameWeaponPool_Runtime_*.json")
            .Where(path => !Path.GetFileName(path).Contains("OLD", StringComparison.Ordinal))
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json",
                "GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json",
            },
            profilePaths.Select(Path.GetFileName).ToArray());

        var pools = profilePaths
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)))
            .ToArray();
        try
        {
            Assert.Equal("Runtime 01 - Vanilla Rot", pools[0].RootElement.GetProperty("Name").GetString());
            Assert.Equal("RW_Rot", pools[0].RootElement.GetProperty("Enemies")[0].GetProperty("EnemyNameString").GetString());
            Assert.Equal("Runtime 03 - Vanilla Mixed Enemy", pools[1].RootElement.GetProperty("Name").GetString());
            Assert.True(pools[1].RootElement.GetProperty("Enemies").GetArrayLength() > 1);
            Assert.Equal(0, pools[1].RootElement.GetProperty("EnemyProgressionType").GetInt32());
            Assert.Equal(
                new[] { 8, 5, 3, 2, 1 },
                pools[1].RootElement.GetProperty("Enemies")
                    .EnumerateArray()
                    .Select(enemy => enemy.GetProperty("Value").GetInt32())
                    .ToArray());
            Assert.All(pools, pool => Assert.Equal("Advanced", pool.RootElement.GetProperty("WeaponPoolType").GetString()));
            Assert.All(pools, pool => Assert.Equal(615, pool.RootElement.GetProperty("Guns").GetArrayLength()));
        }
        finally
        {
            foreach (var pool in pools)
            {
                pool.Dispose();
            }
        }

        var pipeline = File.ReadAllText(H3vrScriptPath);
        Assert.Contains("$offlinePoolNames = @(", pipeline, StringComparison.Ordinal);
        Assert.Contains("GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json", pipeline, StringComparison.Ordinal);
        Assert.Contains("GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("Using metadata exported by the installed GunGame package", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("$runtimeMetadataPath", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_profile_rules_load_shared_blacklists_without_System_Web_extensions()
    {
        var assembly = LoadBuiltMetadataExporter();
        var rulesType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.ProfileRules"));
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var load = Assert.IsAssignableFrom<MethodInfo>(rulesType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static));
        var isBlacklisted = Assert.IsAssignableFrom<MethodInfo>(rulesType.GetMethod("IsBlacklisted", BindingFlags.Public | BindingFlags.Instance));

        using var workspace = TestWorkspace.Create();
        File.WriteAllText(
            Path.Combine(workspace.Path, "profile-rules.json"),
            """
            {
              "firearmBlacklist": ["BlockedFirearm"],
              "feedBlacklist": ["BlockedMagazine"]
            }
            """);

        var rules = load.Invoke(null, new object[] { workspace.Path });
        Assert.NotNull(rules);
        Assert.True((bool)isBlacklisted.Invoke(rules, new[] { RuntimeEntry(entryType, "BlockedFirearm", "Firearm", false) })!);
        Assert.True((bool)isBlacklisted.Invoke(rules, new[] { RuntimeEntry(entryType, "BlockedMagazine", "Magazine", false) })!);
        Assert.False((bool)isBlacklisted.Invoke(rules, new[] { RuntimeEntry(entryType, "AllowedMagazine", "Magazine", false) })!);
    }

    [Fact]
    public void Runtime_metadata_entry_tracks_physical_mount_types_for_verified_optics()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var opticKind = entryType.GetProperty("OpticKind");
        var physicalMountTypes = entryType.GetProperty("PhysicalMountTypes");

        Assert.NotNull(opticKind);
        Assert.Equal(typeof(string), opticKind!.PropertyType);
        Assert.NotNull(physicalMountTypes);
        Assert.Equal(typeof(List<string>), physicalMountTypes!.PropertyType);
        Assert.Equal(typeof(List<string>), entryType.GetProperty("ProvidedMountTypes")!.PropertyType);
        Assert.Equal(typeof(float), entryType.GetProperty("OpticMinMagnification")!.PropertyType);
        Assert.Equal(typeof(float), entryType.GetProperty("OpticMaxMagnification")!.PropertyType);
        Assert.Equal(typeof(bool), entryType.GetProperty("IsVariableMagnification")!.PropertyType);
        Assert.True((bool)entryType.GetProperty("IsGunGameRoundDisplaySupported")!.GetValue(Activator.CreateInstance(entryType))!);
    }

    [Fact]
    public void Runtime_enemy_origin_uses_enum_identity_not_ugc_module_ownership()
    {
        var pluginSource = File.ReadAllText(PluginSourcePath);

        Assert.Contains("IsModContent = !isKnownVanillaId", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HasUgcModule(template)", pluginSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_profile_builder_emits_origin_split_count_progression_runtime_pools()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 6);

        entries.SetValue(RuntimeEntry(entryType, "VanillaGun", "Firearm", false, magazineType: 7), 0);
        entries.SetValue(RuntimeEntry(entryType, "ModdedGun", "Firearm", true, clipType: 11), 1);
        entries.SetValue(RuntimeEntry(entryType, "VanillaMagazine", "Magazine", false, magazineType: 7), 2);
        entries.SetValue(RuntimeEntry(entryType, "ModdedClip", "Clip", true, clipType: 11), 3);
        entries.SetValue(RuntimeEntry(entryType, "VanillaAttachment", "Attachment", false), 4);
        entries.SetValue(RuntimeEntry(entryType, "ModdedAttachment", "Attachment", true), 5);

        var enemies = Array.CreateInstance(enemyType, 4);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "M_Swat_Scout", false, 30), 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "Comperator_Heavy_Tier1_Melee", false, 90), 2);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "-55001", true, 45), 3);

        var pools = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d));
        var vanillaRot = pools.Single(pool => ReadString(pool, "Name") == "Runtime 01 - Vanilla Rot");
        var moddedRot = pools.Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot");
        var vanillaMixed = pools.Single(pool => ReadString(pool, "Name") == "Runtime 03 - Vanilla Mixed Enemy");
        var moddedMixed = pools.Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy");

        Assert.Equal(4, pools.Count);
        Assert.Equal("Runtime 01 - Vanilla Rot", ReadString(pools[0], "Name"));
        Assert.All(pools, pool => Assert.Equal("Advanced", ReadString(pool, "WeaponPoolType")));
        Assert.Equal(new[] { "VanillaGun" }, ReadObjects(vanillaRot, "Guns").Select(gun => ReadString(gun, "GunName")).ToArray());
        Assert.Equal(
            new[] { "ModdedGun" },
            ReadObjects(moddedRot, "Guns").Select(gun => ReadString(gun, "GunName")).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "RW_Rot" }, ReadObjects(vanillaRot, "Enemies").Select(enemy => ReadString(enemy, "EnemyNameString")).ToArray());
        Assert.Equal(new[] { "RW_Rot" }, ReadObjects(moddedRot, "Enemies").Select(enemy => ReadString(enemy, "EnemyNameString")).ToArray());
        Assert.Equal(
            new[] { "RW_Rot", "M_Swat_Scout", "Comperator_Heavy_Tier1_Melee" },
            ReadObjects(vanillaMixed, "Enemies").Select(enemy => ReadString(enemy, "EnemyNameString")).Distinct().ToArray());
        Assert.Equal(
            new[] { "RW_Rot", "M_Swat_Scout", "-55001", "Comperator_Heavy_Tier1_Melee" },
            ReadObjects(moddedMixed, "Enemies").Select(enemy => ReadString(enemy, "EnemyNameString")).Distinct().ToArray());
        Assert.Equal(0, ReadInt(vanillaMixed, "EnemyProgressionType"));
        Assert.Equal(0, ReadInt(moddedMixed, "EnemyProgressionType"));
        var moddedMixedGroups = ReadObjects(moddedMixed, "Enemies")
            .GroupBy(enemy => ReadString(enemy, "EnemyNameString"))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        Assert.Equal(13, moddedMixedGroups["RW_Rot"].Count);
        Assert.Equal(8, moddedMixedGroups["M_Swat_Scout"].Count);
        Assert.Single(moddedMixedGroups["Comperator_Heavy_Tier1_Melee"]);
        Assert.Single(moddedMixedGroups["-55001"]);
        Assert.All(moddedMixedGroups["RW_Rot"], enemy => Assert.Equal(8, ReadInt(enemy, "Value")));
        Assert.All(moddedMixedGroups["M_Swat_Scout"], enemy => Assert.Equal(5, ReadInt(enemy, "Value")));
        Assert.All(moddedMixedGroups["Comperator_Heavy_Tier1_Melee"], enemy => Assert.Equal(1, ReadInt(enemy, "Value")));
        Assert.Equal(1, ReadInt(moddedMixedGroups["-55001"].Single(), "Value"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_count_progression_and_inverse_difficulty_spawn_weights()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 2);
        entries.SetValue(RuntimeEntry(entryType, "VanillaGun", "Firearm", false, magazineType: 7), 0);
        entries.SetValue(RuntimeEntry(entryType, "VanillaMagazine", "Magazine", false, magazineType: 7), 1);

        var enemies = Array.CreateInstance(enemyType, 8);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "M_Swat_Scout", false, 30), 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "M_MercWiener_Riflewiener", false, 45), 2);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "M_Swat_SpecOps", false, 70), 3);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "M_Swat_Heavy", false, 120), 4);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "Comperator_Light_Tier1_Melee", false, 30), 5);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "H_BreadCrabZombie_Standard", false, 30), 6);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "Comperator_Heavy_Tier5_LMG", false, 30), 7);

        var mixed = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
            .Single(pool => ReadString(pool, "Name") == "Runtime 03 - Vanilla Mixed Enemy");
        var grouped = ReadObjects(mixed, "Enemies")
            .GroupBy(enemy => ReadString(enemy, "EnemyNameString"))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        Assert.Equal(0, ReadInt(mixed, "EnemyProgressionType"));
        Assert.All(grouped["RW_Rot"], enemy => Assert.Equal(8, ReadInt(enemy, "Value")));
        Assert.All(grouped["M_Swat_Scout"], enemy => Assert.Equal(5, ReadInt(enemy, "Value")));
        Assert.All(grouped["M_MercWiener_Riflewiener"], enemy => Assert.Equal(3, ReadInt(enemy, "Value")));
        Assert.All(grouped["M_Swat_SpecOps"], enemy => Assert.Equal(2, ReadInt(enemy, "Value")));
        Assert.All(grouped["M_Swat_Heavy"], enemy => Assert.Equal(1, ReadInt(enemy, "Value")));
        Assert.Equal(13, grouped["RW_Rot"].Count);
        Assert.Equal(8, grouped["M_Swat_Scout"].Count);
        Assert.Equal(6, grouped["M_MercWiener_Riflewiener"].Count);
        Assert.Equal(4, grouped["M_Swat_SpecOps"].Count);
        Assert.Equal(2, grouped["M_Swat_Heavy"].Count);
        Assert.Equal(2, grouped["Comperator_Light_Tier1_Melee"].Count);
        Assert.All(grouped["Comperator_Light_Tier1_Melee"], enemy => Assert.Equal(2, ReadInt(enemy, "Value")));
        Assert.Single(grouped["Comperator_Heavy_Tier5_LMG"]);
        Assert.Equal(1, ReadInt(grouped["Comperator_Heavy_Tier5_LMG"].Single(), "Value"));
        Assert.Single(grouped["H_BreadCrabZombie_Standard"]);
        Assert.Equal(2, ReadInt(grouped["H_BreadCrabZombie_Standard"].Single(), "Value"));
    }

    [Fact]
    public void Mount_resolution_preserves_exact_mounts_maps_the_temporary_rmr_alias_and_rejects_unknown_numeric_mounts()
    {
        var assembly = LoadBuiltMetadataExporter();
        var resolutionType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.MountResolution"));
        var resolve = Assert.IsAssignableFrom<MethodInfo>(resolutionType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static));

        var rmr = resolve.Invoke(null, new object[] { "99" });
        var picatinny = resolve.Invoke(null, new object[] { "Picatinny" });
        var unknown = resolve.Invoke(null, new object[] { "123" });

        Assert.Equal("99", ReadString(rmr!, "RawMount"));
        Assert.Equal("RMR", ReadString(rmr!, "CanonicalMount"));
        Assert.True((bool)rmr!.GetType().GetProperty("IsResolved")!.GetValue(rmr)!);
        Assert.Equal("Picatinny", ReadString(picatinny!, "CanonicalMount"));
        Assert.True((bool)picatinny!.GetType().GetProperty("IsResolved")!.GetValue(picatinny)!);
        Assert.Equal(string.Empty, ReadString(unknown!, "CanonicalMount"));
        Assert.False((bool)unknown!.GetType().GetProperty("IsResolved")!.GetValue(unknown)!);
    }

    [Fact]
    public void Optic_mount_policy_is_the_shared_sighting_mount_taxonomy()
    {
        var assembly = LoadBuiltMetadataExporter();
        var policyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.OpticMountPolicy"));
        var isOpticMountType = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethod("IsOpticMountType", BindingFlags.Public | BindingFlags.Static));
        var requiresTopSightingOrientation = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethod("RequiresTopSightingOrientation", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)isOpticMountType.Invoke(null, new object[] { "Picatinny" })!);
        Assert.True((bool)isOpticMountType.Invoke(null, new object[] { "MLokRail" })!);
        Assert.True((bool)isOpticMountType.Invoke(null, new object[] { "Russian" })!);
        Assert.True((bool)isOpticMountType.Invoke(null, new object[] { "Scope_Mosin" })!);
        Assert.False((bool)isOpticMountType.Invoke(null, new object[] { "Stock" })!);
        Assert.False((bool)isOpticMountType.Invoke(null, new object[] { "Suppressor" })!);
        Assert.True((bool)requiresTopSightingOrientation.Invoke(null, new object[] { "Picatinny" })!);
        Assert.True((bool)requiresTopSightingOrientation.Invoke(null, new object[] { "MLokRail" })!);
        Assert.False((bool)requiresTopSightingOrientation.Invoke(null, new object[] { "Russian" })!);
    }

    [Fact]
    public void Optic_classifier_excludes_magnifier_object_ids_case_insensitively()
    {
        var assembly = LoadBuiltMetadataExporter();
        var classifierType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.PipScopeOpticClassifier"));
        var classify = Assert.IsAssignableFrom<MethodInfo>(classifierType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Classify" && method.GetParameters().Length == 3));

        Assert.Equal("Magnifier", classify.Invoke(null, new object[] { "AlphaMagnifierBeta", true, false }));
        Assert.Equal("Magnifier", classify.Invoke(null, new object[] { "alphamagnifierbeta", true, false }));

        Assert.Equal("Scope", classify.Invoke(null, new object[] { "ScopeBR4", true, false }));
        Assert.Equal("Reflex", classify.Invoke(null, new object[] { "ReflexRMR", false, true }));
        Assert.Equal(string.Empty, classify.Invoke(null, new object[] { "Attachment", false, false }));

        var classifyFromMetadata = Assert.IsAssignableFrom<MethodInfo>(classifierType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ClassifyFromMetadata" && method.GetParameters().Length == 2));
        Assert.Equal("Magnifier", classifyFromMetadata.Invoke(null, new object[] { "AlphaMagnifierBeta", "Magnification" }));
        Assert.Equal("Reflex", classifyFromMetadata.Invoke(null, new object[] { "ReflexRMR", "Reflex" }));
        Assert.Equal("Scope", classifyFromMetadata.Invoke(null, new object[] { "ScopeBR4", "Magnification" }));
        Assert.Equal(string.Empty, classifyFromMetadata.Invoke(null, new object[] { "Attachment", "None" }));
    }

    [Fact]
    public void Runtime_pool_persistence_rebuilds_when_active_content_changes_or_files_are_missing()
    {
        var assembly = LoadBuiltMetadataExporter();
        var policyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimePoolPersistence"));
        var shouldWrite = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethod("ShouldWrite", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)shouldWrite.Invoke(null, new object?[] { null, "current", true })!);
        Assert.False((bool)shouldWrite.Invoke(null, new object[] { "current", "current", true })!);
        Assert.True((bool)shouldWrite.Invoke(null, new object[] { "previous", "current", true })!);
        Assert.True((bool)shouldWrite.Invoke(null, new object[] { "current", "current", false })!);
    }

    [Fact]
    public void Runtime_pool_persistence_fingerprint_is_stable_and_changes_with_runtime_metadata()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var persistenceType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimePoolPersistence"));
        var createFingerprint = Assert.IsAssignableFrom<MethodInfo>(persistenceType.GetMethod("CreateFingerprint", BindingFlags.Public | BindingFlags.Static));
        var createStableSeed = Assert.IsAssignableFrom<MethodInfo>(persistenceType.GetMethod("CreateStableSeed", BindingFlags.Public | BindingFlags.Static));
        var entries = Array.CreateInstance(entryType, 1);
        entries.SetValue(RuntimeEntry(entryType, "BattleRifle", "Firearm", true, magazineType: 556, roundType: 556), 0);
        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var first = (string)createFingerprint.Invoke(null, new object[] { entries, enemies })!;
        var second = (string)createFingerprint.Invoke(null, new object[] { entries, enemies })!;
        SetRuntimeProperty(entryType, entries.GetValue(0)!, "CompatibleMagazines", new List<string> { "ARMagazine" });
        var changed = (string)createFingerprint.Invoke(null, new object[] { entries, enemies })!;

        Assert.Equal(first, second);
        Assert.NotEqual(first, changed);
        Assert.Equal(
            (int)createStableSeed.Invoke(null, new object[] { changed })!,
            (int)createStableSeed.Invoke(null, new object[] { changed })!);
    }

    [Fact]
    public void GunGame_spawn_safety_policy_rejects_wrong_runtime_object_categories()
    {
        var assembly = LoadBuiltMetadataExporter();
        var policyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.GunGameSpawnSafetyPolicy"));
        var hasExpectedCategory = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethod("HasExpectedCategory", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)hasExpectedCategory.Invoke(null, new object[] { "Gun", "Firearm" })!);
        Assert.False((bool)hasExpectedCategory.Invoke(null, new object[] { "Gun", "Magazine" })!);
        Assert.True((bool)hasExpectedCategory.Invoke(null, new object[] { "Feed", "Magazine" })!);
        Assert.True((bool)hasExpectedCategory.Invoke(null, new object[] { "Feed", "Cartridge" })!);
        Assert.False((bool)hasExpectedCategory.Invoke(null, new object[] { "Feed", "Attachment" })!);
        Assert.True((bool)hasExpectedCategory.Invoke(null, new object[] { "Extra", "Attachment" })!);
        Assert.False((bool)hasExpectedCategory.Invoke(null, new object[] { "Extra", "Magazine" })!);
    }

    [Fact]
    public void GunGame_spawn_safety_wraps_the_single_upstream_spawn_boundary()
    {
        var safetyPath = Path.Combine(Path.GetDirectoryName(PluginSourcePath)!, "GunGameSpawnSafety.cs");
        var source = File.ReadAllText(safetyPath);

        Assert.Contains("SpawnAndEquipPrefix", source, StringComparison.Ordinal);
        Assert.Contains("SpawnAndEquipPostfix", source, StringComparison.Ordinal);
        Assert.Contains("SpawnAndEquipFinalizer", source, StringComparison.Ordinal);
        Assert.Contains("SpawnAsyncPrefix(object __1, ref IEnumerator __result)", source, StringComparison.Ordinal);
        Assert.Contains("TryValidateCurrentLoadout", source, StringComparison.Ordinal);
        Assert.Contains("TryMountGeneratedOptic", source, StringComparison.Ordinal);
        Assert.Contains("TryAttachOpticThroughAdapter", source, StringComparison.Ordinal);
        Assert.Contains("IsTopSightingMount", source, StringComparison.Ordinal);
        Assert.Contains("OpticMountPolicy.IsOpticMountType", source, StringComparison.Ordinal);
        Assert.Contains("OpticMountPolicy.RequiresTopSightingOrientation", source, StringComparison.Ordinal);
        Assert.Contains("AdvancePastInvalidWeapon", source, StringComparison.Ordinal);
        Assert.Contains("yield return null;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Modded_profile_readiness_waits_for_loader_completion_or_five_seconds_of_registry_quiet()
    {
        var assembly = LoadBuiltMetadataExporter();
        var gateType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.ModdedProfileReadinessGate"));
        var stateType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.ExternalContentLoadState"));
        var observe = Assert.IsAssignableFrom<MethodInfo>(gateType.GetMethod("Observe", BindingFlags.Public | BindingFlags.Instance));
        var isReady = Assert.IsAssignableFrom<MethodInfo>(gateType.GetMethod("IsReady", BindingFlags.Public | BindingFlags.Instance));
        var secondsUntilQuiet = Assert.IsAssignableFrom<MethodInfo>(gateType.GetMethod("SecondsUntilQuiet", BindingFlags.Public | BindingFlags.Instance));
        var unavailable = Enum.Parse(stateType, "Unavailable");
        var loading = Enum.Parse(stateType, "Loading");
        var complete = Enum.Parse(stateType, "Complete");
        var gate = Activator.CreateInstance(gateType)!;

        observe.Invoke(gate, new object[] { 0f, 10, unavailable });
        Assert.False((bool)isReady.Invoke(gate, new object[] { 4.9f, unavailable })!);
        Assert.Equal(1, (int)secondsUntilQuiet.Invoke(gate, new object[] { 4.1f, unavailable })!);
        Assert.True((bool)isReady.Invoke(gate, new object[] { 5f, unavailable })!);

        observe.Invoke(gate, new object[] { 5f, 11, unavailable });
        Assert.False((bool)isReady.Invoke(gate, new object[] { 9.9f, unavailable })!);
        Assert.True((bool)isReady.Invoke(gate, new object[] { 10f, unavailable })!);
        Assert.False((bool)isReady.Invoke(gate, new object[] { 100f, loading })!);
        Assert.True((bool)isReady.Invoke(gate, new object[] { 100f, complete })!);
    }

    [Fact]
    public void GunGame_scene_identity_matches_only_the_GunGame_Atlas_identifier()
    {
        var assembly = LoadBuiltMetadataExporter();
        var identityType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.GunGameSceneIdentity"));
        var isMatch = Assert.IsAssignableFrom<MethodInfo>(identityType.GetMethod("IsMatch", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)isMatch.Invoke(null, new object[] { "GunGame" })!);
        Assert.True((bool)isMatch.Invoke(null, new object[] { "gungame" })!);
        Assert.False((bool)isMatch.Invoke(null, new object[] { "GunGamePlus" })!);
        Assert.False((bool)isMatch.Invoke(null, new object[] { string.Empty })!);
    }

    [Fact]
    public void Runtime_profile_families_partition_vanilla_and_modded_outputs()
    {
        var assembly = LoadBuiltMetadataExporter();
        var familyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileFamily"));
        var isVanilla = Assert.IsAssignableFrom<MethodInfo>(familyType.GetMethod("IsVanilla", BindingFlags.Public | BindingFlags.Static));
        var isModded = Assert.IsAssignableFrom<MethodInfo>(familyType.GetMethod("IsModded", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)isVanilla.Invoke(null, new object[] { "01_Vanilla_Rot" })!);
        Assert.True((bool)isVanilla.Invoke(null, new object[] { "03_Vanilla_Mixed_Enemy" })!);
        Assert.False((bool)isVanilla.Invoke(null, new object[] { "02_Modded_Rot" })!);
        Assert.True((bool)isModded.Invoke(null, new object[] { "02_Modded_Rot" })!);
        Assert.True((bool)isModded.Invoke(null, new object[] { "04_Modded_Mixed_Enemy" })!);
        Assert.False((bool)isModded.Invoke(null, new object[] { "01_Vanilla_Rot" })!);
    }

    [Fact]
    public void Runtime_profile_families_recognize_only_their_own_runtime_pool_files()
    {
        var assembly = LoadBuiltMetadataExporter();
        var familyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileFamily"));
        var isVanillaFile = Assert.IsAssignableFrom<MethodInfo>(familyType.GetMethod("IsVanillaPoolFile", BindingFlags.Public | BindingFlags.Static));
        var isModdedFile = Assert.IsAssignableFrom<MethodInfo>(familyType.GetMethod("IsModdedPoolFile", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)isVanillaFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json" })!);
        Assert.True((bool)isVanillaFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json" })!);
        Assert.False((bool)isVanillaFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_02_Modded_Rot_RW_Rot.json" })!);
        Assert.True((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_02_Modded_Rot_RW_Rot.json" })!);
        Assert.True((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_04_Modded_Mixed_Enemy_RW_Rot.json" })!);
        Assert.True((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_04_Modded_Mixed_Enemy_CustomSosig.json" })!);
        Assert.False((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json" })!);
        Assert.False((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_05_Unknown_RW_Rot.json" })!);
    }

    [Fact]
    public void Atlas_menu_scene_resolver_reads_the_Atlas_menu_definition_shape()
    {
        var assembly = LoadBuiltMetadataExporter();
        var resolverType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.AtlasMenuSceneResolver"));
        var getSceneInfo = Assert.IsAssignableFrom<MethodInfo>(resolverType.GetMethod("GetSceneInfo", BindingFlags.Public | BindingFlags.Static));
        var sceneInfo = new object();
        var menuScreen = new AtlasMenuScreenFixture
        {
            m_def = new AtlasSceneDefinitionFixture { CustomSceneInfo = sceneInfo },
        };

        Assert.Same(sceneInfo, getSceneInfo.Invoke(null, new object[] { menuScreen }));
        Assert.Null(getSceneInfo.Invoke(null, new object[] { new AtlasMenuScreenFixture() }));
    }

    [Fact]
    public void Atlas_menu_scene_resolver_identifies_only_the_GunGame_selection()
    {
        var assembly = LoadBuiltMetadataExporter();
        var resolverType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.AtlasMenuSceneResolver"));
        var isGunGameSelection = Assert.IsAssignableFrom<MethodInfo>(resolverType.GetMethod("IsGunGameSelection", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)isGunGameSelection.Invoke(null, new object[] { AtlasMenuScreen("GunGame") })!);
        Assert.False((bool)isGunGameSelection.Invoke(null, new object[] { AtlasMenuScreen("Sandbox") })!);
        Assert.False((bool)isGunGameSelection.Invoke(null, new object[] { new AtlasMenuScreenFixture() })!);
    }

    [Fact]
    public void Runtime_status_messages_are_concise_and_cover_pool_generation()
    {
        var assembly = LoadBuiltMetadataExporter();
        var messages = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeStatusMessages"));
        var lifecycle = new[]
            {
                "Ready",
                "VanillaPoolsReady",
                "Preparing",
                "PoolsReady",
                "NoModdedPools",
                "ProfileUiUpdateFailed",
                "FallbackPools",
                "SpawnSafetyUnavailable",
            }
            .Select(name => (string)messages.GetField(name, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!)
            .ToArray();

        Assert.Equal("GunGame Progressions: ready.", lifecycle[0]);
        Assert.Equal("GunGame Progressions: vanilla pools ready.", lifecycle[1]);
        Assert.Equal("GunGame Progressions: preparing pools.", lifecycle[2]);
        Assert.Equal("GunGame Progressions: pools ready.", lifecycle[3]);
        Assert.Equal("GunGame Progressions: no modded pools available.", lifecycle[4]);
        Assert.Equal("GunGame Progressions: could not add modded pools.", lifecycle[5]);
        Assert.Equal("GunGame Progressions: using packaged fallback pools.", lifecycle[6]);
        Assert.Equal("GunGame Progressions: spawn safety unavailable.", lifecycle[7]);
        Assert.All(lifecycle, message => Assert.True(message.Length <= 60));
    }

    [Fact]
    public void Runtime_warms_modded_profiles_before_the_GunGame_selector_opens()
    {
        var source = File.ReadAllText(PluginSourcePath);
        var startMethod = source.IndexOf("private void Start()", StringComparison.Ordinal);
        var destroyMethod = source.IndexOf("private void OnDestroy()", StringComparison.Ordinal);

        Assert.True(startMethod >= 0);
        Assert.True(destroyMethod > startMethod);
        var startBody = source[startMethod..destroyMethod];
        Assert.Contains("StartCoroutine(GenerateVanillaPoolsAtStartup());", startBody, StringComparison.Ordinal);
        Assert.Contains("RequestModdedRefresh();", startBody, StringComparison.Ordinal);
        Assert.DoesNotContain("StartCoroutine(GenerateModdedPoolsAtStartup());", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private IEnumerator GenerateModdedPoolsAtStartup()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_keeps_vanilla_profiles_playable_while_modded_profiles_load_into_the_active_selector()
    {
        var source = File.ReadAllText(PluginSourcePath);

        Assert.Contains("WeaponLoadedEvent", source, StringComparison.Ordinal);
        Assert.Contains("WeaponPoolLoaderReady", source, StringComparison.Ordinal);
        Assert.Contains("AddEventHandler", source, StringComparison.Ordinal);
        Assert.Contains("RemoveEventHandler", source, StringComparison.Ordinal);
        Assert.Contains("GunGame.Scripts.Weapons.WeaponPoolLoader", source, StringComparison.Ordinal);
        Assert.Contains("GameManagerOnDestroyPostfix", source, StringComparison.Ordinal);
        Assert.Contains("PrepareModdedProfilesForSelector", source, StringComparison.Ordinal);
        Assert.Contains("CreateModdedProfileLoadingDisplay", source, StringComparison.Ordinal);
        Assert.Contains("UpdateModdedProfileLoadingDisplay", source, StringComparison.Ordinal);
        Assert.Contains("AddGeneratedPoolChoices", source, StringComparison.Ordinal);
        Assert.Contains("Waiting for mod content", source, StringComparison.Ordinal);
        Assert.Contains("RequestModdedRefresh", source, StringComparison.Ordinal);
        Assert.Contains("RefreshModdedPoolsInBackground", source, StringComparison.Ordinal);
        Assert.Contains("ModdedProfileReadinessGate", source, StringComparison.Ordinal);
        Assert.Contains("OtherLoader.LoaderStatus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MainMenuScreenLoadScenePrefix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstGunGameModWarmupSeconds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnDemandGenerationGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WeaponPoolLoaderAwakePostfix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WatchForGunGamePoolLoader", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_selector_tracker_handles_one_live_selector_instance_at_a_time()
    {
        var assembly = LoadBuiltMetadataExporter();
        var trackerType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.GunGameSelectorInstanceTracker"));
        var tracker = Activator.CreateInstance(trackerType)!;
        var observe = Assert.IsAssignableFrom<MethodInfo>(trackerType.GetMethod("Observe", BindingFlags.Instance | BindingFlags.Public));
        var firstSelector = new object();
        var secondSelector = new object();

        Assert.False((bool)observe.Invoke(tracker, new object?[] { null })!);
        Assert.True((bool)observe.Invoke(tracker, new[] { firstSelector })!);
        Assert.False((bool)observe.Invoke(tracker, new[] { firstSelector })!);
        Assert.False((bool)observe.Invoke(tracker, new object?[] { null })!);
        Assert.True((bool)observe.Invoke(tracker, new[] { secondSelector })!);
    }

    [Fact]
    public void Runtime_selector_locator_reads_the_Kodeman_singleton_before_scanning_the_scene()
    {
        var assembly = LoadBuiltMetadataExporter();
        var locatorType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.GunGameSelectorLocator"));
        var resolveSingleton = Assert.IsAssignableFrom<MethodInfo>(locatorType.GetMethod("ResolveSingleton", BindingFlags.Public | BindingFlags.Static));
        var selector = new GunGameSelectorSingletonFixture();
        GunGameSelectorSingletonFixture.Instance = selector;

        try
        {
            Assert.Same(selector, resolveSingleton.Invoke(null, new object[] { typeof(GunGameSelectorSingletonFixture) }));
        }
        finally
        {
            GunGameSelectorSingletonFixture.Instance = null;
        }
    }

    [Fact]
    public void Runtime_item_role_reclassifies_mislabeled_firearm_metadata_from_prefab_components()
    {
        var assembly = LoadBuiltMetadataExporter();
        var roleType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeItemRole"));
        var resolve = Assert.IsAssignableFrom<MethodInfo>(roleType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static));

        var firearm = resolve.Invoke(null, new object[] { "Firearm", true, false, false, false, false });
        var magazine = resolve.Invoke(null, new object[] { "Firearm", false, true, false, false, false });
        var attachment = resolve.Invoke(null, new object[] { "Firearm", false, false, false, false, false });

        Assert.Equal("Firearm", firearm);
        Assert.Equal("Magazine", magazine);
        Assert.Equal("Unknown", attachment);
    }

    [Fact]
    public void Enemy_weight_policy_preserves_the_current_operator_tier_weights()
    {
        var assembly = LoadBuiltMetadataExporter();
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var policyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.EnemyWeightPolicy"));
        var resolve = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static));

        var rot = resolve.Invoke(null, new[] { RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5) })!;
        var standard = resolve.Invoke(null, new[] { RuntimeEnemyEntry(enemyType, "Comperator_Light_Tier1_Melee", false, 30) })!;
        var apex = resolve.Invoke(null, new[] { RuntimeEnemyEntry(enemyType, "Comperator_Heavy_Tier5_LMG", false, 30) })!;

        Assert.Equal((8, 13), (ReadInt(rot, "Value"), ReadInt(rot, "Multiplicity")));
        Assert.Equal((2, 2), (ReadInt(standard, "Value"), ReadInt(standard, "Multiplicity")));
        Assert.Equal((1, 1), (ReadInt(apex, "Value"), ReadInt(apex, "Multiplicity")));
    }

    [Fact]
    public void Runtime_profile_builder_selects_only_exact_mount_verified_optics()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 9);

        var pistol = RuntimeEntry(entryType, "RmrPistol", "Firearm", true);
        SetRuntimeProperty(entryType, pistol, "CompatibleMagazines", new List<string> { "PistolMagazine" });
        SetRuntimeProperty(entryType, pistol, "FirearmSize", "Pistol");
        SetRuntimeProperty(entryType, pistol, "FirearmMounts", new List<string> { "RMR" });
        SetRuntimeProperty(entryType, pistol, "PhysicalMountTypes", new List<string> { "99", "Picatinny" });
        SetRuntimeProperty(entryType, pistol, "BespokeAttachments", new List<string>());
        entries.SetValue(pistol, 0);

        var rifle = RuntimeEntry(entryType, "PicatinnyRifle", "Firearm", true);
        SetRuntimeProperty(entryType, rifle, "CompatibleMagazines", new List<string> { "RifleMagazine" });
        SetRuntimeProperty(entryType, rifle, "FirearmSize", "FullSize");
        SetRuntimeProperty(entryType, rifle, "FirearmMounts", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, rifle, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(rifle, 1);

        entries.SetValue(RuntimeEntry(entryType, "PistolMagazine", "Magazine", false, magazineType: 1), 2);
        entries.SetValue(RuntimeEntry(entryType, "RifleMagazine", "Magazine", false, magazineType: 2), 3);

        var picatinnyReflex = RuntimeEntry(entryType, "A_PicatinnyReflex", "Attachment", true);
        SetRuntimeProperty(entryType, picatinnyReflex, "AttachmentMount", "Picatinny");
        SetRuntimeProperty(entryType, picatinnyReflex, "AttachmentFeature", "Reflex");
        SetRuntimeProperty(entryType, picatinnyReflex, "OpticKind", "Reflex");
        SetRuntimeProperty(entryType, picatinnyReflex, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(picatinnyReflex, 4);

        var magnifier = RuntimeEntry(entryType, "B_PicatinnyMagnifier", "Attachment", true);
        SetRuntimeProperty(entryType, magnifier, "AttachmentMount", "Picatinny");
        SetRuntimeProperty(entryType, magnifier, "AttachmentFeature", "Magnification");
        SetRuntimeProperty(entryType, magnifier, "OpticKind", "Magnifier");
        SetRuntimeProperty(entryType, magnifier, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(magnifier, 5);

        var scope = RuntimeEntry(entryType, "Y_PicatinnyScope", "Attachment", true);
        SetRuntimeProperty(entryType, scope, "AttachmentMount", "Picatinny");
        SetRuntimeProperty(entryType, scope, "AttachmentFeature", "Magnification");
        SetRuntimeProperty(entryType, scope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(scope, 6);

        var reflex = RuntimeEntry(entryType, "Z_RmrReflex", "Attachment", true);
        SetRuntimeProperty(entryType, reflex, "AttachmentMount", "RMR");
        SetRuntimeProperty(entryType, reflex, "AttachmentFeature", "Reflex");
        SetRuntimeProperty(entryType, reflex, "OpticKind", "Reflex");
        SetRuntimeProperty(entryType, reflex, "PhysicalMountTypes", new List<string> { "RMR" });
        entries.SetValue(reflex, 7);

        var mismatchedScope = RuntimeEntry(entryType, "WrongTagScope", "Attachment", true);
        SetRuntimeProperty(entryType, mismatchedScope, "AttachmentMount", "Picatinny");
        SetRuntimeProperty(entryType, mismatchedScope, "AttachmentFeature", "Magnification");
        SetRuntimeProperty(entryType, mismatchedScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, mismatchedScope, "PhysicalMountTypes", new List<string> { "Russian" });
        entries.SetValue(mismatchedScope, 8);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var pools = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d));
        var modded = pools
            .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy");
        var guns = ReadObjects(modded, "Guns");

        Assert.Equal("Z_RmrReflex", ReadString(guns.Single(gun => ReadString(gun, "GunName") == "RmrPistol"), "Extra"));
        Assert.Equal("Y_PicatinnyScope", ReadString(guns.Single(gun => ReadString(gun, "GunName") == "PicatinnyRifle"), "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_a_picatinny_scope_for_a_pistol_without_a_dedicated_reflex_mount()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 3);

        var revolver = RuntimeEntry(entryType, "PicatinnyRevolver", "Firearm", true);
        SetRuntimeProperty(entryType, revolver, "CompatibleMagazines", new List<string> { "RevolverSpeedloader" });
        SetRuntimeProperty(entryType, revolver, "FirearmSize", "Pistol");
        SetRuntimeProperty(entryType, revolver, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, revolver, "BespokeAttachments", new List<string>());
        entries.SetValue(revolver, 0);
        entries.SetValue(RuntimeEntry(entryType, "RevolverSpeedloader", "Magazine", true, magazineType: 9), 1);

        var scope = RuntimeEntry(entryType, "PicatinnyScope", "Attachment", true);
        SetRuntimeProperty(entryType, scope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(scope, 2);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var modded = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
            .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy");
        var gun = ReadObjects(modded, "Guns").Single();

        Assert.Equal("PicatinnyScope", ReadString(gun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_prefers_a_picatinny_reflex_for_close_range_firearms()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 5);

        var pistol = RuntimeEntry(entryType, "PicatinnyPistol", "Firearm", true);
        SetRuntimeProperty(entryType, pistol, "CompatibleSpeedLoaders", new List<string> { "PistolSpeedloader" });
        SetRuntimeProperty(entryType, pistol, "FirearmSize", "Pistol");
        SetRuntimeProperty(entryType, pistol, "PhysicalMountTypes", new List<string> { "NonOpticMountA", "NonOpticMountB", "Picatinny" });
        entries.SetValue(pistol, 0);
        entries.SetValue(RuntimeEntry(entryType, "PistolSpeedloader", "SpeedLoader", true, roundType: 9), 1);

        var picatinnyScope = RuntimeEntry(entryType, "PicatinnyScope", "Attachment", true);
        SetRuntimeProperty(entryType, picatinnyScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, picatinnyScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(picatinnyScope, 2);

        var picatinnyReflex = RuntimeEntry(entryType, "PicatinnyReflex", "Attachment", true);
        SetRuntimeProperty(entryType, picatinnyReflex, "OpticKind", "Reflex");
        SetRuntimeProperty(entryType, picatinnyReflex, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(picatinnyReflex, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("PicatinnyReflex", ReadString(gun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_matches_verified_picatinny_optics_to_firearm_role()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 10);

        var sniper = RuntimeEntry(entryType, "Sniper", "Firearm", true);
        SetRuntimeProperty(entryType, sniper, "CompatibleMagazines", new List<string> { "SniperMagazine" });
        SetRuntimeProperty(entryType, sniper, "FirearmSize", "FullSize");
        SetRuntimeProperty(entryType, sniper, "FirearmRoundPower", "FullPower");
        SetRuntimeProperty(entryType, sniper, "FirearmAction", "BoltAction");
        SetRuntimeProperty(entryType, sniper, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(sniper, 0);

        var smg = RuntimeEntry(entryType, "Smg", "Firearm", true);
        SetRuntimeProperty(entryType, smg, "CompatibleMagazines", new List<string> { "SmgMagazine" });
        SetRuntimeProperty(entryType, smg, "FirearmSize", "Compact");
        SetRuntimeProperty(entryType, smg, "FirearmRoundPower", "Pistol");
        SetRuntimeProperty(entryType, smg, "FirearmAction", "Automatic");
        SetRuntimeProperty(entryType, smg, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(smg, 1);

        var rifle = RuntimeEntry(entryType, "Rifle", "Firearm", true);
        SetRuntimeProperty(entryType, rifle, "CompatibleMagazines", new List<string> { "RifleMagazine" });
        SetRuntimeProperty(entryType, rifle, "FirearmSize", "FullSize");
        SetRuntimeProperty(entryType, rifle, "FirearmRoundPower", "Intermediate");
        SetRuntimeProperty(entryType, rifle, "FirearmAction", "Automatic");
        SetRuntimeProperty(entryType, rifle, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(rifle, 2);

        entries.SetValue(RuntimeEntry(entryType, "SniperMagazine", "Magazine", true, magazineType: 1), 3);
        entries.SetValue(RuntimeEntry(entryType, "SmgMagazine", "Magazine", true, magazineType: 2), 4);
        entries.SetValue(RuntimeEntry(entryType, "RifleMagazine", "Magazine", true, magazineType: 3), 5);

        entries.SetValue(Optic(entryType, "PicatinnyReflex", "Reflex", 1f, 1f, false), 6);
        entries.SetValue(Optic(entryType, "PicatinnyLowScope", "Scope", 1f, 4f, false), 7);
        entries.SetValue(Optic(entryType, "PicatinnyVariableScope", "Scope", 1f, 6f, true), 8);
        entries.SetValue(Optic(entryType, "PicatinnyHighScope", "Scope", 6f, 24f, true), 9);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .ToDictionary(gun => ReadString(gun, "GunName"), StringComparer.Ordinal);

        Assert.Equal("PicatinnyHighScope", ReadString(guns["Sniper"], "Extra"));
        Assert.Equal("PicatinnyReflex", ReadString(guns["Smg"], "Extra"));
        Assert.Equal("PicatinnyVariableScope", ReadString(guns["Rifle"], "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_applies_one_optic_policy_to_vanilla_and_modded_profiles()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 6);

        var vanillaRifle = RuntimeEntry(entryType, "VanillaRifle", "Firearm", false, magazineType: 1);
        SetRuntimeProperty(entryType, vanillaRifle, "CompatibleMagazines", new List<string> { "VanillaMagazine" });
        SetRuntimeProperty(entryType, vanillaRifle, "FirearmSize", "FullSize");
        SetRuntimeProperty(entryType, vanillaRifle, "FirearmRoundPower", "Intermediate");
        SetRuntimeProperty(entryType, vanillaRifle, "FirearmAction", "Automatic");
        SetRuntimeProperty(entryType, vanillaRifle, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(vanillaRifle, 0);

        var moddedSmg = RuntimeEntry(entryType, "ModdedSmg", "Firearm", true, magazineType: 2);
        SetRuntimeProperty(entryType, moddedSmg, "CompatibleMagazines", new List<string> { "ModdedMagazine" });
        SetRuntimeProperty(entryType, moddedSmg, "FirearmSize", "Compact");
        SetRuntimeProperty(entryType, moddedSmg, "FirearmRoundPower", "Pistol");
        SetRuntimeProperty(entryType, moddedSmg, "FirearmAction", "Automatic");
        SetRuntimeProperty(entryType, moddedSmg, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(moddedSmg, 1);

        entries.SetValue(RuntimeEntry(entryType, "VanillaMagazine", "Magazine", false, magazineType: 1), 2);
        entries.SetValue(RuntimeEntry(entryType, "ModdedMagazine", "Magazine", true, magazineType: 2), 3);

        var vanillaVariableScope = RuntimeEntry(entryType, "VanillaVariableScope", "Attachment", false);
        SetRuntimeProperty(entryType, vanillaVariableScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, vanillaVariableScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, vanillaVariableScope, "OpticMinMagnification", 1f);
        SetRuntimeProperty(entryType, vanillaVariableScope, "OpticMaxMagnification", 6f);
        SetRuntimeProperty(entryType, vanillaVariableScope, "IsVariableMagnification", true);
        entries.SetValue(vanillaVariableScope, 4);

        var moddedReflex = RuntimeEntry(entryType, "ModdedReflex", "Attachment", true);
        SetRuntimeProperty(entryType, moddedReflex, "OpticKind", "Reflex");
        SetRuntimeProperty(entryType, moddedReflex, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(moddedReflex, 5);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var pools = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d));

        var vanillaGun = ReadObjects(pools.Single(pool => ReadString(pool, "Name") == "Runtime 01 - Vanilla Rot"), "Guns").Single();
        var moddedGun = ReadObjects(pools.Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"), "Guns").Single();

        Assert.Equal("VanillaVariableScope", ReadString(vanillaGun, "Extra"));
        Assert.Equal("ModdedReflex", ReadString(moddedGun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_skips_firearms_without_gungame_round_display_data()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var safe = RuntimeEntry(entryType, "SafeGun", "Firearm", false);
        SetRuntimeProperty(entryType, safe, "CompatibleMagazines", new List<string> { "SafeMagazine" });
        entries.SetValue(safe, 0);
        var unsupported = RuntimeEntry(entryType, "UnsupportedGun", "Firearm", false);
        SetRuntimeProperty(entryType, unsupported, "CompatibleMagazines", new List<string> { "UnsupportedMagazine" });
        SetRuntimeProperty(entryType, unsupported, "IsGunGameRoundDisplaySupported", false);
        entries.SetValue(unsupported, 1);
        entries.SetValue(RuntimeEntry(entryType, "SafeMagazine", "Magazine", false, magazineType: 1), 2);
        entries.SetValue(RuntimeEntry(entryType, "UnsupportedMagazine", "Magazine", false, magazineType: 2), 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 01 - Vanilla Rot"),
            "Guns");

        Assert.Equal(new[] { "SafeGun" }, guns.Select(gun => ReadString(gun, "GunName")).ToArray());
    }

    [Fact]
    public void Runtime_profile_builder_ignores_unrecognized_non_optic_mounts()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 3);

        var firearm = RuntimeEntry(entryType, "NonOpticMountFirearm", "Firearm", true);
        SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "Magazine" });
        SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "NonOpticMountA", "NonOpticMountB" });
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "Magazine", "Magazine", true, magazineType: 1), 1);

        var invalidScope = RuntimeEntry(entryType, "UnrecognizedMountScope", "Attachment", true);
        SetRuntimeProperty(entryType, invalidScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, invalidScope, "PhysicalMountTypes", new List<string> { "NonOpticMountA" });
        entries.SetValue(invalidScope, 2);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal(string.Empty, ReadString(gun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_prefers_a_proprietary_scope_mount_over_picatinny()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var firearm = RuntimeEntry(entryType, "DualMountFirearm", "Firearm", true);
        SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "Magazine" });
        SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "Picatinny", "M16HandleMount" });
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "Magazine", "Magazine", true, magazineType: 1), 1);

        var picatinnyScope = RuntimeEntry(entryType, "A_PicatinnyScope", "Attachment", true);
        SetRuntimeProperty(entryType, picatinnyScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, picatinnyScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(picatinnyScope, 2);

        var proprietaryScope = RuntimeEntry(entryType, "Z_HandleMountScope", "Attachment", true);
        SetRuntimeProperty(entryType, proprietaryScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, proprietaryScope, "PhysicalMountTypes", new List<string> { "M16HandleMount" });
        entries.SetValue(proprietaryScope, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("Z_HandleMountScope", ReadString(gun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_a_verified_bespoke_scope_when_its_prefab_mount_metadata_is_missing()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var firearm = RuntimeEntry(entryType, "PythonRifle", "Firearm", true);
        SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "PythonMagazine" });
        SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "PythonScopeMount", "Picatinny" });
        SetRuntimeProperty(entryType, firearm, "BespokeAttachments", new List<string> { "ScopePython" });
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "PythonMagazine", "Magazine", true, magazineType: 1), 1);

        // ScopePython is a real proprietary choice. Its prefab exposes the
        // physical mount through the attachment component at runtime, but old
        // capture data did not retain that field, so direct game compatibility
        // must still beat the generic Picatinny fallback.
        var pythonScope = RuntimeEntry(entryType, "ScopePython", "Attachment", true);
        SetRuntimeProperty(entryType, pythonScope, "AttachmentFeature", "Magnification");
        SetRuntimeProperty(entryType, pythonScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, pythonScope, "PhysicalMountTypes", new List<string>());
        entries.SetValue(pythonScope, 2);

        var picatinnyScope = RuntimeEntry(entryType, "PicatinnyScope", "Attachment", true);
        SetRuntimeProperty(entryType, picatinnyScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, picatinnyScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(picatinnyScope, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("ScopePython", ReadString(gun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_an_adapter_when_a_special_top_rail_exposes_picatinny()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var firearm = RuntimeEntry(entryType, "FamasG2", "Firearm", true);
        SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "FamasMagazine" });
        SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "FamasTopRail" });
        SetRuntimeProperty(entryType, firearm, "FirearmSize", "FullSize");
        SetRuntimeProperty(entryType, firearm, "FirearmRoundPower", "Intermediate");
        SetRuntimeProperty(entryType, firearm, "FirearmAction", "Automatic");
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "FamasMagazine", "Magazine", true, magazineType: 1), 1);

        var adapter = RuntimeEntry(entryType, "RailAdapterFamas", "Attachment", true);
        SetRuntimeProperty(entryType, adapter, "AttachmentFeature", "Adapter");
        SetRuntimeProperty(entryType, adapter, "PhysicalMountTypes", new List<string> { "FamasTopRail" });
        SetRuntimeProperty(entryType, adapter, "ProvidedMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(adapter, 2);

        var optic = RuntimeEntry(entryType, "PicatinnyVariableScope", "Attachment", true);
        SetRuntimeProperty(entryType, optic, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, optic, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, optic, "OpticMinMagnification", 1f);
        SetRuntimeProperty(entryType, optic, "OpticMaxMagnification", 6f);
        SetRuntimeProperty(entryType, optic, "IsVariableMagnification", true);
        entries.SetValue(optic, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("PicatinnyVariableScope", ReadString(gun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_prefers_a_russian_side_rail_scope_over_other_shared_mounts()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 5);

        var rifle = RuntimeEntry(entryType, "RussianRailRifle", "Firearm", true, magazineType: 7);
        SetRuntimeProperty(entryType, rifle, "FirearmSize", "FullSize");
        SetRuntimeProperty(entryType, rifle, "PhysicalMountTypes", new List<string> { "Russian", "Picatinny", "Bespoke" });
        SetRuntimeProperty(entryType, rifle, "CompatibleMagazines", new List<string> { "RussianRifleMagazine" });
        entries.SetValue(rifle, 0);
        entries.SetValue(RuntimeEntry(entryType, "RussianRifleMagazine", "Magazine", true, magazineType: 7), 1);

        var genericScope = RuntimeEntry(entryType, "A_BespokePistolScope", "Attachment", true);
        SetRuntimeProperty(entryType, genericScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, genericScope, "PhysicalMountTypes", new List<string> { "Bespoke" });
        entries.SetValue(genericScope, 2);
        var russianScope = RuntimeEntry(entryType, "Z_RussianSideRailScope", "Attachment", true);
        SetRuntimeProperty(entryType, russianScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, russianScope, "PhysicalMountTypes", new List<string> { "Russian" });
        entries.SetValue(russianScope, 3);
        var picatinnyScope = RuntimeEntry(entryType, "B_PicatinnyScope", "Attachment", true);
        SetRuntimeProperty(entryType, picatinnyScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, picatinnyScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(picatinnyScope, 4);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns")
            .Single();

        Assert.Equal("Z_RussianSideRailScope", ReadString(gun, "Extra"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_a_loose_round_only_after_no_higher_priority_feed_exists()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 2);

        entries.SetValue(RuntimeEntry(entryType, "MagazineFedPistol", "Firearm", true, magazineType: 77, roundType: 6), 0);
        entries.SetValue(RuntimeEntry(entryType, "LooseRound", "Cartridge", true, roundType: 6), 1);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("LooseRound", ReadString(gun, "MagName"));
        Assert.Equal(2, ReadInt(gun, "CategoryID"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_shells_for_a_shell_only_shotgun()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 3);

        var shotgun = RuntimeEntry(entryType, "ShellOnlyShotgun", "Firearm", true, magazineType: 77, roundType: 12);
        SetRuntimeProperty(entryType, shotgun, "FirearmRoundPower", "Shotgun");
        SetRuntimeProperty(entryType, shotgun, "FirearmFeedOptions", new List<string> { "InternalMag" });
        SetRuntimeProperty(entryType, shotgun, "CompatibleSingleRounds", new List<string> { "Shell12Gauge" });
        entries.SetValue(shotgun, 0);
        entries.SetValue(RuntimeEntry(entryType, "MisleadingMagazine", "Magazine", true, magazineType: 77), 1);
        entries.SetValue(RuntimeEntry(entryType, "Shell12Gauge", "Cartridge", true, roundType: 12), 2);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns")
            .Single();

        Assert.Equal("Shell12Gauge", ReadString(gun, "MagName"));
        Assert.Equal(2, ReadInt(gun, "CategoryID"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_shells_for_a_breach_loaded_shotgun_before_speedloaders()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var shotgun = RuntimeEntry(entryType, "BreachLoadedShorty", "Firearm", true, roundType: 12);
        SetRuntimeProperty(entryType, shotgun, "FirearmRoundPower", "Shotgun");
        SetRuntimeProperty(entryType, shotgun, "FirearmAction", "BreakAction");
        SetRuntimeProperty(entryType, shotgun, "FirearmFeedOptions", new List<string> { "BreachLoad" });
        SetRuntimeProperty(entryType, shotgun, "CompatibleSingleRounds", new List<string> { "Shell12Gauge" });
        entries.SetValue(shotgun, 0);
        entries.SetValue(RuntimeEntry(entryType, "MisleadingRotaryMagazine", "Magazine", true, magazineType: 12), 1);
        entries.SetValue(RuntimeEntry(entryType, "RotarySpeedloader", "SpeedLoader", true, roundType: 12), 2);
        entries.SetValue(RuntimeEntry(entryType, "Shell12Gauge", "Cartridge", true, roundType: 12), 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("Shell12Gauge", ReadString(gun, "MagName"));
        Assert.Equal(2, ReadInt(gun, "CategoryID"));
    }

    [Fact]
    public void Runtime_profile_builder_skips_unclassified_firearm_entries()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var safeFirearm = RuntimeEntry(entryType, "SafeFirearm", "Firearm", true, magazineType: 1);
        entries.SetValue(safeFirearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "SafeMagazine", "Magazine", true, magazineType: 1), 1);

        var unclassified = RuntimeEntry(entryType, "UnclassifiedFirearm", "Firearm", true, roundType: 158);
        SetRuntimeProperty(entryType, unclassified, "FirearmAction", "None");
        SetRuntimeProperty(entryType, unclassified, "FirearmRoundPower", "None");
        entries.SetValue(unclassified, 2);
        entries.SetValue(RuntimeEntry(entryType, "GenericCartridge", "Cartridge", true, roundType: 158), 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns");

        Assert.Equal(new[] { "SafeFirearm" }, guns.Select(gun => ReadString(gun, "GunName")).ToArray());
    }

    [Fact]
    public void Runtime_profile_builder_resolves_feeds_in_magazine_clip_speedloader_cartridge_order()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 10);

        var magazineFirst = RuntimeEntry(entryType, "MagazineFirst", "Firearm", true, magazineType: 81, clipType: 82, roundType: 83);
        SetRuntimeProperty(entryType, magazineFirst, "CompatibleSingleRounds", new List<string> { "Cartridge83" });
        entries.SetValue(magazineFirst, 0);
        var clipSecond = RuntimeEntry(entryType, "ClipSecond", "Firearm", true, magazineType: 99, clipType: 82, roundType: 83);
        SetRuntimeProperty(entryType, clipSecond, "CompatibleSingleRounds", new List<string> { "Cartridge83" });
        entries.SetValue(clipSecond, 1);
        var speedloaderThird = RuntimeEntry(entryType, "SpeedloaderThird", "Firearm", true, magazineType: 99, clipType: 98, roundType: 83);
        SetRuntimeProperty(entryType, speedloaderThird, "CompatibleSpeedLoaders", new List<string> { "Speedloader83" });
        SetRuntimeProperty(entryType, speedloaderThird, "CompatibleSingleRounds", new List<string> { "Cartridge83" });
        entries.SetValue(speedloaderThird, 2);
        var cartridgeLast = RuntimeEntry(entryType, "CartridgeLast", "Firearm", true, roundType: 84);
        SetRuntimeProperty(entryType, cartridgeLast, "CompatibleSingleRounds", new List<string> { "Cartridge84" });
        entries.SetValue(cartridgeLast, 3);

        entries.SetValue(RuntimeEntry(entryType, "Magazine81", "Magazine", true, magazineType: 81), 4);
        entries.SetValue(RuntimeEntry(entryType, "Clip82", "Clip", true, clipType: 82), 5);
        entries.SetValue(RuntimeEntry(entryType, "Speedloader83", "SpeedLoader", true, roundType: 83), 6);
        entries.SetValue(RuntimeEntry(entryType, "Cartridge83", "Cartridge", true, roundType: 83), 7);
        entries.SetValue(RuntimeEntry(entryType, "Cartridge84", "Cartridge", true, roundType: 84), 8);
        entries.SetValue(RuntimeEntry(entryType, "UnusedAttachment", "Attachment", true), 9);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .ToDictionary(gun => ReadString(gun, "GunName"), StringComparer.Ordinal);

        Assert.Equal("Magazine81", ReadString(guns["MagazineFirst"], "MagName"));
        Assert.Equal("Clip82", ReadString(guns["ClipSecond"], "MagName"));
        Assert.Equal("Speedloader83", ReadString(guns["SpeedloaderThird"], "MagName"));
        Assert.Equal("Cartridge84", ReadString(guns["CartridgeLast"], "MagName"));
    }

    [Fact]
    public void Runtime_profile_builder_does_not_infer_a_speedloader_from_round_type()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 3);

        var firearm = RuntimeEntry(entryType, "NoSpeedloaderCompatibility", "Firearm", true, roundType: 83);
        SetRuntimeProperty(entryType, firearm, "CompatibleSingleRounds", new List<string> { "SafeCartridge83" });
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "WrongSpeedloader83", "SpeedLoader", true, roundType: 83), 1);
        entries.SetValue(RuntimeEntry(entryType, "SafeCartridge83", "Cartridge", true, roundType: 83), 2);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("SafeCartridge83", ReadString(gun, "MagName"));
        Assert.Equal(2, ReadInt(gun, "CategoryID"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_shells_for_an_internal_magazine_pump_shotgun()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var actionProperty = entryType.GetProperty("FirearmAction");
        var feedOptionsProperty = entryType.GetProperty("FirearmFeedOptions");

        Assert.NotNull(actionProperty);
        Assert.NotNull(feedOptionsProperty);

        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);
        var shotgun = RuntimeEntry(entryType, "InternalPumpShotgun", "Firearm", true, magazineType: 77, roundType: 12);
        SetRuntimeProperty(entryType, shotgun, "FirearmRoundPower", "Shotgun");
        SetRuntimeProperty(entryType, shotgun, "FirearmAction", "PumpAction");
        SetRuntimeProperty(entryType, shotgun, "FirearmFeedOptions", new List<string> { "InternalMag" });
        SetRuntimeProperty(entryType, shotgun, "CompatibleMagazines", new List<string> { "RotaryShotgunMagazine" });
        SetRuntimeProperty(entryType, shotgun, "CompatibleSingleRounds", new List<string> { "Shell12Gauge" });
        entries.SetValue(shotgun, 0);
        entries.SetValue(RuntimeEntry(entryType, "RotaryShotgunMagazine", "Magazine", true, magazineType: 77), 1);
        entries.SetValue(RuntimeEntry(entryType, "Shell12Gauge", "Cartridge", true, roundType: 12), 2);
        entries.SetValue(RuntimeEntry(entryType, "BoxMagazineShotgun", "Firearm", true, magazineType: 77, roundType: 12), 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns");
        var pump = guns.Single(gun => ReadString(gun, "GunName") == "InternalPumpShotgun");

        Assert.Equal("Shell12Gauge", ReadString(pump, "MagName"));
        Assert.Equal(2, ReadInt(pump, "CategoryID"));
    }

    [Fact]
    public void Runtime_profile_builder_uses_shells_for_non_box_shotguns_in_both_profile_families()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 6);

        var vanilla = RuntimeEntry(entryType, "VanillaTubeShotgun", "Firearm", false, roundType: 12);
        SetRuntimeProperty(entryType, vanilla, "FirearmRoundPower", "Shotgun");
        SetRuntimeProperty(entryType, vanilla, "FirearmAction", "PumpAction");
        SetRuntimeProperty(entryType, vanilla, "CompatibleMagazines", new List<string> { "WrongRotaryMagazine" });
        entries.SetValue(vanilla, 0);

        var modded = RuntimeEntry(entryType, "ModdedBreakAction", "Firearm", true, roundType: 12);
        SetRuntimeProperty(entryType, modded, "FirearmRoundPower", "Shotgun");
        SetRuntimeProperty(entryType, modded, "FirearmAction", "BreakAction");
        SetRuntimeProperty(entryType, modded, "FirearmFeedOptions", new List<string> { "BreachLoad" });
        SetRuntimeProperty(entryType, modded, "CompatibleSingleRounds", new List<string> { "Shell12Gauge" });
        entries.SetValue(modded, 1);

        entries.SetValue(RuntimeEntry(entryType, "WrongRotaryMagazine", "Magazine", false, magazineType: 12), 2);
        entries.SetValue(RuntimeEntry(entryType, "WrongRotaryLoader", "SpeedLoader", false, roundType: 12), 3);
        entries.SetValue(RuntimeEntry(entryType, "Shell12Gauge", "Cartridge", false, roundType: 12), 4);
        entries.SetValue(RuntimeEntry(entryType, "UnusedAttachment", "Attachment", false), 5);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var pools = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d));

        var vanillaGun = ReadObjects(pools.Single(pool => ReadString(pool, "Name") == "Runtime 01 - Vanilla Rot"), "Guns").Single();
        var moddedGun = ReadObjects(pools.Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"), "Guns").Single();

        Assert.Equal("Shell12Gauge", ReadString(vanillaGun, "MagName"));
        Assert.Equal("Shell12Gauge", ReadString(moddedGun, "MagName"));
        Assert.Equal(2, ReadInt(vanillaGun, "CategoryID"));
        Assert.Equal(2, ReadInt(moddedGun, "CategoryID"));
    }

    [Fact]
    public void Runtime_profile_builder_keeps_a_revolver_shotguns_direct_speedloader()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 3);

        var shotgun = RuntimeEntry(entryType, "RevolverShotgun", "Firearm", true, roundType: 12);
        SetRuntimeProperty(entryType, shotgun, "FirearmRoundPower", "Shotgun");
        SetRuntimeProperty(entryType, shotgun, "FirearmAction", "Revolver");
        SetRuntimeProperty(entryType, shotgun, "FirearmFeedOptions", new List<string> { "BreachLoad" });
        SetRuntimeProperty(entryType, shotgun, "CompatibleSpeedLoaders", new List<string> { "RevolverShotgunLoader" });
        SetRuntimeProperty(entryType, shotgun, "CompatibleSingleRounds", new List<string> { "Shell12Gauge" });
        entries.SetValue(shotgun, 0);
        entries.SetValue(RuntimeEntry(entryType, "RevolverShotgunLoader", "SpeedLoader", true, roundType: 12), 1);
        entries.SetValue(RuntimeEntry(entryType, "Shell12Gauge", "Cartridge", true, roundType: 12), 2);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("RevolverShotgunLoader", ReadString(gun, "MagName"));
        Assert.Equal(2, ReadInt(gun, "CategoryID"));
    }

    [Fact]
    public void Runtime_profile_builder_skips_a_box_fed_shotgun_without_a_compatible_loader()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 5);

        var shotgun = RuntimeEntry(entryType, "UnknownBoxShotgun", "Firearm", true, roundType: 12);
        SetRuntimeProperty(entryType, shotgun, "FirearmRoundPower", "Shotgun");
        SetRuntimeProperty(entryType, shotgun, "FirearmAction", "Automatic");
        SetRuntimeProperty(entryType, shotgun, "FirearmFeedOptions", new List<string> { "BoxMag" });
        SetRuntimeProperty(entryType, shotgun, "CompatibleSingleRounds", new List<string> { "Shell12Gauge" });
        entries.SetValue(shotgun, 0);
        entries.SetValue(RuntimeEntry(entryType, "WrongRotaryLoader", "SpeedLoader", true, roundType: 12), 1);
        entries.SetValue(RuntimeEntry(entryType, "Shell12Gauge", "Cartridge", true, roundType: 12), 2);
        entries.SetValue(RuntimeEntry(entryType, "SafeControlFirearm", "Firearm", true, magazineType: 7), 3);
        entries.SetValue(RuntimeEntry(entryType, "SafeControlMagazine", "Magazine", true, magazineType: 7), 4);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns");

        Assert.Equal(new[] { "SafeControlFirearm" }, guns.Select(gun => ReadString(gun, "GunName")).ToArray());
    }

    [Fact]
    public void Generator_builds_a_vanilla_pool_with_resolved_feeds_and_a_compatible_vanilla_scope()
    {
        using var workspace = TestWorkspace.Create();
        var inputPath = Path.Combine(workspace.Path, "MetaRipper.json");
        var outputPath = Path.Combine(workspace.Path, "output");
        File.WriteAllText(inputPath, JsonSerializer.Serialize(FixtureItems));

        var result = RunGenerator(inputPath, outputPath);

        Assert.True(
            result.ExitCode == 0,
            $"Generator failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardError}");

        var poolPath = Path.Combine(outputPath, "GunGameWeaponPool_All_in_One_RW_Rot.json");
        using var document = JsonDocument.Parse(File.ReadAllText(poolPath));
        var root = document.RootElement;
        var guns = root.GetProperty("Guns").EnumerateArray().ToArray();

        Assert.Equal("Advanced", root.GetProperty("WeaponPoolType").GetString());
        Assert.Equal(0, root.GetProperty("EnemyProgressionType").GetInt32());
        Assert.Equal(
            new[] { "VanillaMagazineGun", "LeverSentinelGun", "PKM" },
            guns.Select(gun => gun.GetProperty("GunName").GetString()).ToArray());
        Assert.Equal(
            new[] { "TestMagazine", "TestCartridge", "MagazinePKM" },
            guns.Select(gun => gun.GetProperty("MagName").GetString()).ToArray());
        Assert.Equal(
            new[] { "TestMagazine" },
            guns.Single(gun => gun.GetProperty("GunName").GetString() == "VanillaMagazineGun")
                .GetProperty("MagNames")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.Equal(
            "VanillaScope",
            guns.Single(gun => gun.GetProperty("GunName").GetString() == "VanillaMagazineGun")
                .GetProperty("Extra")
                .GetString());
    }

    [Fact]
    public void Generator_preserves_existing_valid_feed_assignments()
    {
        using var workspace = TestWorkspace.Create();
        var inputPath = Path.Combine(workspace.Path, "MetaRipper.json");
        var outputPath = Path.Combine(workspace.Path, "output");
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(inputPath, JsonSerializer.Serialize(FixtureItemsWithExistingMagazine));
        File.WriteAllText(
            Path.Combine(outputPath, "GunGameWeaponPool_All_in_One_RW_Rot.json"),
            """
            {
              "GunNames": ["VanillaMagazineGun"],
              "MagNames": ["ExistingCompatibleMagazine"],
              "CategoryIDs": [0]
            }
            """);

        var result = RunGenerator(inputPath, outputPath);

        Assert.True(
            result.ExitCode == 0,
            $"Generator failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardError}");

        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(outputPath, "GunGameWeaponPool_All_in_One_RW_Rot.json")));
        var root = document.RootElement;
        var vanillaGun = root.GetProperty("Guns").EnumerateArray()
            .Single(gun => gun.GetProperty("GunName").GetString() == "VanillaMagazineGun");

        Assert.Equal("ExistingCompatibleMagazine", vanillaGun.GetProperty("MagName").GetString());
        Assert.Equal(0, vanillaGun.GetProperty("CategoryID").GetInt32());
    }

    [Fact]
    public void Generator_discards_a_legacy_primary_from_a_lower_priority_feed_class()
    {
        using var workspace = TestWorkspace.Create();
        var inputPath = Path.Combine(workspace.Path, "MetaRipper.json");
        var outputPath = Path.Combine(workspace.Path, "output");
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(inputPath, JsonSerializer.Serialize(FixtureItems));
        File.WriteAllText(
            Path.Combine(outputPath, "GunGameWeaponPool_All_in_One_RW_Rot.json"),
            """
            {
              "GunNames": ["VanillaMagazineGun"],
              "MagNames": ["TestCartridge"],
              "CategoryIDs": [2]
            }
            """);

        var result = RunGenerator(inputPath, outputPath);

        Assert.True(
            result.ExitCode == 0,
            $"Generator failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardError}");

        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(outputPath, "GunGameWeaponPool_All_in_One_RW_Rot.json")));
        var vanillaGun = document.RootElement.GetProperty("Guns").EnumerateArray()
            .Single(gun => gun.GetProperty("GunName").GetString() == "VanillaMagazineGun");

        Assert.Equal("TestMagazine", vanillaGun.GetProperty("MagName").GetString());
        Assert.Equal(0, vanillaGun.GetProperty("CategoryID").GetInt32());
    }

    [Fact]
    public void Generator_converts_a_named_legacy_subset_profile_to_advanced()
    {
        using var workspace = TestWorkspace.Create();
        var inputPath = Path.Combine(workspace.Path, "MetaRipper.json");
        var outputPath = Path.Combine(workspace.Path, "output");
        var sourceProfilePath = Path.Combine(workspace.Path, "GunGameWeaponPool_All_in_One_New_Guns.json");
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(inputPath, JsonSerializer.Serialize(FixtureItems));
        File.WriteAllText(
            sourceProfilePath,
            """
            {
              "Name": "All_in_One_New_Guns",
              "Description": "Only recently added guns",
              "OrderType": 1,
              "EnemyType": "RW_Rot",
              "GunNames": ["VanillaMagazineGun"],
              "MagNames": ["TestMagazine"],
              "CategoryIDs": [0]
            }
            """);

        var result = RunGenerator(
            inputPath,
            outputPath,
            $"--source-profile \"{sourceProfilePath}\" --output-name GunGameWeaponPool_All_in_One_New_Guns.json");

        Assert.True(
            result.ExitCode == 0,
            $"Generator failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardError}");

        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(outputPath, "GunGameWeaponPool_All_in_One_New_Guns.json")));
        var root = document.RootElement;
        var guns = root.GetProperty("Guns").EnumerateArray().ToArray();

        Assert.Equal("Advanced", root.GetProperty("WeaponPoolType").GetString());
        Assert.Equal("All_in_One_New_Guns", root.GetProperty("Name").GetString());
        Assert.Equal("Only recently added guns", root.GetProperty("Description").GetString());
        Assert.Single(guns);
        Assert.Equal("VanillaMagazineGun", guns[0].GetProperty("GunName").GetString());
    }

    private static readonly object[] FixtureItems =
    {
        Item(
            "VanillaMagazineGun",
            "Firearm",
            false,
            magazineType: 77,
            compatibleMagazines: new[] { "MagazineMp515rnd", "TestMagazine", "ExistingCompatibleMagazine" },
            compatibleSingleRounds: new[] { "TestCartridge" },
            firearmMounts: new[] { "Picatinny" }),
        Item("LeverSentinelGun", "Firearm", false, magazineType: 999, roundType: 2),
        Item("PKM", "Firearm", false, magazineType: 1968691, roundType: 17),
        Item("ZeroFeedGun", "Firearm", false),
        Item("ModdedGun", "Firearm", true, roundType: 2),
        Item("PotatoGun", "Firearm", false, roundType: 2),
        Item("TestMagazine", "Magazine", false, magazineType: 1),
        Item("MagazineMp515rnd", "Magazine", false, magazineType: 77),
        Item("MagazinePKM", "Magazine", false, magazineType: 155, roundType: 17),
        Item("TestCartridge", "Cartridge", false, roundType: 2),
        Item("22LRCartridgeTracer", "Cartridge", false),
        Item("VanillaScope", "Attachment", false, attachmentFeature: "Magnification", attachmentMount: "Picatinny"),
        Item("ModdedScope", "Attachment", true, attachmentFeature: "Magnification", attachmentMount: "Picatinny"),
    };

    private static readonly object[] FixtureItemsWithExistingMagazine =
        FixtureItems.Append(Item("ExistingCompatibleMagazine", "Magazine", false, magazineType: 1)).ToArray();

    private static object Item(
        string objectId,
        string category,
        bool isModContent,
        int magazineType = 0,
        int clipType = 0,
        int roundType = 0,
        string[]? compatibleMagazines = null,
        string[]? compatibleSingleRounds = null,
        string[]? firearmMounts = null,
        string attachmentFeature = "None",
        string attachmentMount = "None")
    {
        return new
        {
            ObjectID = objectId,
            Category = category,
            IsModContent = isModContent,
            MagazineType = magazineType,
            ClipType = clipType,
            RoundType = roundType,
            CompatibleMagazines = compatibleMagazines ?? Array.Empty<string>(),
            CompatibleSingleRounds = compatibleSingleRounds ?? Array.Empty<string>(),
            FirearmMounts = firearmMounts ?? Array.Empty<string>(),
            AttachmentFeature = attachmentFeature,
            AttachmentMount = attachmentMount,
        };
    }

    private static ProcessResult RunGenerator(string inputPath, string outputPath, string extraArguments = "")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{GeneratorPath}\" 0 --input \"{inputPath}\" --output-dir \"{outputPath}\" --seed 0 {extraArguments}",
            WorkingDirectory = Path.GetDirectoryName(GeneratorPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process!.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string GeneratorPath
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "GunGameProgressions", "jsonGen.py");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate GunGameProgressions/jsonGen.py.");
        }
    }

    private static string ModsConfigPath
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "build", "mods.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate build/mods.json.");
        }
    }

    private static string H3vrScriptPath
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "tools", "h3vr.ps1");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate tools/h3vr.ps1.");
        }
    }

    private static string MetadataExporterProjectPath
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidate = Path.Combine(
                    current.FullName,
                    "GunGameProgressions",
                    "MetadataExporter",
                    "GunGameProgressionsMetadataExporter.csproj");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the GunGame metadata exporter project.");
        }
    }

    private static string PluginSourcePath
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidate = Path.Combine(
                    current.FullName,
                    "GunGameProgressions",
                    "MetadataExporter",
                    "src",
                    "Plugin.cs");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the GunGame metadata exporter source.");
        }
    }

    private static Assembly LoadBuiltMetadataExporter()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{MetadataExporterProjectPath}\" -c Release --no-restore",
            WorkingDirectory = Path.GetDirectoryName(MetadataExporterProjectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process!.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"Metadata exporter build failed:{Environment.NewLine}{standardOutput}{standardError}");

        var originalPath = Path.Combine(
            Path.GetDirectoryName(MetadataExporterProjectPath)!,
            "bin",
            "Release",
            "net35",
            "GunGameProgressionsMetadataExporter.dll");
        var loadDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(loadDirectory);
        var loadPath = Path.Combine(loadDirectory, Path.GetFileName(originalPath));
        File.Copy(originalPath, loadPath);
        return Assembly.LoadFrom(loadPath);
    }

    private static object RuntimeEntry(
        Type entryType,
        string objectId,
        string category,
        bool isModContent,
        int magazineType = 0,
        int clipType = 0,
        int roundType = 0)
    {
        var entry = Activator.CreateInstance(entryType)!;
        entryType.GetProperty("ObjectID")!.SetValue(entry, objectId);
        entryType.GetProperty("Category")!.SetValue(entry, category);
        entryType.GetProperty("IsModContent")!.SetValue(entry, isModContent);
        entryType.GetProperty("MagazineType")!.SetValue(entry, magazineType);
        entryType.GetProperty("ClipType")!.SetValue(entry, clipType);
        entryType.GetProperty("RoundType")!.SetValue(entry, roundType);
        return entry;
    }

    private static object Optic(
        Type entryType,
        string objectId,
        string opticKind,
        float minimumMagnification,
        float maximumMagnification,
        bool variableMagnification)
    {
        var optic = RuntimeEntry(entryType, objectId, "Attachment", true);
        SetRuntimeProperty(entryType, optic, "OpticKind", opticKind);
        SetRuntimeProperty(entryType, optic, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, optic, "OpticMinMagnification", minimumMagnification);
        SetRuntimeProperty(entryType, optic, "OpticMaxMagnification", maximumMagnification);
        SetRuntimeProperty(entryType, optic, "IsVariableMagnification", variableMagnification);
        return optic;
    }

    private static object RuntimeEnemyEntry(Type enemyType, string enemyNameString, bool isModContent, int difficultyScore)
    {
        var enemy = Activator.CreateInstance(enemyType)!;
        enemyType.GetProperty("EnemyNameString")!.SetValue(enemy, enemyNameString);
        enemyType.GetProperty("DisplayName")!.SetValue(enemy, enemyNameString);
        enemyType.GetProperty("IsModContent")!.SetValue(enemy, isModContent);
        enemyType.GetProperty("IsSpawnable")!.SetValue(enemy, true);
        enemyType.GetProperty("DifficultyScore")!.SetValue(enemy, difficultyScore);
        return enemy;
    }

    private static List<object> BuildRuntimePools(MethodInfo build, Array entries, Array enemies, Random random)
    {
        var result = Assert.IsAssignableFrom<IEnumerable>(build.Invoke(null, new object[] { entries, enemies, random }));
        return result.Cast<object>().ToList();
    }

    private static void SetRuntimeProperty(Type entryType, object entry, string propertyName, object value)
    {
        entryType.GetProperty(propertyName)!.SetValue(entry, value);
    }

    private static List<object> ReadObjects(object value, string propertyName)
    {
        return ((IEnumerable)value.GetType().GetProperty(propertyName)!.GetValue(value)!)
            .Cast<object>()
            .ToList();
    }

    private static string ReadString(object value, string propertyName)
    {
        return (string)value.GetType().GetProperty(propertyName)!.GetValue(value)!;
    }

    private static int ReadInt(object value, string propertyName)
    {
        return (int)value.GetType().GetProperty(propertyName)!.GetValue(value)!;
    }

    private static string[] ReadStrings(object value, string propertyName)
    {
        return ((IEnumerable)value.GetType().GetProperty(propertyName)!.GetValue(value)!)
            .Cast<string>()
            .ToArray();
    }

    private sealed class AtlasMenuScreenFixture
    {
        public object? m_def;
    }

    private sealed class AtlasSceneDefinitionFixture
    {
        public object? CustomSceneInfo;
    }

    private static AtlasMenuScreenFixture AtlasMenuScreen(string identifier)
    {
        return new AtlasMenuScreenFixture
        {
            m_def = new AtlasSceneDefinitionFixture
            {
                CustomSceneInfo = new AtlasSceneInfoFixture { Identifier = identifier },
            },
        };
    }

    private sealed class AtlasSceneInfoFixture
    {
        public string Identifier = string.Empty;
    }

    private class GunGameSelectorSingletonBase<T> where T : class
    {
        public static T? Instance { get; set; }
    }

    private sealed class GunGameSelectorSingletonFixture : GunGameSelectorSingletonBase<GunGameSelectorSingletonFixture>
    {
    }

    private sealed class SequenceRandom : Random
    {
        private readonly Queue<double> samples;

        public SequenceRandom(params double[] samples)
        {
            this.samples = new Queue<double>(samples);
        }

        protected override double Sample()
        {
            return samples.Count == 0 ? 0d : samples.Dequeue();
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestWorkspace Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TestWorkspace(path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
