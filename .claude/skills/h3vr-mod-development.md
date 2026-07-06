---
name: h3vr-mod-development
description: Use when creating, building, deploying, or modifying H3VR BepInEx mods. Covers the full workflow from scaffolding a new mod through Harmony patching, building via Windows dotnet, packaging for Thunderstore/r2modman, and deploying to the plugin folder.
---

# H3VR Mod Development

## Overview

H3VR mods are BepInEx 5 plugins that use HarmonyX to patch the game at runtime. Development happens in WSL (code editing) with builds executed via Windows `cmd.exe` interop (`dotnet build`). The game namespace is `FistVR`.

## Environment

| Component | Path |
|---|---|
| Mod repo | `E:\Dev\H3VR-Mods` (WSL: `/mnt/e/Dev/H3VR-Mods`) |
| Game DLLs | `E:\Steam\steamapps\common\H3VR\h3vr_Data\Managed` |
| r2modman plugins | `C:\Users\y\AppData\Roaming\r2modmanPlus-local\H3VR\profiles\Default\BepInEx\plugins` |
| dnSpy | `E:\Dev\dnSpy-net-win64\dnSpy.exe` |
| Unity Projects | `E:\Dev\Unity Projects` |
| .NET SDK (Windows) | dotnet 7.0.306 |

**WSL note:** The `E:` and `C:` drives are read-only from WSL bash. Use `cmd.exe /c "..."` for builds and `powershell.exe -Command "..."` for file operations on Windows paths.

## Build Command (from WSL)

```bash
cmd.exe /c "cd /d E:\Dev\H3VR-Mods\<ModName> && dotnet build -c Release 2>&1"
```

Output DLL: `E:\Dev\H3VR-Mods\<ModName>\bin\Release\net35\<ModName>.dll`

## Deploy Command (from WSL)

```bash
powershell.exe -Command "Copy-Item 'E:\Dev\H3VR-Mods\<ModName>\bin\Release\net35\<ModName>.dll' 'C:\Users\y\AppData\Roaming\r2modmanPlus-local\H3VR\profiles\Default\BepInEx\plugins\HLin_Mods-<ModName>\<ModName>.dll' -Force"
```

If the plugin folder doesn't exist yet, create it first:

```bash
powershell.exe -Command "New-Item -ItemType Directory -Force -Path 'C:\Users\y\AppData\Roaming\r2modmanPlus-local\H3VR\profiles\Default\BepInEx\plugins\HLin_Mods-<ModName>'"
```

## New Mod Scaffold

Every mod is a self-contained project. To create a new mod named `MyMod`:

### 1. Directory structure

```
MyMod/
  MyMod.sln
  MyMod.csproj
  NuGet.Config
  src/
    Plugin.cs
    HarmonyMod.cs    # if using Harmony patches
```

Create via: `cmd.exe /c "mkdir E:\Dev\H3VR-Mods\MyMod\src"`

### 2. NuGet.Config

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="BepInEx" value="https://nuget.bepinex.dev/v3/index.json" />
  </packageSources>
</configuration>
```

### 3. .csproj template

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net35</TargetFramework>
    <AssemblyName>MyMod</AssemblyName>
    <Description>Mod description</Description>
    <Version>1.0.0</Version>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <LangVersion>latest</LangVersion>
    <Authors>HLin</Authors>
    <PackageId>HLin-MyMod</PackageId>
    <Title>MyMod</Title>
    <GeneratePackageOnBuild>False</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BepInEx.Analyzers" Version="1.*" PrivateAssets="all" />
    <PackageReference Include="BepInEx.Core" Version="5.4.21" />
    <PackageReference Include="BepInEx.PluginInfoProps" Version="2.1.0" />
    <PackageReference Include="H3VR.GameLibs" Version="0.111.10" />
    <PackageReference Include="UnityEngine.Modules" Version="5.6.3" IncludeAssets="compile" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework.TrimEnd(`0123456789`))' == 'net'">
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.2" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### 4. Plugin.cs template

```csharp
using BepInEx;
using HarmonyLib;

namespace MyMod
{
    [BepInProcess("h3vr.exe")]
    [BepInPlugin("HLin-MyMod", MyPluginInfo.PLUGIN_NAME, "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly Harmony harmony = new Harmony("HLin-MyMod");

        public void Awake()
        {
            harmony.PatchAll();
            Logger.LogInfo("Loaded MyMod Successfully\!");
        }
    }
}
```

### 5. HarmonyMod.cs template

```csharp
using UnityEngine;
using HarmonyLib;
using FistVR;

namespace MyMod
{
    [HarmonyPatch(typeof(TargetClass), "TargetMethod")]
    public static class Harmony_TargetMethod
    {
        [HarmonyPrefix]
        public static bool Prefix(TargetClass __instance)
        {
            // Return false to skip original, true to continue
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(TargetClass __instance)
        {
            // Runs after the original method
        }
    }
}
```

### 6. .sln template

Generate with: `cmd.exe /c "cd /d E:\Dev\H3VR-Mods\MyMod && dotnet new sln && dotnet sln add MyMod.csproj"`

## Thunderstore/r2modman Package

Each mod release needs a package folder in `Release/HLin_Mods-<ModName>/` with:

| File | Required | Description |
|---|---|---|
| `<ModName>.dll` | Yes | Built DLL from `bin/Release/net35/` |
| `manifest.json` | Yes | Thunderstore package metadata |
| `icon.png` | Yes | 256x256 PNG icon |
| `README.md` | Yes | Mod description and changelog |

### manifest.json template

```json
{
    "name": "MyMod",
    "author": "HLin_Mods",
    "version_number": "1.0.0",
    "dependencies": [],
    "description": "Short description of the mod.",
    "website_url": "https://github.com/haohlin/H3VR-Mods.git"
}
```

### README.md template

```markdown
# Mod Name

Description of what the mod does.

## Changelog

- 1.0.0: First upload.
```

### Deploy full package to r2modman

```bash
# Copy all package files to the plugin folder
powershell.exe -Command "Copy-Item 'E:\Dev\H3VR-Mods\Release\HLin_Mods-<ModName>\*' 'C:\Users\y\AppData\Roaming\r2modmanPlus-local\H3VR\profiles\Default\BepInEx\plugins\HLin_Mods-<ModName>\' -Force -Recurse"
```

## Researching Game Code

To understand what to patch, use dnSpy on Windows:
- Open `E:\Steam\steamapps\common\H3VR\h3vr_Data\Managed\Assembly-CSharp.dll`
- All game types are in the `FistVR` namespace
- Key base classes: `FVRPhysicalObject`, `FVRFireArm`, `FVRInteractiveObject`
- Sosigs (enemies): `Sosig`, `SosigWeapon`, `SosigLink`
- VR interaction: `FVRViveHand`, `FVRQuickBeltSlot`

Alternatively, export from dnSpy (File > Export to Project) to get browsable `.cs` files that Claude can read directly.

## Harmony Patching Quick Reference

| Pattern | Use |
|---|---|
| `[HarmonyPrefix]` + `return false` | Replace original method entirely |
| `[HarmonyPrefix]` + `return true` | Run code before original, then let it execute |
| `[HarmonyPostfix]` | Run code after original method |
| `[HarmonyTranspiler]` | Modify IL instructions (advanced) |
| `__instance` | Access the patched object's instance |
| `__result` | Access/modify return value (postfix) |
| `ref` parameters | Modify method parameters before execution |
| Manual: `harmony.Patch(original, new HarmonyMethod(patch))` | Patch without attributes |

## Full Workflow

```
1. Research target class/method in dnSpy
2. Scaffold or edit mod (WSL file editing)
3. Build:  cmd.exe /c "cd /d E:\Dev\H3VR-Mods\<Mod> && dotnet build -c Release 2>&1"
4. Deploy: powershell.exe -Command "Copy-Item ... -Force"
5. Launch H3VR via r2modman and test in VR
6. Package: copy DLL + manifest.json + icon.png + README.md to Release/HLin_Mods-<Mod>/
```

## Writing Files to Windows Paths (from WSL)

Since mounted drives are read-only from WSL bash, use this pattern:

```bash
# Write content to temp file in WSL, then copy to Windows
cat > /tmp/claude-1000/myfile.cs << 'EOF'
// file content here
EOF
powershell.exe -Command "Copy-Item '\\wsl.localhost\Ubuntu\tmp\claude-1000\myfile.cs' 'E:\Dev\H3VR-Mods\MyMod\src\myfile.cs'"
```
