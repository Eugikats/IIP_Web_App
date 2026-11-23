"""Adds the compiled code, native libraries and unpacked assets to the APK that aapt2 made.

Usage: pack.py <apk> <dex-dir> <lib-dir>

The plain SDK tools have no zip command on every machine, so this does the one job needed: dex files are
compressed, native libraries are stored uncompressed (zipalign then page-aligns them).
"""
import os
import sys
import zipfile


def main(apk, dex_dir, lib_dir):
    with zipfile.ZipFile(apk, "a", zipfile.ZIP_DEFLATED) as archive:
        for name in sorted(os.listdir(dex_dir)):
            if name.endswith(".dex"):
                archive.write(os.path.join(dex_dir, name), name, zipfile.ZIP_DEFLATED)
        for root, _, files in os.walk(lib_dir):
            for name in sorted(files):
                path = os.path.join(root, name)
                relative = os.path.relpath(path, os.path.dirname(lib_dir)).replace(os.sep, "/")
                archive.write(path, relative, zipfile.ZIP_STORED)


if __name__ == "__main__":
    main(*sys.argv[1:4])
