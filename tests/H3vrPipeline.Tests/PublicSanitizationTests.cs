using System.Text.RegularExpressions;
using Xunit;

namespace H3vrPipeline.Tests;

public sealed class PublicSanitizationTests
{
    [Fact]
    public void Public_operational_files_do_not_embed_machine_or_account_identifiers()
    {
        foreach (var relativePath in new[]
                 {
                     ".claude/skills/h3vr-mod-development.md",
                     ".codex/skills/h3vr-mod-development/SKILL.md",
                     "skills/h3vr-remote-development/SKILL.md",
                     "skills/h3vr-remote-development/references/headless-unity-asset-inspection.md",
                     "build/environment.json",
                     "build/environment.local.example.json",
                     "tools/H3VRAssetInspector/inspect_assets.py",
                     "tools/H3VRAssetInspector/export_private_asset_rip.ps1",
                     "tools/H3VRAssetInspector/H3VRAssetInspectionBatch.cs",
                     "ThePing/Properties/launchSettings.json",
                 })
        {
            var content = File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
            Assert.DoesNotContain("C:\\Users\\", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Users/", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(new Regex(@"(?im)^\s*ssh\s+(?!""?\$H3VR_WINDOWS_HOST\b)[^\s`]+"), content);
            Assert.DoesNotMatch(new Regex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.(local|ts\.net)\b"), content);
        }
    }

    [Fact]
    public void Pipeline_supports_an_ignored_local_environment_override()
    {
        var publicConfig = File.ReadAllText(Path.Combine(RepositoryRoot, "build", "environment.json"));
        var localExample = Path.Combine(RepositoryRoot, "build", "environment.local.example.json");
        var ignoreRules = File.ReadAllText(Path.Combine(RepositoryRoot, ".gitignore"));
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));

        Assert.Contains("%H3VR_MANAGED_DLLS%", publicConfig, StringComparison.Ordinal);
        Assert.True(File.Exists(localExample));
        Assert.Contains("/build/environment.local.json", ignoreRules, StringComparison.Ordinal);
        Assert.Contains("environment.local.json", pipeline, StringComparison.Ordinal);
        Assert.Contains("ExpandEnvironmentVariables", pipeline, StringComparison.Ordinal);
        Assert.Contains("SetupAssetInspector", pipeline, StringComparison.Ordinal);
        Assert.Contains("InspectAssets", pipeline, StringComparison.Ordinal);
    }

    [Fact]
    public void Preflight_handles_a_detached_git_worktree()
    {
        var pipeline = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "h3vr.ps1"));

        Assert.Contains("detached HEAD", pipeline, StringComparison.Ordinal);
        Assert.DoesNotContain("branch --show-current).Trim", pipeline, StringComparison.Ordinal);
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

            throw new DirectoryNotFoundException("Could not locate the H3VR-Mods repository root.");
        }
    }
}
