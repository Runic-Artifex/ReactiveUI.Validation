#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Strict packaged observable/delegate consumers; deliberately not a whole-library AOT claim."""

import argparse
import hashlib
import importlib.util
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
CASES = {"generic-field", "nullable-editor-rich-target", "blocking-advisory-cross-field", "rows-async-uniqueness", "existing-observable-foundation"}


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


PACKAGES = module("verify_packages", ROOT / "eng/verify-packages.py")
DEPENDENCIES = module("fork_dependencies", ROOT / "eng/restore-fork-dependencies.py")
RELEASE = module("validation_release", ROOT / "investigations/NativeAot/restore-validation-feed.py")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_report(output, report):
    (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")


def initialize_report(output, report):
    """Invalidate prior success before any input or toolchain check can fail."""
    report["completed"] = False
    write_report(output, report)


def verify_native_graph(assets, reactive, pins, version, rid=None):
    """Validate every RID target as well as the neutral target using the core gate."""
    expected = {"net10.0"} | ({f"net10.0/{rid}"} if rid else set())
    if set(assets["targets"]) != expected:
        raise ValueError(f"Expected exact consumer targets {sorted(expected)}")
    if any(library.get("type") == "project" for library in assets["libraries"].values()):
        raise ValueError("Package consumer must not contain project references")
    for target in expected:
        normalized = dict(assets, targets={"net10.0": assets["targets"][target]})
        PACKAGES.verify_graph(normalized, reactive, pins, version)


def verify_expected_failure(returncode, output):
    """An immutable-release contract failure requires diagnostic and API provenance."""
    if returncode == 0:
        raise ValueError("Released legacy consumer unexpectedly passed strict analysis")
    expected = {(code, method) for code in ("IL2026", "IL3050")
                for method in ("ValidationRule", "BindValidationState")}
    found = set()
    errors = [line for line in output.splitlines() if re.search(r"\berror (?:[A-Z]+\d+|:)", line)]
    for line in errors:
        match = re.search(r"(?:Program|ApplicationModels)\.cs\(\d+,\d+\): error (IL2026|IL3050): Using member '(ReactiveUI\.Validation[^']+)'", line)
        if not match:
            raise ValueError(f"Unexpected build failure in baseline: {line}")
        code, member = match.groups()
        allowed = (".ValidationRule", ".BindValidationState", ".ValidationContext.ValidationContext(",
                   ".ValidationHelper.ValidationHelper(", ".ReactiveValidationObject.ReactiveValidationObject(")
        if not any(method in member for method in allowed):
            raise ValueError(f"Unexpected annotated API in baseline: {member}")
        for method in ("ValidationRule", "BindValidationState"):
            if "Program.cs(" in line and f".{method}" in member:
                found.add((code, method))
    if found != expected:
        raise ValueError(f"Expected release IL2026 and IL3050 from both Validation legacy calls; found {sorted(found)}")


def verify_runtime(output):
    records = [json.loads(line) for line in output.splitlines() if line.startswith('{')]
    cases = [record for record in records if "case" in record]
    if len(cases) != len(CASES) or {record["case"] for record in cases} != CASES:
        raise ValueError("Consumer did not report exactly the required application cases")
    if any(record.get("passed") is not True for record in cases):
        raise ValueError("Application consumer reported a failed case")


def guard_consumer_sources(folder):
    """Reject warning hiding and preservation escape hatches in standalone fixtures."""
    banned = {"ProjectReference", "NoWarn", "WarningsNotAsErrors", "TrimmerRootAssembly",
              "TrimmerRootDescriptor", "RdXmlFile", "WarningsAsErrors"}
    for path in folder.rglob("*"):
        if set(path.relative_to(folder).parts) & {"bin", "obj", "artifacts", "packages"}:
            continue
        if path.suffix in {".csproj", ".props", ".targets"}:
            for node in ET.parse(path).iter():
                if node.tag in banned:
                    raise ValueError(f"Forbidden native consumer setting {node.tag}: {path}")
                if node.tag == "IlcGenerateCompleteTypeMetadata" and node.text.strip().lower() != "false":
                    raise ValueError(f"Complete native metadata forbidden: {path}")
                enforced = {"RunAnalyzers", "RunAnalyzersDuringBuild", "EnableTrimAnalyzer", "EnableAotAnalyzer",
                            "TreatWarningsAsErrors", "ILLinkTreatWarningsAsErrors", "IlcTreatWarningsAsErrors"}
                if node.tag in enforced and (node.text or "").strip().lower() != "true":
                    raise ValueError(f"Analysis or strict warning policy disabled: {path}: {node.tag}")
                if node.tag in {"SuppressTrimAnalysisWarnings", "SuppressAotAnalysisWarnings"} and (node.text or "").strip().lower() != "false":
                    raise ValueError(f"Native/trim diagnostic suppression forbidden: {path}: {node.tag}")
                if node.tag == "Import":
                    raise ValueError(f"Consumer must not import external build definitions: {path}")
        if path.suffix == ".cs":
            source = path.read_text()
            if re.search(r"#pragma\s+warning\s+disable|SuppressMessage|DynamicDependency|DynamicallyAccessedMembers|RequiresUnreferencedCode|RequiresDynamicCode", source):
                raise ValueError(f"Suppression, preservation or hidden analysis boundary: {path}")
        if path.name == ".editorconfig" and re.search(r"dotnet_diagnostic\.IL\d+\.severity", path.read_text()):
            raise ValueError(f"IL severity override forbidden: {path}")


def run(command, log, cwd, expected=False):
    print(f"Running {' '.join(map(str, command))}", flush=True)
    with log.open("w") as output:
        result = subprocess.run(list(map(str, command)), cwd=cwd, stdout=output, stderr=subprocess.STDOUT)
    content = log.read_text(errors="replace")
    if not expected and result.returncode:
        print(content[-20000:], flush=True)
        raise RuntimeError(f"Command failed ({result.returncode}); full log: {log}")
    return result.returncode, content


def write_config(folder, validation_feed):
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    for key, value in (("validation", validation_feed), ("dynamicdata", ROOT / "artifacts/dependencies"),
                       ("nuget.org", "https://api.nuget.org/v3/index.json")):
        ET.SubElement(sources, "add", key=key, value=str(value))
    mapping = ET.SubElement(config, "packageSourceMapping")
    for key, pattern in (("validation", "Runic.ReactiveUI.Validation*"),
                         ("dynamicdata", "Runic.DynamicData*"), ("nuget.org", "*")):
        ET.SubElement(ET.SubElement(mapping, "packageSource", key=key), "package", pattern=pattern)
    path = folder / "nuget.config"
    ET.ElementTree(config).write(path, encoding="utf-8", xml_declaration=True)
    return path


def packed_versions(feed, pins, source=None):
    selected = []
    for reactive in (False, True):
        package_id = PACKAGES.flavor_ids(reactive)[0]
        paths = [path for path in feed.glob(f"{package_id}.*.nupkg")
                 if not path.name.startswith(f"{package_id}.Reactive.")]
        if len(paths) != 1:
            raise ValueError(f"Expected one current {package_id}, found {len(paths)}")
        with zipfile.ZipFile(paths[0]) as archive:
            metadata = ET.fromstring(archive.read(f"{package_id}.nuspec"))
        version = PACKAGES.verify_metadata(metadata, reactive, pins)
        if source is not None:
            if version == RELEASE.VERSION:
                raise ValueError("Candidate must not reuse the immutable released Validation version")
            repository = metadata.find("n:metadata/n:repository", PACKAGES.NS)
            if repository is None or repository.get("commit") != source:
                raise ValueError(f"Candidate package must record current source SHA {source}: {paths[0]}")
        selected.append((reactive, package_id, version, paths[0]))
    if selected[0][2] != selected[1][2]:
        raise ValueError("Validation flavor versions differ")
    return selected


def legacy_pins(pins):
    """Keep the immutable Validation baseline on its released DynamicData pair."""
    result = dict(pins)
    for package in DEPENDENCIES.LEGACY_DIGESTS:
        result[package.casefold()] = DEPENDENCIES.LEGACY_VERSION
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", choices=("linux-x64", "win-x64"), required=True)
    parser.add_argument("--mode", choices=("all", "managed", "trimmed", "native"), default="all")
    parser.add_argument("--package-feed", type=Path,
                        help="Use same-SHA core CI package artifacts; default freshly packs current source")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/verification/native-gates")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    report = {"source": None, "dirty": None, "sdk": None, "rid": args.rid, "mode": args.mode,
              "baselineSource": "c9fa501c4d2e9442d85693dae77bb6f727e9eb7a",
              "cohorts": {
                  "candidate": {"dynamicDataVersion": DEPENDENCIES.CURRENT_VERSION, "dynamicDataSha256": DEPENDENCIES.DIGESTS},
                  "baseline": {"dynamicDataVersion": DEPENDENCIES.LEGACY_VERSION, "dynamicDataSha256": DEPENDENCIES.LEGACY_DIGESTS},
              }, "packages": {}, "checks": []}
    initialize_report(output, report)
    expected_host = "Windows" if args.rid == "win-x64" else "Linux"
    if platform.system() != expected_host or platform.machine().lower() not in {"x86_64", "amd64"}:
        raise ValueError(f"Actual {args.rid} execution requires a matching x64 host")
    sdk = subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip()
    if sdk != json.loads((ROOT / "global.json").read_text())["sdk"]["version"]:
        raise ValueError("Pinned SDK is required")
    print(f"Available space: {shutil.disk_usage(output).free // (1024 ** 3)} GiB", flush=True)
    fixture = ROOT / "examples/NativeValidation"
    guard_consumer_sources(fixture)
    pins = PACKAGES.dependency_pins(ROOT)
    for package_id, expected_hash in DEPENDENCIES.DIGESTS.items():
        package = ROOT / "artifacts/dependencies" / f"{package_id}.{pins[package_id.casefold()]}.nupkg"
        if digest(package) != expected_hash:
            raise ValueError(f"DynamicData release bytes differ from bootstrap SHA-256: {package}")
    baseline_pins = legacy_pins(pins)
    DEPENDENCIES.restore_cohort(DEPENDENCIES.LEGACY_VERSION, DEPENDENCIES.LEGACY_DIGESTS)
    report.update({"source": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
                   "dirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip()),
                   "sdk": sdk})
    write_report(output, report)
    # Each invocation packs current source into its own feed. Consumers have no
    # production ProjectReference and restore Validation in a fresh local cache.
    with tempfile.TemporaryDirectory(prefix="work-", dir=output) as temporary:
        work = Path(temporary)
        if report["dirty"]:
            raise ValueError("Strict candidate verification requires a clean current source checkout")
        if args.package_feed:
            feed = args.package_feed.resolve()
        else:
            feed = work / "candidate-feed"
            run(["dotnet", "pack", "ReactiveUI.Validation.slnx", "-c", "Release", "-m:2", "-warnaserror", "-o", feed],
                output / "candidate-pack.log", ROOT / "src")
            retained = output / "candidate-packages" / report["source"]
            retained.mkdir(parents=True, exist_ok=True)
            for package in feed.glob("*.nupkg"):
                shutil.copyfile(package, retained / package.name)
            feed = retained
        release_feed = ROOT / "artifacts/verification/released-validation"
        run([sys.executable, ROOT / "investigations/NativeAot/restore-validation-feed.py", release_feed],
            output / "baseline-feed.log", ROOT)
        inputs = (("candidate", feed, packed_versions(feed, pins, report["source"]), pins, DEPENDENCIES.DIGESTS),
                  ("baseline", release_feed, packed_versions(release_feed, baseline_pins), baseline_pins, DEPENDENCIES.LEGACY_DIGESTS))
        for kind, validation_feed, packages, cohort_pins, cohort_digests in inputs:
            if kind == "baseline":
                for _, _, version, path in packages:
                    if version != RELEASE.VERSION:
                        raise ValueError("Wrong immutable release version")
                    RELEASE.verify(path, RELEASE.HASHES[path.name])
            for reactive, package_id, version, package_path in packages:
                flavor = "Reactive" if reactive else "Primitives"
                folder = work / f"{kind}-{flavor}"
                shutil.copytree(fixture, folder, ignore=shutil.ignore_patterns("bin", "obj", "artifacts", ".editorconfig"))
                config = write_config(folder, validation_feed)
                project = folder / ("Safe" if kind == "candidate" else "ExpectedFailure") / flavor / "Examples.csproj"
                cache = output / "packages"
                # Retain this task's dependency cache across invocations. Remove only
                # a stale candidate version owned by this gate, never shared caches.
                cached = cache / package_id.lower() / version
                cached_package = cached / f"{package_id.lower()}.{version}.nupkg"
                if cached.exists() and (not cached_package.exists() or digest(cached_package) != digest(package_path)):
                    shutil.rmtree(cached)
                flags = ["-c", "Release", "-m:2", "-warnaserror", "--configfile", config,
                         f"-p:ValidationVersion={version}", f"-p:RestorePackagesPath={cache}",
                         "-p:EnableTrimAnalyzer=true", "-p:EnableAotAnalyzer=true", "-p:TreatWarningsAsErrors=true",
                         "-p:TrimmerSingleWarn=false", "-p:ILLinkTreatWarningsAsErrors=true",
                         "-p:SuppressTrimAnalysisWarnings=false", "-p:SuppressAotAnalysisWarnings=false"]
                package_hash = digest(package_path)
                report["packages"][f"{kind}-{flavor}"] = {"id": package_id, "version": version, "sha256": package_hash}
                stages = ["baseline"] if kind == "baseline" else (["managed", "trimmed", "native"] if args.mode == "all" else [args.mode])
                for stage in stages:
                    stem = f"{kind}-{flavor}-{stage}"
                    publish = folder / f"publish-{stage}"
                    command = ["dotnet", "build" if stage in {"baseline", "managed"} else "publish", project, *flags]
                    rid = None
                    if stage in {"trimmed", "native"}:
                        rid = args.rid
                        command += ["-r", rid, "--self-contained", "true", "-o", publish,
                                    "-p:PublishTrimmed=true", "-p:TrimMode=full",
                                    f"-p:PublishAot={'true' if stage == 'native' else 'false'}"]
                        if stage == "native":
                            command += ["-p:IlcSingleThreaded=true"]
                    code, content = run(command, output / f"{stem}.build.log", folder, expected=kind == "baseline")
                    assets = json.loads((project.parent / "obj/project.assets.json").read_text())
                    verify_native_graph(assets, reactive, cohort_pins, version, rid)
                    PACKAGES.verify_restored_bytes(assets, package_id, version, package_hash)
                    dynamic_data = PACKAGES.flavor_ids(reactive)[1]
                    PACKAGES.verify_restored_bytes(assets, dynamic_data, cohort_pins[dynamic_data.casefold()], cohort_digests[dynamic_data])
                    (output / f"{stem}.graph.json").write_text(json.dumps(assets, indent=2))
                    if kind == "baseline":
                        verify_expected_failure(code, content)
                    else:
                        if re.search(r"\bwarning [A-Z]+\d+\b", content):
                            raise ValueError(f"Successful positive build still emitted a warning: {output / f'{stem}.build.log'}")
                        assembly = f"NativeValidation.{flavor}"
                        if stage == "managed":
                            runtime = ["dotnet", project.parent / f"bin/Release/net10.0/{assembly}.dll"]
                        else:
                            runtime = [publish / (assembly + (".exe" if args.rid == "win-x64" else ""))]
                            if stage == "native":
                                runtime += ["--require-aot"]
                        _, runtime_output = run(runtime, output / f"{stem}.runtime.log", folder)
                        verify_runtime(runtime_output)
                        print(runtime_output, flush=True)
                    report["checks"].append({"name": stem, "passed": True})
                    write_report(output, report)
    report["completed"] = True
    write_report(output, report)
    print(f"Strict package consumer gate passed: {output / 'results.json'}", flush=True)


if __name__ == "__main__":
    main()
