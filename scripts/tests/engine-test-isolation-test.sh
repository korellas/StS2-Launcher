#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK_DIR="$(mktemp -d)"
trap 'rm -rf "$CHECK_DIR"' EXIT
cat > "$CHECK_DIR/Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$ROOT/src/STS2Mobile/AppPaths.cs" Link="AppPaths.cs" />
    <Compile Include="$ROOT/src/STS2Mobile/Steam/AppUpdateChecker.cs" Link="AppUpdateChecker.cs" />
  </ItemGroup>
</Project>
XML
cat > "$CHECK_DIR/Program.cs" <<'CS'
using STS2Mobile;
using STS2Mobile.Steam;

var failures = new List<string>();
void Check(bool condition, string message)
{
    if (!condition)
        failures.Add(message);
}
#if ENGINE_452_TEST
Check(AppPaths.ExternalModsDir != "/storage/emulated/0/StS2Launcher/Mods" &&
      AppPaths.ExternalSaveBackupsDir != "/storage/emulated/0/StS2Launcher/Saves",
      "Test build shares production mods or save backups");
await AppUpdateChecker.CheckAsync();
Check(Godot.Engine.Calls == 0, "Test build entered the production update check");
#else
Check(AppPaths.ExternalModsDir == "/storage/emulated/0/StS2Launcher/Mods" &&
      AppPaths.ExternalSaveBackupsDir == "/storage/emulated/0/StS2Launcher/Saves",
      "Production storage paths changed");
#endif
if (failures.Count != 0)
    throw new Exception(string.Join("; ", failures));
Console.WriteLine("PASS storage and update behavior for this build mode");

namespace Godot
{
    public class GodotObject
    {
        public object Call(string name, params object[] args) => throw new NotSupportedException();
    }
    public static class Engine
    {
        public static int Calls;
        public static GodotObject GetSingleton(string name)
        {
            Calls++;
            throw new NotSupportedException("Android is unavailable in this check");
        }
    }
}
namespace STS2Mobile
{
    public static class PatchHelper
    {
        public static void Log(string message) { }
    }
}
CS
dotnet run --project "$CHECK_DIR/Check.csproj" -p:NuGetAudit=false --verbosity quiet
dotnet run --project "$CHECK_DIR/Check.csproj" -p:NuGetAudit=false -p:DefineConstants=ENGINE_452_TEST --verbosity quiet
