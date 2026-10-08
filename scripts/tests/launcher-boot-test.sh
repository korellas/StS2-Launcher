#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
check_dir="$(mktemp -d)"
trap 'rm -rf "$check_dir"' EXIT
cat > "$check_dir/Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net9.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <Compile Include="$repo_root/src/STS2Mobile/Launcher/LauncherUI.cs" Link="LauncherUI.cs" />
    <Compile Include="$repo_root/scripts/tests/launcher-boot/Program.cs" Link="Program.cs" />
  </ItemGroup>
</Project>
XML
dotnet run --project "$check_dir/Check.csproj" --verbosity quiet
