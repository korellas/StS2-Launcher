#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
cat > "$test_dir/trace.c" <<'C'
#include <stdbool.h>
#include <stdint.h>
#include <stdatomic.h>
static atomic_int begins, ends, sync_begins, sync_ends;
static bool enabled = true;
bool ATrace_isEnabled(void) { return enabled; }
void ATrace_beginSection(const char* n) { sync_begins++; }
void ATrace_endSection(void) { sync_ends++; }
void ATrace_beginAsyncSection(const char* n, int32_t c) { begins++; }
void ATrace_endAsyncSection(const char* n, int32_t c) { ends++; }
void ATrace_setCounter(const char* n, int64_t v) {}
int trace_open(void) { return begins - ends; }
int trace_sync_open(void) { return sync_begins - sync_ends; }
void trace_enabled(bool value) { enabled = value; }
C
if [ "$(uname -s)" = Darwin ]; then
    clang -dynamiclib "$test_dir/trace.c" -o "$test_dir/trace-native"
else
    clang -shared -fPIC "$test_dir/trace.c" -o "$test_dir/trace-native"
fi
cat > "$test_dir/Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net9.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>
    <Compile Include="$root/src/STS2Mobile/Launcher/BenchmarkTrace.cs" Link="BenchmarkTrace.cs" />
    <Compile Include="$root/scripts/tests/benchmark-trace/Program.cs" Link="Program.cs" />
    <Reference Include="0Harmony"><HintPath>$root/upstream/godot-export/.godot/mono/publish/arm64/0Harmony.dll</HintPath></Reference>
  </ItemGroup>
</Project>
XML
dotnet run --project "$test_dir/Check.csproj" --verbosity quiet -- "$test_dir/trace-native"
