using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;

namespace HLin_Mods.NuketownGunGameCompatibilityPatch
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(AtlasPluginGuid, "1.0.1")]
    [BepInDependency(GunGamePluginGuid, "1.0.2")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "HLin_Mods.NuketownGunGameCompatibilityPatch";
        public const string PluginName = "Nuketown GunGame Compatibility Patch";
        public const string PluginVersion = "1.0.0";

        private const string AtlasPluginGuid = "nrgill28.Atlas";
        private const string GunGamePluginGuid = "Kodeman.GunGame";
        private const string OriginalPackageDirectory = "localpcnerd-NuketownGunGame";
        private const string OriginalManifestFileName = "manifest.json";
        private const string OriginalDescriptorFileName = "nuketown.json";
        private const string OriginalBundleFileName = "nuketown";
        private const string SupportedPackageVersion = "2.1.6";

        private static bool _initializationAttempted;

        private void Awake()
        {
            if (_initializationAttempted)
            {
                Logger.LogWarning("Compatibility registration already attempted; skipping duplicate startup.");
                return;
            }

            _initializationAttempted = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var bundlePath = GetValidatedBundlePath(Paths.PluginPath);
                RegisterAtlasScene(bundlePath);
                Logger.LogInfo(string.Format(
                    "Registered original BO1 Nuketown bundle once in {0} ms. No per-frame compatibility work is active.",
                    stopwatch.ElapsedMilliseconds));
            }
            catch (Exception exception)
            {
                Logger.LogError("Nuketown compatibility registration skipped: " + Describe(exception));
            }
        }

        private static string GetValidatedBundlePath(string pluginsRoot)
        {
            var packageRoot = Path.Combine(pluginsRoot, OriginalPackageDirectory);
            var manifestPath = Path.Combine(packageRoot, OriginalManifestFileName);
            var descriptorPath = Path.Combine(packageRoot, OriginalDescriptorFileName);
            var bundlePath = Path.Combine(packageRoot, OriginalBundleFileName);

            RequireFile(manifestPath, "original Nuketown manifest");
            RequireFile(descriptorPath, "original Nuketown scene descriptor");
            RequireFile(bundlePath, "original Nuketown scene bundle");

            var manifest = File.ReadAllText(manifestPath);
            var version = ReadJsonString(manifest, "version_number");
            if (!string.Equals(version, SupportedPackageVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    string.Format("unsupported Nuketown package version '{0}'; supported version is '{1}'", version ?? "missing", SupportedPackageVersion));
            }

            var descriptor = File.ReadAllText(descriptorPath);
            var identifier = ReadJsonString(descriptor, "Identifier");
            if (!string.Equals(identifier, OriginalBundleFileName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    string.Format("unsupported Nuketown descriptor identifier '{0}'", identifier ?? "missing"));
            }

            return bundlePath;
        }

        private static void RegisterAtlasScene(string bundlePath)
        {
            var atlasPluginType = FindLoadedType("Atlas.AtlasPlugin");
            if (atlasPluginType == null)
            {
                throw new InvalidOperationException("Atlas plugin type was not loaded.");
            }

            var registerScene = atlasPluginType.GetMethod(
                "RegisterScene",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (registerScene == null)
            {
                throw new MissingMethodException("Atlas.AtlasPlugin", "RegisterScene(string)");
            }

            registerScene.Invoke(null, new object[] { bundlePath });
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void RequireFile(string path, string description)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Missing " + description + ".", path);
            }
        }

        private static string ReadJsonString(string json, string propertyName)
        {
            var match = Regex.Match(json, "\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"");
            return match.Success ? match.Groups["value"].Value : null;
        }

        private static string Describe(Exception exception)
        {
            var invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null
                ? invocation.InnerException.Message
                : exception.Message;
        }
    }
}
