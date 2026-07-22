using System.Diagnostics;
using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class GunGameGeneratorTests
{
    private static readonly Lazy<Assembly> RuntimeMetadataExporter = new(BuildMetadataExporter);

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
        Assert.Equal(
            "GunGameProgressions\\OfflineProfileGenerator\\OfflineProfileGenerator.csproj",
            gunGame.GetProperty("offlineProfileGeneratorCsproj").GetString());
        Assert.Equal("GunGameProgressions", gunGame.GetProperty("profileSource").GetString());
        Assert.Equal(
            "GunGameProgressions\\Thunderstore\\HLin_Mods-GunGameProgression",
            gunGame.GetProperty("packageSource").GetString());

        Assert.Contains(
            gunGame.GetProperty("payload").EnumerateArray(),
            payload => payload.GetProperty("to").GetString() == "GunGameProgressionsMetadataExporter.dll");
        Assert.Contains(
            gunGame.GetProperty("payload").EnumerateArray(),
            payload =>
                payload.GetProperty("from").GetString() == "GunGameProgressions\\ObjectData.json" &&
                payload.GetProperty("to").GetString() == "ObjectData.json");
        Assert.Contains(
            gunGame.GetProperty("payload").EnumerateArray(),
            payload =>
                payload.GetProperty("from").GetString() ==
                    "GunGameProgressions\\MetadataExporter\\bin\\{configuration}\\net35\\GunGameProgressionsMetadataExporter.dll" &&
                payload.GetProperty("to").GetString() == "GunGameProgressionsMetadataExporter.dll");
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
        var readme = File.ReadAllText(readmePath);
        Assert.Contains("Count mode", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Runtime 05", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Compatibility Probe", readme, StringComparison.Ordinal);
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

    [WindowsH3vrFact]
    public void GunGame_metadata_exporter_builds_for_the_runtime()
    {
        _ = LoadBuiltMetadataExporter();
    }

    [WindowsH3vrFact]
    public void GunGame_release_exporter_disables_runtime_05_and_debug_exporter_compiles()
    {
        var assembly = LoadBuiltMetadataExporter();
        var features = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeBuildFeatures"));
        var enabled = Assert.IsAssignableFrom<PropertyInfo>(features.GetProperty("CompatibilityProbeEnabled", BindingFlags.Public | BindingFlags.Static));
        Assert.False((bool)enabled.GetValue(null)!);

        var debugAssembly = LoadBuiltMetadataExporter("Debug");
        var debugFeatures = Assert.IsAssignableFrom<Type>(debugAssembly.GetType("HLin.GunGameProgressions.RuntimeBuildFeatures"));
        var debugEnabled = Assert.IsAssignableFrom<PropertyInfo>(debugFeatures.GetProperty("CompatibilityProbeEnabled", BindingFlags.Public | BindingFlags.Static));
        Assert.True((bool)debugEnabled.GetValue(null)!);
    }

    [Fact]
    public void GunGame_windows_pipeline_enables_runtime_metadata_exporter_tests()
    {
        var pipeline = File.ReadAllText(H3vrScriptPath);
        Assert.Contains("$env:H3VR_METADATA_EXPORTER_TESTS = '1'", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void GunGame_runtime_05_is_debug_only_and_release_packages_reject_it()
    {
        var project = File.ReadAllText(MetadataExporterProjectPath);
        Assert.Contains("Condition=\"'$(Configuration)' == 'Debug'\"", project, StringComparison.Ordinal);
        Assert.Contains("GUNGAME_COMPATIBILITY_PROBE", project, StringComparison.Ordinal);

        var source = File.ReadAllText(PluginSourcePath);
        Assert.Contains("#if GUNGAME_COMPATIBILITY_PROBE", source, StringComparison.Ordinal);
        Assert.Contains("private IEnumerator GenerateCompatibilityProbe(", source, StringComparison.Ordinal);
        Assert.Contains("RuntimeBuildFeatures.CompatibilityProbeEnabled", source, StringComparison.Ordinal);

        var pipeline = File.ReadAllText(H3vrScriptPath);
        Assert.Contains("[string]$GunGameBuildConfiguration = 'Release'", pipeline, StringComparison.Ordinal);
        Assert.Contains("[ValidateSet('Release', 'Debug')]", pipeline, StringComparison.Ordinal);
        Assert.Contains("bin\\\\{configuration}\\\\net35\\\\GunGameProgressionsMetadataExporter.dll", File.ReadAllText(ModsConfigPath), StringComparison.Ordinal);
        Assert.Contains("function Assert-GunGameReleasePackage", pipeline, StringComparison.Ordinal);
        Assert.Contains("GunGame release package must not contain Runtime 05 pool files.", pipeline, StringComparison.Ordinal);
        Assert.Contains("GunGame Debug packages are local-only and cannot be published.", pipeline, StringComparison.Ordinal);
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
            Assert.All(pools, pool => Assert.Equal(659, pool.RootElement.GetProperty("Guns").GetArrayLength()));
            Assert.All(
                pools,
                pool => Assert.DoesNotContain(
                    pool.RootElement.GetProperty("Guns").EnumerateArray(),
                    gun => string.IsNullOrEmpty(gun.GetProperty("Extra").GetString())));
            var goldenWeaponIds = pools[0].RootElement
                .GetProperty("Guns")
                .EnumerateArray()
                .Select(gun => gun.GetProperty("GunName").GetString())
                .ToArray();
            Assert.Equal(659, goldenWeaponIds.Distinct(StringComparer.Ordinal).Count());
            Assert.All(
                new[] { "Slingshot", "BrownBess", "Degle", "JunkyardFlameThrower", "LaserPistol", "MF_Flamethrower", "Stinger", "PlungerLauncher" },
                weaponId => Assert.DoesNotContain(weaponId, goldenWeaponIds));
            Assert.All(
                new[] { "GravitonBeamer", "HiroEnki", "RailTater", "SustenanceCrossbow" },
                weaponId => Assert.Contains(weaponId, goldenWeaponIds));

            var metadataPath = Path.Combine(profileDirectory, "ObjectData.json");
            Assert.True(File.Exists(metadataPath));
            using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
            Assert.Equal(4339, metadata.RootElement.GetArrayLength());
            Assert.All(metadata.RootElement.EnumerateArray(), entry => Assert.False(entry.GetProperty("IsModContent").GetBoolean()));
            var variablePicatinnyScopes = metadata.RootElement
                .EnumerateArray()
                .Where(entry =>
                    entry.GetProperty("Category").GetString() == "Attachment" &&
                    entry.GetProperty("OpticKind").GetString() == "Scope" &&
                    entry.GetProperty("IsVariableMagnification").GetBoolean() &&
                    entry.GetProperty("PhysicalMountTypes").EnumerateArray().Any(mount => mount.GetString() == "Picatinny"))
                .Select(entry => entry.GetProperty("ObjectID").GetString())
                .ToArray();
            var m4CarbineExtra = pools[0].RootElement
                .GetProperty("Guns")
                .EnumerateArray()
                .Single(gun => gun.GetProperty("GunName").GetString() == "M4A1Block2CQBR")
                .GetProperty("Extra")
                .GetString();
            Assert.Contains(m4CarbineExtra, variablePicatinnyScopes);
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
        Assert.Contains("offlineProfileGeneratorCsproj", pipeline, StringComparison.Ordinal);
        Assert.Contains("Missing versioned GunGame vanilla metadata snapshot", pipeline, StringComparison.Ordinal);
        Assert.Contains("GunGame offline metadata must contain only vanilla entries.", pipeline, StringComparison.Ordinal);
        Assert.Contains("$hasApprovedEmptyFeed", pipeline, StringComparison.Ordinal);
        Assert.Contains("$gun.GunName -eq 'GravitonBeamer'", pipeline, StringComparison.Ordinal);
        Assert.Contains("--verify", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("Using metadata exported by the installed GunGame package", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("$runtimeMetadataPath", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void GunGame_offline_fallbacks_are_generated_by_the_shared_runtime_profile_builder()
    {
        var profileDirectory = Path.GetDirectoryName(GeneratorPath)!;
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{OfflineProfileGeneratorProjectPath}\" -c Release -- --input \"{Path.Combine(profileDirectory, "ObjectData.json")}\" --output-dir \"{profileDirectory}\" --verify",
            WorkingDirectory = Path.GetDirectoryName(OfflineProfileGeneratorProjectPath)!,
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
            $"Offline fallback does not match the shared runtime profile builder:{Environment.NewLine}{standardOutput}{standardError}");
    }

    [WindowsH3vrFact]
    public void Offline_generator_emits_a_metadata_only_runtime_05_scope_audit()
    {
        var profileDirectory = Path.GetDirectoryName(GeneratorPath)!;
        using var workspace = TestWorkspace.Create();
        var inputPath = Path.Combine(workspace.Path, "ObjectData.json");
        var rulesPath = Path.Combine(workspace.Path, "profile-rules.json");
        var outputPath = Path.Combine(workspace.Path, "Runtime05.json");
        File.Copy(Path.Combine(profileDirectory, "ObjectData.json"), inputPath);
        File.Copy(Path.Combine(profileDirectory, "profile-rules.json"), rulesPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{OfflineProfileGeneratorProjectPath}\" -c Release -- --input \"{inputPath}\" --probe-output \"{outputPath}\"",
            WorkingDirectory = Path.GetDirectoryName(OfflineProfileGeneratorProjectPath)!,
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
            $"Runtime 05 metadata audit failed:{Environment.NewLine}{standardOutput}{standardError}");
        using var pool = JsonDocument.Parse(File.ReadAllText(outputPath));
        var guns = pool.RootElement.GetProperty("Guns").EnumerateArray().ToArray();
        var gunNames = guns.Select(gun => gun.GetProperty("GunName").GetString()).ToArray();
        using var rules = JsonDocument.Parse(File.ReadAllText(rulesPath));
        var runtimeBlacklist = rules.RootElement
            .GetProperty("runtimeFirearmBlacklist")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var globalBlacklist = rules.RootElement
            .GetProperty("firearmBlacklist")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.NotEmpty(guns);
        Assert.All(guns, gun => Assert.False(string.IsNullOrEmpty(gun.GetProperty("Extra").GetString())));
        Assert.DoesNotContain(gunNames, gunName => runtimeBlacklist.Contains(gunName));
        Assert.DoesNotContain(gunNames, gunName => globalBlacklist.Contains(gunName));
        Assert.Equal(
            new[]
            {
                "Airgun", "Flaregun", "MF_Medical180", "Pocket1906", "Quackenbush1886",
            },
            gunNames);
    }

    [WindowsH3vrFact]
    public void Runtime_profile_rules_load_shared_blacklists_without_System_Web_extensions()
    {
        var assembly = LoadBuiltMetadataExporter();
        var rulesType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.ProfileRules"));
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var load = Assert.IsAssignableFrom<MethodInfo>(rulesType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static));
        var isBlacklisted = Assert.IsAssignableFrom<MethodInfo>(rulesType.GetMethod("IsBlacklisted", BindingFlags.Public | BindingFlags.Instance));
        var isGloballyBlacklisted = Assert.IsAssignableFrom<MethodInfo>(rulesType.GetMethod("IsGloballyBlacklisted", BindingFlags.Public | BindingFlags.Instance));

        using var workspace = TestWorkspace.Create();
        File.WriteAllText(
            Path.Combine(workspace.Path, "profile-rules.json"),
            """
            {
              "firearmBlacklist": ["BlockedFirearm"],
              "feedBlacklist": ["BlockedMagazine"],
              "compatibilityProbeFirearms": ["ProbeFirearm"]
            }
            """);

        var rules = load.Invoke(null, new object[] { workspace.Path });
        Assert.NotNull(rules);
        Assert.True((bool)isBlacklisted.Invoke(rules, new[] { RuntimeEntry(entryType, "BlockedFirearm", "Firearm", false) })!);
        Assert.True((bool)isGloballyBlacklisted.Invoke(rules, new[] { RuntimeEntry(entryType, "BlockedFirearm", "Firearm", false) })!);
        Assert.True((bool)isBlacklisted.Invoke(rules, new[] { RuntimeEntry(entryType, "BlockedMagazine", "Magazine", false) })!);
        Assert.False((bool)isBlacklisted.Invoke(rules, new[] { RuntimeEntry(entryType, "AllowedMagazine", "Magazine", false) })!);
        var probeIds = Assert.IsAssignableFrom<IEnumerable>(
            rulesType.GetProperty("CompatibilityProbeFirearms")!.GetValue(rules));
        Assert.Equal(new[] { "ProbeFirearm" }, probeIds.Cast<string>().ToArray());
        Assert.Null(rulesType.GetProperty("CompatibilityProbeForceIncludeFirearms"));
    }

    [Fact]
    public void Production_profile_rules_keep_requested_runtime_and_global_exclusions()
    {
        var rulesPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(PluginSourcePath)!,
            "..",
            "..",
            "profile-rules.json"));
        using var rules = JsonDocument.Parse(File.ReadAllText(rulesPath));
        var firearmBlacklist = rules.RootElement
            .GetProperty("runtimeFirearmBlacklist")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Equal(
            new[]
            {
                "GrappleGun", "M224Mortar", "LadiesPepperbox", "M6Survival", "MF_LongShot",
                "PotatoGun", "SustenanceCrossbow", "MP510A4", "MP540A4", "MP5A2",
                "MP5A3", "MP5A4", "MP5K", "MP5KA2", "MP5KA3", "MP5KN", "MP5SD1", "MP5SD2",
                "MP5SD3", "MP5SD4", "MP5SD5", "MP5SFA2", "SP5K", "SP5KA2", "SP5KA3", "SP5KFolding",
            },
            firearmBlacklist);
        Assert.Equal(
            new[] { "Slingshot", "BrownBess", "Degle", "JunkyardFlameThrower", "LaserPistol", "MF_Flamethrower", "Stinger", "PlungerLauncher" },
            rules.RootElement
                .GetProperty("firearmBlacklist")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray());
        Assert.False(rules.RootElement.TryGetProperty("compatibilityProbeForceIncludeFirearms", out _));
        Assert.Equal(0, rules.RootElement.GetProperty("feedBlacklist").GetArrayLength());
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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
        Assert.False((bool)isOpticMountType.Invoke(null, new object[] { "Muzzle" })!);
        Assert.False((bool)isOpticMountType.Invoke(null, new object[] { "Grip" })!);
        Assert.False((bool)isOpticMountType.Invoke(null, new object[] { "Side" })!);
        Assert.False((bool)isOpticMountType.Invoke(null, new object[] { "Bottom" })!);
        Assert.False((bool)isOpticMountType.Invoke(null, new object[] { "Suppressor" })!);
        Assert.True((bool)requiresTopSightingOrientation.Invoke(null, new object[] { "Picatinny" })!);
        Assert.True((bool)requiresTopSightingOrientation.Invoke(null, new object[] { "MLokRail" })!);
        Assert.False((bool)requiresTopSightingOrientation.Invoke(null, new object[] { "Russian" })!);
    }

    [WindowsH3vrFact]
    public void Optic_classifier_excludes_generic_magnifier_ids_but_normalizes_pso1_scope()
    {
        var assembly = LoadBuiltMetadataExporter();
        var classifierType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.PipScopeOpticClassifier"));
        var classify = Assert.IsAssignableFrom<MethodInfo>(classifierType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Classify" && method.GetParameters().Length == 3));

        Assert.Equal("Magnifier", classify.Invoke(null, new object[] { "AlphaMagnifierBeta", true, false }));
        Assert.Equal("Magnifier", classify.Invoke(null, new object[] { "alphamagnifierbeta", true, false }));
        Assert.Equal("Scope", classify.Invoke(null, new object[] { "MagnifierPSO1", true, false }));

        Assert.Equal("Scope", classify.Invoke(null, new object[] { "ScopeBR4", true, false }));
        Assert.Equal("Reflex", classify.Invoke(null, new object[] { "ReflexRMR", false, true }));
        Assert.Equal(string.Empty, classify.Invoke(null, new object[] { "Attachment", false, false }));

        var classifyFromMetadata = Assert.IsAssignableFrom<MethodInfo>(classifierType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ClassifyFromMetadata" && method.GetParameters().Length == 2));
        Assert.Equal("Magnifier", classifyFromMetadata.Invoke(null, new object[] { "AlphaMagnifierBeta", "Magnification" }));
        Assert.Equal("Scope", classifyFromMetadata.Invoke(null, new object[] { "MagnifierPSO1", "Magnification" }));
        Assert.Equal("Reflex", classifyFromMetadata.Invoke(null, new object[] { "ReflexRMR", "Reflex" }));
        Assert.Equal("Scope", classifyFromMetadata.Invoke(null, new object[] { "ScopeBR4", "Magnification" }));
        Assert.Equal(string.Empty, classifyFromMetadata.Invoke(null, new object[] { "Attachment", "None" }));
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
    public void Runtime_modded_profiles_create_first_pair_and_replace_only_with_a_larger_complete_pair()
    {
        var assembly = LoadBuiltMetadataExporter();
        var policyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimePoolPersistence"));
        var shouldPromote = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ShouldPromoteModdedCandidate" && method.GetParameters().Length == 5));

        // Incomplete candidates and empty unconfirmed snapshots cannot write.
        Assert.False((bool)shouldPromote.Invoke(null, new object?[] { 1, 24, null, false, false })!);
        Assert.False((bool)shouldPromote.Invoke(null, new object?[] { 2, 0, null, false, false })!);
        Assert.False((bool)shouldPromote.Invoke(null, new object?[] { 0, 0, 24, true, false })!);

        // First safe pair is usable immediately, even while mod loaders run.
        Assert.True((bool)shouldPromote.Invoke(null, new object?[] { 2, 24, null, false, false })!);

        // A saved pair remains until a strictly larger complete candidate exists.
        Assert.False((bool)shouldPromote.Invoke(null, new object?[] { 2, 24, 32, true, false })!);
        Assert.False((bool)shouldPromote.Invoke(null, new object?[] { 2, 32, 32, true, false })!);
        Assert.False((bool)shouldPromote.Invoke(null, new object?[] { 2, 33, null, true, false })!);
        Assert.True((bool)shouldPromote.Invoke(null, new object?[] { 2, 33, 32, true, false })!);

        // Only an explicit completed-loader empty snapshot clears stale IDs.
        Assert.True((bool)shouldPromote.Invoke(null, new object?[] { 0, 0, 32, true, true })!);

    }

    [WindowsH3vrFact]
    public void Runtime_modded_profiles_defer_smaller_policy_replacement_until_the_ten_minute_window()
    {
        var assembly = LoadBuiltMetadataExporter();
        var policyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimePoolPersistence"));
        var shouldPromote = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ShouldPromoteModdedCandidate" && method.GetParameters().Length == 6));
        var shouldPromoteAfterTenMinutes = Assert.IsAssignableFrom<MethodInfo>(policyType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "ShouldPromoteModdedCandidate" && method.GetParameters().Length == 7));

        // A policy change alone keeps the saved pair during early rescans.
        Assert.False((bool)shouldPromote.Invoke(null, new object?[] { 2, 24, 32, true, false, true })!);

        // The scheduled ten-minute window allows one complete, safe policy
        // replacement even when the new pair is smaller.
        Assert.True((bool)shouldPromoteAfterTenMinutes.Invoke(
            null,
            new object?[] { 2, 24, 32, true, false, true, true })!);
        Assert.False((bool)shouldPromoteAfterTenMinutes.Invoke(
            null,
            new object?[] { 2, 24, 32, true, false, false, true })!);
        Assert.False((bool)shouldPromoteAfterTenMinutes.Invoke(
            null,
            new object?[] { 1, 24, 32, true, false, true, true })!);
        Assert.False((bool)shouldPromoteAfterTenMinutes.Invoke(
            null,
            new object?[] { 0, 0, 32, true, false, true, true })!);
    }

    [WindowsH3vrFact]
    public void Runtime_catalog_capture_never_materializes_the_prefab_registry()
    {
        var source = File.ReadAllText(PluginSourcePath);

        Assert.Contains("CatalogPhysicalMountTypes", source, StringComparison.Ordinal);
        Assert.Contains("CatalogOpticKind", source, StringComparison.Ordinal);
        Assert.Contains("HasCatalogFirearmIdentity", source, StringComparison.Ordinal);
        Assert.Contains("HasCatalogFirearmProof", source, StringComparison.Ordinal);
        Assert.Contains("HasDeclaredCompatibleFeed", source, StringComparison.Ordinal);
        Assert.Contains("PipScopeOpticClassifier.ClassifyFromMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetGameObject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetGameObjectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimePrefabMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeMetadataReconciler", source, StringComparison.Ordinal);
    }

    [WindowsH3vrFact]
    public void Runtime_pool_persistence_fingerprint_is_stable_and_changes_with_runtime_metadata()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var persistenceType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimePoolPersistence"));
        var createFingerprint = Assert.IsAssignableFrom<MethodInfo>(persistenceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "CreateFingerprint" && method.GetParameters().Length == 2));
        var createPhaseFingerprint = Assert.IsAssignableFrom<MethodInfo>(persistenceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "CreateFingerprint" && method.GetParameters().Length == 3));
        var createStableSeed = Assert.IsAssignableFrom<MethodInfo>(persistenceType.GetMethod("CreateStableSeed", BindingFlags.Public | BindingFlags.Static));
        var entries = Array.CreateInstance(entryType, 1);
        entries.SetValue(RuntimeEntry(entryType, "BattleRifle", "Firearm", true, magazineType: 556, roundType: 556), 0);
        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var first = (string)createFingerprint.Invoke(null, new object[] { entries, enemies })!;
        var second = (string)createFingerprint.Invoke(null, new object[] { entries, enemies })!;
        var probeFirst = (string)createPhaseFingerprint.Invoke(null, new object[] { entries, enemies, new[] { "Airgun" } })!;
        var probeChanged = (string)createPhaseFingerprint.Invoke(null, new object[] { entries, enemies, new[] { "Airgun", "Flaregun" } })!;
        SetRuntimeProperty(entryType, entries.GetValue(0)!, "CompatibleMagazines", new List<string> { "ARMagazine" });
        var changed = (string)createFingerprint.Invoke(null, new object[] { entries, enemies })!;

        Assert.Equal(first, second);
        Assert.NotEqual(first, probeFirst);
        Assert.NotEqual(probeFirst, probeChanged);
        Assert.NotEqual(first, changed);
        Assert.Equal(
            (int)createStableSeed.Invoke(null, new object[] { changed })!,
            (int)createStableSeed.Invoke(null, new object[] { changed })!);
    }

    [WindowsH3vrFact]
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
        Assert.Contains("SpawnAsyncPrefix(object __instance, object __1, ref IEnumerator __result)", source, StringComparison.Ordinal);
        Assert.Contains("SpawnAsyncPostfix", source, StringComparison.Ordinal);
        Assert.Contains("SpawnAsyncFinalizer", source, StringComparison.Ordinal);
        Assert.Contains("GuardSpawnAsync", source, StringComparison.Ordinal);
        Assert.Contains("TryValidateCurrentLoadout", source, StringComparison.Ordinal);
        Assert.Contains("TryMountGeneratedOptic", source, StringComparison.Ordinal);
        Assert.Contains("IsTopSightingMount", source, StringComparison.Ordinal);
        Assert.Contains("OpticMountPolicy.IsOpticMountType", source, StringComparison.Ordinal);
        Assert.Contains("OpticMountPolicy.RequiresTopSightingOrientation", source, StringComparison.Ordinal);
        Assert.Contains("AdvancePastInvalidWeapon", source, StringComparison.Ordinal);
        Assert.Contains("yield return null;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetGameObject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetGameObjectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityEngine.Object.Instantiate(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GunGame_spawn_safety_skips_unavailable_or_mismatched_objects_without_leaking_exceptions()
    {
        var safetyPath = Path.Combine(Path.GetDirectoryName(PluginSourcePath)!, "GunGameSpawnSafety.cs");
        var source = File.ReadAllText(safetyPath);

        Assert.Contains("TryValidateGunData", source, StringComparison.Ordinal);
        Assert.Contains("HasExpectedObject", source, StringComparison.Ordinal);
        Assert.Contains("id is unavailable", source, StringComparison.Ordinal);
        Assert.Contains("id has category", source, StringComparison.Ordinal);
        Assert.Contains("SpawnAndEquipFinalizer", source, StringComparison.Ordinal);
        Assert.Contains("QueueSkip", source, StringComparison.Ordinal);
        Assert.Contains("QueueSkipFromWeaponBuffer", source, StringComparison.Ordinal);
        Assert.Contains("DescribeCurrentLoadout", source, StringComparison.Ordinal);
        Assert.Contains("buffer spawn exception ", source, StringComparison.Ordinal);
        Assert.Contains("ClearWeaponBuffer", source, StringComparison.Ordinal);
        Assert.Contains("AdvancePastInvalidWeapon", source, StringComparison.Ordinal);
        Assert.Contains("return null;", source, StringComparison.Ordinal);
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
    public void Runtime_profile_families_recognize_only_their_own_runtime_pool_files()
    {
        var assembly = LoadBuiltMetadataExporter();
        var familyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileFamily"));
        var isVanillaFile = Assert.IsAssignableFrom<MethodInfo>(familyType.GetMethod("IsVanillaPoolFile", BindingFlags.Public | BindingFlags.Static));
        var isModdedFile = Assert.IsAssignableFrom<MethodInfo>(familyType.GetMethod("IsModdedPoolFile", BindingFlags.Public | BindingFlags.Static));
        var isProbeFile = Assert.IsAssignableFrom<MethodInfo>(familyType.GetMethod("IsCompatibilityProbePoolFile", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)isVanillaFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json" })!);
        Assert.True((bool)isVanillaFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json" })!);
        Assert.False((bool)isVanillaFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_02_Modded_Rot_RW_Rot.json" })!);
        Assert.True((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_02_Modded_Rot_RW_Rot.json" })!);
        Assert.True((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_04_Modded_Mixed_Enemy_RW_Rot.json" })!);
        Assert.True((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_04_Modded_Mixed_Enemy_CustomSosig.json" })!);
        Assert.False((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json" })!);
        Assert.False((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_05_Unknown_RW_Rot.json" })!);
        Assert.True((bool)isProbeFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_05_Compatibility_Probe_RW_Rot.json" })!);
        Assert.False((bool)isModdedFile.Invoke(null, new object[] { "GunGameWeaponPool_Runtime_05_Compatibility_Probe_RW_Rot.json" })!);
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
    public void Atlas_menu_scene_resolver_identifies_only_the_GunGame_selection()
    {
        var assembly = LoadBuiltMetadataExporter();
        var resolverType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.AtlasMenuSceneResolver"));
        var isGunGameSelection = Assert.IsAssignableFrom<MethodInfo>(resolverType.GetMethod("IsGunGameSelection", BindingFlags.Public | BindingFlags.Static));

        Assert.True((bool)isGunGameSelection.Invoke(null, new object[] { AtlasMenuScreen("GunGame") })!);
        Assert.False((bool)isGunGameSelection.Invoke(null, new object[] { AtlasMenuScreen("Sandbox") })!);
        Assert.False((bool)isGunGameSelection.Invoke(null, new object[] { new AtlasMenuScreenFixture() })!);
    }

    [WindowsH3vrFact]
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
        Assert.Equal("GunGame Progressions: using packaged fallback pools.", lifecycle[5]);
        Assert.Equal("GunGame Progressions: spawn safety unavailable.", lifecycle[6]);
        Assert.All(lifecycle, message => Assert.True(message.Length <= 60));

        var scanCompleted = Assert.IsAssignableFrom<MethodInfo>(messages.GetMethod(
            "ModdedScanCompleted",
            BindingFlags.Public | BindingFlags.Static));
        var scanMessage = Assert.IsType<string>(scanCompleted.Invoke(null, new object[] { 123L, 1435 })!);
        Assert.Equal("GunGame Progressions: modded scan 123ms; 1435 entries.", scanMessage);
        Assert.True(scanMessage.Length <= 60);
    }

    [Fact]
    public void Runtime_warms_modded_profiles_before_the_GunGame_selector_opens()
    {
        var source = File.ReadAllText(PluginSourcePath);
        var awakeMethod = source.IndexOf("private void Awake()", StringComparison.Ordinal);
        var startMethod = source.IndexOf("private void Start()", StringComparison.Ordinal);
        var warmupMethod = source.IndexOf("private void ScheduleStartupProfileWarmup()", StringComparison.Ordinal);
        var destroyMethod = source.IndexOf("private void OnDestroy()", StringComparison.Ordinal);

        Assert.True(awakeMethod >= 0 && startMethod > awakeMethod);
        Assert.True(warmupMethod > startMethod && destroyMethod > warmupMethod);
        var awakeBody = source.Substring(awakeMethod, startMethod - awakeMethod);
        var startBody = source.Substring(startMethod, warmupMethod - startMethod);
        var warmupBody = source.Substring(warmupMethod, destroyMethod - warmupMethod);
        Assert.Contains("ScheduleStartupProfileWarmup();", awakeBody, StringComparison.Ordinal);
        Assert.Contains("ScheduleStartupProfileWarmup();", startBody, StringComparison.Ordinal);
        Assert.Contains("if (startupWarmupScheduled)", warmupBody, StringComparison.Ordinal);
        Assert.Contains("startupWarmupScheduled = true;", warmupBody, StringComparison.Ordinal);
        Assert.Contains("StartCoroutine(GenerateVanillaPoolsAtStartup());", warmupBody, StringComparison.Ordinal);
        Assert.Contains("RequestModdedRefresh(\"startup immediate\");", warmupBody, StringComparison.Ordinal);
        Assert.DoesNotContain("WeaponPoolLoader", warmupBody, StringComparison.Ordinal);
        Assert.DoesNotContain("FindGunGamePoolLoader", warmupBody, StringComparison.Ordinal);
        Assert.DoesNotContain("StartCoroutine(GenerateModdedPoolsAtStartup());", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private IEnumerator GenerateModdedPoolsAtStartup()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_emits_bounded_modded_scan_diagnostics()
    {
        var source = File.ReadAllText(PluginSourcePath);
        var captureStart = source.IndexOf("private IEnumerator CaptureRuntimeMetadata(", StringComparison.Ordinal);
        var captureEnd = source.IndexOf("private static bool HasGunGameRoundDisplayData", StringComparison.Ordinal);

        Assert.Contains("private string moddedRefreshTrigger;", source, StringComparison.Ordinal);
        Assert.Contains("RequestModdedRefresh(\"startup immediate\");", source, StringComparison.Ordinal);
        Assert.Contains("RequestModdedRefresh(traceMessage);", source, StringComparison.Ordinal);
        Assert.Contains("private void LogModdedRefreshStart(", source, StringComparison.Ordinal);
        Assert.Contains("private void LogModdedRefreshOutcome(", source, StringComparison.Ordinal);
        Assert.Equal(2, source.Split("GunGame Progressions debug: scan #", StringSplitOptions.None).Length - 1);
        Assert.Contains("start; trigger=", source, StringComparison.Ordinal);
        Assert.Contains("outcome=", source, StringComparison.Ordinal);
        Assert.Contains("capture=", source, StringComparison.Ordinal);
        Assert.Contains("worker=", source, StringComparison.Ordinal);
        Assert.Contains("total=", source, StringComparison.Ordinal);
        Assert.True(captureStart >= 0 && captureEnd > captureStart);
        var captureBody = source.Substring(captureStart, captureEnd - captureStart);
        Assert.DoesNotContain("LogModdedRefreshStart", captureBody, StringComparison.Ordinal);
        Assert.DoesNotContain("LogModdedRefreshOutcome", captureBody, StringComparison.Ordinal);
    }

    [WindowsH3vrFact]
    public void Runtime_schedules_nonblocking_one_five_ten_and_thirty_minute_startup_modded_rescans()
    {
        var source = File.ReadAllText(PluginSourcePath);
        var warmupMethod = source.IndexOf("private void ScheduleStartupProfileWarmup()", StringComparison.Ordinal);
        var destroyMethod = source.IndexOf("private void OnDestroy()", StringComparison.Ordinal);

        Assert.True(warmupMethod >= 0 && destroyMethod > warmupMethod);
        var warmupBody = source.Substring(warmupMethod, destroyMethod - warmupMethod);
        Assert.Contains("StartCoroutine(RequestStartupModdedRescan(60f, \"startup 1-minute rescan requested.\", false));", warmupBody, StringComparison.Ordinal);
        Assert.Contains("StartCoroutine(RequestStartupModdedRescan(300f, \"startup 5-minute rescan requested.\", false));", warmupBody, StringComparison.Ordinal);
        Assert.Contains("StartCoroutine(RequestStartupModdedRescan(600f, \"startup 10-minute rescan requested; policy replacement eligible.\", true));", warmupBody, StringComparison.Ordinal);
        Assert.Contains("StartCoroutine(RequestStartupModdedRescan(1800f, \"startup 30-minute final rescan requested.\", false));", warmupBody, StringComparison.Ordinal);
        Assert.Contains("private IEnumerator RequestStartupModdedRescan(", source, StringComparison.Ordinal);
        Assert.Contains("bool enablePolicyReplacement)", source, StringComparison.Ordinal);
        Assert.Contains("new WaitForSecondsRealtime(delaySeconds)", source, StringComparison.Ordinal);
        Assert.Contains("policyReplacementEligible = true;", source, StringComparison.Ordinal);
        Assert.Contains("var allowPolicyReplacement = policyReplacementEligible;", source, StringComparison.Ordinal);
        Assert.Contains("startup 1-minute rescan requested.", source, StringComparison.Ordinal);
        Assert.Contains("startup 5-minute rescan requested.", source, StringComparison.Ordinal);
        Assert.Contains("startup 10-minute rescan requested; policy replacement eligible.", source, StringComparison.Ordinal);
        Assert.Contains("startup 30-minute final rescan requested.", source, StringComparison.Ordinal);
    }

    [WindowsH3vrFact]
    public void Runtime_captures_each_modded_snapshot_without_waiting_for_loader_readiness()
    {
        var source = File.ReadAllText(PluginSourcePath);

        Assert.Contains("private readonly OtherLoaderStatusProbe otherLoaderStatusProbe = new OtherLoaderStatusProbe();", source, StringComparison.Ordinal);
        Assert.Contains("var externalLoadState = otherLoaderStatusProbe.Read(Logger.LogDebug);", source, StringComparison.Ordinal);
        Assert.Contains("Trace(\"modded capture started.\");", source, StringComparison.Ordinal);
        Assert.Contains("externalLoadState == ExternalContentLoadState.Complete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModdedRefreshAttemptSeconds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModdedProfileReadinessGate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_modded_refresh_keeps_heavy_work_off_the_unity_thread()
    {
        var pluginSource = File.ReadAllText(PluginSourcePath);
        var builderPath = Path.Combine(Path.GetDirectoryName(PluginSourcePath)!, "RuntimeProfileBuilder.cs");
        var builderSource = File.ReadAllText(builderPath);
        var candidateStart = pluginSource.IndexOf("private IEnumerator GenerateModdedPoolCandidate(", StringComparison.Ordinal);
        var candidateEnd = pluginSource.IndexOf("private static string RuntimePoolDisplayName", StringComparison.Ordinal);

        Assert.True(candidateStart >= 0 && candidateEnd > candidateStart);
        var candidateBody = pluginSource.Substring(candidateStart, candidateEnd - candidateStart);
        Assert.Contains("new RuntimeGenerationJob(", candidateBody, StringComparison.Ordinal);
        Assert.Contains("var combinedMetadata = MergeRuntimeMetadata(", candidateBody, StringComparison.Ordinal);
        Assert.Contains("private static List<RuntimeMetadataEntry> MergeRuntimeMetadata(", pluginSource, StringComparison.Ordinal);
        Assert.Contains("ThreadPriority.BelowNormal", pluginSource, StringComparison.Ordinal);
        Assert.Contains("private const float SelectorSubscriptionRetrySeconds = 10f;", pluginSource, StringComparison.Ordinal);
        Assert.Contains("new WaitForSecondsRealtime(SelectorSubscriptionRetrySeconds)", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WaitForSeconds(0.5f)", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private void Update()", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private void FixedUpdate()", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetGameObject(", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetGameObjectAsync", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildModdedWithDiagnostics", builderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("includeVanillaWeapons", builderSource, StringComparison.Ordinal);
        Assert.Contains("RuntimeProfileBuilder.BuildWithDiagnostics(", pluginSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_keeps_vanilla_profiles_playable_while_modded_profiles_refresh_off_selector_path()
    {
        var source = File.ReadAllText(PluginSourcePath);

        Assert.Contains("WeaponLoadedEvent", source, StringComparison.Ordinal);
        Assert.Contains("WeaponPoolLoaderReady", source, StringComparison.Ordinal);
        Assert.Contains("AddEventHandler", source, StringComparison.Ordinal);
        Assert.Contains("RemoveEventHandler", source, StringComparison.Ordinal);
        Assert.Contains("GunGame.Scripts.Weapons.WeaponPoolLoader", source, StringComparison.Ordinal);
        Assert.Contains("RestorePersistedRuntimeProfilesForSelector", source, StringComparison.Ordinal);
        Assert.Contains("RequestModdedRefresh", source, StringComparison.Ordinal);
        Assert.Contains("RefreshModdedPoolsInBackground", source, StringComparison.Ordinal);
        Assert.Contains("OtherLoaderStatusProbe", source, StringComparison.Ordinal);
        Assert.Contains("if (instance.selectorTracker.Observe(loader))", source, StringComparison.Ordinal);
        Assert.Contains("// reloads. Restore only once; scheduled startup warmup owns", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareModdedProfilesForSelector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModdedProfileLoadingDisplay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateModdedProfileLoadingDisplay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateModdedProfileLoadingDisplay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddGeneratedPoolChoices", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Waiting for mod content", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MainMenuScreenLoadScenePrefix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstGunGameModWarmupSeconds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnDemandGenerationGate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WeaponPoolLoaderAwakePostfix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WatchForGunGamePoolLoader", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_selector_and_GunGame_teardown_never_request_post_startup_modded_refreshes()
    {
        var source = File.ReadAllText(PluginSourcePath);
        var selectorHandler = source.IndexOf("private static void WeaponPoolLoaderReady()", StringComparison.Ordinal);
        var selectorLocator = source.IndexOf("private object FindGunGamePoolLoader()", StringComparison.Ordinal);

        Assert.True(selectorHandler >= 0 && selectorLocator > selectorHandler);
        var selectorBody = source.Substring(selectorHandler, selectorLocator - selectorHandler);
        Assert.Contains("RestorePersistedRuntimeProfilesForSelector", selectorBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestModdedRefresh();", selectorBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GameManagerOnDestroyPostfix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstallGunGameRefreshHooks", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GunGame.Scripts.GameManager", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_uses_a_cached_otherloader_readiness_probe()
    {
        var probePath = Path.Combine(Path.GetDirectoryName(PluginSourcePath)!, "OtherLoaderStatusProbe.cs");
        Assert.True(File.Exists(probePath));

        var pluginSource = File.ReadAllText(PluginSourcePath);
        var probeSource = File.ReadAllText(probePath);
        Assert.DoesNotContain("AccessTools.TypeByName(\"OtherLoader.LoaderStatus\")", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Could not read OtherLoader load status", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new OtherLoaderStatusProbe().Read", pluginSource, StringComparison.Ordinal);
        Assert.Contains("private bool initialized", probeSource, StringComparison.Ordinal);
        Assert.Contains("private bool failureLogged", probeSource, StringComparison.Ordinal);
        Assert.Contains("AccessTools.TypeByName(\"OtherLoader.LoaderStatus\")", probeSource, StringComparison.Ordinal);
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

        var scope = RuntimeEntry(entryType, "Y_PicatinnyScope", "Attachment", false);
        SetRuntimeProperty(entryType, scope, "AttachmentMount", "Picatinny");
        SetRuntimeProperty(entryType, scope, "AttachmentFeature", "Magnification");
        SetRuntimeProperty(entryType, scope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, scope, "OpticMaxMagnification", 4f);
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

    [WindowsH3vrFact]
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

        var scope = RuntimeEntry(entryType, "ReflexCom4", "Attachment", false);
        SetRuntimeProperty(entryType, scope, "OpticKind", "Reflex");
        SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(scope, 2);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var modded = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
            .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy");
        var gun = ReadObjects(modded, "Guns").Single();

        Assert.Equal("ReflexCom4", ReadString(gun, "Extra"));
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

        Assert.Equal("PicatinnyVariableScope", ReadString(guns["Sniper"], "Extra"));
        Assert.Equal("PicatinnyReflex", ReadString(guns["Smg"], "Extra"));
        Assert.Equal("PicatinnyVariableScope", ReadString(guns["Rifle"], "Extra"));
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_balances_equal_rank_compatible_optics()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 10);

        for (var index = 0; index < 6; index++)
        {
            var firearm = RuntimeEntry(entryType, "BalancedCarbine" + index, "Firearm", true, magazineType: 1);
            SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "BalancedMagazine" });
            SetRuntimeProperty(entryType, firearm, "FirearmSize", "Carbine");
            SetRuntimeProperty(entryType, firearm, "FirearmRoundPower", "Intermediate");
            SetRuntimeProperty(entryType, firearm, "FirearmAction", "Automatic");
            SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "Picatinny" });
            entries.SetValue(firearm, index);
        }

        entries.SetValue(RuntimeEntry(entryType, "BalancedMagazine", "Magazine", false, magazineType: 1), 6);
        entries.SetValue(Optic(entryType, "ScopeLion2-5x", "Scope", 1f, 5f, true), 7);
        entries.SetValue(Optic(entryType, "ScopeST6T16x24mmBlack", "Scope", 1f, 6f, true), 8);
        entries.SetValue(Optic(entryType, "ScopeVRZ_1_6x24mm", "Scope", 1f, 6f, true), 9);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var useCounts = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns")
            .GroupBy(gun => ReadString(gun, "Extra"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(3, useCounts.Count);
        Assert.All(useCounts.Values, count => Assert.Equal(2, count));
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_assigns_variable_scope_to_picatinny_only_rifle_carbines()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 7);

        var rifleCarbine = RuntimeEntry(entryType, "M4StyleCarbine", "Firearm", true, magazineType: 1);
        SetRuntimeProperty(entryType, rifleCarbine, "CompatibleMagazines", new List<string> { "RifleCarbineMagazine" });
        SetRuntimeProperty(entryType, rifleCarbine, "FirearmSize", "Carbine");
        SetRuntimeProperty(entryType, rifleCarbine, "FirearmRoundPower", "Intermediate");
        SetRuntimeProperty(entryType, rifleCarbine, "FirearmAction", "Automatic");
        SetRuntimeProperty(entryType, rifleCarbine, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(rifleCarbine, 0);

        var pistolCarbine = RuntimeEntry(entryType, "PistolCarbine", "Firearm", true, magazineType: 2);
        SetRuntimeProperty(entryType, pistolCarbine, "CompatibleMagazines", new List<string> { "PistolCarbineMagazine" });
        SetRuntimeProperty(entryType, pistolCarbine, "FirearmSize", "Carbine");
        SetRuntimeProperty(entryType, pistolCarbine, "FirearmRoundPower", "Pistol");
        SetRuntimeProperty(entryType, pistolCarbine, "FirearmAction", "Automatic");
        SetRuntimeProperty(entryType, pistolCarbine, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(pistolCarbine, 1);

        entries.SetValue(RuntimeEntry(entryType, "RifleCarbineMagazine", "Magazine", true, magazineType: 1), 2);
        entries.SetValue(RuntimeEntry(entryType, "PistolCarbineMagazine", "Magazine", true, magazineType: 2), 3);
        entries.SetValue(Optic(entryType, "PicatinnyReflex", "Reflex", 1f, 1f, false), 4);
        entries.SetValue(Optic(entryType, "PicatinnyFixedScope", "Scope", 1f, 4f, false), 5);
        entries.SetValue(Optic(entryType, "PicatinnyVariableScope", "Scope", 1f, 6f, true), 6);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .ToDictionary(gun => ReadString(gun, "GunName"), StringComparer.Ordinal);

        Assert.Equal("PicatinnyVariableScope", ReadString(guns["M4StyleCarbine"], "Extra"));
        Assert.Equal("PicatinnyReflex", ReadString(guns["PistolCarbine"], "Extra"));
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
    public void Runtime_profile_builder_applies_one_magazine_first_policy_to_vanilla_and_modded_profiles()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 6);

        var vanillaRifle = RuntimeEntry(entryType, "VanillaMagazineRifle", "Firearm", false, magazineType: 11, roundType: 556);
        SetRuntimeProperty(entryType, vanillaRifle, "CompatibleMagazines", new List<string> { "VanillaRifleMagazine" });
        SetRuntimeProperty(entryType, vanillaRifle, "CompatibleSingleRounds", new List<string> { "VanillaLoose556" });
        entries.SetValue(vanillaRifle, 0);

        var moddedRifle = RuntimeEntry(entryType, "ModdedMagazineRifle", "Firearm", true, magazineType: 22, roundType: 556);
        SetRuntimeProperty(entryType, moddedRifle, "CompatibleMagazines", new List<string> { "ModdedRifleMagazine" });
        SetRuntimeProperty(entryType, moddedRifle, "CompatibleSingleRounds", new List<string> { "ModdedLoose556" });
        entries.SetValue(moddedRifle, 1);

        entries.SetValue(RuntimeEntry(entryType, "VanillaRifleMagazine", "Magazine", false, magazineType: 11), 2);
        entries.SetValue(RuntimeEntry(entryType, "ModdedRifleMagazine", "Magazine", true, magazineType: 22), 3);
        entries.SetValue(RuntimeEntry(entryType, "VanillaLoose556", "Cartridge", false, roundType: 556), 4);
        entries.SetValue(RuntimeEntry(entryType, "ModdedLoose556", "Cartridge", true, roundType: 556), 5);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var pools = BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d));

        var vanillaGun = ReadObjects(pools.Single(pool => ReadString(pool, "Name") == "Runtime 01 - Vanilla Rot"), "Guns").Single();
        var moddedGun = ReadObjects(pools.Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"), "Guns").Single();

        Assert.Equal("VanillaRifleMagazine", ReadString(vanillaGun, "MagName"));
        Assert.Equal("ModdedRifleMagazine", ReadString(moddedGun, "MagName"));
        Assert.Equal(0, ReadInt(vanillaGun, "CategoryID"));
        Assert.Equal(0, ReadInt(moddedGun, "CategoryID"));
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
    public void Runtime_compatibility_probe_uses_verified_feed_and_global_picatinny_scope_fallback()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var buildProbe = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "BuildCompatibilityProbe" && method.GetParameters().Length == 4));
        var entries = Array.CreateInstance(entryType, 4);

        var probeGun = RuntimeEntry(entryType, "LegacyProbeGun", "Firearm", false, magazineType: 27);
        SetRuntimeProperty(entryType, probeGun, "PhysicalMountTypes", new List<string> { "Muzzle" });
        entries.SetValue(probeGun, 0);
        entries.SetValue(RuntimeEntry(entryType, "LegacyProbeMagazine", "Magazine", false, magazineType: 27), 1);

        var picatinnyScope = RuntimeEntry(entryType, "ScopeAcog4x32", "Attachment", false);
        SetRuntimeProperty(entryType, picatinnyScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, picatinnyScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(picatinnyScope, 2);

        var slingshot = RuntimeEntry(entryType, "Slingshot", "Firearm", false, magazineType: 27);
        entries.SetValue(slingshot, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var result = buildProbe.Invoke(
            null,
            new object[] { entries, enemies, new[] { "LegacyProbeGun", "Slingshot" }, new SequenceRandom(0d) });
        var pools = ReadObjects(result!, "Pools");

        var pool = Assert.Single(pools);
        Assert.Equal("Runtime 05 - Compatibility Probe", ReadString(pool, "Name"));
        var gun = Assert.Single(ReadObjects(pool, "Guns"));
        Assert.Equal("LegacyProbeGun", ReadString(gun, "GunName"));
        Assert.Equal("LegacyProbeMagazine", ReadString(gun, "MagName"));
        Assert.Equal("ScopeAcog4x32", ReadString(gun, "Extra"));
    }

    [WindowsH3vrFact]
    public void Runtime_compatibility_probe_never_bypasses_ordinary_safety_gates()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var buildProbe = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "BuildCompatibilityProbe" && method.GetParameters().Length == 4));
        var entries = Array.CreateInstance(entryType, 3);
        var unsafeProbe = RuntimeEntry(entryType, "UnsafeProbe", "Firearm", false);
        SetRuntimeProperty(entryType, unsafeProbe, "CompatibleMagazines", new List<string> { "UnsafeProbeMagazine" });
        SetRuntimeProperty(entryType, unsafeProbe, "IsGunGameRoundDisplaySupported", false);
        SetRuntimeProperty(entryType, unsafeProbe, "IsVerifiedFirearmPrefab", false);
        entries.SetValue(unsafeProbe, 0);
        entries.SetValue(RuntimeEntry(entryType, "UnsafeProbeMagazine", "Magazine", false, magazineType: 1), 1);
        var scope = RuntimeEntry(entryType, "ScopeAcog4x32", "Attachment", false);
        SetRuntimeProperty(entryType, scope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(scope, 2);
        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var result = buildProbe.Invoke(
            null,
            new object[] { entries, enemies, new[] { "UnsafeProbe" }, new SequenceRandom(0d) });
        Assert.Empty(ReadObjects(result!, "Pools"));
    }

    [WindowsH3vrFact]
    public void Local_metadata_compatibility_probe_assigns_an_optic_to_every_generated_firearm()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var buildProbe = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "BuildCompatibilityProbe" && method.GetParameters().Length == 4));
        var profileDirectory = Path.GetDirectoryName(GeneratorPath)!;
        var metadataType = typeof(List<>).MakeGenericType(entryType);
        var entries = JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(profileDirectory, "ObjectData.json")),
            metadataType);
        Assert.NotNull(entries);

        using var rules = JsonDocument.Parse(File.ReadAllText(Path.Combine(profileDirectory, "profile-rules.json")));
        var probeIds = rules.RootElement
            .GetProperty("compatibilityProbeFirearms")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrEmpty(item))
            .Cast<string>()
            .ToArray();
        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var result = buildProbe.Invoke(null, new object[] { entries!, enemies, probeIds, new Random(0) });
        var pools = ReadObjects(result!, "Pools");
        var pool = Assert.Single(pools);
        var guns = ReadObjects(pool, "Guns");

        Assert.NotEmpty(guns);
        Assert.All(guns, gun => Assert.False(string.IsNullOrEmpty(ReadString(gun, "Extra")), ReadString(gun, "GunName")));
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_uses_picatinny_scope_fallback_when_no_verified_optic_route_exists()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var firearm = RuntimeEntry(entryType, "NonOpticMountFirearm", "Firearm", true);
        SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "Magazine" });
        SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "NonOpticMountA", "NonOpticMountB" });
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "Magazine", "Magazine", true, magazineType: 1), 1);

        var invalidScope = RuntimeEntry(entryType, "UnrecognizedMountScope", "Attachment", true);
        SetRuntimeProperty(entryType, invalidScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, invalidScope, "PhysicalMountTypes", new List<string> { "NonOpticMountA" });
        entries.SetValue(invalidScope, 2);

        var fallbackScope = RuntimeEntry(entryType, "ScopeAcog4x32", "Attachment", false);
        SetRuntimeProperty(entryType, fallbackScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, fallbackScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, fallbackScope, "OpticMaxMagnification", 4f);
        entries.SetValue(fallbackScope, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("ScopeAcog4x32", ReadString(gun, "Extra"));
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_uses_vanilla_low_power_and_rmr_fallbacks_for_modded_firearms()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 10);

        var handgun = RuntimeEntry(entryType, "UnknownMountHandgun", "Firearm", true, magazineType: 1);
        SetRuntimeProperty(entryType, handgun, "FirearmSize", "Pistol");
        SetRuntimeProperty(entryType, handgun, "CompatibleMagazines", new List<string> { "HandgunMagazine" });
        entries.SetValue(handgun, 0);

        var rifle = RuntimeEntry(entryType, "UnknownMountRifle", "Firearm", true, magazineType: 2);
        SetRuntimeProperty(entryType, rifle, "FirearmSize", "Carbine");
        SetRuntimeProperty(entryType, rifle, "FirearmRoundPower", "Intermediate");
        SetRuntimeProperty(entryType, rifle, "CompatibleMagazines", new List<string> { "RifleMagazine" });
        entries.SetValue(rifle, 1);

        var russianRifle = RuntimeEntry(entryType, "RussianRailModRifle", "Firearm", true, magazineType: 3);
        SetRuntimeProperty(entryType, russianRifle, "FirearmSize", "FullSize");
        SetRuntimeProperty(entryType, russianRifle, "PhysicalMountTypes", new List<string> { "Russian", "Picatinny" });
        SetRuntimeProperty(entryType, russianRifle, "CompatibleMagazines", new List<string> { "RussianMagazine" });
        entries.SetValue(russianRifle, 2);

        entries.SetValue(RuntimeEntry(entryType, "HandgunMagazine", "Magazine", true, magazineType: 1), 3);
        entries.SetValue(RuntimeEntry(entryType, "RifleMagazine", "Magazine", true, magazineType: 2), 4);
        entries.SetValue(RuntimeEntry(entryType, "RussianMagazine", "Magazine", true, magazineType: 3), 5);

        var rmr = RuntimeEntry(entryType, "ReflexRMRSlideMount", "Attachment", false);
        SetRuntimeProperty(entryType, rmr, "OpticKind", "Reflex");
        SetRuntimeProperty(entryType, rmr, "PhysicalMountTypes", new List<string> { "RMR" });
        entries.SetValue(rmr, 6);

        var lowVariable = RuntimeEntry(entryType, "ScopeLion2-5x", "Attachment", false);
        SetRuntimeProperty(entryType, lowVariable, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, lowVariable, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, lowVariable, "OpticMinMagnification", 1f);
        SetRuntimeProperty(entryType, lowVariable, "OpticMaxMagnification", 5f);
        SetRuntimeProperty(entryType, lowVariable, "IsVariableMagnification", true);
        entries.SetValue(lowVariable, 7);

        var russianScope = RuntimeEntry(entryType, "Scope_M76", "Attachment", false);
        SetRuntimeProperty(entryType, russianScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, russianScope, "PhysicalMountTypes", new List<string> { "Russian" });
        entries.SetValue(russianScope, 8);

        var moddedSniperScope = RuntimeEntry(entryType, "ModdedSniperScope", "Attachment", true);
        SetRuntimeProperty(entryType, moddedSniperScope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, moddedSniperScope, "PhysicalMountTypes", new List<string> { "Picatinny" });
        SetRuntimeProperty(entryType, moddedSniperScope, "OpticMinMagnification", 6f);
        SetRuntimeProperty(entryType, moddedSniperScope, "OpticMaxMagnification", 24f);
        SetRuntimeProperty(entryType, moddedSniperScope, "IsVariableMagnification", true);
        entries.SetValue(moddedSniperScope, 9);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns")
            .ToDictionary(gun => ReadString(gun, "GunName"), StringComparer.Ordinal);

        Assert.Equal("ReflexRMRSlideMount", ReadString(guns["UnknownMountHandgun"], "Extra"));
        Assert.Equal("ScopeLion2-5x", ReadString(guns["UnknownMountRifle"], "Extra"));
        Assert.Equal("Scope_M76", ReadString(guns["RussianRailModRifle"], "Extra"));
        Assert.DoesNotContain(guns.Values, gun => ReadString(gun, "Extra") == "ModdedSniperScope");
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_assigns_picatinny_scope_fallback_to_otherwise_valid_firearms()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var mountTypes = new[] { "Muzzle", "Stock", "Grip", "Side" };
        var entries = Array.CreateInstance(entryType, mountTypes.Length * 3);

        for (var index = 0; index < mountTypes.Length; index++)
        {
            var firearm = RuntimeEntry(entryType, "UnsafeMountFirearm" + index, "Firearm", true, magazineType: index + 1);
            SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "UnsafeMountMagazine" + index });
            SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { mountTypes[index] });
            entries.SetValue(firearm, index * 3);

            entries.SetValue(RuntimeEntry(entryType, "UnsafeMountMagazine" + index, "Magazine", true, magazineType: index + 1), index * 3 + 1);
            var scope = RuntimeEntry(entryType, "ScopeAcog4x32", "Attachment", false);
            SetRuntimeProperty(entryType, scope, "OpticKind", "Scope");
            SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "Picatinny" });
            SetRuntimeProperty(entryType, scope, "OpticMaxMagnification", 4f);
            entries.SetValue(scope, index * 3 + 2);
        }

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);
        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns");

        Assert.Equal(mountTypes.Length, guns.Count);
        Assert.All(guns, gun => Assert.Equal("ScopeAcog4x32", ReadString(gun, "Extra")));
    }

    [Fact]
    public void Generation_policy_records_each_playtest_regression_and_its_guard()
    {
        var policyPath = Path.Combine(Path.GetDirectoryName(PluginSourcePath)!, "..", "..", "GENERATION_POLICY.md");
        var policy = File.ReadAllText(Path.GetFullPath(policyPath));
        var requiredGuards = new[]
        {
            "Runtime_profile_builder_skips_explicitly_blacklisted_slingshot",
            "Runtime_profile_builder_skips_unproven_modded_cartridge_guesses_and_bad_feedless_objects",
            "Runtime_catalog_capture_never_materializes_the_prefab_registry",
            "Runtime_profile_builder_skips_firearms_without_gungame_round_display_data",
            "Runtime_profile_builder_applies_one_magazine_first_policy_to_vanilla_and_modded_profiles",
            "Runtime_profile_builder_does_not_infer_a_speedloader_from_round_type",
            "Runtime_profile_builder_uses_shells_for_non_box_shotguns_in_both_profile_families",
            "Runtime_profile_builder_keeps_a_revolver_shotguns_direct_speedloader",
            "Runtime_profile_builder_skips_a_box_fed_shotgun_without_a_compatible_loader",
            "GunGame_spawn_safety_skips_unavailable_or_mismatched_objects_without_leaking_exceptions",
            "Runtime_profile_builder_selects_only_exact_mount_verified_optics",
            "Runtime_profile_builder_prefers_a_proprietary_scope_mount_over_picatinny",
            "Runtime_profile_builder_prefers_a_russian_side_rail_scope_over_other_shared_mounts",
            "Runtime_profile_builder_uses_a_declared_mp5_adapter_mount_without_prefab_materialization",
            "Runtime_profile_builder_uses_the_default_pso1_scope_when_no_modded_scope_is_available",
            "Runtime_profile_builder_matches_verified_picatinny_optics_to_firearm_role",
            "Runtime_profile_builder_balances_equal_rank_compatible_optics",
            "Runtime_profile_builder_assigns_variable_scope_to_picatinny_only_rifle_carbines",
            "Runtime_profile_builder_uses_picatinny_scope_fallback_when_no_verified_optic_route_exists",
            "Runtime_profile_builder_assigns_picatinny_scope_fallback_to_otherwise_valid_firearms",
            "Runtime_profile_builder_uses_vanilla_low_power_and_rmr_fallbacks_for_modded_firearms",
            "Runtime_compatibility_probe_uses_verified_feed_and_global_picatinny_scope_fallback",
            "Optic_classifier_excludes_generic_magnifier_ids_but_normalizes_pso1_scope",
            "Runtime_profile_builder_applies_one_optic_policy_to_vanilla_and_modded_profiles",
            "Runtime_profile_builder_uses_catalog_proven_modded_magazines_and_exact_mount_scopes",
            "Runtime_captures_each_modded_snapshot_without_waiting_for_loader_readiness",
            "Runtime_keeps_vanilla_profiles_playable_while_modded_profiles_refresh_off_selector_path",
            "Runtime_schedules_nonblocking_one_five_and_ten_minute_startup_modded_rescans",
            "Runtime_pool_persistence_rebuilds_when_active_content_changes_or_files_are_missing",
            "Runtime_modded_profiles_keep_the_last_complete_set_until_a_complete_replacement_is_ready",
        };

        Assert.Contains("## Playtest regression matrix", policy, StringComparison.Ordinal);
        Assert.All(requiredGuards, guard => Assert.Contains(guard, policy, StringComparison.Ordinal));
    }

    [WindowsH3vrFact]
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

        var proprietaryScope = RuntimeEntry(entryType, "Z_HandleMountScope", "Attachment", false);
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

    [WindowsH3vrFact]
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
        var pythonScope = RuntimeEntry(entryType, "ScopePython", "Attachment", false);
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

    [WindowsH3vrFact]
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

        var optic = RuntimeEntry(entryType, "PicatinnyVariableScope", "Attachment", false);
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

    [WindowsH3vrFact]
    public void Runtime_profile_builder_uses_a_declared_mp5_adapter_mount_without_prefab_materialization()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        // MP5 entries can expose only a compatible adapter. The adapter's
        // declared MP5RailMount still proves the matching vanilla scope;
        // neither firearm nor adapter prefab may be read or instantiated.
        var firearm = RuntimeEntry(entryType, "MetadataOnlyMp5", "Firearm", true, magazineType: 9);
        SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "MP5Magazine" });
        SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "Bespoke" });
        SetRuntimeProperty(entryType, firearm, "BespokeAttachments", new List<string> { "MP5PicatinnyAdapter" });
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "MP5Magazine", "Magazine", true, magazineType: 9), 1);

        var adapter = RuntimeEntry(entryType, "MP5PicatinnyAdapter", "Attachment", false);
        SetRuntimeProperty(entryType, adapter, "AttachmentFeature", "Adapter");
        SetRuntimeProperty(entryType, adapter, "PhysicalMountTypes", new List<string> { "MP5RailMount" });
        entries.SetValue(adapter, 2);

        var scope = RuntimeEntry(entryType, "Scope_G3SG1", "Attachment", false);
        SetRuntimeProperty(entryType, scope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "MP5RailMount" });
        entries.SetValue(scope, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns")
            .Single();

        Assert.Equal("Scope_G3SG1", ReadString(gun, "Extra"));
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_uses_the_default_pso1_scope_when_no_modded_scope_is_available()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 4);

        var firearm = RuntimeEntry(entryType, "MetadataRussianRifle", "Firearm", true, magazineType: 7);
        SetRuntimeProperty(entryType, firearm, "CompatibleMagazines", new List<string> { "RussianMagazine" });
        SetRuntimeProperty(entryType, firearm, "PhysicalMountTypes", new List<string> { "Russian" });
        entries.SetValue(firearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "RussianMagazine", "Magazine", true, magazineType: 7), 1);

        // H3VR's PSO-1 has a legacy Magnifier object ID but is a real scope.
        // It wins over M76 for the requested Modded Russian fallback.
        var scope = RuntimeEntry(entryType, "MagnifierPSO1", "Attachment", false);
        SetRuntimeProperty(entryType, scope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, scope, "PhysicalMountTypes", new List<string> { "Russian" });
        entries.SetValue(scope, 2);
        var m76Scope = RuntimeEntry(entryType, "Scope_M76", "Attachment", false);
        SetRuntimeProperty(entryType, m76Scope, "OpticKind", "Scope");
        SetRuntimeProperty(entryType, m76Scope, "PhysicalMountTypes", new List<string> { "Russian" });
        entries.SetValue(m76Scope, 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns")
            .Single();

        Assert.Equal("MagnifierPSO1", ReadString(gun, "Extra"));
    }

    [WindowsH3vrFact]
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
        var russianScope = RuntimeEntry(entryType, "Z_RussianSideRailScope", "Attachment", false);
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

    [WindowsH3vrFact]
    public void Runtime_profile_builder_uses_a_loose_round_only_after_no_higher_priority_feed_exists()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 2);

        entries.SetValue(RuntimeEntry(entryType, "MagazineFedPistol", "Firearm", false, magazineType: 77, roundType: 6), 0);
        entries.SetValue(RuntimeEntry(entryType, "LooseRound", "Cartridge", false, roundType: 6), 1);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var gun = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 03 - Vanilla Mixed Enemy"),
            "Guns")
            .Single();

        Assert.Equal("LooseRound", ReadString(gun, "MagName"));
        Assert.Equal(2, ReadInt(gun, "CategoryID"));
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
    public void Runtime_profile_builder_skips_unproven_modded_cartridge_guesses_and_bad_feedless_objects()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 8);

        var safeFirearm = RuntimeEntry(entryType, "SafeFirearm", "Firearm", true, magazineType: 1);
        entries.SetValue(safeFirearm, 0);
        entries.SetValue(RuntimeEntry(entryType, "SafeMagazine", "Magazine", true, magazineType: 1), 1);

        var unclassifiedWithFeed = RuntimeEntry(entryType, "UnclassifiedWithFeed", "Firearm", true, roundType: 158);
        SetRuntimeProperty(entryType, unclassifiedWithFeed, "FirearmAction", "None");
        SetRuntimeProperty(entryType, unclassifiedWithFeed, "FirearmRoundPower", "None");
        entries.SetValue(unclassifiedWithFeed, 2);
        entries.SetValue(RuntimeEntry(entryType, "GenericCartridge", "Cartridge", true, roundType: 158), 3);
        var graviton = RuntimeEntry(entryType, "GravitonBeamer", "Firearm", true);
        SetRuntimeProperty(entryType, graviton, "FirearmAction", "None");
        SetRuntimeProperty(entryType, graviton, "FirearmRoundPower", "None");
        entries.SetValue(graviton, 4);
        var malformedFeedlessMod = RuntimeEntry(entryType, "CompoundBow", "Firearm", true);
        SetRuntimeProperty(entryType, malformedFeedlessMod, "FirearmAction", "None");
        SetRuntimeProperty(entryType, malformedFeedlessMod, "FirearmRoundPower", "None");
        entries.SetValue(malformedFeedlessMod, 5);
        var incompleteG28 = RuntimeEntry(entryType, "NWHKG28Auto", "Firearm", true);
        SetRuntimeProperty(entryType, incompleteG28, "FirearmAction", "None");
        SetRuntimeProperty(entryType, incompleteG28, "FirearmRoundPower", "None");
        entries.SetValue(incompleteG28, 6);
        var mislabeledRail = RuntimeEntry(entryType, "Mount_MCX_LegacyRail_2", "Firearm", true);
        SetRuntimeProperty(entryType, mislabeledRail, "FirearmAction", "None");
        SetRuntimeProperty(entryType, mislabeledRail, "FirearmRoundPower", "None");
        entries.SetValue(mislabeledRail, 7);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns");

        Assert.Equal(
            new[] { "GravitonBeamer", "SafeFirearm" },
            guns.Select(gun => ReadString(gun, "GunName")).ToArray());
        var feedless = guns.Single(gun => ReadString(gun, "GunName") == "GravitonBeamer");
        Assert.Equal(string.Empty, ReadString(feedless, "MagName"));
        Assert.Empty(ReadObjects(feedless, "MagNames"));
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_uses_catalog_proven_modded_magazines_and_exact_mount_scopes()
    {
        var assembly = LoadBuiltMetadataExporter();
        var entryType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeMetadataEntry"));
        var enemyType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeEnemyEntry"));
        var builderType = Assert.IsAssignableFrom<Type>(assembly.GetType("HLin.GunGameProgressions.RuntimeProfileBuilder"));
        var build = Assert.IsAssignableFrom<MethodInfo>(builderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Build" && method.GetParameters().Length == 3));
        var entries = Array.CreateInstance(entryType, 6);

        // The live G28 catalog uses a direct magazine list and Picatinny tag
        // while omitting optional firearm identity tags. The capture proof
        // promotes it; this shared resolver must keep the direct magazine and
        // exact mount-based scope without prefab inspection.
        var g28 = RuntimeEntry(entryType, "CatalogProvenG28", "Firearm", true, magazineType: -6600, roundType: 16);
        SetRuntimeProperty(entryType, g28, "CompatibleMagazines", new List<string> { "G28Magazine10", "G28Magazine20" });
        SetRuntimeProperty(entryType, g28, "PhysicalMountTypes", new List<string> { "Picatinny" });
        entries.SetValue(g28, 0);
        entries.SetValue(RuntimeEntry(entryType, "G28Magazine10", "Magazine", true), 1);
        entries.SetValue(RuntimeEntry(entryType, "G28Magazine20", "Magazine", true), 2);
        entries.SetValue(Optic(entryType, "G28Scope", "Scope", 1f, 6f, true), 3);

        // A loose same-round cartridge is not proof that it fits a mod rifle.
        // Without direct/exact metadata this entry must be omitted.
        entries.SetValue(RuntimeEntry(entryType, "UnprovenModRifle", "Firearm", true, roundType: 16), 4);
        entries.SetValue(RuntimeEntry(entryType, "LooseRound16", "Cartridge", true, roundType: 16), 5);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 02 - Modded Rot"),
            "Guns")
            .ToDictionary(gun => ReadString(gun, "GunName"), StringComparer.Ordinal);

        Assert.True(guns.ContainsKey("CatalogProvenG28"));
        Assert.Equal("G28Magazine10", ReadString(guns["CatalogProvenG28"], "MagName"));
        Assert.Equal(0, ReadInt(guns["CatalogProvenG28"], "CategoryID"));
        Assert.Equal("G28Scope", ReadString(guns["CatalogProvenG28"], "Extra"));
        Assert.False(guns.ContainsKey("UnprovenModRifle"));
    }

    [WindowsH3vrFact]
    public void Runtime_profile_builder_skips_explicitly_blacklisted_slingshot()
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
        var slingshot = RuntimeEntry(entryType, "Slingshot", "Firearm", true, roundType: 158);
        SetRuntimeProperty(entryType, slingshot, "FirearmAction", "None");
        SetRuntimeProperty(entryType, slingshot, "FirearmRoundPower", "None");
        entries.SetValue(slingshot, 2);
        entries.SetValue(RuntimeEntry(entryType, "GenericCartridge", "Cartridge", true, roundType: 158), 3);

        var enemies = Array.CreateInstance(enemyType, 1);
        enemies.SetValue(RuntimeEnemyEntry(enemyType, "RW_Rot", false, 5), 0);

        var guns = ReadObjects(BuildRuntimePools(build, entries, enemies, new SequenceRandom(0d))
                .Single(pool => ReadString(pool, "Name") == "Runtime 04 - Modded Mixed Enemy"),
            "Guns");

        Assert.Equal(new[] { "SafeFirearm" }, guns.Select(gun => ReadString(gun, "GunName")).ToArray());
    }

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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

    [WindowsH3vrFact]
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
            new[] { "VanillaMagazineGun", "LeverSentinelGun", "PKM", "PotatoGun" },
            guns.Select(gun => gun.GetProperty("GunName").GetString()).ToArray());
        Assert.Equal(
            new[] { "MagazineMp515rnd", "TestCartridge", "MagazinePKM", "TestCartridge" },
            guns.Select(gun => gun.GetProperty("MagName").GetString()).ToArray());
        Assert.Equal(
            new[] { "MagazineMp515rnd", "TestMagazine" },
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
    public void Generator_discards_a_legacy_cartridge_primary_when_direct_magazine_exists()
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

        Assert.Equal("MagazineMp515rnd", vanillaGun.GetProperty("MagName").GetString());
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

    private static string OfflineProfileGeneratorProjectPath
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "build", "mods.json")))
            {
                current = current.Parent;
            }

            Assert.NotNull(current);
            return Path.Combine(
                current!.FullName,
                "GunGameProgressions",
                "OfflineProfileGenerator",
                "OfflineProfileGenerator.csproj");
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
        return RuntimeMetadataExporter.Value;
    }

    private static Assembly BuildMetadataExporter()
    {
        return LoadBuiltMetadataExporter("Release");
    }

    private static Assembly LoadBuiltMetadataExporter(string configuration)
    {
        var originalPath = BuildMetadataExporterConfiguration(configuration);
        var loadDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(loadDirectory);
        var loadPath = Path.Combine(loadDirectory, Path.GetFileName(originalPath));
        File.Copy(originalPath, loadPath);
        return new AssemblyLoadContext(Guid.NewGuid().ToString("N"), isCollectible: true)
            .LoadFromAssemblyPath(loadPath);
    }

    private static string BuildMetadataExporterConfiguration(string configuration)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{MetadataExporterProjectPath}\" -c {configuration}",
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
            configuration,
            "net35",
            "GunGameProgressionsMetadataExporter.dll");
        Assert.True(File.Exists(originalPath), $"Metadata exporter output missing: {originalPath}");
        return originalPath;
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
        var optic = RuntimeEntry(entryType, objectId, "Attachment", false);
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

[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsH3vrFactAttribute : FactAttribute
{
    public WindowsH3vrFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("H3VR_METADATA_EXPORTER_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Requires the authoritative Windows H3VR build environment.";
        }
    }
}
