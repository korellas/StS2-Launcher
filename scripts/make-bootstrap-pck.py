#!/usr/bin/env python3
"""Generate a minimal Godot 4.5 PCK with a project and empty main scene.

This bootstrap PCK lets the engine initialize normally (project settings,
.NET/Mono, GodotSharp) so the STS2Mobile launcher can run without game files.
"""

import argparse
import hashlib
import struct
import sys
import os

# PCK format constants
MAGIC = 0x43504447  # "GDPC"
FORMAT_VERSION = 3
GODOT_MAJOR = 4
GODOT_MINOR = 5
GODOT_PATCH = 1
PACK_REL_FILEBASE = 0x02
ALIGNMENT = 32
HEADER_SIZE = 4 + 4 + 4 + 4 + 4 + 4 + 8 + 8 + (16 * 4)  # 104 bytes

# Minimal project.godot that enables .NET and sets a dummy main scene
PROJECT_GODOT = """\
; Minimal bootstrap project for STS2Mobile launcher
config_version=5
_custom_features="dotnet"

[application]

config/name="sts2"
config/features=PackedStringArray("4.5", "Forward Plus", "C#")
run/main_scene="res://bootstrap.tscn"
; Godot quits on the system back gesture by default, which closed the
; launcher outright. The game handles the notification itself; nothing
; should be closed just because back was pressed.
config/quit_on_go_back=false

[display]

window/size/viewport_width=1920
window/size/viewport_height=1080
window/stretch/mode="canvas_items"
window/stretch/aspect="expand"
window/handheld/orientation=4

[dotnet]

project/assembly_name="sts2"
"""

BOOTSTRAP_SCENE = """\
[gd_scene format=3]

[node name="Bootstrap" type="Node"]
"""


def align(offset, alignment=ALIGNMENT):
    return (offset + alignment - 1) & ~(alignment - 1)


def pad_string_len(s):
    """String length padded to 4-byte boundary."""
    encoded = s.encode("utf-8")
    padded = len(encoded) + ((4 - len(encoded) % 4) % 4)
    return padded, encoded


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        default=os.path.join(script_dir, "..", "android", "assets", "bootstrap.pck"),
    )
    output = os.path.abspath(parser.parse_args().output)
    files = [
        ("res://project.godot", PROJECT_GODOT.encode("utf-8")),
        ("res://bootstrap.tscn", BOOTSTRAP_SCENE.encode("utf-8")),
    ]

    # Calculate layout
    file_base = align(HEADER_SIZE)  # file data starts after header, aligned
    file_data = b"".join(data for _, data in files)
    file_end = file_base + len(file_data)
    dir_base = align(file_end)  # directory starts after file data, aligned

    # Build directory entry
    dir_section = struct.pack("<I", len(files))
    offset = 0
    for file_path, data in files:
        padded_len, path_bytes = pad_string_len(file_path)
        dir_section += struct.pack("<I", padded_len)
        dir_section += path_bytes + b"\x00" * (padded_len - len(path_bytes))
        dir_section += struct.pack("<Q", offset)  # relative to file_base
        dir_section += struct.pack("<Q", len(data))
        dir_section += hashlib.md5(data).digest()
        dir_section += struct.pack("<I", 0)  # flags (no encryption)
        offset += len(data)

    # Build header
    header = struct.pack("<I", MAGIC)
    header += struct.pack("<I", FORMAT_VERSION)
    header += struct.pack("<I", GODOT_MAJOR)
    header += struct.pack("<I", GODOT_MINOR)
    header += struct.pack("<I", GODOT_PATCH)
    header += struct.pack("<I", PACK_REL_FILEBASE)
    header += struct.pack("<Q", file_base)  # file_base offset
    header += struct.pack("<Q", dir_base)   # dir_base offset
    header += b"\x00" * (16 * 4)  # 16 reserved uint32s

    assert len(header) == HEADER_SIZE

    # Write PCK
    os.makedirs(os.path.dirname(output), exist_ok=True)
    with open(output, "wb") as f:
        f.write(header)
        f.write(b"\x00" * (file_base - HEADER_SIZE))  # padding to alignment
        f.write(file_data)
        f.write(b"\x00" * (dir_base - file_end))  # padding to alignment
        f.write(dir_section)

    size = os.path.getsize(output)
    print(f"Created bootstrap PCK: {output} ({size} bytes)")


if __name__ == "__main__":
    main()
