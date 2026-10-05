#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Negative dependency-cohort cases for the independent package consumer gate."""

from copy import deepcopy
import importlib.util
from pathlib import Path
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


if __name__ == "__main__":
    unittest.main()
