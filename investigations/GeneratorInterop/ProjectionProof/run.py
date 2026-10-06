#!/usr/bin/env python3
"""Sequential managed compiler proof; uses the locked SDK and the existing cache."""
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parent
SDK = Path('/home/viktor/Development/RunicArtifex/runic-sdk')
CACHE = SDK / '.cache/nuget'
CANDIDATES = Path('/home/viktor/Development/RunicArtifex/ReactiveUI.Validation/artifacts/verification/generated-support/2550376/candidate-packages')
PREFIX = ['direnv', 'exec', str(SDK)]

def invoke(command, log):
    with (ROOT / log).open('w') as stream:
        subprocess.run(PREFIX + command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT, check=True)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

for flavor in ['Primitives', 'Reactive']:
    invoke(['dotnet', 'build', 'ProjectionProof.csproj', '-c', 'Release', f'-p:Flavor={flavor}', '-warnaserror'], f'evidence-build-{flavor.lower()}.log')
    invoke(['dotnet', str(ROOT / f'bin/{flavor}/Release/net10.0/ProjectionProof.{flavor}.dll'), flavor, str(ROOT), str(CACHE)], f'evidence-run-{flavor.lower()}.log')

package_paths = sorted(CANDIDATES.glob('*.nupkg')) + [
    CACHE / f'{package}/{version}/{package}.{version}.nupkg'
    for package, version in [
        ('reactiveui.sourcegenerators', '4.2.0'), ('reactiveui.binding', '9.1.0'), ('reactiveui.binding.reactive', '9.1.0'),
        ('reactiveui', '26.0.1'), ('reactiveui.reactive', '26.0.1'),
        ('runic.dynamicdata', '10.0.0-runic.5'), ('runic.dynamicdata.reactive', '10.0.0-runic.5'),
        ('microsoft.codeanalysis.csharp', '5.9.0'), ('microsoft.codeanalysis.common', '5.9.0'),
    ]
]
binding = Path('/tmp/runic-validation-binding-sources/binding')
binding_files = [binding / 'src/ReactiveUI.Binding.SourceGenerators/Generators/SourceGeneratorsCompilation.cs',
                 binding / 'src/ReactiveUI.Binding.SourceGenerators/Helpers/SourceGeneratorsMemberExtractor.cs']
for flavor in ['Primitives', 'Reactive']:
    result = json.loads((ROOT / f'evidence/{flavor}/result.json').read_text())
    for item in result['inputs']:
        analyzer = Path(item['path'])
        package_root = analyzer.parents[4]
        package_archive = next(package_root.glob('*.nupkg'))
        with zipfile.ZipFile(package_archive) as archive:
            packaged_bytes = archive.read(str(analyzer.relative_to(package_root)))
        assert hashlib.sha256(packaged_bytes).hexdigest() == item['sha256'], analyzer
manifest = {
    'purpose': 'bounded nonshipping private semantic projection feasibility proof',
    'sdk': subprocess.check_output(PREFIX + ['dotnet', '--version'], cwd=ROOT, text=True).strip(),
    'worktreeBase': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
    'candidateSource': json.loads((CANDIDATES / 'manifest.json').read_text()),
    'packages': [{'path': str(path), 'sha256': digest(path)} for path in package_paths],
    'bindingProjectionReferenceRevision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=binding, text=True).strip(),
    'bindingProjectionReferenceFiles': [{'path': str(path), 'sha256': digest(path)} for path in binding_files],
    'proofSources': [{'path': path.name, 'sha256': digest(path)} for path in sorted(ROOT.iterdir()) if path.suffix in ['.cs', '.csproj', '.props', '.targets', '.py']],
    'runs': [json.loads((ROOT / f'evidence/{flavor}/result.json').read_text()) for flavor in ['Primitives', 'Reactive']],
    'limitations': ['one public nongeneric partial model and one [Reactive] string? _name field',
                    'private inner Validation driver is prototype plumbing, not production generator ordering',
                    'no command, derived list, generated IReactiveObject, XAML, nested/generic model, attribute modifier, or version matrix',
                    'managed compiler and runtime proof only; no NativeAOT publishing or production support change'],
}
(ROOT / 'evidence/manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print('PASS: both flavors, both generator orders, final dispatch, real generated property updates, no emitted projection')
