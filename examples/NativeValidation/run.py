#!/usr/bin/env python3
"""Verify package-only examples; native publishing is serialized by the native gate."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[1]
BASELINE = "8.1.0-runic.0.790.17"
HASHES = {
    f"Runic.ReactiveUI.Validation.{BASELINE}.nupkg": "5d3c5261324181b7d8b995cc7b242977adba21a0ad81c361a4d4bd8a163d14df",
    f"Runic.ReactiveUI.Validation.Reactive.{BASELINE}.nupkg": "e3ba50fa1ad991912bd2d243a923c25055e9d434fb6d7c2eb840cf1ebbd5cb4b",
    "Runic.DynamicData.10.0.0-runic.5.nupkg": "ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366",
    "Runic.DynamicData.Reactive.10.0.0-runic.5.nupkg": "49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268",
}


def run(command, log):
    result = subprocess.run(command, cwd=REPO, text=True, capture_output=True)
    if log.suffix == ".jsonl":
        log.write_text(result.stdout)
        log.with_suffix(".stderr.log").write_text(result.stderr)
    else:
        log.write_text(result.stdout + result.stderr)
    return result


def verify_graph(project, flavor, version):
    libraries = json.loads((project.parent / "obj/project.assets.json").read_text())["libraries"]
    suffix = "" if flavor == "Primitives" else ".Reactive"
    expected = {f"Runic.ReactiveUI.Validation{suffix}/{version}", f"Runic.DynamicData{suffix}/10.0.0-runic.5"}
    assert expected <= libraries.keys(), (expected, libraries.keys())
    assert not any(key.startswith(("DynamicData/", "DynamicData.Reactive/")) for key in libraries)
    opposite = ".Reactive" if not suffix else ""
    assert not any(key.startswith(f"Runic.ReactiveUI.Validation{opposite}/") or key.startswith(f"Runic.DynamicData{opposite}/") for key in libraries)
    if flavor == "Primitives":
        assert not any(key.startswith("System.Reactive/") for key in libraries)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["baseline-managed", "baseline-strict", "candidate-managed", "candidate-strict"])
    parser.add_argument("validation_feed", type=Path)
    parser.add_argument("dynamicdata_feed", type=Path)
    parser.add_argument("--version", default=BASELINE)
    args = parser.parse_args()
    candidate = args.mode.startswith("candidate")
    expectations = json.loads((ROOT / "manifest.json").read_text())["candidate" if candidate else "baseline"]
    if not candidate and args.version != BASELINE:
        parser.error("baseline uses immutable released .790.17")
    if candidate and args.version == BASELINE:
        parser.error("candidate requires a fresh version and candidate feed")
    assert subprocess.check_output(["dotnet", "--version"], text=True).strip() == "10.0.401"
    args.validation_feed = args.validation_feed.resolve()
    args.dynamicdata_feed = args.dynamicdata_feed.resolve()
    for name, digest in HASHES.items():
        if name.startswith("Runic.ReactiveUI") and candidate:
            continue
        feed = args.validation_feed if name.startswith("Runic.ReactiveUI") else args.dynamicdata_feed
        assert hashlib.sha256((feed / name).read_bytes()).hexdigest() == digest, name
    output = ROOT / "artifacts" / args.mode
    output.mkdir(parents=True, exist_ok=True)
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    for key, value in [("validation", str(args.validation_feed)), ("dynamicdata", str(args.dynamicdata_feed)), ("nuget.org", "https://api.nuget.org/v3/index.json")]:
        ET.SubElement(sources, "add", key=key, value=value)
    mapping = ET.SubElement(config, "packageSourceMapping")
    for key, pattern in [("validation", "Runic.ReactiveUI.Validation*"), ("dynamicdata", "Runic.DynamicData*"), ("nuget.org", "*")]:
        ET.SubElement(ET.SubElement(mapping, "packageSource", key=key), "package", pattern=pattern)
    config_path = output / "nuget.config"
    ET.ElementTree(config).write(config_path)
    for flavor in ("Primitives", "Reactive"):
        project = ROOT / ("Safe" if candidate else "ExpectedFailure") / flavor / "Examples.csproj"
        command = ["dotnet", "build", str(project), "-c", "Release", "-m:2", "--configfile", str(config_path), f"-p:ValidationVersion={args.version}"]
        strict = args.mode.endswith("strict")
        if strict:
            command += ["-p:PublishAot=true", "-p:EnableAotAnalyzer=true", "-p:EnableTrimAnalyzer=true", "-p:TreatWarningsAsErrors=true"]
        result = run(command, output / f"{flavor}.build.log")
        if strict and not candidate:
            assert result.returncode != 0, "baseline unexpectedly became strict-compatible"
            # Assert genuine Validation API annotation provenance, not an unrelated failed restore.
            for diagnostic in expectations["strictExpectedDiagnostics"]:
                code, method = diagnostic["code"], diagnostic["callee"]
                caller = re.escape(diagnostic["caller"])
                assert re.search(rf"{caller}\(\d+,\d+\): error {code}: Using member 'ReactiveUI\.Validation.*{method}", result.stdout), (flavor, code, method)
            unexpected = re.findall(r"error ((?!IL2026|IL3050)[A-Z]+\d+):", result.stdout)
            assert not unexpected, unexpected
        else:
            assert result.returncode == 0, result.stdout + result.stderr
            if not strict:
                assembly = project.parent / f"bin/Release/net10.0/NativeValidation.{flavor}.dll"
                runtime = run(["dotnet", str(assembly)], output / f"{flavor}.runtime.jsonl")
                records = [json.loads(line) for line in runtime.stdout.splitlines()]
                expected = expectations["managedExpected"]
                assert [record["case"] for record in records] == list(expected)
                assert [record["passed"] for record in records] == list(expected.values()), records
                for record in records:
                    if not record["passed"]:
                        failure = expectations["managedExpectedFailures"][record["case"]]
                        assert record["exception"] == failure["exception"] and record["failureId"] == failure["failureId"], record
                assert runtime.returncode == (0 if candidate else 1), runtime.stderr
        verify_graph(project, flavor, args.version)
        print(f"{args.mode} {flavor}: verified")


if __name__ == "__main__":
    main()
