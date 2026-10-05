#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Download the pinned Runic DynamicData release into the local NuGet feed."""

import hashlib
from pathlib import Path
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
FEED = ROOT / "artifacts" / "dependencies"
CURRENT_VERSION = "10.0.0-runic.30"
DIGESTS = {
    "Runic.DynamicData": "cf0d369f43774c2ba1535db8c468c0214d9a82c7f3d1a3d6fa1bf74918f8cbbc",
    "Runic.DynamicData.Reactive": "23cc3f33c029157585a2521cc1c04afbaba21efc6dc6659a855ecafc67217fc5",
}
LEGACY_VERSION = "10.0.0-runic.5"
LEGACY_DIGESTS = {
    "Runic.DynamicData": "ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366",
    "Runic.DynamicData.Reactive": "49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268",
}


def restore_cohort(version, digests):
    """Reuse or download one immutable DynamicData pair into the local feed."""
    FEED.mkdir(parents=True, exist_ok=True)
    for package, digest in digests.items():
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


def main():
    """Restore the current central pair; native gates request their immutable legacy pair separately."""
    versions = {
        node.attrib["Include"]: node.attrib["Version"]
        for node in ET.parse(ROOT / "src" / "Directory.Packages.props").iter("PackageVersion")
    }
    if {versions[package] for package in DIGESTS} != {CURRENT_VERSION}:
        raise ValueError("Update the release version and checksums together when changing the DynamicData cohort")
    restore_cohort(CURRENT_VERSION, DIGESTS)


if __name__ == "__main__":
    main()
