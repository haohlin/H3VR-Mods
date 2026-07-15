using Xunit;

namespace H3vrPipeline.Tests;

public sealed class ModStateDocumentationTests
{
    [Fact]
    public void Active_mods_have_cross_session_state_documents()
    {
        RequireFiles(
            "BubbleLevel",
            "DESIGN.md",
            "DEV_STATUS.md");
        RequireFiles(
            "GunGameProgressions",
            "DESIGN.md",
            "DEV_STATUS.md");

        RequireDevelopmentStatusSections("BubbleLevel");
        RequireDevelopmentStatusSections("GunGameProgressions");
        AssertNoLegacyStateFiles("BubbleLevel");
        AssertNoLegacyStateFiles("GunGameProgressions");
    }

    [Fact]
    public void H3vr_workflow_has_versioned_state_templates_and_skill_rules()
    {
        RequireFiles(
            "docs/mod-development",
            "README.md",
            "MOD_DESIGN_TEMPLATE.md",
            "MOD_DEV_STATUS_TEMPLATE.md");
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, "MOD_STATE_INDEX.md")));

        foreach (var relativePath in new[]
                 {
                     ".codex/skills/h3vr-mod-development/SKILL.md",
                     "skills/h3vr-remote-development/SKILL.md",
                 })
        {
            var content = File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
            Assert.Contains("Cross-session mod state", content, StringComparison.Ordinal);
            Assert.Contains("DEV_STATUS.md", content, StringComparison.Ordinal);
            Assert.Contains("git pull --ff-only", content, StringComparison.Ordinal);
            Assert.Contains("tracked", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Public_workflow_uses_only_portable_checks()
    {
        var workflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "verify.yml"));

        Assert.Contains("Verify H3VR Portable Checks", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test tests/H3vrPipeline.Tests/H3vrPipeline.Tests.csproj", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("-Action Package", workflow, StringComparison.Ordinal);
    }

    private static void RequireFiles(string directory, params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            Assert.True(
                File.Exists(Path.Combine(RepositoryRoot, directory, fileName)),
                "Missing cross-session state file: " + Path.Combine(directory, fileName));
        }
    }

    private static void RequireDevelopmentStatusSections(string directory)
    {
        var content = File.ReadAllText(Path.Combine(RepositoryRoot, directory, "DEV_STATUS.md"));
        Assert.Contains("## Status", content, StringComparison.Ordinal);
        Assert.Contains("## Plan", content, StringComparison.Ordinal);
        Assert.Contains("## Testing", content, StringComparison.Ordinal);
    }

    private static void AssertNoLegacyStateFiles(string directory)
    {
        foreach (var fileName in new[] { "STATUS.md", "PLAN.md", "TESTING.md" })
        {
            Assert.False(
                File.Exists(Path.Combine(RepositoryRoot, directory, fileName)),
                "Legacy split state file must not return: " + Path.Combine(directory, fileName));
        }
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
