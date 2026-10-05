#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Restore and exercise each packed validation flavor in an independent consumer."""

import json
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
NS = {"n": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}


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
    pins = dependency_pins(ROOT)
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
        selected.append((reactive, package_id, verify_metadata(metadata, reactive, pins)))
    if selected[0][2] != selected[1][2]:
        raise ValueError("Packed validation flavors must have the same version")
    for reactive, package_id, version in selected:
        suffix = ".Reactive" if reactive else ""
        with tempfile.TemporaryDirectory(prefix="package-smoke-", dir=ROOT / "artifacts") as temporary:
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
            subprocess.run(["dotnet", "run", "--project", str(project), "-c", "Release"], check=True, cwd=folder)
            assets = json.loads((folder / "obj" / "project.assets.json").read_text())
            verify_graph(assets, reactive, pins, version)



if __name__ == "__main__":
    main()
