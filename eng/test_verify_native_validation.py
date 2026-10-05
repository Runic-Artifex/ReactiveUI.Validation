#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Adversarial cases for strict native evidence, not native compiler simulations."""

from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


VERIFY = load("verify_native_validation", "verify-native-validation.py")
CORE_TESTS = load("package_test_fixtures", "test_verify_packages.py")


class NativeGateTests(unittest.TestCase):
    def test_baseline_requires_both_validation_diagnostics(self):
        lines = [f"Program.cs(42,9): error {code}: Using member 'ReactiveUI.Validation.Extensions.{method}' requires contract" for code in ("IL2026", "IL3050") for method in ("ValidationRule", "BindValidationState")]
        VERIFY.verify_expected_failure(1, "\n".join(lines))
        for code, text in ((0, "\n".join(lines)), (1, lines[0]),
                           (1, "error NU1301: Unable to load service index"),
                           (1, "\n".join(lines).replace("ReactiveUI.Validation", "Other.Library")),
                           (1, "\n".join(lines) + "\nProgram.cs(1,1): error CS1002: ; expected"),
                           (1, "\n".join(lines) + "\nOther.cs(1,1): error IL2026: Using member 'Other.Unsafe'"),
                           (1, "\n".join(lines) + "\nProgram.cs(1,1): error IL3050: Using member 'ReactiveUI.Validation.OtherUnsafe'")):
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_expected_failure(code, text)

    def test_native_targets_are_both_checked(self):
        graph = CORE_TESTS.consumer_graph()
        graph["targets"]["net10.0/linux-x64"] = deepcopy(graph["targets"]["net10.0"])
        VERIFY.verify_native_graph(graph, False, CORE_TESTS.PINS, CORE_TESTS.VERSION, "linux-x64")
        del graph["targets"]["net10.0/linux-x64"][f"Runic.DynamicData/{CORE_TESTS.PINS['runic.dynamicdata']}"]
        with self.assertRaisesRegex(ValueError, "Missing"):
            VERIFY.verify_native_graph(graph, False, CORE_TESTS.PINS, CORE_TESTS.VERSION, "linux-x64")
        graph = CORE_TESTS.consumer_graph()
        with self.assertRaisesRegex(ValueError, "exact consumer targets"):
            VERIFY.verify_native_graph(graph, False, CORE_TESTS.PINS, CORE_TESTS.VERSION, "linux-x64")

    def test_any_project_reference_is_rejected(self):
        graph = CORE_TESTS.consumer_graph()
        graph["libraries"]["ApplicationAdapter/1.0"] = {"type": "project"}
        with self.assertRaisesRegex(ValueError, "project references"):
            VERIFY.verify_native_graph(graph, False, CORE_TESTS.PINS, CORE_TESTS.VERSION)

    def test_suppression_and_preservation_escape_hatches_are_rejected(self):
        for name, source in (("App.csproj", '<Project><PropertyGroup><NoWarn>IL2026</NoWarn></PropertyGroup></Project>'),
                             ("App.csproj", '<Project><ItemGroup><ProjectReference Include="../../src/Library.csproj" /></ItemGroup></Project>'),
                             ("App.csproj", '<Project><ItemGroup><TrimmerRootAssembly Include="Library" /></ItemGroup></Project>'),
                             ("App.cs", '#pragma warning disable IL2026'),
                             ("App.cs", '#pragma warning disable 2026'),
                             ("App.csproj", '<Project><PropertyGroup><RunAnalyzers>false</RunAnalyzers></PropertyGroup></Project>'),
                             ("App.csproj", '<Project><PropertyGroup><EnableAotAnalyzer>false</EnableAotAnalyzer></PropertyGroup></Project>'),
                             ("App.cs", '[DynamicDependency("Setter")]'),
                             (".editorconfig", 'dotnet_diagnostic.IL2026.severity = warning')):
            with self.subTest(source=source), tempfile.TemporaryDirectory() as temp:
                folder = Path(temp)
                (folder / name).write_text(source)
                with self.assertRaises(ValueError):
                    VERIFY.guard_consumer_sources(folder)

    def test_all_required_cases_must_pass_exactly_once(self):
        records = [json.dumps({"case": case, "passed": True}) for case in VERIFY.CASES]
        VERIFY.verify_runtime("\n".join(records))
        for lines in (records[:-1], records + records[:1], [line.replace('true', 'false') for line in records]):
            with self.subTest(lines=lines), self.assertRaises(ValueError):
                VERIFY.verify_runtime("\n".join(lines))

    def test_same_version_stale_cache_is_not_package_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            folder = root / "package/1.0"
            folder.mkdir(parents=True)
            package = folder / "package.1.0.nupkg"
            package.write_bytes(b"stale package")
            assets = {"libraries": {"Package/1.0": {"path": "package/1.0"}}, "packageFolders": {str(root): {}}}
            VERIFY.verify_restored_bytes(assets, "Package", "1.0", VERIFY.digest(package))
            with self.assertRaisesRegex(ValueError, "bytes do not match"):
                VERIFY.verify_restored_bytes(assets, "Package", "1.0", "new-package-hash")


if __name__ == "__main__":
    unittest.main()
