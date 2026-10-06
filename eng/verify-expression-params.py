#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Separate current analyzer guidance from preserved original Expression caller IL."""

import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

OLD_SOURCE = "8c872ef7224d27ddb7708fab4f691f4be44bc0e2"
OLD_VERSION = "8.1.0-runic.0.790.17.15.206"
OLD_PACKAGES = {
    "Primitives": "2d541292f8bc24cd3e59be3451166de8e32dee5d45349a91234588e0d9587fd6",
    "Reactive": "1d2a91e292fdbe293f1131464e7bfcc7049a8c01de363a24368fa27561928557",
}
FATAL_OUTPUT = re.compile(r"Unhandled exception|Process terminated|Fatal error|Segmentation fault|Stack overflow|"
                          r"You must install or update .NET|could not load (?:file|type)", re.IGNORECASE)


def verify_construction_warning(code, output):
    if code != 1:
        raise ValueError("Current construction control requires a normal dotnet build error exit")
    if FATAL_OUTPUT.search(output) or re.search(r"\bwarning [A-Z]+\d+\b", output):
        raise ValueError("Current construction warning cannot conceal a fatal tool failure or unrelated warning")
    errors = [line for line in output.splitlines() if re.search(r"\berror (?:[A-Z]+\d+|:)", line)]
    locations = []
    for line in errors:
        match = re.search(r"(?:^|[\\/])ExpressionParams\.cs\((\d+),\d+\): error RUVG011:", line)
        if (match is None or "retained Expression selector constructs an array" not in line
                or "normal Func overload" not in line or "interceptor cannot remove original argument construction" not in line):
            raise ValueError("Current construction control failed outside its precise actionable RUVG011 warning")
        locations.append(match[1])
    if len(set(locations)) != 1 or re.search(r"\b(?:warning|error) IL\d+\b", output):
        raise ValueError("Current analyzer guidance must identify one source location without substituting linker diagnostics")


def verify_native_construction_failure(code, output):
    if code != 1:
        raise ValueError("Preserved construction control requires a normal dotnet publish error exit")
    if FATAL_OUTPUT.search(output):
        raise ValueError("Expected original Expression IL3050 cannot conceal a fatal compiler/runtime failure")
    diagnostic_lines = [line for line in output.splitlines()
                        if re.search(r"\b(?:warning|error) (?:[A-Z]+\d+|:)", line)]
    primary = []
    for line in diagnostic_lines:
        if re.search(r"\berror MSB3077\b", line):
            continue
        if (not re.search(r"\bAOT analysis error IL3050:", line)
                or "RuleExpressionParamsNegative.Program.Run():" not in line
                or "Using member 'System.Linq.Expressions.Expression.NewArrayInit(Type,Expression[])'" not in line
                or "RequiresDynamicCodeAttribute" not in line):
            raise ValueError("Preserved caller failed outside the exact reachable original NewArrayInit IL3050")
        primary.append(line)
    if not primary:
        raise ValueError("Native control omitted the actual preserved Expression factory diagnostic")


def records(project, final=False):
    name = "generated-compiler-input.txt" if final else "generated-compiler-input.before.txt"
    path = project.parent / "obj" / name
    result = [line.split("|", 1) for line in path.read_text(encoding="utf-8-sig").splitlines() if line]
    if any(len(item) != 2 for item in result):
        raise ValueError("Construction control omitted its actual compiler inputs")
    return result


def aot_evidence(project, gate, final=False):
    captured = records(project, final)
    properties = {}
    for key in ("publishAot", "isAotCompatible", "enableAotAnalyzer"):
        values = [value for kind, value in captured if kind == key]
        if len(values) != 1:
            raise ValueError("Construction control must capture every actual AOT property")
        properties[key] = values[0]
    configs = [{"path": value, "sha256": gate.NATIVE.digest(Path(value))}
               for value in sorted({value for kind, value in captured if kind == "analyzerConfig"})]
    evidence = {"aotProperties": properties, "analyzerConfigs": configs}
    gate.verify_aot_configuration(captured, evidence)
    return evidence


def write_project(path, assembly, package_id, version, reactive, peer=None):
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    ET.SubElement(properties, "AssemblyName").text = assembly
    if peer is None:
        ET.SubElement(properties, "OutputType").text = "Library"
    if reactive:
        ET.SubElement(properties, "DefineConstants").text = "$(DefineConstants);REACTIVE_SHIM"
    items = ET.SubElement(project, "ItemGroup")
    for name, value in ((package_id, version), ("ReactiveUI" + (".Reactive" if reactive else ""), "26.0.1"),
                        ("Runic.DynamicData" + (".Reactive" if reactive else ""), "10.0.0-runic.30")):
        ET.SubElement(items, "PackageReference", Include=name, Version=value)
    if peer is not None:
        ET.SubElement(items, "Reference", Include=peer.stem, HintPath=str(peer))
    ET.ElementTree(project).write(path, encoding="utf-8", xml_declaration=True)


def run_cases(*, root, work, output, source, reactive, package_id, version, package_hash, cache, config, rid, native, inspector, gate):
    flavor = "Reactive" if reactive else "Primitives"
    frozen = root / "eng/verification-fixtures/ExpressionParams/frozen"
    manifest = json.loads((frozen / "manifest.json").read_text())
    if (manifest["oldPackageSource"] != OLD_SOURCE or manifest["oldPackageVersion"] != OLD_VERSION
            or manifest["sdk"] != "10.0.401" or manifest["sourceSha256"] != gate.NATIVE.digest(frozen / manifest["sourceFile"])):
        raise ValueError("Preserved Expression control must identify the immutable original compilation cohort")
    selected = [item for item in manifest["peers"] if item["flavor"] == flavor]
    if len(selected) != 1 or selected[0]["package"]["sha256"] != OLD_PACKAGES[flavor]:
        raise ValueError("Preserved Expression control has the wrong original package bytes")
    peer = selected[0]
    frozen_pe = frozen / peer["assembly"]
    if gate.NATIVE.digest(frozen_pe) != peer["sha256"]:
        raise ValueError("Preserved original Expression PE was changed")
    retained_peer = output / peer["assembly"]
    shutil.copyfile(frozen_pe, retained_peer)
    if "pdb" in peer:
        if gate.NATIVE.digest(frozen / peer["pdb"]) != peer["pdbSha256"]:
            raise ValueError("Preserved Expression debug provenance was changed")
        shutil.copyfile(frozen / peer["pdb"], output / peer["pdb"])
    shutil.copyfile(frozen / "manifest.json", output / "expression-peer.manifest.json")
    il_report = output / f"{flavor}-expression-peer.expression-il.json"
    gate.NATIVE.run(["dotnet", inspector, "expression-il", retained_peer, il_report], output / f"{flavor}-expression-peer.expression-il.log", root)
    gate.verify_expression_il(json.loads(il_report.read_text()), retained_peer, negative=True)
    common = ["-c", "Release", "-m:2", "-warnaserror", "--configfile", config, f"-p:RestorePackagesPath={cache}",
              "-p:EnableTrimAnalyzer=true", "-p:EnableAotAnalyzer=true", "-p:TreatWarningsAsErrors=true",
              "-p:RunAnalyzers=true", "-p:RunAnalyzersDuringBuild=true", "-p:SuppressAotAnalysisWarnings=false",
              "-p:SuppressTrimAnalysisWarnings=false", "-p:ILLinkTreatWarningsAsErrors=true", "-p:IlcTreatWarningsAsErrors=true"]
    results = []
    journal = output / f"{flavor}-expression-controls.json"
    def save_journal():
        journal.write_text(json.dumps({"completed": False, "source": source, "packageSha256": package_hash,
                                       "checks": results}, indent=2) + "\n")
    save_journal()
    warning_folder = work / f"{flavor}-expression-warning"
    warning_folder.mkdir()
    for name in ("Directory.Build.props", "Directory.Build.targets"):
        shutil.copyfile(root / "examples/GeneratedValidation" / name, warning_folder / name)
    # The preserved source is also the current analyzer control. No shape rewriting or suppression.
    shutil.copyfile(frozen / manifest["sourceFile"], warning_folder / "ExpressionParams.cs")
    child = warning_folder / "Control"
    child.mkdir()
    project = child / "Control.csproj"
    write_project(project, f"GeneratedValidation.ExpressionWarning.{flavor}", package_id, version, reactive)
    code, text = gate.NATIVE.run(["dotnet", "build", project, *common], output / f"{flavor}-expression-warning.build.log", warning_folder, expected=True)
    inputs = gate.retain_failed_compile(project, output, f"{flavor}-expression-warning")
    verify_construction_warning(code, text)
    assets = json.loads((child / "obj/project.assets.json").read_text())
    gate.PACKAGES.verify_restored_bytes(assets, package_id, version, package_hash)
    results.append({"name": f"{flavor}-expression-warning", "passed": True, "diagnostic": "RUVG011", "strictCompilerSeverity": "error",
                    "source": source, "packageSha256": package_hash, "retainedInputs": inputs,
                    "configuration": aot_evidence(project, gate)})
    save_journal()
    if not native:
        return results
    host_folder = work / f"{flavor}-expression-native"
    host_folder.mkdir()
    for name in ("Directory.Build.props", "Directory.Build.targets"):
        shutil.copyfile(root / "examples/GeneratedValidation" / name, host_folder / name)
    (host_folder / "Program.cs").write_text("internal static class Program { private static void Main() => RuleExpressionParamsNegative.Program.Run(); }\n")
    child = host_folder / "Host"
    child.mkdir()
    project = child / "Host.csproj"
    assembly = f"GeneratedValidation.ExpressionParams.Host.{flavor}"
    write_project(project, assembly, package_id, version, reactive, retained_peer)
    flags = [*common, "-r", rid, "--self-contained", "true", "-p:GeneratedVerificationStage=NATIVE",
             *gate.NATIVE.strict_positive_publish_flags("native")]
    gate.NATIVE.run(["dotnet", "build", project, *flags], output / f"{flavor}-expression-native.prepare.log", host_folder)
    prepared = gate.retain_failed_compile(project, output, f"{flavor}-expression-native", capture="before-semantic")
    assets = json.loads((child / "obj/project.assets.json").read_text())
    gate.NATIVE.verify_native_graph(assets, reactive, gate.PACKAGES.dependency_pins(root), version, rid)
    gate.verify_no_roslyn_runtime(assets)
    gate.PACKAGES.verify_restored_bytes(assets, package_id, version, package_hash)
    binary = child / f"bin/Release/net10.0/{rid}/{assembly}.dll"
    host_il = output / f"{flavor}-expression-native.host-il.json"
    gate.NATIVE.run(["dotnet", inspector, "expression-il", binary, host_il], output / f"{flavor}-expression-native.host-il.log", host_folder)
    entry = gate.verify_expression_il(json.loads(host_il.read_text()), binary, host=True)
    _, runtime = gate.NATIVE.run(["dotnet", binary], output / f"{flavor}-expression-native.managed.log", host_folder)
    if runtime.strip() != "True":
        raise ValueError("Unchanged retained Expression caller did not execute against the actual current package")
    config_evidence = aot_evidence(project, gate, final=True)
    captured = records(project, final=True)
    value = lambda key: next(item for kind, item in captured if kind == key)
    origin = gate.verify_sdk_warning_origin(project, value("ilLinkTarget"))
    gate.verify_warning_policy(sorted({item for kind, item in captured if kind == "nowarn"}), value("publishTrimmed"),
                               value("redundantSuppressions"), [item for kind, item in captured if kind == "define"], origin)
    shutil.copyfile(binary, output / f"{flavor}-expression-native-before-semantic-inputs" / binary.name)
    code, text = gate.NATIVE.run(["dotnet", "publish", project, *flags, "-o", host_folder / "publish"],
                                output / f"{flavor}-expression-native.publish.log", host_folder, expected=True)
    failed = gate.retain_failed_compile(project, output, f"{flavor}-expression-native")
    verify_native_construction_failure(code, text)
    if gate.NATIVE.digest(retained_peer) != peer["sha256"]:
        raise ValueError("Native negative changed the preserved original Expression PE")
    results.append({"name": f"{flavor}-expression-native", "passed": True, "diagnostic": "IL3050", "source": source,
                    "packageSha256": package_hash, "oldPeer": peer, "preparedInputs": prepared, "failedPublishInputs": failed,
                    "directEntry": entry, "configuration": config_evidence, "sdkWarningPolicy": origin})
    save_journal()
    return results
