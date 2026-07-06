using System.IO.Compression;
using System.Reflection;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class PackageValidatorTests
{
    [Fact]
    public void Validate_accepts_a_legacy_flat_package_with_required_metadata()
    {
        using var package = TestPackage.Create(includeManifest: true, includePlugin: true);

        var result = Validate(package.Path, "legacy-flat");

        Assert.True(result);
    }

    [Fact]
    public void Validate_rejects_a_package_without_a_root_manifest()
    {
        using var package = TestPackage.Create(includeManifest: false, includePlugin: true);

        var result = Validate(package.Path, "legacy-flat");

        Assert.False(result);
    }

    private static bool Validate(string packagePath, string layout)
    {
        var validatorType = Assembly.Load("H3vrPipeline").GetType("H3vrPipeline.PackageValidator");
        Assert.NotNull(validatorType);

        var validateMethod = validatorType!.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(validateMethod);

        var result = validateMethod!.Invoke(null, new object[] { packagePath, layout });
        Assert.NotNull(result);

        var isValidProperty = result!.GetType().GetProperty("IsValid");
        Assert.NotNull(isValidProperty);

        return Assert.IsType<bool>(isValidProperty!.GetValue(result));
    }

    private sealed class TestPackage : IDisposable
    {
        private readonly string _directory;

        private TestPackage(string directory, string path)
        {
            _directory = directory;
            Path = path;
        }

        public string Path { get; }

        public static TestPackage Create(bool includeManifest, bool includePlugin)
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var packagePath = System.IO.Path.Combine(directory, "package.zip");

            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
            if (includeManifest)
            {
                WriteEntry(archive, "manifest.json", """
                    {"name":"The_Ping","version_number":"1.0.4","website_url":"https://github.com/haohlin/H3VR-Mods","description":"Long range impact audio.","dependencies":[]}
                    """);
            }

            WriteEntry(archive, "README.md", "# The Ping\n");
            WriteEntry(archive, "icon.png", CreatePngHeader());
            if (includePlugin)
            {
                WriteEntry(archive, "ThePing.dll", "placeholder");
            }

            return new TestPackage(directory, packagePath);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }

        private static void WriteEntry(ZipArchive archive, string name, byte[] content)
        {
            using var stream = archive.CreateEntry(name).Open();
            stream.Write(content);
        }

        private static byte[] CreatePngHeader()
        {
            return new byte[]
            {
                137, 80, 78, 71, 13, 10, 26, 10,
                0, 0, 0, 13, 73, 72, 68, 82,
                0, 0, 1, 0, 0, 0, 1, 0,
                8, 6, 0, 0, 0
            };
        }
    }
}
