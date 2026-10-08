#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK_DIR="$(mktemp -d)"
trap 'rm -rf "$CHECK_DIR"' EXIT
python3 - "$ROOT/vendor/godot/modules/mono/mono_gd/gd_mono.cpp" "$CHECK_DIR/bootstrap.cpp" <<'PY'
import pathlib
import sys

source = pathlib.Path(sys.argv[1]).read_text()
start = source.index('godot_plugins_initialize_fn initialize_coreclr_and_godot_plugins(')
end = source.index('\n}\n#endif', start) + 2
function = source[start:end]
pathlib.Path(sys.argv[2]).write_text(r'''
#include <cassert>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <string>
#define ANDROID_ENABLED
#define ERR_FAIL_COND_V_MSG(condition, value, message) if (condition) return value
struct String {
    std::string value;
    String utf8() const { return *this; }
    const char *get_data() const { return value.c_str(); }
};
namespace Path { String get_csharp_project_name() { return {"sts2"}; } }
namespace GDMonoCache { struct ManagedCallbacks {}; }
using godot_plugins_initialize_fn = bool (*)(void *, GDMonoCache::ManagedCallbacks *, const void **, int32_t);
using sts2mobile_initialize_fn = int (*)(void *, GDMonoCache::ManagedCallbacks *, const void **, int32_t);
sts2mobile_initialize_fn sts2mobile_initialize = nullptr;
void (*sts2mobile_apply)() = nullptr;
void (*mono_install_assembly_preload_hook)(void *, void *) = nullptr;
void *load_assembly_from_pck = nullptr;
bool game_present;
bool registered_game_scripts = false;
bool initialized_launcher = false;
bool applied_patches = false;
void print_verbose(const char *) {}
bool initialize_game(void *, GDMonoCache::ManagedCallbacks *, const void **, int32_t) {
    registered_game_scripts = true;
    return true;
}
int initialize_launcher(void *, GDMonoCache::ManagedCallbacks *, const void **, int32_t) {
    initialized_launcher = true;
    return 1;
}
bool initialize_sts2mobile(void *handle, GDMonoCache::ManagedCallbacks *callbacks, const void **interop, int32_t size) {
    return sts2mobile_initialize(handle, callbacks, interop, size) != 0;
}
void apply_patches() { applied_patches = true; }
int coreclr_initialize(void *, void *, int, void *, void *, void **handle, unsigned int *domain) {
    *handle = nullptr;
    *domain = 1;
    return 0;
}
int coreclr_create_delegate(void *, unsigned int, const char *assembly, const char *type, const char *method, void **result) {
    if (std::strcmp(assembly, "sts2") == 0 && std::strcmp(type, "GodotPlugins.Game.Main") == 0 &&
        std::strcmp(method, "InitializeFromGameProject") == 0) {
        *result = game_present ? reinterpret_cast<void *>(&initialize_game) : nullptr;
        return game_present ? 0 : -1;
    }
    assert(std::strcmp(assembly, "STS2Mobile") == 0);
    assert(std::strcmp(type, "STS2Mobile.ModEntry") == 0);
    if (std::strcmp(method, "InitializeGodotSharp") == 0) {
        *result = reinterpret_cast<void *>(&initialize_launcher);
    } else {
        assert(std::strcmp(method, "Apply") == 0);
        *result = reinterpret_cast<void *>(&apply_patches);
    }
    return 0;
}
''' + function + r'''
int main() {
    for (bool present : {false, true}) {
        game_present = present;
        registered_game_scripts = initialized_launcher = applied_patches = false;
        bool runtime_initialized = false;
        auto initialize = initialize_coreclr_and_godot_plugins(runtime_initialized);
        assert(runtime_initialized && initialize != nullptr && sts2mobile_apply != nullptr);
        assert(initialize(nullptr, nullptr, nullptr, 0));
        if (present && !registered_game_scripts) {
            std::cerr << "Game present: game initializer was skipped; scene scripts remain unregistered\n";
            return 1;
        }
        assert(registered_game_scripts == present);
        assert(initialized_launcher == !present);
        sts2mobile_apply();
        assert(applied_patches);
    }
    std::cout << "PASS game-present and launcher-only initialization paths\n";
}
''')
PY
c++ -std=c++17 "$CHECK_DIR/bootstrap.cpp" -o "$CHECK_DIR/bootstrap"
"$CHECK_DIR/bootstrap"
