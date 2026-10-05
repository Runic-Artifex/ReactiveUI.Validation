#!/usr/bin/env python3
"""Fetch the immutable released Validation pair, verifying hashes before use."""
import argparse
import hashlib
from pathlib import Path
import shutil
import tempfile
from urllib.request import urlopen

VERSION = '8.1.0-runic.0.790.17'
HASHES = {
    f'Runic.ReactiveUI.Validation.{VERSION}.nupkg': '5d3c5261324181b7d8b995cc7b242977adba21a0ad81c361a4d4bd8a163d14df',
    f'Runic.ReactiveUI.Validation.Reactive.{VERSION}.nupkg': 'e3ba50fa1ad991912bd2d243a923c25055e9d434fb6d7c2eb840cf1ebbd5cb4b',
}

def verify(path, expected):
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != expected:
        raise ValueError(f'Immutable asset SHA-256 mismatch: {path}; expected {expected}, received {digest}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('feed', type=Path)
    parser.add_argument('--reuse', type=Path, help='Reuse a verified existing release feed before downloading')
    args = parser.parse_args()
    args.feed.mkdir(parents=True, exist_ok=True)
    for name, expected in HASHES.items():
        destination = args.feed / name
        if destination.exists():
            verify(destination, expected)
            print(f'verified {destination}')
            continue
        # Every temporary download/copy is removed on failure. A mismatched existing
        # immutable asset is reported, never silently replaced.
        with tempfile.TemporaryDirectory(prefix='.validation-release-', dir=args.feed) as temp:
            pending = Path(temp) / name
            candidate = args.reuse / name if args.reuse else None
            if candidate is not None and candidate.exists():
                verify(candidate, expected)
                shutil.copyfile(candidate, pending)
            else:
                url = f'https://github.com/Runic-Artifex/ReactiveUI.Validation/releases/download/runic-v{VERSION}/{name}'
                with urlopen(url, timeout=60) as response, pending.open('wb') as output:
                    shutil.copyfileobj(response, output)
            verify(pending, expected)
            pending.replace(destination)
        print(f'verified {destination}')

if __name__ == '__main__':
    main()
