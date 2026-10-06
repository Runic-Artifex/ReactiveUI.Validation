#!/usr/bin/env python3
"""Opt-in managed reproduction. Never overwrites the original tested evidence."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent
HISTORICAL = ROOT / 'evidence/manifest.json'
SNAPSHOT = '9ad0d6a'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', required=True, type=Path, help='approved direnv-enabled locked SDK project (10.0.401)')
    parser.add_argument('--package-feed', required=True, type=Path, help='local feed containing the exact retained Validation .10 pair')
    parser.add_argument('--dependencies-feed', required=True, type=Path, help='local feed containing the exact released DynamicData .5 pair')
    parser.add_argument('--cache', required=True, type=Path, help='existing NuGet global-packages cache to reuse')
    parser.add_argument('--output', type=Path, help='fresh nonexistent directory; default: ignored reproductions/<UTC timestamp>-<id>')
    parser.add_argument('--binding-source', type=Path, help='optional pinned Binding checkout to reverify inspected source; not needed to run generators')
    args = parser.parse_args()
    sdk, feed, dependencies, cache = (path.expanduser().resolve() for path in (args.sdk, args.package_feed, args.dependencies_feed, args.cache))
    for path in (sdk, feed, dependencies, cache):
        if not path.is_dir():
            parser.error(f'directory does not exist: {path}')
    historical_hash = digest(HISTORICAL)
    historical = json.loads(HISTORICAL.read_text())
    expected = {Path(item['path']).name.lower(): item['sha256'] for item in historical['packages']}
    verified_feeds = []
    for package_feed, names in [
        (feed, [f'Runic.ReactiveUI.Validation{suffix}.8.1.0-runic.0.790.17.15.10.nupkg' for suffix in ['', '.Reactive']]),
        (dependencies, [f'Runic.DynamicData{suffix}.10.0.0-runic.5.nupkg' for suffix in ['', '.Reactive']]),
    ]:
        archives = {path.name.lower(): path for path in package_feed.glob('*.nupkg')}
        for name in names:
            path = archives.get(name.lower())
            if path is None or digest(path) != expected[name.lower()]:
                parser.error(f'feed must contain exact historical package bytes: {name}')
            verified_feeds.append({'path': str(path), 'sha256': digest(path)})
    # Reuse restore caches without substituting different archive bytes.
    for name, sha256 in expected.items():
        for path in cache.glob(f'*/*/{name}'):
            if digest(path) != sha256:
                parser.error(f'cached package differs from historical pinned bytes: {path}')
    prefix = ['direnv', 'exec', str(sdk)]
    version = subprocess.check_output(prefix + ['dotnet', '--version'], cwd=ROOT, text=True).strip()
    if version != historical['sdk']:
        parser.error(f'expected locked SDK {historical["sdk"]}, got {version}')
    binding_reference = {'reinspected': False, 'historicalSnapshot': SNAPSHOT,
                         'historicalRevision': historical['bindingProjectionReferenceRevision'],
                         'historicalFiles': historical['bindingProjectionReferenceFiles']}
    if args.binding_source:
        binding = args.binding_source.expanduser().resolve()
        revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=binding, text=True).strip()
        if revision != historical['bindingProjectionReferenceRevision']:
            parser.error('optional Binding source must match the historical inspected revision')
        files = []
        for item in historical['bindingProjectionReferenceFiles']:
            directory = 'Generators' if item['path'].endswith('SourceGeneratorsCompilation.cs') else 'Helpers'
            path = binding / 'src/ReactiveUI.Binding.SourceGenerators' / directory / Path(item['path']).name
            if not path.is_file() or digest(path) != item['sha256']:
                parser.error(f'optional inspected Binding source differs: {path}')
            files.append({'path': str(path), 'sha256': digest(path)})
        binding_reference = {'reinspected': True, 'revision': revision, 'files': files}
    output = (args.output.expanduser().resolve() if args.output else
              ROOT / 'reproductions' / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '-' + uuid.uuid4().hex[:8]))
    if ROOT in output.parents and not output.is_relative_to(ROOT / 'reproductions'):
        parser.error('output inside the proof tree must be beneath excluded reproductions/')
    if output.exists() or output == ROOT / 'evidence' or ROOT / 'evidence' in output.parents:
        parser.error(f'output must be a fresh directory outside historical evidence: {output}')
    output.mkdir(parents=True)
    print(f'Reproduction output: {output}', flush=True)
    config = ET.Element('configuration')
    sources = ET.SubElement(config, 'packageSources')
    ET.SubElement(sources, 'clear')
    for name, value in [('candidate', str(feed)), ('dependencies', str(dependencies)), ('nuget.org', 'https://api.nuget.org/v3/index.json')]:
        ET.SubElement(sources, 'add', key=name, value=value)
    mappings = ET.SubElement(config, 'packageSourceMapping')
    for source, pattern in [('candidate', 'Runic.ReactiveUI.Validation*'), ('dependencies', 'Runic.DynamicData*'), ('nuget.org', '*')]:
        mapping = ET.SubElement(mappings, 'packageSource', key=source)
        ET.SubElement(mapping, 'package', pattern=pattern)
    config_path = output / 'restore.nuget.config'
    ET.ElementTree(config).write(config_path, encoding='utf-8', xml_declaration=True)

    def invoke(command, log):
        with (output / log).open('w') as stream:
            subprocess.run(prefix + command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT, check=True)

    for flavor in ['Primitives', 'Reactive']:
        invoke(['dotnet', 'build', 'ProjectionProof.csproj', '-c', 'Release', f'-p:Flavor={flavor}', '-warnaserror',
                f'-p:RestoreConfigFile={config_path}', f'-p:RestorePackagesPath={cache}'], f'build-{flavor.lower()}.log')
        invoke(['dotnet', str(ROOT / f'bin/{flavor}/Release/net10.0/ProjectionProof.{flavor}.dll'), flavor, str(ROOT), str(cache), str(output)], f'run-{flavor.lower()}.log')
    packages = []
    for name, sha256 in expected.items():
        paths = list(cache.glob(f'*/*/{name}'))
        if len(paths) != 1 or digest(paths[0]) != sha256:
            raise RuntimeError(f'restored cache must contain exact historical archive: {name}')
        packages.append({'path': str(paths[0]), 'sha256': sha256})
    runs = [json.loads((output / f'evidence/{flavor}/result.json').read_text()) for flavor in ['Primitives', 'Reactive']]
    for result in runs:
        for item in result['inputs']:
            analyzer = Path(item['path'])
            package_root = analyzer.parents[4]
            package_archive = next(package_root.glob('*.nupkg'))
            with zipfile.ZipFile(package_archive) as archive:
                packaged_bytes = archive.read(str(analyzer.relative_to(package_root)))
            if hashlib.sha256(packaged_bytes).hexdigest() != item['sha256']:
                raise RuntimeError(f'loaded analyzer differs from packaged bytes: {analyzer}')
    manifest = {
        'purpose': 'parameterized reproduction of bounded nonshipping private semantic projection proof',
        'sdk': version, 'worktreeBase': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'historicalSnapshot': SNAPSHOT, 'historicalManifestSha256': historical_hash,
        'candidateSource': historical['candidateSource'], 'verifiedFeeds': verified_feeds, 'packages': packages,
        'bindingProjectionReference': binding_reference,
        'restoreConfigSha256': digest(config_path),
        'proofSources': [{'path': path.name, 'sha256': digest(path)} for path in sorted(ROOT.iterdir()) if path.suffix in ['.cs', '.csproj', '.props', '.targets', '.py', '.config']],
        'runs': runs, 'limitations': historical['limitations'],
    }
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    if digest(HISTORICAL) != historical_hash:
        raise RuntimeError('historical manifest changed during reproduction')
    print('PASS: both flavors, both generator orders; historical evidence unchanged')


if __name__ == '__main__':
    main()
