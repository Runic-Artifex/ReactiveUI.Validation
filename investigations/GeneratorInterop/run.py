#!/usr/bin/env python3
"""Opt-in real-package cross-generator investigation; never builds shipping sources."""
import argparse
import hashlib
import importlib.util
import json
import platform
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('native_gate', ROOT / 'eng/verify-native-validation.py')
NATIVE = importlib.util.module_from_spec(spec); spec.loader.exec_module(NATIVE)
CASES = ('PARTIAL', 'FIELD_EMAIL', 'FIELD_HELPER', 'FIELD_CONTEXT', 'FIELD_TARGET', 'COMMAND_METADATA', 'GENERATED_INTERFACE', 'FIELD_OBSERVABLE')
SOURCE = '2550376b230ccfb78beb8d3b4ced8866b65d809e'
VERSION = '8.1.0-runic.0.790.17.15.10'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def config(folder, validation, dependencies):
    tree = ET.Element('configuration'); sources = ET.SubElement(tree, 'packageSources'); ET.SubElement(sources, 'clear')
    mapping = ET.SubElement(tree, 'packageSourceMapping')
    for key, feed, pattern in (('validation', validation, 'Runic.ReactiveUI.Validation*'), ('dynamicdata', dependencies, 'Runic.DynamicData*'), ('nuget', 'https://api.nuget.org/v3/index.json', '*')):
        ET.SubElement(sources, 'add', key=key, value=str(feed))
        ET.SubElement(ET.SubElement(mapping, 'packageSource', key=key), 'package', pattern=pattern)
    path = folder / 'nuget.config'; ET.ElementTree(tree).write(path, encoding='utf-8', xml_declaration=True)
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-feed', type=Path, required=True)
    parser.add_argument('--dependencies-feed', type=Path, required=True)
    parser.add_argument('--cache', type=Path)
    parser.add_argument('--mode', choices=('managed', 'native'), default='managed')
    parser.add_argument('--case', choices=('all', *CASES), default='all')
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/GeneratorInterop')
    args = parser.parse_args()
    if args.mode == 'native' and (platform.system() != 'Linux' or platform.machine().lower() != 'x86_64'):
        raise ValueError('Native probe requires matching Linux x64 host')
    sdk = subprocess.check_output(['dotnet', '--version'], cwd=ROOT, text=True).strip()
    if sdk != '10.0.401': raise ValueError('Pinned SDK required')
    pins = NATIVE.PACKAGES.dependency_pins(ROOT)
    feed = args.package_feed.resolve(); dependencies = args.dependencies_feed.resolve()
    packages = NATIVE.packed_versions(feed, pins, SOURCE)
    if any(version != VERSION for _, _, version, _ in packages): raise ValueError('Exact verified .10 package pair required')
    for package_id, expected in NATIVE.DEPENDENCIES.DIGESTS.items():
        path = dependencies / f'{package_id}.{pins[package_id.casefold()]}.nupkg'
        if sha(path) != expected: raise ValueError('Released DynamicData bytes differ')
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    cache = args.cache.resolve() if args.cache else output / 'packages'
    report = {'investigationHead': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
              'shippingSource': SOURCE, 'shippingVersion': VERSION, 'sdk': sdk, 'host': platform.platform(),
              'mode': args.mode, 'runnerSha256': sha(Path(__file__)), 'fixtureSources': {p.name: sha(p) for p in FIXTURE.glob('*.cs')},
              'publishedDynamicDataSha256': NATIVE.DEPENDENCIES.DIGESTS, 'shippingPackages': {}, 'checks': []}
    for reactive, package_id, version, package in packages:
        flavor = 'Reactive' if reactive else 'Primitives'
        report['shippingPackages'][flavor] = {'id': package_id, 'version': version, 'sha256': sha(package)}
    selected = CASES if args.case == 'all' else (args.case,)
    if args.mode == 'native' and selected not in {('PARTIAL',), ('FIELD_OBSERVABLE',)}: raise ValueError('Native mode only publishes a selected positive PARTIAL or FIELD_OBSERVABLE corpus')
    with tempfile.TemporaryDirectory(prefix='consumer-', dir=output) as temporary:
        for reactive, package_id, version, package in packages:
            flavor = 'Reactive' if reactive else 'Primitives'
            for case in selected:
                name = f'{flavor}-{case}-{args.mode}'
                folder = Path(temporary) / name
                shutil.copytree(FIXTURE, folder, ignore=shutil.ignore_patterns('bin', 'obj', 'evidence', 'ProjectionProof', '__pycache__'))
                NATIVE.guard_consumer_sources(folder)
                project = folder / flavor / 'Probe.csproj'
                nuget = config(folder, feed, dependencies)
                flags = ['-c', 'Release', '-m:2', '-warnaserror', '-v:normal', '--configfile', nuget,
                         f'-p:RestorePackagesPath={cache}', f'-p:ProbeCase={case}',
                         '-p:EnableTrimAnalyzer=true', '-p:EnableAotAnalyzer=true', '-p:TreatWarningsAsErrors=true',
                         '-p:RunAnalyzers=true', '-p:RunAnalyzersDuringBuild=true', '-p:ILLinkTreatWarningsAsErrors=true',
                         '-p:IlcTreatWarningsAsErrors=true', '-p:TrimmerSingleWarn=false',
                         '-p:SuppressTrimAnalysisWarnings=false', '-p:SuppressAotAnalysisWarnings=false']
                publish = folder / 'publish'
                command = ['dotnet', 'publish' if args.mode == 'native' else 'build', project, *flags]
                if args.mode == 'native': command += ['-r', 'linux-x64', '--self-contained', 'true', '-o', publish, '-p:PublishTrimmed=true', '-p:TrimMode=full', '-p:PublishAot=true', '-p:IlcSingleThreaded=true']
                code, content = NATIVE.run(command, output / f'{name}.build.log', folder, expected=True)
                assets = json.loads((project.parent / 'obj/project.assets.json').read_text())
                NATIVE.verify_native_graph(assets, reactive, pins, version, 'linux-x64' if args.mode == 'native' else None)
                NATIVE.verify_restored_bytes(assets, package_id, version, sha(package))
                if any(name.split('/', 1)[0].lower().startswith('microsoft.codeanalysis') for name in assets['libraries']):
                    raise ValueError('Roslyn runtime package leaked into consumer graph')
                forbidden = {'ReactiveUI.Validation.SourceGenerators.dll', 'ReactiveUI.Binding.SourceGenerators.dll', 'ReactiveUI.SourceGenerators.Roslyn.dll', 'ReactiveUI.Binding.Analyzer.dll', 'ReactiveUI.SourceGenerators.Analyzers.CodeFixes.dll'}
                for target in assets['targets'].values():
                    for library in target.values():
                        for group in ('compile', 'runtime', 'runtimeTargets'):
                            if any(Path(asset).name in forbidden or 'Microsoft.CodeAnalysis' in asset for asset in library.get(group, {})):
                                raise ValueError('Analyzer or Roslyn assembly leaked into compile/runtime assets')
                cohort = {}
                for wanted, expected in ((f'ReactiveUI{ ".Reactive" if reactive else ""}', '26.0.1'), (f'ReactiveUI.Binding{ ".Reactive" if reactive else ""}', '9.1.0'), ('ReactiveUI.SourceGenerators', '4.2.0')):
                    if f'{wanted}/{expected}' not in assets['libraries']: raise ValueError(f'Wrong actual generator/runtime cohort: {wanted}')
                    library = assets['libraries'][f'{wanted}/{expected}']
                    restored = [Path(base) / library['path'] / f'{wanted.lower()}.{expected}.nupkg' for base in assets['packageFolders']]
                    present = [path for path in restored if path.is_file()]
                    if len(present) != 1: raise ValueError('Expected one actual restored cohort package')
                    cohort[wanted] = {'version': expected, 'sha256': sha(present[0])}
                (output / f'{name}.graph.json').write_text(json.dumps(assets, indent=2))
                analyzer_file = project.parent / 'obj/analyzers.txt'
                analyzers = [Path(line) for line in analyzer_file.read_text().splitlines() if line.strip()]
                wanted = ('ReactiveUI.Validation.SourceGenerators.dll', 'ReactiveUI.Binding.SourceGenerators.dll', 'ReactiveUI.SourceGenerators.Roslyn.dll')
                if any(not any(path.name == assembly for path in analyzers) for assembly in wanted): raise ValueError('Missing actual analyzer in compiler inputs')
                analyzer_records = [{'path': str(path), 'sha256': sha(path)} for path in analyzers if path.name in wanted]
                (output / f'{name}.compiler.txt').write_text('\n'.join(line for line in content.splitlines() if '/analyzer:' in line or 'InterceptorsNamespaces' in line) + '\n')
                generated = project.parent / 'obj/generated'
                retained = output / f'{name}-generated'
                if retained.exists(): shutil.rmtree(retained)
                if generated.exists(): shutil.copytree(generated, retained)
                generated_records = [{'path': p.relative_to(output).as_posix(), 'sha256': sha(p)} for p in retained.rglob('*.cs')] if retained.exists() else []
                errors = sorted(set(re.findall(r'\berror ([A-Z]+\d+): ([^\n]+)', content)))
                warnings = sorted(set(re.findall(r'\bwarning ([A-Z]+\d+): ([^\n]+)', content)))
                record = {'name': name, 'returnCode': code, 'analyzersInReportedOrder': analyzer_records, 'generated': generated_records,
                          'cohortPackages': cohort, 'nativeRuntimeRequired': args.mode == 'native',
                          'errors': [{'id': identifier, 'message': message} for identifier, message in errors],
                          'warnings': [{'id': identifier, 'message': message} for identifier, message in warnings]}
                if code == 0:
                    assembly = f'GeneratorInterop.{flavor}'
                    runtime = [publish / assembly, '--require-aot'] if args.mode == 'native' else ['dotnet', project.parent / f'bin/Release/net10.0/{assembly}.dll']
                    _, runtime_output = NATIVE.run(runtime, output / f'{name}.runtime.log', folder)
                    if '"passed":true' not in runtime_output: raise ValueError('Interop proof did not report passing application behavior')
                    record['runtimePassed'] = True
                    print(runtime_output, flush=True)
                error_ids = {entry['id'] for entry in record['errors']}
                expected_ids = set() if case in {'PARTIAL', 'FIELD_OBSERVABLE'} else {'RUVG001'} if case == 'COMMAND_METADATA' else {'RUVG001', 'RUVG005'}
                record['expectedDiagnostics'] = sorted(expected_ids)
                record['expectedOutcomeMatched'] = error_ids == expected_ids and (code == 0 if not expected_ids else code != 0) and not warnings
                report['checks'].append(record)
                (output / 'results.json').write_text(json.dumps(report, indent=2) + '\n')
    if any(not check['expectedOutcomeMatched'] for check in report['checks']):
        raise ValueError('A real-package probe did not match its documented expected outcome; inspect recorded diagnostics')
    print(f'Investigation reports recorded without assuming positive/negative outcomes: {output / "results.json"}')


if __name__ == '__main__': main()
