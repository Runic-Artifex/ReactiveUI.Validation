#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Pin the Runic DynamicData cohorts and restore the legacy release pair.

The current RunicArtifex.DynamicData pair restores from NuGet.org like any other
package; DIGESTS pins the SHA-256 of the NuGet.org (repository-signed) archives,
which the package gates compare with the restored bytes. Without arguments this
checks that the central versions match those pins.

The immutable legacy Runic.DynamicData 10.0.0-runic.5 pair, which the released
Validation baseline depends on, is published only as GitHub release assets;
--legacy downloads and verifies it into the local feed artifacts/dependencies.
"""

import argparse
import hashlib
from pathlib import Path
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
FEED = ROOT / "artifacts" / "dependencies"
CURRENT_VERSION = "10.0.0-runic.118"
DIGESTS = {
    "RunicArtifex.DynamicData": "8cbb24bad17666d45ea429465314b535e0b459b1910cb4f5fa2a23bd531014ac",
    "RunicArtifex.DynamicData.Reactive": "2fc449f7f33888f19d377f08ad99880cd89714b7305719a1f15758f2b906e9e3",
}
LEGACY_VERSION = "10.0.0-runic.5"
LEGACY_DIGESTS = {
    "Runic.DynamicData": "ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366",
    "Runic.DynamicData.Reactive": "49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268",
}


def restore_cohort(version, digests):
    """Reuse or download one immutable GitHub-released DynamicData pair into the local feed."""
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


def verify_central_pins(root=ROOT):
    """Require the central DynamicData versions to be the pinned NuGet.org pair."""
    versions = {
        node.attrib["Include"]: node.attrib["Version"]
        for node in ET.parse(root / "src" / "Directory.Packages.props").iter("PackageVersion")
    }
    if any(package in versions for package in LEGACY_DIGESTS):
        raise ValueError("The source build must not pin the legacy Runic.DynamicData IDs")
    if {versions.get(package) for package in DIGESTS} != {CURRENT_VERSION}:
        raise ValueError("Update the DynamicData version and NuGet.org checksums together when changing the cohort")


def main():
    """Check the current central pair or restore the explicit immutable legacy baseline."""
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--legacy", action="store_true", help="Restore the immutable .5 pair used by the native gate's released Validation baseline")
    args = parser.parse_args()
    if args.legacy:
        restore_cohort(LEGACY_VERSION, LEGACY_DIGESTS)
        return
    verify_central_pins()
    print(f"Central RunicArtifex.DynamicData pins match {CURRENT_VERSION}; the pair restores from NuGet.org")


if __name__ == "__main__":
    main()
