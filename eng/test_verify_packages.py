#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Negative dependency-cohort cases for the independent package consumer gate."""

from copy import deepcopy
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location("verify_packages", Path(__file__).with_name("verify-packages.py"))
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)
PINS = VERIFY.dependency_pins(Path(__file__).resolve().parent.parent)
VERSION = "8.1.0-runic.0.790"


def consumer_graph(reactive=False):
    """A minimal resolved net10.0 cohort, including the shared ReactiveUI core."""
    suffix = ".Reactive" if reactive else ""
    identities = [
        f"Runic.ReactiveUI.Validation{suffix}/{VERSION}",
        f"Runic.DynamicData{suffix}/{PINS[f'runic.dynamicdata{suffix}'.casefold()]}",
        f"ReactiveUI{suffix}/{PINS[f'reactiveui{suffix}'.casefold()]}",
        f"ReactiveUI.Core/{PINS[f'reactiveui{suffix}'.casefold()]}",
    ]
    if reactive:
        identities.append("System.Reactive/7.0.0")
    return {"libraries": {name: {"type": "package"} for name in identities},
            "targets": {"net10.0": {name: {} for name in identities}}}


def package_metadata(reactive=False):
    """A valid shipped nuspec; unrelated allowed dependencies remain permitted."""
    suffix = ".Reactive" if reactive else ""
    return ET.fromstring(f'''<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
      <metadata><id>Runic.ReactiveUI.Validation{suffix}</id><version>{VERSION}</version>
        <dependencies><group targetFramework="net10.0">
          <dependency id="Runic.DynamicData{suffix}" version="{PINS[f'runic.dynamicdata{suffix}'.casefold()]}" />
          <dependency id="ReactiveUI{suffix}" version="{PINS[f'reactiveui{suffix}'.casefold()]}" />
          <dependency id="Splat" version="21.0.0" />
        </group></dependencies>
      </metadata></package>''')


class PackageCohortTests(unittest.TestCase):
    def test_both_valid_flavors_and_shared_core_are_allowed(self):
        for reactive in (False, True):
            VERIFY.verify_graph(consumer_graph(reactive), reactive, PINS, VERSION)
            self.assertEqual(VERIFY.verify_metadata(package_metadata(reactive), reactive, PINS), VERSION)

    def test_transitive_upstream_and_opposite_flavors_are_rejected(self):
        cases = {
            False: ["dynamicdata/9.4.1", "DynamicData.Reactive/9.4.1",
                    f"Runic.DynamicData.Reactive/{PINS['runic.dynamicdata.reactive']}",
                    f"Runic.ReactiveUI.Validation.Reactive/{VERSION}",
                    f"ReactiveUI.Reactive/{PINS['reactiveui.reactive']}", "System.Reactive/7.0.0"],
            True: ["DynamicData/9.4.1", "DYNAMICDATA.REACTIVE/9.4.1",
                   f"Runic.DynamicData/{PINS['runic.dynamicdata']}",
                   f"Runic.ReactiveUI.Validation/{VERSION}", f"ReactiveUI/{PINS['reactiveui']}"],
        }
        for reactive, identities in cases.items():
            for identity in identities:
                with self.subTest(reactive=reactive, identity=identity):
                    graph = consumer_graph(reactive)
                    graph["libraries"][identity] = {"type": "package"}
                    with self.assertRaisesRegex(ValueError, "Forbidden dependencies"):
                        VERIFY.verify_graph(graph, reactive, PINS, VERSION)

    def test_binary_generation_mismatch_is_rejected(self):
        for package in ("Runic.DynamicData", "ReactiveUI", "ReactiveUI.Core", "Runic.ReactiveUI.Validation"):
            with self.subTest(package=package):
                graph = consumer_graph()
                identity = next(name for name in graph["libraries"] if name.split("/")[0] == package)
                library = graph["libraries"].pop(identity)
                graph["libraries"][f"{package}/1.0.0"] = library
                with self.assertRaisesRegex(ValueError, "must resolve package"):
                    VERIFY.verify_graph(graph, False, PINS, VERSION)

    def test_source_project_cannot_replace_released_dependency(self):
        graph = consumer_graph()
        identity = f"Runic.DynamicData/{PINS['runic.dynamicdata']}"
        graph["libraries"][identity]["type"] = "project"
        with self.assertRaisesRegex(ValueError, "must resolve package"):
            VERIFY.verify_graph(graph, False, PINS, VERSION)

    def test_library_catalog_does_not_replace_target_resolution(self):
        graph = consumer_graph()
        del graph["targets"]["net10.0"][f"Runic.DynamicData/{PINS['runic.dynamicdata']}"]
        with self.assertRaisesRegex(ValueError, "Missing"):
            VERIFY.verify_graph(graph, False, PINS, VERSION)

    def test_unexpected_consumer_target_is_rejected(self):
        graph = consumer_graph()
        graph["targets"]["net11.0"] = deepcopy(graph["targets"]["net10.0"])
        with self.assertRaisesRegex(ValueError, "exactly the net10.0"):
            VERIFY.verify_graph(graph, False, PINS, VERSION)

    def test_package_declaration_must_match_flavor_and_pin(self):
        for replacement in ("DynamicData", "Runic.DynamicData.Reactive", "ReactiveUI.Reactive"):
            metadata = package_metadata()
            dependency = metadata.find("n:metadata/n:dependencies/n:group/n:dependency", VERIFY.NS)
            dependency.set("id", replacement)
            with self.subTest(replacement=replacement), self.assertRaisesRegex(ValueError, "Forbidden dependencies"):
                VERIFY.verify_metadata(metadata, False, PINS)
        metadata = package_metadata()
        metadata.find("n:metadata/n:dependencies/n:group/n:dependency", VERIFY.NS).set("version", "9.0.0")
        with self.assertRaisesRegex(ValueError, "central pin"):
            VERIFY.verify_metadata(metadata, False, PINS)

    def test_unexpected_shipped_target_or_package_id_is_rejected(self):
        metadata = package_metadata()
        metadata.find("n:metadata/n:dependencies/n:group", VERIFY.NS).set("targetFramework", "net11.0")
        with self.assertRaisesRegex(ValueError, "net10.0 dependency group"):
            VERIFY.verify_metadata(metadata, False, PINS)
        with self.assertRaisesRegex(ValueError, "Expected package ID"):
            VERIFY.verify_metadata(package_metadata(True), False, PINS)

    def test_package_must_record_the_clean_checkout_source(self):
        metadata = package_metadata()
        namespace = "{http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd}"
        ET.SubElement(metadata.find(f"{namespace}metadata"), f"{namespace}repository", commit="expected-source")
        VERIFY.verify_repository_source(metadata, "candidate.nupkg", "expected-source")
        with self.assertRaisesRegex(ValueError, "source SHA"):
            VERIFY.verify_repository_source(metadata, "candidate.nupkg", "other-source")

    def test_restored_package_must_bind_its_asset_sha512_and_input_sha256(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            package = root / "package/1.0/package.1.0.nupkg"
            package.parent.mkdir(parents=True)
            package.write_bytes(b"verified package")
            sha512 = base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode()
            assets = {"libraries": {"Package/1.0": {"path": "package/1.0", "sha512": sha512}},
                      "packageFolders": {str(root): {}}}
            VERIFY.verify_restored_bytes(assets, "Package", "1.0", VERIFY.digest(package))
            with self.assertRaisesRegex(ValueError, "bytes"):
                VERIFY.verify_restored_bytes(assets, "Package", "1.0", "different-input-hash")
            assets["libraries"]["Package/1.0"]["sha512"] = "sha512-stale"
            with self.assertRaisesRegex(ValueError, "SHA-512"):
                VERIFY.verify_restored_bytes(assets, "Package", "1.0", VERIFY.digest(package))

    def test_incomplete_report_replaces_prior_success_and_keeps_partial_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp)
            (output / "results.json").write_text('{"completed": true, "checks": [{"name": "old"}]}')
            report = {"source": "current", "checks": []}
            VERIFY.initialize_report(output, report)
            report["checks"].append({"name": "current-first", "passed": True})
            VERIFY.write_report(output, report)
            actual = json.loads((output / "results.json").read_text())
            self.assertFalse(actual["completed"])
            self.assertEqual(actual["checks"], [{"name": "current-first", "passed": True}])


if __name__ == "__main__":
    unittest.main()
