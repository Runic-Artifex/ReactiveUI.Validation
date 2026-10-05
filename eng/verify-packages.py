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
using {validation}.Extensions;
using {validation}.Helpers;

RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
using var model = new Model();
var rule = model.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Required");
DynamicData{suffix}.IObservableList<IValidationComponent> rules = model.ValidationContext.Validations;
if (rules.Count != 1 || model.ValidationContext.IsValid || !model.HasErrors)
    throw new InvalidOperationException("Expected an invalid empty name");
model.Name = "Runic";
if (!model.ValidationContext.IsValid || model.HasErrors)
    throw new InvalidOperationException("Expected validation to react to the property change");
rule.Dispose();
if (rules.Count != 0 || !model.ValidationContext.IsValid)
    throw new InvalidOperationException("Expected disposal to remove the validation rule");
Console.WriteLine("{package_id}: package consumer passed");

sealed class Model : ReactiveValidationObject
{{
    private string _name = "";
    public string Name
    {{
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }}
}}
''')
            subprocess.run(["dotnet", "run", "--project", str(project), "-c", "Release"], check=True, cwd=folder)
            assets = json.loads((folder / "obj" / "project.assets.json").read_text())
            verify_graph(assets, reactive, pins, version)



if __name__ == "__main__":
    main()
