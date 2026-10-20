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
BASELINE_VALIDATION_HASHES = {
    f"Runic.ReactiveUI.Validation.{BASELINE}.nupkg": "5d3c5261324181b7d8b995cc7b242977adba21a0ad81c361a4d4bd8a163d14df",
    f"Runic.ReactiveUI.Validation.Reactive.{BASELINE}.nupkg": "e3ba50fa1ad991912bd2d243a923c25055e9d434fb6d7c2eb840cf1ebbd5cb4b",
}
# The baseline's Runic.DynamicData .5 pair exists only as GitHub release assets in a
# local feed; the candidate's RunicArtifex.DynamicData pair restores from NuGet.org
# and its restored (repository-signed) archives are checked after the build.
BASELINE_DYNAMICDATA = ("Runic.DynamicData", "10.0.0-runic.5")
CURRENT_DYNAMICDATA = ("RunicArtifex.DynamicData", "10.0.0-runic.118")
BASELINE_DYNAMICDATA_HASHES = {
    f"Runic.DynamicData.{BASELINE_DYNAMICDATA[1]}.nupkg": "ebd6e4329af148f8a1b3caa00e1391726e2379872b027a693cce787718055366",
    f"Runic.DynamicData.Reactive.{BASELINE_DYNAMICDATA[1]}.nupkg": "49f3e11fdd62b357b376ee26b45deca94cbfc1a52a918bc228aff09c92fc5268",
}
CURRENT_DYNAMICDATA_HASHES = {
    "RunicArtifex.DynamicData": "8cbb24bad17666d45ea429465314b535e0b459b1910cb4f5fa2a23bd531014ac",
    "RunicArtifex.DynamicData.Reactive": "2fc449f7f33888f19d377f08ad99880cd89714b7305719a1f15758f2b906e9e3",
}
DYNAMICDATA_FAMILIES = ("Runic.DynamicData", "RunicArtifex.DynamicData")


def run(command, log):
    result = subprocess.run(command, cwd=REPO, text=True, capture_output=True)
    if log.suffix == ".jsonl":
        log.write_text(result.stdout)
        log.with_suffix(".stderr.log").write_text(result.stderr)
    else:
        log.write_text(result.stdout + result.stderr)
    return result


def verify_graph(project, flavor, version, dynamicdata):
    assets = json.loads((project.parent / "obj/project.assets.json").read_text())
    libraries = assets["libraries"]
    suffix = "" if flavor == "Primitives" else ".Reactive"
    family, dynamicdata_version = dynamicdata
    expected = {f"Runic.ReactiveUI.Validation{suffix}/{version}", f"{family}{suffix}/{dynamicdata_version}"}
    assert expected <= libraries.keys(), (expected, libraries.keys())
    names = {key.rsplit("/", 1)[0].casefold() for key in libraries}
    opposite = ".Reactive" if not suffix else ""
    forbidden = {"dynamicdata", "dynamicdata.reactive", f"runic.reactiveui.validation{opposite}".casefold()}
    for other in DYNAMICDATA_FAMILIES:
        forbidden.add(f"{other}{opposite}".casefold())
        if other != family:
            forbidden |= {other.casefold(), f"{other}.reactive".casefold()}
    assert not names & forbidden, sorted(names & forbidden)
    if flavor == "Primitives":
        assert "system.reactive" not in names
    return assets


def verify_current_dynamicdata(assets, flavor):
    """Bind the NuGet.org DynamicData archive the candidate restored to its pinned SHA-256."""
    package_id = CURRENT_DYNAMICDATA[0] + ("" if flavor == "Primitives" else ".Reactive")
    library = assets["libraries"][f"{package_id}/{CURRENT_DYNAMICDATA[1]}"]
    name = f"{package_id.lower()}.{CURRENT_DYNAMICDATA[1]}.nupkg"
    present = [Path(folder) / library["path"] / name for folder in assets["packageFolders"]
               if (Path(folder) / library["path"] / name).is_file()]
    assert len(present) == 1, present
    assert hashlib.sha256(present[0].read_bytes()).hexdigest() == CURRENT_DYNAMICDATA_HASHES[package_id], present[0]
    metadata = json.loads((present[0].parent / ".nupkg.metadata").read_text())
    assert metadata["contentHash"] == library["sha512"], present[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["baseline-managed", "baseline-strict", "candidate-managed", "candidate-strict"])
    parser.add_argument("validation_feed", type=Path)
    parser.add_argument("dynamicdata_feed", type=Path, nargs="?",
                        help="Local feed with the legacy Runic.DynamicData .5 pair (baseline modes only)")
    parser.add_argument("--version", default=BASELINE)
    args = parser.parse_args()
    candidate = args.mode.startswith("candidate")
    expectations = json.loads((ROOT / "manifest.json").read_text())["candidate" if candidate else "baseline"]
    dynamicdata = CURRENT_DYNAMICDATA if candidate else BASELINE_DYNAMICDATA
    assert expectations["dynamicDataVersion"] == dynamicdata[1]
    if not candidate and args.version != BASELINE:
        parser.error("baseline uses immutable released .790.17")
    if not candidate and args.dynamicdata_feed is None:
        parser.error("baseline requires the local feed holding the legacy Runic.DynamicData pair")
    if candidate and args.dynamicdata_feed is not None:
        parser.error("candidate restores RunicArtifex.DynamicData from NuGet.org; omit the DynamicData feed")
    if candidate and args.version == BASELINE:
        parser.error("candidate requires a fresh version and candidate feed")
    assert subprocess.check_output(["dotnet", "--version"], text=True).strip() == "10.0.401"
    args.validation_feed = args.validation_feed.resolve()
    source_list = [("validation", str(args.validation_feed), "Runic.ReactiveUI.Validation*")]
    if not candidate:
        args.dynamicdata_feed = args.dynamicdata_feed.resolve()
        for name, digest in BASELINE_VALIDATION_HASHES.items():
            assert hashlib.sha256((args.validation_feed / name).read_bytes()).hexdigest() == digest, name
        for name, digest in BASELINE_DYNAMICDATA_HASHES.items():
            assert hashlib.sha256((args.dynamicdata_feed / name).read_bytes()).hexdigest() == digest, name
        source_list.append(("dynamicdata", str(args.dynamicdata_feed), "Runic.DynamicData*"))
    source_list.append(("nuget.org", "https://api.nuget.org/v3/index.json", "*"))
    output = ROOT / "artifacts" / args.mode
    output.mkdir(parents=True, exist_ok=True)
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    for key, value, _ in source_list:
        ET.SubElement(sources, "add", key=key, value=value)
    mapping = ET.SubElement(config, "packageSourceMapping")
    for key, _, pattern in source_list:
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
        assets = verify_graph(project, flavor, args.version, dynamicdata)
        if candidate:
            verify_current_dynamicdata(assets, flavor)
        print(f"{args.mode} {flavor}: verified")


if __name__ == "__main__":
    main()
