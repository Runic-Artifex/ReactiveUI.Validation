#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Bounded managed configuration controls against the main gate's exact package pair.

This module never packs or publishes. The caller owns the package feed, shared
cache, matching host and build slot. Missing analyzer assets cannot diagnose
themselves: those controls execute the normal runtime route and require its
actionable missing-capability failure. RunAnalyzers=false keeps source generation
and is a positive control, distinct from removing the analyzer assembly.
"""

from contextlib import contextmanager
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET
import zipfile

TOOLSET_VERSION = "5.0.0"
GENERATED_OK = "CONFIG_GENERATED_OK"
MISSING_PREFIX = "CONFIG_FAILURE|No typed validation capability is registered for RuleValue."
DIAGNOSTIC = re.compile(r"^(?:(.*?):\s*)?(error|warning)\s+([A-Z]+\d+)\s*:\s*(.*?)(?:\s+\[[^\]\r\n]+\])?$")
CASES = ("generated", "excluded-analyzer", "removed-analyzer", "analyzers-disabled",
         "excluded-build", "wrong-allowlist", "csharp13", "missing-runtime", "old-runtime", "older-compiler")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def verify_diagnostics(code, output, required, allowed, provenance):
    """Reject accidental compiler/load failures instead of accepting any nonzero exit."""
    if code == 0:
        raise ValueError("Configuration negative unexpectedly compiled")
    if re.search(r"Unhandled exception|Process terminated|Fatal error|Segmentation fault|Stack overflow|"
                 r"You must install or update .NET|could not load (?:file|type)", output, re.IGNORECASE):
        raise ValueError("Expected configuration diagnostic cannot conceal a fatal runtime/compiler failure")
    diagnostics = []
    for line in output.splitlines():
        if not re.search(r"\b(?:error|warning)(?:\s+[A-Z]+\d+\b|\s*:)", line):
            continue
        match = DIAGNOSTIC.fullmatch(line.strip())
        if match is None or match[2] != "error" or match[3] not in allowed:
            raise ValueError(f"Unrelated configuration diagnostic: {line}")
        if provenance not in line:
            raise ValueError(f"Configuration diagnostic has the wrong provenance: {line}")
        messages = {"RUVG003": ("buildTransitive", "10.0.401", "C# 14"),
                    "RUVG004": ("matching validation runtime", "bundled analyzer"),
                    "RUVG005": ("bundled analyzer", "Unsafe")}
        if any(token not in match[4] for token in messages.get(match[3], ())):
            raise ValueError("Configuration diagnostic omitted its actionable contract")
        diagnostics.append((match[3], match[4]))
    if not required.issubset({item[0] for item in diagnostics}):
        raise ValueError("Configuration negative omitted its required actionable diagnostic")
    if not diagnostics:
        raise ValueError("Configuration negative failed without a compiler diagnostic")
    return sorted(set(diagnostics))


def verify_missing_dispatch(code, output):
    if code != 17 or len([line for line in output.splitlines() if line.startswith(MISSING_PREFIX)]) != 1:
        raise ValueError("Missing analyzer must reach exactly the actionable typed runtime failure")
    if any(word not in output for word in ("generator", "IValidationPlanProvider", "ValidationPlanRegistry", "Unsafe")):
        raise ValueError("Missing-dispatch failure omitted its supported alternatives")


def verify_success(code, output):
    if code != 0 or output.splitlines().count(GENERATED_OK) != 1:
        raise ValueError("Configuration positive did not execute its generated typed rule")


@contextmanager
def environment(**values):
    previous = {key: os.environ.get(key) for key in values}
    os.environ.update(values)
    try:
        yield
    finally:
        for key, value in previous.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value


def fixture_source(reactive, *, private=False, peer=False, static_call=False):
    root = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
    reactive_root = "ReactiveUI.Reactive" if reactive else "ReactiveUI"
    namespace = "ConfigurationPeer" if peer else "ConfigurationConsumer"
    access = f"[{root}.Capabilities.GeneratedValidationAccess]\n    private" if private else "public"
    call = (f"{root}.Extensions.ValidatableViewModelExtensions.ValidationRule<Model, string>(model, "
            if static_call else "model.ValidationRule(")
    header = '[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Configuration.Peer_a")]\n' if peer else ""
    program = "" if peer else f"""
internal static class Program
{{
    private static int Main()
    {{
        try
        {{
            {'ConfigurationPeer.Model.Run();' if private else ''}
            Model.Run();
            Console.WriteLine("{GENERATED_OK}");
            return 0;
        }}
        catch (InvalidOperationException failure)
        {{
            Console.WriteLine("CONFIG_FAILURE|" + failure.Message);
            return 17;
        }}
    }}
}}
"""
    return f"""using System;
using {reactive_root};
using {root}.Abstractions;
using {root}.Contexts;
using {root}.Extensions;
{header}namespace {namespace};
public sealed class Model : ReactiveObject, IValidatableViewModel
{{
    {access} string? Value {{ get; set; }} = "present";
    public IValidationContext ValidationContext {{ get; }} = new ValidationContext();
    public static void Run()
    {{
        var model = new Model();
        using var context = model.ValidationContext;
        string? observed = null;
        using var rule = {call}current => current.Value, value => {{ observed = value; return value == "present"; }}, "missing");
        if (observed != "present") throw new InvalidOperationException("The typed selector did not observe the actual receiver.");
    }}
}}
{program}"""


def write_project(folder, package_id, version, case, *, current_runtime, analyzer,
                  old_runtime=None, peer_reference=None, peer=False):
    folder.mkdir(parents=True, exist_ok=True)
    # Bound inheritance even when the gate output is under an enclosing checkout.
    (folder / "Directory.Build.props").write_text("<Project />\n")
    (folder / "Directory.Build.targets").write_text("<Project />\n")
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    for key, value in {
        "TargetFramework": "net10.0", "OutputType": "Library" if peer else "Exe",
        "AssemblyName": "Configuration.Peer-a" if peer else "Configuration.Peer_a" if case == "collision" else "Configuration.Control",
        "LangVersion": "13.0" if case == "csharp13" else "14.0", "Nullable": "enable",
        "ImplicitUsings": "disable", "ManagePackageVersionsCentrally": "false", "IsPackable": "false",
        "EnableNETAnalyzers": "false", "UseSharedCompilation": "false",
        "EmitCompilerGeneratedFiles": "true", "CompilerGeneratedFilesOutputPath": "$(MSBuildProjectDirectory)/obj/generated",
    }.items():
        ET.SubElement(properties, key).text = value
    references = ET.SubElement(project, "ItemGroup")
    package = ET.SubElement(references, "PackageReference", Include=package_id, Version=version)
    exclusions = {"excluded-analyzer": "analyzers", "excluded-build": "build;buildTransitive", "missing-runtime": "compile"}
    if case in exclusions:
        ET.SubElement(package, "ExcludeAssets").text = exclusions[case]
    if case == "older-compiler":
        ET.SubElement(references, "PackageReference", Include="Microsoft.Net.Compilers.Toolset", Version=TOOLSET_VERSION, PrivateAssets="all")
    if peer_reference is not None:
        reference = ET.SubElement(references, "Reference", Include="Configuration.Peer-a")
        ET.SubElement(reference, "HintPath").text = str(peer_reference)
    if case == "missing-runtime":
        # Compile exclusion also removes transitive references. Package-injected
        # global usings would fail first; this fixture probes the explicit runtime
        # type reference in Program.cs, with no implicit import dependency.
        isolate = ET.SubElement(project, "Target", Name="IsolateMissingRuntimeReference", BeforeTargets="GenerateGlobalUsings")
        ET.SubElement(ET.SubElement(isolate, "ItemGroup"), "Using", Remove="@(Using)")
    target = ET.SubElement(project, "Target", Name="CaptureConfigurationCompilerInputs", BeforeTargets="CoreCompile",
                           DependsOnTargets="FindReferenceAssembliesForReferences")
    items = ET.SubElement(target, "ItemGroup")
    if case in {"removed-analyzer", "older-compiler"}:
        ET.SubElement(items, "Analyzer", Remove="@(Analyzer)" if case == "older-compiler" else str(analyzer))
    if case == "older-compiler":
        # Force a precise load attempt even if NuGet correctly excludes roslyn5.9
        # assets for the older band. This case intentionally tests that load boundary.
        ET.SubElement(items, "Analyzer", Include=str(analyzer))
    if old_runtime is not None:
        ET.SubElement(items, "ReferencePathWithRefAssemblies", Remove=str(current_runtime))
        ET.SubElement(items, "ReferencePathWithRefAssemblies", Include=str(old_runtime))
    ET.SubElement(items, "_ConfigurationDefine", Include="$(DefineConstants)")
    ET.SubElement(items, "_ConfigurationNamespace", Include="$(InterceptorsNamespaces)")
    for value in ("assembly|$(AssemblyName)", "language|$(LangVersion)",
                  "@(_ConfigurationNamespace->'allowlist|%(Identity)')",
                  "@(_ConfigurationDefine->'defines|%(Identity)')",
                  "runAnalyzers|$(RunAnalyzers)", "runAnalyzersDuringBuild|$(RunAnalyzersDuringBuild)",
                  "compilerPath|$(CscToolPath)", "compilerExe|$(CscToolExe)",
                  "compilerTasks|$(RoslynTasksAssembly)", "compilerEnvironment|$(CscEnvironment)",
                  "@(Compile->'source|%(FullPath)')", "@(Analyzer->'analyzer|%(FullPath)')",
                  "@(ReferencePathWithRefAssemblies->'reference|%(FullPath)')"):
        ET.SubElement(items, "_ConfigurationInput", Include=value)
    ET.SubElement(target, "WriteLinesToFile", File="$(MSBuildProjectDirectory)/obj/configuration-inputs.txt",
                  Lines="@(_ConfigurationInput)", Overwrite="true", Encoding="UTF-8")
    ET.indent(project)
    path = folder / "Control.csproj"
    ET.ElementTree(project).write(path, encoding="unicode")
    return path


def read_inputs(folder):
    path = folder / "obj/configuration-inputs.txt"
    if not path.exists():
        raise ValueError("Configuration case never reached actual compiler inputs")
    records = [line.split("|", 1) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    if any(len(record) != 2 for record in records):
        raise ValueError("Malformed configuration compiler input")
    return {key: [value for kind, value in records if kind == key] for key, _ in records}


def retain_inputs(folder, output, stem, inputs):
    retained = output / f"{stem}-inputs"
    retained.mkdir(exist_ok=True)
    files = [folder / "Control.csproj", folder / "Program.cs", folder / "Directory.Build.props",
             folder / "Directory.Build.targets", folder / "obj/configuration-inputs.txt",
             folder / "obj/project.assets.json", folder / "obj/Control.csproj.nuget.g.props",
             folder / "obj/Control.csproj.nuget.g.targets", folder / "compiler.rsp"]
    files += sorted((folder / "bin/Release/net10.0").glob("Configuration.*.dll"))
    files += [Path(path) for path in inputs.get("source", []) if Path(path).name != "Program.cs"]
    evidence = []
    for index, path in enumerate(files):
        if not path.exists():
            continue
        destination = retained / f"{index:03}-{path.name}"
        shutil.copyfile(path, destination)
        evidence.append({"file": str(destination.relative_to(output)), "sha256": digest(destination)})
    generated = []
    for path in sorted((folder / "obj/generated").rglob("*.cs")):
        destination = output / f"{stem}-generated" / path.relative_to(folder / "obj/generated")
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)
        generated.append({"file": str(destination.relative_to(output)), "sha256": digest(destination)})
    return {"files": evidence, "compilerInputs": inputs,
            "referencedBytes": [{"kind": kind, "path": path, "sha256": digest(path)}
                                for kind in ("reference", "analyzer") for path in inputs.get(kind, [])],
            "generatedSources": generated}


def compiler_response(folder, inputs):
    """Replay the captured package compiler inputs; do not invoke old MSBuild tasks."""
    def quoted(value):
        if any(character in str(value) for character in ('"', '\n', '\r')):
            raise ValueError("Unsupported compiler response argument")
        return '"' + str(value) + '"'
    destination = folder / "obj/Configuration.Control.dll"
    arguments = ["/nostdlib+", "/target:exe", "/warnaserror+", "/nullable:enable",
                 "/langversion:" + inputs["language"][0], "/out:" + quoted(destination),
                 "/define:" + ",".join(inputs.get("defines", [])),
                 "/features:" + quoted("InterceptorsNamespaces=" + ";".join(inputs.get("allowlist", [])))]
    arguments += ["/reference:" + quoted(path) for path in inputs.get("reference", [])]
    arguments += ["/analyzer:" + quoted(path) for path in inputs.get("analyzer", [])]
    arguments += [quoted(path) for path in inputs.get("source", [])]
    if not inputs.get("source") or not inputs.get("reference") or not inputs.get("analyzer"):
        raise ValueError("Older compiler control requires actual sources, references and analyzer inputs")
    response = folder / "compiler.rsp"
    response.write_text("\n".join(arguments) + "\n", encoding="utf-8")
    return response


def run_older_compiler(*, folder, project, toolset, csc, cache, config, run, output, stem):
    """Restore with locked MSBuild, then run actual old csc on an exact CLR 10 child."""
    with environment(DOTNET_CLI_UI_LANGUAGE="en-US"):
        _, host_info = run(["dotnet", "--info"], output / f"{stem}.host-info.log", folder)
        selected = re.search(r"^Host:\s*\n\s*Version:\s*(10\.0\.\d+)\s*$", host_info, re.MULTILINE)
        if selected is None:
            raise ValueError("Older compiler control requires the declared locked .NET 10 host")
        framework = selected[1]
        if not re.search(rf"Microsoft.NETCore.App {re.escape(framework)} \[", host_info):
            raise ValueError("Locked host's exact CLR 10 framework is not installed")
        run(["dotnet", "restore", project, "--configfile", config, f"-p:RestorePackagesPath={cache}"],
            output / f"{stem}.restore.log", folder)
        retain_inputs(folder, output, stem, {})
        archive = bind_compiler_archive(toolset)
        run(["dotnet", "msbuild", project, "-t:CaptureConfigurationCompilerInputs", "-p:Configuration=Release",
             "-m:2", "-warnaserror", f"-p:RestorePackagesPath={cache}"],
            output / f"{stem}.inputs.log", folder)
        inputs = read_inputs(folder)
        response = compiler_response(folder, inputs)
        command = [shutil.which("dotnet"), "exec", "--fx-version", framework, "--roll-forward", "Disable", csc]
        trace_path = folder / "obj/csc-host-trace.log"
        with environment(DOTNET_HOST_TRACE="1", DOTNET_HOST_TRACEFILE=str(trace_path)):
            code, text = run([*command, "/noconfig", "@" + str(response)], output / f"{stem}.build.log", folder, expected=True)
        trace = trace_path.read_text()
        coreclr = re.search(r"^CoreCLR path = '([^'\r\n]+)'", trace, re.MULTILINE)
        if coreclr is None or not re.search(rf"Microsoft.NETCore.App[/\\]+{re.escape(framework)}[/\\]+(?:lib)?coreclr", coreclr[1], re.IGNORECASE):
            raise ValueError("Older compiler trace did not prove the exact locked CLR 10 selection")
        retained_trace = output / f"{stem}.host-trace.log"
        shutil.copyfile(trace_path, retained_trace)
        retained = output / f"{stem}-inputs"
        retained.mkdir(exist_ok=True)
        for name in ("csc.runtimeconfig.json", "csc.deps.json"):
            shutil.copyfile(csc.parent / name, retained / name)
        _, compiler_version = run([*command, "-version"], output / f"{stem}.version.log", folder)
    if not compiler_version.strip().startswith("5.0."):
        raise ValueError("Older compiler control did not execute the selected Roslyn 5.0 toolset")
    return code, text, {"package": "Microsoft.Net.Compilers.Toolset", "packageVersion": TOOLSET_VERSION,
                        **archive, "executedVersion": compiler_version.strip(),
                        "runtimeconfig": json.loads(csc.with_suffix(".runtimeconfig.json").read_text()),
                        "runtimeconfigSha256": digest(csc.with_suffix(".runtimeconfig.json")),
                        "hostInfo": {"file": f"{stem}.host-info.log", "sha256": digest(output / f"{stem}.host-info.log")},
                        "hostTrace": {"file": retained_trace.name, "sha256": digest(retained_trace)},
                        "dotnetHost": {"path": command[0], "sha256": digest(command[0])},
                        "coreClr": {"path": coreclr[1], "sha256": digest(coreclr[1])},
                        "selectedFramework": framework, "rollForward": "Disable",
                        "command": [str(argument) for argument in [*command, "/noconfig", "@" + str(response)]],
                        "responseSha256": digest(response), "currentAnalyzerForced": True,
                        "scope": "Old csc replay of actual locked-MSBuild package sources/references/analyzer/allowlist; no old MSBuild task execution"}


def inspect_identity(run, inspector, path, output, stem):
    _, text = run(["dotnet", inspector, "--metadata-only", path], output / f"{stem}.metadata.log", output)
    identities = re.findall(r"^IDENTITY (\S+) VERSION (\S+) MVID ([0-9a-f-]+)$", text, re.MULTILINE)
    if len(identities) != 1:
        raise ValueError("Metadata inspector did not retain exactly one assembly identity")
    name, version, mvid = identities[0]
    return {"path": str(path), "sha256": digest(path), "assembly": name, "version": version, "mvid": mvid}


def bind_compiler_archive(toolset):
    """Every selected older compiler/task/config input must match its recorded archive."""
    archive = toolset / f"microsoft.net.compilers.toolset.{TOOLSET_VERSION}.nupkg"
    archive_sha512 = base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode()
    if archive_sha512 != archive.with_suffix(".nupkg.sha512").read_text().strip():
        raise ValueError("Older compiler package archive does not match its restored SHA512")
    entries = ["tasks/netcore/bincore/csc.dll", "tasks/netcore/bincore/csc.runtimeconfig.json",
               "tasks/netcore/bincore/csc.deps.json", "tasks/netcore/bincore/Microsoft.CodeAnalysis.dll",
               "tasks/netcore/bincore/Microsoft.CodeAnalysis.CSharp.dll",
               "tasks/netcore/Microsoft.Build.Tasks.CodeAnalysis.dll",
               "tasks/netcore/Microsoft.CSharp.Core.targets", "build/Microsoft.Net.Compilers.Toolset.props"]
    evidence = []
    with zipfile.ZipFile(archive) as contents:
        for entry in entries:
            data = contents.read(entry)
            path = toolset / entry
            if path.read_bytes() != data:
                raise ValueError(f"Selected compiler input differs from exact archive entry: {entry}")
            evidence.append({"entry": entry, "sha256": digest(path)})
    return {"archiveSha256": digest(archive), "archiveSha512": archive_sha512, "boundEntries": evidence}


def run_collision(*, work, output, reactive, package_id, version, current_runtime, analyzer,
                  flags, run, verify_restored_bytes, package_hash, source):
    """Actual packed friend assemblies expose identical bridge IDs with colliding sanitized names."""
    flavor = "Reactive" if reactive else "Primitives"
    stem = f"{flavor}-configuration-collision"
    peer = work / f"{stem}-peer"
    peer_project = write_project(peer, package_id, version, "collision", current_runtime=current_runtime, analyzer=analyzer, peer=True)
    (peer / "Program.cs").write_text(fixture_source(reactive, private=True, peer=True))
    peer_dll = peer / "bin/Release/net10.0/Configuration.Peer-a.dll"
    consumer = work / f"{stem}-consumer"
    project = write_project(consumer, package_id, version, "collision", current_runtime=current_runtime,
                            analyzer=analyzer, peer_reference=peer_dll)
    (consumer / "Program.cs").write_text(fixture_source(reactive, private=True))
    evidence = []
    bridge_namespaces = []
    for label, folder, target in (("peer", peer, peer_project), ("consumer", consumer, project)):
        retain_inputs(folder, output, f"{stem}-{label}", {})
        try:
            _, text = run(["dotnet", "build", target, *flags], output / f"{stem}-{label}.build.log", folder)
        finally:
            retain_inputs(folder, output, f"{stem}-{label}", {})
        if re.search(r"\bwarning [A-Z]+\d+\b", text):
            raise ValueError("Packed friend collision control emitted a warning")
        inputs = read_inputs(folder)
        retained_inputs = retain_inputs(folder, output, f"{stem}-{label}", inputs)
        own = [path for path in inputs.get("analyzer", []) if Path(path).name == analyzer.name]
        refs = [path for path in inputs.get("reference", []) if Path(path).name == current_runtime.name]
        if len(own) != 1 or digest(own[0]) != digest(analyzer) or len(refs) != 1 or digest(refs[0]) != digest(current_runtime):
            raise ValueError("Packed collision peer and consumer did not use the exact same runtime/analyzer pair")
        verify_restored_bytes(json.loads((folder / "obj/project.assets.json").read_text()), package_id, version, package_hash)
        bridges = list((folder / "obj/generated").rglob("AccessBridge0_0.g.cs"))
        if len(bridges) != 1:
            raise ValueError("Each real friend assembly must emit the same first bridge ID")
        names = re.findall(r"^namespace (ReactiveUI\.Validation\.Generated\.Assembly_Configuration_Peer_a_[0-9A-F]{16}\.Accessors);$", bridges[0].read_text(), re.MULTILINE)
        if len(names) != 1:
            raise ValueError("Packed collision bridge did not preserve its assembly-qualified namespace identity")
        bridge_namespaces.append(names[0])
        evidence.append({"role": label, **retained_inputs})
    if len(set(bridge_namespaces)) != 2:
        raise ValueError("Distinct friend assemblies reused the same generated bridge namespace")
    runtime = consumer / "bin/Release/net10.0/Configuration.Peer_a.dll"
    code, text = run(["dotnet", runtime], output / f"{stem}.runtime.log", consumer)
    verify_success(code, text)
    return {"name": stem, "source": source, "packageSha256": package_hash, "passed": True,
            "scope": "Managed packed peer and consumer compilation/execution; no extra publish matrix",
            "bridgeNamespaces": bridge_namespaces, "inputs": evidence}


def run_packed_cases(*, root, work, output, source, reactive, package_id, version, package,
                     package_hash, cache, config, pins, run, verify_restored_bytes, metadata_inspector):
    """Run only managed build/configuration controls; use the exact already selected pair."""
    if digest(package) != package_hash:
        raise ValueError("Configuration controls received different candidate package bytes")
    flavor = "Reactive" if reactive else "Primitives"
    runtime_name = "ReactiveUI.Validation.Reactive.dll" if reactive else "ReactiveUI.Validation.dll"
    package_root = cache / package_id.lower() / version
    current_runtime = package_root / "lib/net10.0" / runtime_name
    analyzer = package_root / "analyzers/dotnet/roslyn5.9/cs/ReactiveUI.Validation.SourceGenerators.dll"
    old_folder = root / "eng/verification-fixtures/UnsupportedRuntime"
    old_manifest = json.loads((old_folder / "provenance.json").read_text())
    old = old_manifest["flavors"][flavor]
    old_runtime = old_folder / old["file"]
    if digest(old_runtime) != old["sha256"]:
        raise ValueError("Historical unsupported-runtime fixture bytes changed")
    common = ["-c", "Release", "-m:2", "-warnaserror", "--configfile", config,
              f"-p:RestorePackagesPath={cache}", "-p:TreatWarningsAsErrors=true",
              "-p:EnableTrimAnalyzer=true", "-p:EnableAotAnalyzer=true",
              "-p:RunAnalyzers=true", "-p:RunAnalyzersDuringBuild=true"]
    results = []
    checkpoint = output / f"{flavor}-configuration-results.json"
    journal = {"source": source, "packageSha256": package_hash, "completed": False, "cases": results}
    def persist():
        checkpoint.write_text(json.dumps(journal, indent=2) + "\n")
    persist()
    for case in CASES:
        stem = f"{flavor}-configuration-{case}"
        folder = work / stem
        toolset = cache / "microsoft.net.compilers.toolset" / TOOLSET_VERSION
        csc = toolset / "tasks/netcore/bincore/csc.dll"
        project = write_project(folder, package_id, version, case, current_runtime=current_runtime,
                                analyzer=analyzer, old_runtime=old_runtime if case == "old-runtime" else None)
        src = fixture_source(reactive, static_call=case == "csharp13")
        if case == "missing-runtime":
            root_name = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
            src = f"internal static class Program {{ private static void Main() => _ = typeof(global::{root_name}.Extensions.ValidatableViewModelExtensions); }}\n"
        (folder / "Program.cs").write_text(src)
        journal["currentCase"] = {"name": stem, "inputs": retain_inputs(folder, output, stem, {})}
        persist()
        flags = list(common)
        if case == "wrong-allowlist":
            flags.append("-p:InterceptorsNamespaces=Configuration.Forbidden")
        if case == "analyzers-disabled":
            flags.extend(["-p:RunAnalyzers=false", "-p:RunAnalyzersDuringBuild=false"])
        compiler_evidence = None
        if case == "older-compiler":
            code, text, compiler_evidence = run_older_compiler(folder=folder, project=project, toolset=toolset,
                csc=csc, cache=cache, config=config, run=run, output=output, stem=stem)
        else:
            with environment(DOTNET_CLI_UI_LANGUAGE="en-US"):
                code, text = run(["dotnet", "build", project, *flags], output / f"{stem}.build.log", folder, expected=True)
        journal["currentCase"]["inputs"] = retain_inputs(folder, output, stem, {})
        persist()
        inputs = read_inputs(folder)
        journal["currentCase"]["inputs"] = retain_inputs(folder, output, stem, inputs)
        persist()
        own = [path for path in inputs.get("analyzer", []) if Path(path).name == analyzer.name]
        if len(own) != (0 if case == "removed-analyzer" else 1):
            raise ValueError(f"{case} selected the wrong packaged validation analyzer inputs")
        if own and digest(own[0]) != digest(analyzer):
            raise ValueError("Configuration control selected different validation analyzer bytes")
        imports = (folder / "obj/Control.csproj.nuget.g.props").read_text()
        if (f"{package_id}.props" in imports) != (case != "excluded-build"):
            raise ValueError(f"{case} imported the wrong packaged transitive props")
        if inputs.get("language") != ["13.0" if case == "csharp13" else "14.0"]:
            raise ValueError("Configuration case did not select its exact language band")
        allowlist = ";".join(inputs.get("allowlist", []))
        if case in {"excluded-build", "wrong-allowlist"}:
            if "ReactiveUI.Validation.Generated" in allowlist:
                raise ValueError("Allowlist-negative configuration still admitted the generated namespace")
        elif "ReactiveUI.Validation.Generated" not in allowlist:
            raise ValueError("Configuration control unexpectedly lost its packaged allowlist")
        references = [path for path in inputs.get("reference", []) if Path(path).name == runtime_name]
        expected_reference = None if case == "missing-runtime" else old_runtime if case == "old-runtime" else current_runtime
        if len(references) != (0 if expected_reference is None else 1) or references and digest(references[0]) != digest(expected_reference):
            raise ValueError(f"{case} selected the wrong runtime compile reference")
        assets = json.loads((folder / "obj/project.assets.json").read_text())
        verify_restored_bytes(assets, package_id, version, package_hash)
        if case == "excluded-analyzer":
            include = assets["project"]["frameworks"]["net10.0"]["dependencies"][package_id].get("include")
            if include is None or "Analyzers" in include.split(", "):
                raise ValueError("Analyzer-exclusion control did not retain its actual NuGet exclusion flags")
        result = {"name": stem, "source": source, "packageSha256": package_hash, "passed": False,
                  "scope": "Managed packed configuration control; no trim/native publication", "inputs": retain_inputs(folder, output, stem, inputs)}
        if case in {"excluded-build", "wrong-allowlist", "csharp13", "old-runtime", "missing-runtime", "older-compiler"}:
            required = {"RUVG004"} if case == "old-runtime" else {"CS0234"} if case == "missing-runtime" else {"CS9057"} if case == "older-compiler" else {"RUVG003"}
            allowed = required | ({"RUVG005"} if case in {"excluded-build", "wrong-allowlist", "csharp13", "old-runtime"} else set())
            provenance = "ReactiveUI.Validation.SourceGenerators.dll" if case == "older-compiler" else "Program.cs"
            result["diagnostics"] = verify_diagnostics(code, text, required, allowed, provenance)
            if case == "older-compiler":
                result["compiler"] = {**compiler_evidence,
                    "identity": inspect_identity(run, metadata_inspector, csc, output, stem),
                    "roslynIdentities": [inspect_identity(run, metadata_inspector, csc.parent / name, output, f"{stem}-{name}")
                                         for name in ("Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll")]}
            if case == "old-runtime":
                result["unsupportedRuntime"] = {**old, "source": old_manifest["source"], "version": old_manifest["version"],
                                                "identity": inspect_identity(run, metadata_inspector, old_runtime, output, stem)}
                if result["unsupportedRuntime"]["identity"]["mvid"] != old["mvid"]:
                    raise ValueError("Unsupported-runtime metadata identity differs from its immutable provenance")
        else:
            if code != 0 or re.search(r"\bwarning [A-Z]+\d+\b", text):
                raise ValueError("Configuration runtime control must first compile cleanly")
            runtime = folder / f"bin/Release/net10.0/Configuration.Control.dll"
            runtime_code, runtime_text = run(["dotnet", runtime], output / f"{stem}.runtime.log", folder, expected=True)
            if case == "removed-analyzer":
                verify_missing_dispatch(runtime_code, runtime_text)
                result["boundary"] = "Analyzer absent; build cannot self-diagnose, actual normal runtime route fails actionably"
            else:
                verify_success(runtime_code, runtime_text)
                if case == "excluded-analyzer":
                    result["boundary"] = "Locked SDK10.0.401 preserves NuGet analyzer-exclusion flags but still selects the exact generator; generated typed rule executes. Actual absence is separately proved by removed-analyzer."
                else:
                    result["boundary"] = "Generated typed rule executes; diagnostic-analyzer disable does not disable generators" if case == "analyzers-disabled" else "Generated typed rule executes"
        result["passed"] = True
        results.append(result)
        persist()
    journal["currentCase"] = {"name": f"{flavor}-configuration-collision"}
    persist()
    results.append(run_collision(work=work, output=output, reactive=reactive, package_id=package_id, version=version,
                                 current_runtime=current_runtime, analyzer=analyzer, flags=common, run=run,
                                 verify_restored_bytes=verify_restored_bytes, package_hash=package_hash, source=source))
    journal.pop("currentCase")
    journal["completed"] = True
    persist()
    return results
