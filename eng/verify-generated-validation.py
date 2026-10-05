#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Strict actual-package acceptance of generated familiar Validation call syntax."""

import argparse
import json
import platform
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
# Share strict package provenance/graph checks, never the historical expectations.
import importlib.util
_spec = importlib.util.spec_from_file_location("native_validation_gate", ROOT / "eng/verify-native-validation.py")
NATIVE = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(NATIVE)
PACKAGES = NATIVE.PACKAGES
DEPENDENCIES = NATIVE.DEPENDENCIES
CASES = {"initial-field-and-text", "nested-null-and-replacement", "helper-model-rich-state",
         "context-replacement", "nested-target-handoff", "property-state-membership", "rows-owned-observable-results"}
NEGATIVES = {"NEGATIVE_STORED_SELECTOR": "RUVG001", "NEGATIVE_COMPUTED_SELECTOR": "RUVG001",
             "NEGATIVE_INDEXER": "RUVG001", "NEGATIVE_NESTED_TARGET": "RUVG006",
             "NEGATIVE_NONNOTIFY_RULE": "RUVG001", "NEGATIVE_STRUCT_OWNER": "RUVG001"}


def verify_runtime(output):
    records = [json.loads(line) for line in output.splitlines() if line.startswith('{')]
    cases = [record for record in records if "case" in record]
    if len(cases) != len(CASES) or {record["case"] for record in cases} != CASES:
        raise ValueError("Generated consumer must report exactly every required application case once")
    if any(record.get("passed") is not True for record in cases):
        raise ValueError("Generated application consumer reported a failed case")


def verify_negative(code, output, diagnostic):
    if code == 0:
        raise ValueError("Unsupported generated selector unexpectedly compiled")
    errors = [line for line in output.splitlines() if re.search(r"\berror (?:[A-Z]+\d+|:)", line)]
    parsed = []
    for line in errors:
        match = re.search(r"(?:^|[\\/])Negative\.cs\((\d+),\d+\): error ([A-Z]+\d+):", line)
        if match is None or match[2] not in {diagnostic, "RUVG005"}:
            raise ValueError(f"Negative consumer failed for an unexpected reason; expected {diagnostic}")
        parsed.append((match[1], match[2], line))
    primary_lines = {number for number, code, _ in parsed if code == diagnostic}
    if len(primary_lines) != 1 or any(number not in primary_lines for number, _, _ in parsed):
        raise ValueError("Expected a primary selector diagnostic; secondary dispatch errors must identify the same Negative.cs invocation line")
    if any("observable" not in line.lower() or "Unsafe" not in line for _, _, line in parsed):
        raise ValueError("Unsupported call diagnostic must offer explicit observable and Unsafe alternatives")



def verify_generator_assets(package):
    """The shipping pair itself must carry the analyzer and namespace opt-in."""
    with zipfile.ZipFile(package) as archive:
        for name in archive.namelist():
            if name.startswith(("lib/", "runtimes/")) and ("Microsoft.CodeAnalysis" in name or "ReactiveUI.Validation.SourceGenerators" in name):
                raise ValueError("Roslyn and generator assemblies must never be runtime assets")
            if name.endswith(".nuspec"):
                metadata = ET.fromstring(archive.read(name))
                if any(node.tag.rsplit("}", 1)[-1] == "dependency" and node.get("id", "").lower().startswith("microsoft.codeanalysis") for node in metadata.iter()):
                    raise ValueError("Shipping package must not depend on Roslyn runtime packages")
        analyzers = [name for name in archive.namelist() if name.startswith("analyzers/") and name.endswith(".dll")]
        expected = [name for name in analyzers if name.endswith("/ReactiveUI.Validation.SourceGenerators.dll")]
        if len(expected) != 1 or not expected[0].startswith("analyzers/dotnet/"):
            raise ValueError("Validation package must bundle exactly one Roslyn generator analyzer")
        if len(archive.read(expected[0])) < 1024:
            raise ValueError("Bundled analyzer is not a built assembly")
        props = [name for name in archive.namelist() if name.startswith("buildTransitive/") and name.endswith(".props")]
        if len(props) != 1:
            raise ValueError("Validation package must carry exactly one transitive interceptor opt-in props")
        nodes = list(ET.fromstring(archive.read(props[0])).iter("InterceptorsNamespaces"))
        if len(nodes) != 1 or "$(InterceptorsNamespaces)" not in (nodes[0].text or "") or "ReactiveUI.Validation.Generated" not in (nodes[0].text or "").split(";"):
            raise ValueError("Package must append its exact generated namespace to the interceptor allowlist")
        return {"analyzer": expected[0], "props": props[0]}


def verify_no_roslyn_runtime(assets):
    for name in assets["libraries"]:
        if name.split("/", 1)[0].lower().startswith("microsoft.codeanalysis"):
            raise ValueError("Roslyn package leaked into external consumer dependency graph")
    for target in assets["targets"].values():
        for library in target.values():
            for group in ("compile", "runtime", "runtimeTargets"):
                for asset in library.get(group, {}):
                    if "Microsoft.CodeAnalysis" in asset or "ReactiveUI.Validation.SourceGenerators" in asset:
                        raise ValueError("Compiler or generator assembly leaked into compile/runtime assets")


def verify_overload_inventory(source):
    inventory = set()
    for header in re.findall(r"internal static [^\n]*? Intercept\d+\(([^\n]*)\)", source):
        names = set(re.findall(r"@(\w+)", header))
        if "isPropertyValid" in names:
            message = header.split("@isPropertyValid", 1)[1]
            inventory.add(("rule", "context" in names, "Func<" in message))
        elif "contextProperty" in names:
            inventory.add(("context", "viewModelProperty" in names, "viewProperty" in names))
        elif "converter" in names:
            inventory.add(("state", "modelProperty" in names, "viewProperty" in names))
        else:
            source_kind = "helper" if "viewModelHelperProperty" in names else "property" if "viewModelProperty" in names else "model"
            inventory.add(("text", source_kind, "formatter" in names))
    expected = {(family, first, second) for family in ("rule", "context", "state") for first in (False, True) for second in (False, True)}
    expected |= {("text", kind, formatter) for kind in ("helper", "property", "model") for formatter in (False, True)}
    if not expected.issubset(inventory):
        raise ValueError(f"External generator consumer did not emit every shipped overload: missing {sorted(expected - inventory)}")
    typed = []
    for family, first, second in sorted(expected):
        if family == "rule":
            typed.append({"api": "ValidationRule", "context": "explicit" if first else "default", "message": "dynamic" if second else "static"})
        elif family == "context":
            typed.append({"api": "BindValidationContext", "selection": "property" if first else "aggregate", "destination": "target" if second else "callback"})
        elif family == "state":
            typed.append({"api": "BindValidationState", "selection": "property" if first else "helper", "destination": "target" if second else "callback"})
        else:
            typed.append({"api": "BindValidation", "selection": first, "formatter": "explicit" if second else "default"})
    return typed


def verify_generated_sources(project, retained=None):
    generated_root = project.parent / "obj/generated"
    generated = list(generated_root.rglob("ValidationInterceptors.g.cs"))
    interceptor_sources = [path for path in generated if "ReactiveUI.Validation.SourceGenerators" in path.relative_to(generated_root).parts and "InterceptsLocation" in path.read_text() and "ReactiveUI.Validation.Generated" in path.read_text()]
    if not interceptor_sources or sum(path.read_text().count("[global::System.Runtime.CompilerServices.InterceptsLocation") for path in interceptor_sources) < 18:
        raise ValueError("External package consumer did not emit all 18 required familiar API interceptors")
    inventory = verify_overload_inventory("\n".join(path.read_text() for path in interceptor_sources))
    for path in interceptor_sources:
        source = path.read_text()
        if re.search(r"\.Compile\s*\(|\.GetProperty\s*\(|\.SetValue\s*\(|Unsafe\s*\(|WhenAnyValue(?:Unsafe)?\s*\(|#pragma\s+warning\s+disable|SuppressMessage|DynamicDependency", source):
            raise ValueError(f"Generated interceptor hides reflection or diagnostic suppression: {path}")
    files = []
    if retained is not None:
        # This fixed per-stage evidence folder belongs only to this gate; replace
        # previous emitted files so an old run cannot masquerade as current output.
        if retained.exists():
            shutil.rmtree(retained)
        for path in interceptor_sources:
            destination = retained / path.relative_to(generated_root)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, destination)
            files.append({"path": f"{retained.name}/{destination.relative_to(retained).as_posix()}", "sha256": NATIVE.digest(destination)})
    return {"sourceCount": len(interceptor_sources), "overloads": inventory, "files": files}


def guard_consumer_sources(folder):
    NATIVE.guard_consumer_sources(folder)
    for path in folder.rglob("*.cs"):
        if set(path.relative_to(folder).parts) & {"bin", "obj", "artifacts", "packages"}:
            continue
        if re.search(r"\b(?:ValidationRule|BindValidation|BindValidationState|BindValidationContext)Unsafe\s*\(", path.read_text()):
            raise ValueError("Generated acceptance consumer must exercise normal call names")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", choices=("linux-x64", "win-x64"), required=True)
    parser.add_argument("--mode", choices=("all", "managed", "trimmed", "native"), default="all")
    parser.add_argument("--package-feed", type=Path, help="Use exact same-SHA CI package artifact; otherwise pack current source")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/verification/generated-gates")
    args = parser.parse_args()
    if platform.system() != ("Windows" if args.rid == "win-x64" else "Linux") or platform.machine().lower() not in {"x86_64", "amd64"}:
        raise ValueError("Execution requires a matching x64 host for the selected RID")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    sdk = subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip()
    if sdk != json.loads((ROOT / "global.json").read_text())["sdk"]["version"]:
        raise ValueError("Pinned SDK is required")
    if subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip():
        raise ValueError("Strict generated acceptance requires a clean current source checkout")
    source = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    print(f"Available space: {shutil.disk_usage(output).free // (1024 ** 3)} GiB", flush=True)
    fixture = ROOT / "examples/GeneratedValidation"
    guard_consumer_sources(fixture)
    pins = PACKAGES.dependency_pins(ROOT)
    for package_id, expected in DEPENDENCIES.DIGESTS.items():
        package = ROOT / "artifacts/dependencies" / f"{package_id}.{pins[package_id.casefold()]}.nupkg"
        if NATIVE.digest(package) != expected:
            raise ValueError("DynamicData feed must match the SHA-verified immutable release")
    report = {"source": source, "dirty": False, "sdk": sdk, "rid": args.rid, "mode": args.mode,
              "dynamicDataSha256": DEPENDENCIES.DIGESTS, "packages": {}, "checks": []}
    with tempfile.TemporaryDirectory(prefix="work-", dir=output) as temporary:
        work = Path(temporary)
        feed = args.package_feed.resolve() if args.package_feed else work / "candidate-feed"
        if not args.package_feed:
            NATIVE.run(["dotnet", "pack", "ReactiveUI.Validation.slnx", "-c", "Release", "-m:2", "-warnaserror", "-o", feed], output / "candidate-pack.log", ROOT / "src")
            retained = output / "candidate-packages" / source
            retained.mkdir(parents=True, exist_ok=True)
            for package in feed.glob("*.nupkg"):
                shutil.copyfile(package, retained / package.name)
            feed = retained
        for reactive, package_id, version, package in NATIVE.packed_versions(feed, pins, source):
            flavor = "Reactive" if reactive else "Primitives"
            folder = work / flavor
            shutil.copytree(fixture, folder, ignore=shutil.ignore_patterns("bin", "obj", "artifacts", ".editorconfig"))
            project = folder / flavor / "Examples.csproj"
            config = NATIVE.write_config(folder, feed)
            cache = output / "packages"
            cached = cache / package_id.lower() / version
            cached_package = cached / f"{package_id.lower()}.{version}.nupkg"
            package_hash = NATIVE.digest(package)
            if cached.exists() and (not cached_package.exists() or NATIVE.digest(cached_package) != package_hash):
                shutil.rmtree(cached)
            report["packages"][flavor] = {"id": package_id, "version": version, "sha256": package_hash, **verify_generator_assets(package)}
            flags = ["-c", "Release", "-m:2", "-warnaserror", "--configfile", config,
                     f"-p:ValidationVersion={version}", f"-p:RestorePackagesPath={cache}",
                     "-p:EnableTrimAnalyzer=true", "-p:EnableAotAnalyzer=true", "-p:TreatWarningsAsErrors=true",
                     "-p:RunAnalyzers=true", "-p:RunAnalyzersDuringBuild=true", "-p:TrimmerSingleWarn=false",
                     "-p:ILLinkTreatWarningsAsErrors=true", "-p:IlcTreatWarningsAsErrors=true",
                     "-p:SuppressTrimAnalysisWarnings=false", "-p:SuppressAotAnalysisWarnings=false"]
            stages = ["managed", "trimmed", "native"] if args.mode == "all" else [args.mode]
            for stage in stages:
                stem = f"{flavor}-{stage}"
                publish = folder / f"publish-{stage}"
                command = ["dotnet", "build" if stage == "managed" else "publish", project, *flags]
                rid = None
                if stage != "managed":
                    rid = args.rid
                    command += ["-r", rid, "--self-contained", "true", "-o", publish,
                                "-p:PublishTrimmed=true", "-p:TrimMode=full", f"-p:PublishAot={'true' if stage == 'native' else 'false'}"]
                    if stage == "native":
                        command += ["-p:IlcSingleThreaded=true"]
                _, content = NATIVE.run(command, output / f"{stem}.build.log", folder)
                if re.search(r"\bwarning [A-Z]+\d+\b", content):
                    raise ValueError("Successful generated consumer emitted a warning")
                assets = json.loads((project.parent / "obj/project.assets.json").read_text())
                NATIVE.verify_native_graph(assets, reactive, pins, version, rid)
                verify_no_roslyn_runtime(assets)
                NATIVE.verify_restored_bytes(assets, package_id, version, package_hash)
                dynamic_data = PACKAGES.flavor_ids(reactive)[1]
                NATIVE.verify_restored_bytes(assets, dynamic_data, pins[dynamic_data.casefold()], DEPENDENCIES.DIGESTS[dynamic_data])
                (output / f"{stem}.graph.json").write_text(json.dumps(assets, indent=2))
                generated_evidence = verify_generated_sources(project, output / f"{stem}-generated")
                generated_evidence.update({"source": source, "packageSha256": package_hash, "flavor": flavor, "stage": stage})
                assembly = f"GeneratedValidation.{flavor}"
                runtime = ["dotnet", project.parent / f"bin/Release/net10.0/{assembly}.dll"] if stage == "managed" else [publish / (assembly + (".exe" if args.rid == "win-x64" else ""))]
                if stage == "native":
                    runtime += ["--require-aot"]
                _, runtime_output = NATIVE.run(runtime, output / f"{stem}.runtime.log", folder)
                verify_runtime(runtime_output)
                print(runtime_output, flush=True)
                report["checks"].append({"name": stem, "passed": True, "generatedSources": generated_evidence["sourceCount"], "generated": generated_evidence})
                (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
            # Fresh folders avoid previous RID/negative outputs influencing compiler evidence.
            for negative, diagnostic in NEGATIVES.items():
                negative_folder = work / f"{flavor}-{negative}"
                shutil.copytree(fixture, negative_folder, ignore=shutil.ignore_patterns("bin", "obj", "artifacts", ".editorconfig"))
                negative_project = negative_folder / flavor / "Examples.csproj"
                stem = f"{flavor}-{negative}"
                code, content = NATIVE.run(["dotnet", "build", negative_project, *flags, f"-p:NegativeCase={negative}"], output / f"{stem}.build.log", negative_folder, expected=True)
                verify_negative(code, content, diagnostic)
                report["checks"].append({"name": stem, "diagnostic": diagnostic, "passed": True})
                (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
    print(f"Generated package acceptance passed: {output / 'results.json'}", flush=True)


if __name__ == "__main__":
    main()
