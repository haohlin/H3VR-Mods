using Xunit;

namespace H3vrPipeline.Tests;

public sealed class H3vrRemoteWorktreeTests
{
    [Fact]
    public void Remote_worktree_pipeline_uses_the_private_canonical_environment_configuration()
    {
        var root = FindRepositoryRoot();
        var remoteWrapper = File.ReadAllText(Path.Combine(root, "tools", "h3vr-remote.sh"));
        var pipeline = File.ReadAllText(Path.Combine(root, "tools", "h3vr.ps1"));

        Assert.Contains("H3VR_WINDOWS_WORKTREE_BRANCH", remoteWrapper, StringComparison.Ordinal);
        Assert.Contains("H3VR_PIPELINE_ENVIRONMENT_CONFIG", remoteWrapper, StringComparison.Ordinal);
        Assert.Contains("H3VR_PIPELINE_ENVIRONMENT_CONFIG", pipeline, StringComparison.Ordinal);
        Assert.Contains("Test-Path -LiteralPath $PrivateEnvironmentConfigPath", pipeline, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "h3vr-remote.sh")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the H3VR-Mods repository root.");
    }
}
