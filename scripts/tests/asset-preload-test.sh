#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
cat > "$test_dir/Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net9.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <Compile Include="$root/src/STS2Mobile/Patches/AssetPreloadPatches.cs" Link="AssetPreloadPatches.cs" />
    <Compile Include="$root/src/STS2Mobile/PatchHelper.cs" Link="PatchHelper.cs" />
    <Compile Include="$root/scripts/tests/asset-preload/Program.cs" Link="Program.cs" />
    <Reference Include="0Harmony"><HintPath>$root/upstream/godot-export/.godot/mono/publish/arm64/0Harmony.dll</HintPath></Reference>
  </ItemGroup>
</Project>
XML
dotnet run --project "$test_dir/Check.csproj" --verbosity quiet
