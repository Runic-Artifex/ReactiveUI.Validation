#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Restore and exercise each packed validation flavor in an independent consumer."""

import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import shutil
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
NS = {"n": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


DEPENDENCIES = module("fork_dependencies", ROOT / "eng/restore-fork-dependencies.py")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dependency_pins(root):
    """Read the repository's explicit released dependency cohort."""
    return {
        node.attrib["Include"].casefold(): node.attrib["Version"]
        for node in ET.parse(root / "src" / "Directory.Packages.props").iter("PackageVersion")
    }


def flavor_ids(reactive):
    """Return the validation, DynamicData and ReactiveUI package identities."""
    suffix = ".Reactive" if reactive else ""
    return tuple(f"{name}{suffix}" for name in (
        "Runic.ReactiveUI.Validation", "Runic.DynamicData", "ReactiveUI"))


def forbidden_ids(reactive):
    """Exclude upstream and opposite-flavor dependencies, regardless of casing."""
    forbidden = {"DynamicData", "DynamicData.Reactive", "ReactiveUI.Validation",
                 "ReactiveUI.Validation.Reactive", *flavor_ids(not reactive)}
    if not reactive:
        forbidden.add("System.Reactive")
    return {name.casefold() for name in forbidden}


def verify_metadata(metadata, reactive, pins):
    """Check the shipped identity, target and declared dependency cohort."""
    package_id, dynamic_data, reactive_ui = flavor_ids(reactive)
    actual_id = metadata.findtext("n:metadata/n:id", namespaces=NS)
    if actual_id != package_id:
        raise ValueError(f"Expected package ID {package_id}, got {actual_id}")
    version = metadata.findtext("n:metadata/n:version", namespaces=NS)
    if not version or "-runic." not in version:
        raise ValueError(f"Expected a Runic prerelease version, got {version}")
    groups = metadata.findall("n:metadata/n:dependencies/n:group", NS)
    if len(groups) != 1 or groups[0].get("targetFramework") != "net10.0":
        raise ValueError(f"{package_id} must declare exactly one net10.0 dependency group")
    dependencies = metadata.findall("n:metadata/n:dependencies/n:dependency", NS)
    if dependencies:
        raise ValueError(f"{package_id} must declare dependencies inside its target group")
    dependencies = groups[0].findall("n:dependency", NS)
    declared = {}
    for dependency in dependencies:
        name = dependency.attrib["id"].casefold()
        if name in declared:
            raise ValueError(f"Duplicate dependency {name} in {package_id}")
        declared[name] = dependency.get("version")
    unexpected = set(declared) & forbidden_ids(reactive)
    if unexpected:
        raise ValueError(f"Forbidden dependencies in {package_id}: {sorted(unexpected)}")
    for name in (dynamic_data, reactive_ui):
        expected = pins[name.casefold()]
        if declared.get(name.casefold()) not in (expected, f"[{expected}]"):
            raise ValueError(f"{package_id} must declare {name} at the central pin {expected}")
    return version


def verify_repository_source(metadata, package, source):
    """Require the packed Validation bytes to identify this clean checkout."""
    repository = metadata.find("n:metadata/n:repository", NS)
    if repository is None or repository.get("commit") != source:
        raise ValueError(f"Packed package must record source SHA {source}: {package}")


def verify_restored_bytes(assets, package_id, version, expected):
    """Prove NuGet restored the exact input nupkg, never a stale same-version cache entry."""
    library = assets["libraries"][f"{package_id}/{version}"]
    relative = Path(library["path"])
    candidates = [Path(base) / relative / f"{package_id.lower()}.{version}.nupkg"
                  for base in assets["packageFolders"]]
    present = [path for path in candidates if path.is_file()]
    if len(present) != 1 or digest(present[0]) != expected:
        raise ValueError(f"Restored {package_id} bytes do not match the verified input package")
    restored_sha512 = base64.b64encode(hashlib.sha512(present[0].read_bytes()).digest()).decode()
    if library.get("sha512") != restored_sha512:
        raise ValueError(f"Restored {package_id} asset SHA-512 does not bind its package bytes")


def verified_dynamic_data_inputs(pins):
    """Return the current immutable DynamicData pair after checking its bootstrap hashes."""
    inputs = {}
    for package_id, expected_hash in DEPENDENCIES.DIGESTS.items():
        version = pins[package_id.casefold()]
        if version != DEPENDENCIES.CURRENT_VERSION:
            raise ValueError(f"{package_id} must use the current immutable DynamicData version")
        package = ROOT / "artifacts" / "dependencies" / f"{package_id}.{version}.nupkg"
        if not package.is_file() or digest(package) != expected_hash:
            raise ValueError(f"DynamicData feed must match the SHA-verified immutable release: {package}")
        inputs[package_id] = expected_hash
    return inputs


def write_report(output, report):
    (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")


def initialize_report(output, report):
    """Invalidate prior success before any input or toolchain check can fail."""
    report["completed"] = False
    write_report(output, report)


def run(command, log, cwd):
    print(f"Running {' '.join(map(str, command))}", flush=True)
    with log.open("w") as output:
        result = subprocess.run(list(map(str, command)), check=False, cwd=cwd,
                                stdout=output, stderr=subprocess.STDOUT)
    if result.returncode:
        content = log.read_text(errors="replace")
        print(content[-20000:], flush=True)
        raise RuntimeError(f"Command failed ({result.returncode}); full log: {log}")


def verify_graph(assets, reactive, pins, validation_version):
    """Reject restored graphs that mix package flavors or dependency generations."""
    package_id, dynamic_data, reactive_ui = flavor_ids(reactive)
    libraries = {}
    for identity, library in assets["libraries"].items():
        name, version = identity.rsplit("/", 1)
        name = name.casefold()
        if name in libraries:
            raise ValueError(f"Multiple resolved versions of {name} in {package_id}")
        libraries[name] = (version, library.get("type"))
    unexpected = set(libraries) & forbidden_ids(reactive)
    if unexpected:
        raise ValueError(f"Forbidden dependencies in {package_id}: {sorted(unexpected)}")
    expected_packages = {
        package_id.casefold(): validation_version,
        dynamic_data.casefold(): pins[dynamic_data.casefold()],
        reactive_ui.casefold(): pins[reactive_ui.casefold()],
        "reactiveui.core": pins[reactive_ui.casefold()],
    }
    for name, version in expected_packages.items():
        if libraries.get(name) != (version, "package"):
            raise ValueError(f"{package_id} must resolve package {name}/{version}")
    targets = assets.get("targets", {})
    if set(targets) != {"net10.0"}:
        raise ValueError(f"{package_id} consumer must restore exactly the net10.0 target")
    target_ids = {identity.casefold() for identity in targets["net10.0"]}
    for name, version in expected_packages.items():
        if f"{name}/{version}".casefold() not in target_ids:
            raise ValueError(f"Missing {name}/{version} in the net10.0 consumer target")


def main():
    """Verify package metadata, consumer behavior and the restored dependency graph."""
    output = ROOT / "artifacts" / "verification" / "package-smoke"
    output.mkdir(parents=True, exist_ok=True)
    report = {"source": None, "dirty": None, "sdk": None, "packages": {}, "checks": []}
    initialize_report(output, report)
    pins = dependency_pins(ROOT)
    report.update({"source": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
                   "dirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip()),
                   "sdk": subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip()})
    write_report(output, report)
    if report["dirty"]:
        raise ValueError("Package acceptance requires a clean source checkout")
    source = report["source"]
    if report["sdk"] != json.loads((ROOT / "global.json").read_text())["sdk"]["version"]:
        raise ValueError("Pinned SDK is required")
    dynamic_data_hashes = verified_dynamic_data_inputs(pins)
    selected = []
    for reactive in (False, True):
        package_id, _, _ = flavor_ids(reactive)
        packages = list((ROOT / "artifacts" / "packages").glob(f"{package_id}.*.nupkg"))
        packages = [p for p in packages if not p.name.startswith(f"{package_id}.Reactive.")]
        if len(packages) != 1:
            raise ValueError(f"Expected exactly one packed {package_id}, found {len(packages)}")
        with zipfile.ZipFile(packages[0]) as archive:
            metadata = ET.fromstring(archive.read(f"{package_id}.nuspec"))
            frameworks = {name.split("/")[1] for name in archive.namelist()
                          if name.startswith("lib/") and name.endswith(".dll")}
        if frameworks != {"net10.0"}:
            raise ValueError(f"{package_id} must contain only net10.0 library assemblies")
        version = verify_metadata(metadata, reactive, pins)
        verify_repository_source(metadata, packages[0], source)
        selected.append((reactive, package_id, version, packages[0], digest(packages[0])))
    if selected[0][2] != selected[1][2]:
        raise ValueError("Packed validation flavors must have the same version")
    cache = output / "packages"
    for reactive, package_id, version, package, package_hash in selected:
        suffix = ".Reactive" if reactive else ""
        dynamic_data = flavor_ids(reactive)[1]
        flavor = "Reactive" if reactive else "Primitives"
        expected_inputs = ((package_id, version, package_hash),
                           (dynamic_data, pins[dynamic_data.casefold()], dynamic_data_hashes[dynamic_data]))
        for input_id, input_version, input_hash in expected_inputs:
            cached = cache / input_id.lower() / input_version
            cached_package = cached / f"{input_id.lower()}.{input_version}.nupkg"
            if cached.exists() and (not cached_package.is_file() or digest(cached_package) != input_hash):
                shutil.rmtree(cached)
        report["packages"][flavor] = {"id": package_id, "version": version, "sha256": package_hash,
                                      "repositoryCommit": source, "dynamicData": {"id": dynamic_data,
                                      "version": pins[dynamic_data.casefold()], "sha256": dynamic_data_hashes[dynamic_data]}}
        write_report(output, report)
        with tempfile.TemporaryDirectory(prefix="work-", dir=output) as temporary:
            folder = Path(temporary)
            (folder / "nuget.config").write_text(f'''<configuration>
  <packageSources>
    <clear />
    <add key="runic-forks" value="{ROOT / 'artifacts' / 'dependencies'}" />
    <add key="runic-validation" value="{ROOT / 'artifacts' / 'packages'}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="runic-validation"><package pattern="Runic.ReactiveUI.Validation*" /></packageSource>
    <packageSource key="runic-forks"><package pattern="Runic.DynamicData*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
''')
            project = folder / "Smoke.csproj"
            project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="{package_id}" Version="{version}" /></ItemGroup>
</Project>
''')
            validation = f"ReactiveUI.Validation{suffix}"
            rx = f"ReactiveUI{suffix}"
            (folder / "Program.cs").write_text(f'''using {rx};
using {rx}.Builder;
using {validation}.Components.Abstractions;
using {validation}.Contexts;
using {validation}.Extensions;
using {validation}.Helpers;

namespace Runic.Validation.PackageConsumer;

public static class Program
{{
    public static void Main()
    {{
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        using var model = new Model();
        var rule = model.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Required");
        DynamicData{suffix}.IObservableList<IValidationComponent> rules = model.ValidationContext.Validations;
        if (rules.Count != 1 || model.ValidationContext.IsValid || !model.HasErrors)
            throw new InvalidOperationException("Expected an invalid empty name");
        model.Name = "Runic";
        if (!model.ValidationContext.IsValid || model.HasErrors)
            throw new InvalidOperationException("Expected validation to react to the property change");
        using var advice = new ValidationContext();
        model.Advice = advice;
        using var adviceRule = model.ValidationRule(advice, x => x.Name, name => name == "Excellent", "Improve the name");
        if (!model.ValidationContext.GetIsValid() || model.HasErrors || advice.GetIsValid())
            throw new InvalidOperationException("Advice must not invalidate the default blocking context");
        model.Name = "";
        if (model.ValidationContext.GetIsValid() || !model.HasErrors)
            throw new InvalidOperationException("Blocking rules must invalidate the default context");
        model.Name = "Runic";

        using var replacement = new Model();
        using var replacementRule = replacement.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Required");
        using var replacementAdvice = new ValidationContext();
        using var emptyAdvice = new ValidationContext();
        replacement.Advice = replacementAdvice;
        using var replacementAdviceRule = replacement.ValidationRule(
            replacementAdvice, x => x.Name, name => name == "Excellent", "Replacement advice");
        var view = new ModelView {{ ViewModel = model }};
        var stateValues = new List<bool>();
        var projections = new List<ValidationProjection>();
        using var stateBinding = view.BindValidationState(
            model, x => x.Name, states => states.All(state => state.IsValid), stateValues.Add, true);
        using var contextBinding = view.BindValidationContext(
            model, x => x.Advice, state => projections.Add(new ValidationProjection(state.IsValid, state.Text.ToSingleLine())));
        if (!stateValues[^1] || projections[^1].IsValid || projections[^1].Text != "Improve the name")
            throw new InvalidOperationException("Expected typed blocking validity and a separate advice projection");

        view.ViewModel = replacement;
        if (stateValues[^1] || projections[^1].Text != "Replacement advice")
            throw new InvalidOperationException("Bindings must switch to the replacement model");
        var stateCount = stateValues.Count;
        var contextCount = projections.Count;
        model.Name = "Excellent";
        if (stateValues.Count != stateCount || projections.Count != contextCount)
            throw new InvalidOperationException("Detached model must not update either binding");
        replacement.Name = "Runic";
        if (!stateValues[^1] || projections[^1].IsValid)
            throw new InvalidOperationException("Replacement blocking rules and advice must remain independent");

        replacement.Advice = emptyAdvice;
        if (!projections[^1].IsValid || projections[^1].Text.Length != 0)
            throw new InvalidOperationException("Empty replacement context must clear the projection");
        contextCount = projections.Count;
        replacement.Name = "Excellent";
        if (projections.Count != contextCount)
            throw new InvalidOperationException("Detached advice context must not update the projection");
        view.ViewModel = null;
        if (!stateValues[^1] || !projections[^1].IsValid || projections[^1].Text.Length != 0)
            throw new InvalidOperationException("Null model must produce the documented empty selections");
        stateCount = stateValues.Count;
        contextCount = projections.Count;
        replacement.Name = "";
        if (stateValues.Count != stateCount || projections.Count != contextCount)
            throw new InvalidOperationException("Null model must detach both subscriptions");

        view.ViewModel = replacement;
        stateBinding.Dispose();
        stateBinding.Dispose();
        contextBinding.Dispose();
        contextBinding.Dispose();
        stateCount = stateValues.Count;
        contextCount = projections.Count;
        replacement.Name = "Runic";
        replacement.Advice = replacementAdvice;
        if (stateValues.Count != stateCount || projections.Count != contextCount)
            throw new InvalidOperationException("Disposed bindings must stop projection updates");
        if (replacementAdvice.Validations.Count != 1 || replacementAdvice.IsDisposed || emptyAdvice.IsDisposed)
            throw new InvalidOperationException("Bindings must not own selected contexts or rules");

        model.Advice = emptyAdvice;
        adviceRule.Dispose();
        if (advice.Validations.Count != 0 || replacementAdvice.Validations.Count != 1)
            throw new InvalidOperationException("Helper removal must use its original explicit context");
        rule.Dispose();
        if (rules.Count != 0 || !model.ValidationContext.IsValid)
            throw new InvalidOperationException("Expected disposal to remove the default validation rule");
        Console.WriteLine("{package_id}: package consumer passed (default, explicit context and typed projections)");
    }}
}}

public sealed record ValidationProjection(bool IsValid, string Text);

public sealed class Model : ReactiveValidationObject
{{
    private string _name = "";
    private IValidationContext? _advice;

    public string Name
    {{
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }}

    public IValidationContext? Advice
    {{
        get => _advice;
        set => this.RaiseAndSetIfChanged(ref _advice, value);
    }}
}}

public sealed class ModelView : ReactiveObject, IViewFor<Model>
{{
    private Model? _viewModel;

    public Model? ViewModel
    {{
        get => _viewModel;
        set => this.RaiseAndSetIfChanged(ref _viewModel, value);
    }}

    object? IViewFor.ViewModel
    {{
        get => ViewModel;
        set => ViewModel = (Model?)value;
    }}
}}
''')
            config = folder / "nuget.config"
            run(["dotnet", "restore", str(project), "--configfile", str(config),
                 f"-p:RestorePackagesPath={cache}"], output / f"{flavor}.restore.log", folder)
            assets = json.loads((folder / "obj" / "project.assets.json").read_text())
            (output / f"{flavor}.graph.json").write_text(json.dumps(assets, indent=2) + "\n")
            verify_graph(assets, reactive, pins, version)
            for input_id, input_version, input_hash in expected_inputs:
                verify_restored_bytes(assets, input_id, input_version, input_hash)
            run(["dotnet", "run", "--project", str(project), "--no-restore", "-c", "Release",
                 f"-p:RestorePackagesPath={cache}"], output / f"{flavor}.runtime.log", folder)
            report["checks"].append({"name": f"{flavor}-managed", "passed": True})
            write_report(output, report)
    report["completed"] = True
    write_report(output, report)



if __name__ == "__main__":
    main()
