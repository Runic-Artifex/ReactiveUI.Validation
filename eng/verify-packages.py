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


def main():
    """Verify package metadata, consumer behavior and the restored dependency graph."""
    for reactive in (False, True):
        suffix = ".Reactive" if reactive else ""
        package_id = f"Runic.ReactiveUI.Validation{suffix}"
        packages = list((ROOT / "artifacts" / "packages").glob(f"{package_id}.*.nupkg"))
        packages = [p for p in packages if not p.name.startswith(f"{package_id}.Reactive.")]
        if len(packages) != 1:
            raise ValueError(f"Expected exactly one packed {package_id}, found {len(packages)}")
        with zipfile.ZipFile(packages[0]) as archive:
            metadata = ET.fromstring(archive.read(f"{package_id}.nuspec"))
        version = metadata.findtext("n:metadata/n:version", namespaces=NS)
        if "-runic." not in version:
            raise ValueError(f"Expected a Runic prerelease version, got {version}")
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
            libraries = {name.split("/")[0] for name in assets["libraries"]}
            if "DynamicData" in libraries or f"Runic.DynamicData{suffix}" not in libraries:
                raise ValueError(f"Incorrect DynamicData dependency graph for {package_id}")
            if not reactive and "System.Reactive" in libraries:
                raise ValueError("The Primitives consumer must not depend on System.Reactive")


if __name__ == "__main__":
    main()
