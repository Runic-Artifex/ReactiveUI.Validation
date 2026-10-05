#!/usr/bin/env python3
"""Bounded managed generator investigation; never invokes publish or shipping builds."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
VERSION = "8.1.0-runic.0.790.17"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--feed-root", type=Path, default=REPO, help="Repository holding verified artifacts/packages and artifacts/dependencies")
parser.add_argument("--output", type=Path, default=REPO / "artifacts/BindingGenerators")
parser.add_argument("--retain-evidence", action="store_true", help="Copy successful output and diagnostics into the versioned evidence directory")
args = parser.parse_args()
out = args.output.resolve()
out.mkdir(parents=True, exist_ok=True)
feeds = args.feed_root.resolve() / "artifacts"
config = ET.Element("configuration")
sources = ET.SubElement(config, "packageSources")
ET.SubElement(sources, "clear")
for name, path in (("released-validation", feeds / "packages"), ("released-dynamicdata", feeds / "dependencies"), ("nuget.org", "https://api.nuget.org/v3/index.json")):
    ET.SubElement(sources, "add", key=name, value=str(path))
mapping = ET.SubElement(config, "packageSourceMapping")
for source, pattern in (("released-validation", "Runic.ReactiveUI.Validation*"), ("released-dynamicdata", "Runic.DynamicData*"), ("nuget.org", "*")):
    ET.SubElement(ET.SubElement(mapping, "packageSource", key=source), "package", pattern=pattern)
config_path = out / "feeds.config"
ET.ElementTree(config).write(config_path, encoding="utf-8", xml_declaration=True)

def run(command, name, expected=0):
    result = subprocess.run(command, cwd=REPO, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=180)
    normalized = result.stdout.replace(str(REPO), "<repo>")
    (out / name).write_text(normalized)
    if expected == 0 and result.returncode != 0 or expected != 0 and result.returncode == 0:
        raise RuntimeError(f"Unexpected exit {result.returncode}: {' '.join(command)}\n{result.stdout[-5000:]}")
    return result.stdout

pins = {"sdk": run(["dotnet", "--version"], "sdk.txt").strip(), "source": "2ba57c537172d841cdc545ef9cd255f1e41a4af8", "releasedSource": "c9fa501c4d2e9442d85693dae77bb6f727e9eb7a", "packages": {}}
assert pins["sdk"] == "10.0.401", pins
inspect_paths = []
package_metadata = {}
for flavor, suffix in (("Primitives", ""), ("Reactive", ".Reactive")):
    package = feeds / "packages" / f"Runic.ReactiveUI.Validation{suffix}.{VERSION}.nupkg"
    assert package.is_file(), package
    with ZipFile(package) as archive:
        dll = next(x for x in archive.namelist() if x.startswith("lib/net10.0/") and x.endswith(".dll"))
        dest = out / Path(dll).name
        dest.write_bytes(archive.read(dll))
        inspect_paths.append(str(dest))
    pins["packages"][package.name] = hashlib.sha256(package.read_bytes()).hexdigest()
    project = HERE / flavor / f"BindingGenerators.{flavor}.csproj"
    command = ["dotnet", "build", str(project), "-c", "Release", "-m:2", "-t:Rebuild", "--configfile", str(config_path)]
    run(command, f"{flavor}-build.txt")
    text = run(["dotnet", "run", "--project", str(project), "-c", "Release", "--no-build", "--no-restore"], f"{flavor}-run.txt")
    assert "PASS generated properties" in text
    assets = json.loads((project.parent / "obj/project.assets.json").read_text())
    for package_key, library in assets["libraries"].items():
        if package_key.split("/")[0] not in ("ReactiveUI", "ReactiveUI.Reactive", "ReactiveUI.Binding", "ReactiveUI.Binding.Reactive", "ReactiveUI.SourceGenerators"):
            continue
        package_folder = next(Path(root) / library["path"] for root in assets["packageFolders"] if (Path(root) / library["path"]).is_dir())
        nuspec = ET.parse(next(package_folder.glob("*.nuspec"))).getroot()
        ns = {"n": nuspec.tag[1:nuspec.tag.index("}")]}
        metadata = nuspec.find("n:metadata", ns)
        repository = metadata.find("n:repository", ns)
        dependencies = metadata.find('n:dependencies/n:group[@targetFramework="net10.0"]', ns)
        package_metadata[package_key] = {"repository": repository.attrib if repository is not None else {}, "net10Dependencies": [dependency.attrib for dependency in dependencies] if dependencies is not None else []}
    graph = {key: value.get("dependencies", {}) for key, value in assets["targets"]["net10.0"].items()}
    expected_package = f"Runic.ReactiveUI.Validation{suffix}/{VERSION}"
    assert expected_package in graph, graph
    assert f"Runic.DynamicData{suffix}/10.0.0-runic.5" in graph
    assert not any(key.startswith(("DynamicData/", "DynamicData.Reactive/")) for key in graph)
    if flavor == "Primitives":
        assert not any(key.startswith("System.Reactive/") for key in graph)
    (out / f"{flavor}-graph.json").write_text(json.dumps({"target": "net10.0", "directReferences": list(assets["project"]["frameworks"]["net10.0"]["dependencies"]), "resolved": graph}, indent=2) + "\n")
    generated = project.parent / "obj/generated"
    dispatches = list(generated.rglob("*Dispatch.g.cs"))
    dispatch_text = "\n".join(path.read_text(encoding="utf-8-sig") for path in dispatches)
    assert "InterceptsLocation" in dispatch_text
    assert "__Intercept_BindTo" in dispatch_text and "__Intercept_WhenAnyValue" in dispatch_text
    assert "BindValidationState" not in dispatch_text and "ValidationRule" not in dispatch_text
    snapshot = out / flavor
    snapshot.mkdir(exist_ok=True)
    for source in generated.rglob("*.g.cs"):
        shutil.copyfile(source, snapshot / (source.name + ".txt"))
    # Force actual compilation and keep a focused record of loaded analyzers.
    full = run(command + ["-v:normal"], f"{flavor}-compiler-full.txt")
    selected = sorted(set(re.findall(r'/analyzer:[^\s]+ReactiveUI[^\s]+|/features:[^\r\n]*?Microsoft.Extensions.Validation.Generated"', full)))
    (out / f"{flavor}-compiler.txt").write_text("\n".join(selected) + "\n" + "No direct ReactiveUI/SourceGenerators package references; rebuild succeeded.\n")
    (out / f"{flavor}-compiler-full.txt").unlink()

# Negative forms remain opt-in. A fresh compile proves installation is not coverage.
project = HERE / "Primitives/BindingGenerators.Primitives.csproj"
unpromoted = run(["dotnet", "build", str(project), "-c", "Release", "-m:2", "-t:Rebuild", "--configfile", str(config_path), "-p:PinVmGenerator=false", "-v:normal"], "unpromoted-vm-generator-full.txt")
analyzers = sorted(set(re.findall(r'/analyzer:[^\s]+ReactiveUI[^\s]+', unpromoted)))
assert any("ReactiveUI.SourceGenerators.Roslyn.dll" in path for path in analyzers)
(out / "unpromoted-vm-generator-compiler.txt").write_text("\n".join(analyzers) + "\nNo SourceGenerators central pin or direct ReactiveUI/SourceGenerators references; fresh restore/rebuild succeeded.\n")
(out / "unpromoted-vm-generator-full.txt").unlink()
# Restore the pinned baseline after the unpromoted observation.
run(["dotnet", "restore", str(project), "--configfile", str(config_path)], "Primitives-pinned-restore.txt")
for constant, name, diagnostic in (("NEGATIVE_ANONYMOUS", "anonymous-diagnostics.txt", "RXUIBIND015"), ("NEGATIVE_SELECTOR", "selector-diagnostics.txt", "RXUIBIND001")):
    text = run(["dotnet", "build", str(project), "-c", "Release", "-m:2", "--no-restore", f"-p:ProbeConstants={constant}"], name, expected=1)
    assert diagnostic in text
# Leave baseline build artifacts and generated files matching baseline source.
run(["dotnet", "build", str(project), "-c", "Release", "-m:2", "--no-restore"], "Primitives-final-build.txt")
inspector = HERE / "InspectShipping/InspectShipping.csproj"
run(["dotnet", "build", str(inspector), "-c", "Release", "-m:2", "--configfile", str(config_path)], "inspector-build.txt")
il = run(["dotnet", "run", "--project", str(inspector), "-c", "Release", "--no-build", "--no-restore", "--", *inspect_paths], "released-oaph-il.txt")
assert il.count("__Intercept_ToProperty") == 8, il
(out / "pins.json").write_text(json.dumps(pins, indent=2) + "\n")
(out / "package-metadata.json").write_text(json.dumps(package_metadata, indent=2) + "\n")
# Extracted DLLs/config are task-owned disposable outputs; logs/graph/snapshots remain.
for path in inspect_paths:
    Path(path).unlink()
config_path.unlink()
if args.retain_evidence:
    evidence = HERE / "evidence"
    evidence.mkdir(exist_ok=True)
    for source in out.iterdir():
        if source.is_dir():
            shutil.copytree(source, evidence / source.name, dirs_exist_ok=True)
        elif source.suffix in (".txt", ".json"):
            shutil.copyfile(source, evidence / source.name)
print(f"PASS both managed flavors; transitive generators; graph guards; two negative forms; released OAPH IL. Evidence: {out}")
