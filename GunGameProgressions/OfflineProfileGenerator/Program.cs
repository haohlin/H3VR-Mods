using System.Text;
using System.Text.Json;
using HLin.GunGameProgressions;

namespace HLin.GunGameProgressions.OfflineProfileGenerator;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly RuntimeEnemy[] OfflineMixedEnemies =
    {
        Enemy("RW_Rot", 8),
        Enemy("M_Swat_Scout", 5),
        Enemy("M_MercWiener_Riflewiener", 3),
        Enemy("M_Swat_SpecOps", 2),
        Enemy("M_Swat_Heavy", 1),
    };

    private const int OfflineSeed = 0;

    private static readonly RuntimeEnemyEntry[] ProbeEnemies =
    {
        new RuntimeEnemyEntry
        {
            EnemyNameString = "RW_Rot",
            DisplayName = "Rot",
            IsSpawnable = true,
            DifficultyScore = 1,
        },
    };

    public static int Main(string[] args)
    {
        try
        {
            var options = OfflineGeneratorOptions.Parse(args);
            var entries = JsonSerializer.Deserialize<List<RuntimeMetadataEntry>>(
                File.ReadAllText(options.InputPath),
                JsonOptions) ?? new List<RuntimeMetadataEntry>();
            if (!string.IsNullOrEmpty(options.ProbeOutputPath))
            {
                return WriteCompatibilityProbeReport(entries, options);
            }

            var vanillaEntries = entries.Where(entry => entry != null && !entry.IsModContent).ToList();
            var pools = RuntimeProfileBuilder.Build(vanillaEntries, new Random(OfflineSeed));

            var rot = FindPool(pools, "01_Vanilla_Rot");
            var mixed = FindPool(pools, "03_Vanilla_Mixed_Enemy");
            mixed.Enemies = OfflineMixedEnemies.Select(CloneEnemy).ToList();

            var generated = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GunGameWeaponPool_Runtime_01_Vanilla_Rot_RW_Rot.json"] = SerializePool(rot),
                ["GunGameWeaponPool_Runtime_03_Vanilla_Mixed_Enemy_RW_Rot.json"] = SerializePool(mixed),
            };

            if (options.VerifyOnly)
            {
                foreach (var profile in generated)
                {
                    var existingPath = Path.Combine(options.OutputDirectory, profile.Key);
                    if (!File.Exists(existingPath) || !JsonEquals(File.ReadAllText(existingPath), profile.Value))
                    {
                        Console.Error.WriteLine("Offline fallback is stale: " + existingPath);
                        return 1;
                    }
                }

                Console.WriteLine("Offline GunGame fallbacks match the shared runtime profile builder.");
                return 0;
            }

            Directory.CreateDirectory(options.OutputDirectory);
            foreach (var profile in generated)
            {
                File.WriteAllText(Path.Combine(options.OutputDirectory, profile.Key), profile.Value, new UTF8Encoding(false));
            }

            Console.WriteLine("Generated " + generated.Count + " offline GunGame fallback profiles from " + vanillaEntries.Count + " vanilla metadata entries.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int WriteCompatibilityProbeReport(
        List<RuntimeMetadataEntry> entries,
        OfflineGeneratorOptions options)
    {
        var inputDirectory = Path.GetDirectoryName(Path.GetFullPath(options.InputPath)) ?? ".";
        var rules = ProfileRules.Load(inputDirectory);
        var runtimeEntries = entries.Where(entry => entry != null && !rules.IsBlacklisted(entry)).ToList();
        var result = RuntimeProfileBuilder.BuildCompatibilityProbe(
            runtimeEntries,
            ProbeEnemies,
            rules.CompatibilityProbeFirearms,
            rules.CompatibilityProbeForceIncludeFirearms,
            new Random(OfflineSeed));
        var pool = result.Pools.SingleOrDefault(candidate => candidate.Family == "05_Compatibility_Probe");
        if (pool == null)
        {
            throw new InvalidOperationException("Local metadata produced no Runtime 05 compatibility candidates.");
        }

        var outputPath = Path.GetFullPath(options.ProbeOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, SerializePool(pool), new UTF8Encoding(false));
        Console.WriteLine(
            "Generated Runtime 05 metadata-only report with " + pool.Guns.Count +
            " firearms; skipped " + result.SkippedFirearms.Count + ".");
        return 0;
    }

    private static RuntimeWeaponPool FindPool(IEnumerable<RuntimeWeaponPool> pools, string family)
    {
        var pool = pools.SingleOrDefault(candidate => candidate.Family == family);
        if (pool == null)
        {
            throw new InvalidOperationException("Shared builder did not produce offline pool family " + family + ".");
        }

        return pool;
    }

    private static RuntimeEnemy Enemy(string name, int value)
    {
        return new RuntimeEnemy
        {
            EnemyNameString = name,
            Value = value,
        };
    }

    private static RuntimeEnemy CloneEnemy(RuntimeEnemy enemy)
    {
        return Enemy(enemy.EnemyNameString, enemy.Value);
    }

    private static string SerializePool(RuntimeWeaponPool pool)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("WeaponPoolType", pool.WeaponPoolType);
            writer.WriteString("Description", pool.Description);
            writer.WriteNumber("EnemyProgressionType", pool.EnemyProgressionType);
            writer.WritePropertyName("Enemies");
            writer.WriteStartArray();
            foreach (var enemy in pool.Enemies)
            {
                writer.WriteStartObject();
                writer.WriteNumber("EnemyName", 0);
                writer.WriteString("EnemyNameString", enemy.EnemyNameString);
                writer.WriteNumber("Value", enemy.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("Guns");
            writer.WriteStartArray();
            foreach (var gun in pool.Guns)
            {
                writer.WriteStartObject();
                writer.WriteString("GunName", gun.GunName);
                writer.WriteString("MagName", gun.MagName);
                writer.WritePropertyName("MagNames");
                writer.WriteStartArray();
                foreach (var magazine in gun.MagNames ?? new List<string>())
                {
                    writer.WriteStringValue(magazine);
                }

                writer.WriteEndArray();
                writer.WriteNumber("CategoryID", gun.CategoryID);
                writer.WriteString("Extra", gun.Extra ?? string.Empty);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteString("Name", pool.Name);
            writer.WriteNumber("OrderType", pool.OrderType);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static bool JsonEquals(string left, string right)
    {
        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return JsonEquals(leftDocument.RootElement, rightDocument.RootElement);
    }

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                var leftProperties = left.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
                var rightProperties = right.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
                return leftProperties.Count == rightProperties.Count && leftProperties.All(property =>
                    rightProperties.TryGetValue(property.Key, out var rightValue) && JsonEquals(property.Value, rightValue));
            case JsonValueKind.Array:
                var leftItems = left.EnumerateArray().ToArray();
                var rightItems = right.EnumerateArray().ToArray();
                return leftItems.Length == rightItems.Length && leftItems
                    .Zip(rightItems, (leftItem, rightItem) => JsonEquals(leftItem, rightItem))
                    .All(equal => equal);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            default:
                return left.GetRawText() == right.GetRawText();
        }
    }
}

internal sealed class OfflineGeneratorOptions
{
    public string InputPath { get; private set; } = string.Empty;
    public string OutputDirectory { get; private set; } = string.Empty;
    public string ProbeOutputPath { get; private set; } = string.Empty;
    public bool VerifyOnly { get; private set; }

    public static OfflineGeneratorOptions Parse(string[] args)
    {
        var options = new OfflineGeneratorOptions
        {
            InputPath = Path.Combine("GunGameProgressions", "ObjectData.json"),
            OutputDirectory = "GunGameProgressions",
        };

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--input":
                    options.InputPath = ReadValue(args, ref index, "--input");
                    break;
                case "--output-dir":
                    options.OutputDirectory = ReadValue(args, ref index, "--output-dir");
                    break;
                case "--verify":
                    options.VerifyOnly = true;
                    break;
                case "--probe-output":
                    options.ProbeOutputPath = ReadValue(args, ref index, "--probe-output");
                    break;
                default:
                    throw new ArgumentException("Unknown option: " + args[index]);
            }
        }

        return options;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException(option + " requires a value.");
        }

        index++;
        return args[index];
    }
}
