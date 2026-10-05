#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Download the pinned Runic DynamicData release into the local NuGet feed."""

import hashlib
from pathlib import Path
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
FEED = ROOT / "artifacts" / "dependencies"
DIGESTS = {
    "Runic.DynamicData": "ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366",
    "Runic.DynamicData.Reactive": "49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268",
}


def main():
    """Reuse verified packages; fail closed when a dependency pin changes."""
    versions = {
        node.attrib["Include"]: node.attrib["Version"]
        for node in ET.parse(ROOT / "src" / "Directory.Packages.props").iter("PackageVersion")
    }
    FEED.mkdir(parents=True, exist_ok=True)
    for package, digest in DIGESTS.items():
        version = versions[package]
        if version != "10.0.0-runic.5":
            raise ValueError("Update the release checksums when changing the DynamicData version")
        destination = FEED / f"{package}.{version}.nupkg"
        if destination.exists() and hashlib.sha256(destination.read_bytes()).hexdigest() == digest:
            print(f"Verified {destination.name}")
            continue
        url = f"https://github.com/Runic-Artifex/DynamicData/releases/download/v{version}/{destination.name}"
        with urllib.request.urlopen(url, timeout=60) as response:
            content = response.read()
        if hashlib.sha256(content).hexdigest() != digest:
            raise ValueError(f"Checksum mismatch for {package}")
        destination.write_bytes(content)
        print(f"Downloaded and verified {destination.name}")


if __name__ == "__main__":
    main()
