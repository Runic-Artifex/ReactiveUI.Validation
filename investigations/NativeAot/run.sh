#!/usr/bin/env bash
set -euo pipefail
# Run inside the repository's locked .NET/native toolchain, e.g. direnv exec SDK.
if [[ $# -lt 2 || $# -gt 4 ]]; then
    echo "usage: $0 VALIDATION_FEED DYNAMICDATA_FEED [managed|trimmed|native|all] [RID]" >&2
    exit 2
fi
root="$(cd -- "$(dirname -- "$0")" && pwd)"
validation_feed="$(realpath "$1")"
dynamicdata_feed="$(realpath "$2")"
mode="${3:-all}"
rid="${4:-linux-x64}"
[[ "$mode" =~ ^(managed|trimmed|native|all)$ ]] || exit 2
[[ "$(dotnet --version)" == "10.0.401" ]] || { echo 'SDK 10.0.401 required' >&2; exit 2; }
mkdir -p "$root/artifacts"
export PROBE_VALIDATION_FEED="$validation_feed" PROBE_DYNAMICDATA_FEED="$dynamicdata_feed" PROBE_CONFIG="$root/artifacts/nuget.config"
python3 - <<'PY'
import os, hashlib
from pathlib import Path
from xml.etree import ElementTree as ET
expected = {
    ('PROBE_VALIDATION_FEED','Runic.ReactiveUI.Validation.8.1.0-runic.0.790.17.nupkg'): '5d3c5261324181b7d8b995cc7b242977adba21a0ad81c361a4d4bd8a163d14df',
    ('PROBE_VALIDATION_FEED','Runic.ReactiveUI.Validation.Reactive.8.1.0-runic.0.790.17.nupkg'): 'e3ba50fa1ad991912bd2d243a923c25055e9d434fb6d7c2eb840cf1ebbd5cb4b',
    ('PROBE_DYNAMICDATA_FEED','Runic.DynamicData.10.0.0-runic.5.nupkg'): 'ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366',
    ('PROBE_DYNAMICDATA_FEED','Runic.DynamicData.Reactive.10.0.0-runic.5.nupkg'): '49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268',
}
for (env,name),digest in expected.items():
    path=Path(os.environ[env])/name
    actual=hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != digest: raise SystemExit(f'Unexpected immutable asset hash: {path}')
config=ET.Element('configuration')
sources=ET.SubElement(config, 'packageSources'); ET.SubElement(sources,'clear')
for key,value in [('validation',os.environ['PROBE_VALIDATION_FEED']),('dynamicdata',os.environ['PROBE_DYNAMICDATA_FEED']),('nuget.org','https://api.nuget.org/v3/index.json')]:
    ET.SubElement(sources,'add',key=key,value=value)
mapping=ET.SubElement(config,'packageSourceMapping')
for key,pattern in [('validation','Runic.ReactiveUI.Validation*'),('dynamicdata','Runic.DynamicData*'),('nuget.org','*')]:
    ET.SubElement(ET.SubElement(mapping,'packageSource',key=key),'package',pattern=pattern)
ET.ElementTree(config).write(os.environ['PROBE_CONFIG'],encoding='utf-8',xml_declaration=True)
PY
# Conservative sequential execution; continue runtime failures to record both flavors.
failed=0
for flavor in Primitives Reactive; do
    lower="${flavor,,}"
    project="$root/$flavor/Probe.csproj"
    for kind in managed trimmed native; do
        [[ "$mode" == all || "$mode" == "$kind" ]] || continue
        stem="$root/artifacts/$kind-$lower"
        if [[ "$kind" == managed ]]; then
            dotnet build "$project" -c Release -m:2 --configfile "$PROBE_CONFIG" > "$stem.build.log" 2>&1 || { failed=1; continue; }
            command=(dotnet "$root/$flavor/bin/Release/net10.0/ValidationAot.$flavor.dll")
        else
            args=(-c Release -r "$rid" -m:2 --configfile "$PROBE_CONFIG" -p:SelfContained=true -p:TrimmerSingleWarn=false -o "$stem")
            if [[ "$kind" == native ]]; then
                args+=(-p:PublishAot=true -p:IlcSingleThreaded=true)
            else
                args+=(-p:PublishTrimmed=true -p:TrimMode=full -p:PublishAot=false)
            fi
            dotnet publish "$project" "${args[@]}" > "$stem.publish.log" 2>&1 || { failed=1; continue; }
            command=("$stem/ValidationAot.$flavor")
            [[ "$rid" == win-* ]] && command=("$stem/ValidationAot.$flavor.exe")
            [[ "$kind" == native ]] && command+=(--require-aot)
        fi
        python3 - "$root/$flavor/obj/project.assets.json" "$flavor" <<'PYGRAPH'
import json, sys
libraries=json.load(open(sys.argv[1]))['libraries']
if any(name.startswith('DynamicData/') for name in libraries):
    raise SystemExit('Upstream DynamicData entered the consumer graph')
if sys.argv[2] == 'Primitives' and any(name.startswith('System.Reactive/') for name in libraries):
    raise SystemExit('System.Reactive entered the Primitives graph')
PYGRAPH
        "${command[@]}" > "$stem.runtime.jsonl" 2> "$stem.runtime.log" || failed=1
        cat "$stem.runtime.jsonl"
    done
done
exit "$failed"
