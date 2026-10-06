#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Failure guards prevent unrelated errors and missing generation from passing A05."""

import importlib.util
import base64
import hashlib
from pathlib import Path
import tempfile
import unittest
import zipfile

SPEC = importlib.util.spec_from_file_location("configuration", Path(__file__).with_name("verify-generated-configuration.py"))
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)


class ConfigurationFailureGuards(unittest.TestCase):
    def test_expected_diagnostics_require_the_exact_boundary_and_source(self):
        valid = "Program.cs(3,1): error RUVG004: Use the matching validation runtime package with its bundled analyzer [Control.csproj]"
        self.assertEqual(VERIFY.verify_diagnostics(1, valid, {"RUVG004"}, {"RUVG004", "RUVG005"}, "Program.cs")[0][0], "RUVG004")
        bad = [(0, valid), (1, valid.replace("error", "warning")),
               (1, valid.replace("Program.cs", "Other.cs")),
               (1, valid.replace("RUVG004", "CS0234")),
               (1, valid.replace("matching validation runtime", "unrelated")),
               (1, valid + "\nCSC : error CS8032: Analyzer failed to load"),
               (1, valid + "\nMSBUILD : error : unrelated uncoded failure"),
               (1, valid + "\nUnhandled exception. System.InvalidOperationException: unrelated"),
               (1, valid + "\nProcess terminated. unrelated"),
               (1, "You must install or update .NET to run this application.")]
        for code, text in bad:
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_diagnostics(code, text, {"RUVG004"}, {"RUVG004", "RUVG005"}, "Program.cs")
        compiler = "CSC : error CS9057: The analyzer assembly 'ReactiveUI.Validation.SourceGenerators.dll' references newer compiler 5.9.0.0 than 5.0.0.0"
        self.assertEqual(VERIFY.verify_diagnostics(1, compiler, {"CS9057"}, {"CS9057"}, "ReactiveUI.Validation.SourceGenerators.dll")[0][0], "CS9057")
        self.assertEqual(VERIFY.verify_diagnostics(1, compiler.removeprefix("CSC : "), {"CS9057"}, {"CS9057"}, "ReactiveUI.Validation.SourceGenerators.dll")[0][0], "CS9057")
        entries = ("tasks/netcore/bincore/csc.dll", "tasks/netcore/bincore/csc.runtimeconfig.json",
                   "tasks/netcore/bincore/csc.deps.json", "tasks/netcore/bincore/Microsoft.CodeAnalysis.dll",
                   "tasks/netcore/bincore/Microsoft.CodeAnalysis.CSharp.dll",
                   "tasks/netcore/Microsoft.Build.Tasks.CodeAnalysis.dll",
                   "tasks/netcore/Microsoft.CSharp.Core.targets", "build/Microsoft.Net.Compilers.Toolset.props")
        with tempfile.TemporaryDirectory() as temporary:
            toolset = Path(temporary)
            archive = toolset / f"microsoft.net.compilers.toolset.{VERIFY.TOOLSET_VERSION}.nupkg"
            with zipfile.ZipFile(archive, "w") as package:
                for entry in entries:
                    path = toolset / entry
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(entry.encode())
                    package.write(path, entry)
            sha = archive.with_suffix(".nupkg.sha512")
            sha.write_text(base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode())
            self.assertEqual(len(VERIFY.bind_compiler_archive(toolset)["boundEntries"]), len(entries))
            for entry in entries:
                path = toolset / entry
                original = path.read_bytes()
                for mutation in (b"stale compiler input", None):
                    if mutation is None:
                        path.unlink()
                    else:
                        path.write_bytes(mutation)
                    with self.subTest(entry=entry, mutation=mutation), self.assertRaises((ValueError, FileNotFoundError)):
                        VERIFY.bind_compiler_archive(toolset)
                    path.write_bytes(original)
            sha.write_text("invalid archive SHA512")
            with self.assertRaises(ValueError):
                VERIFY.bind_compiler_archive(toolset)
            inputs = {"language": ["14.0"], "defines": ["TRACE,RELEASE"],
                      "allowlist": ["ReactiveUI.Validation.Generated"], "reference": [r"C:\path with spaces\runtime.dll"],
                      "analyzer": [r"C:\path with spaces\analyzer.dll"], "source": [r"C:\path with spaces\Program.cs"]}
            response = VERIFY.compiler_response(toolset, inputs).read_text()
            self.assertIn('/reference:"C:\\path with spaces\\runtime.dll"', response)
            self.assertNotIn("/noconfig", response)
            with self.assertRaises(ValueError):
                VERIFY.compiler_response(toolset, dict(inputs, source=['bad"source.cs']))

    def test_missing_analyzer_requires_the_actual_actionable_runtime_failure(self):
        message = VERIFY.MISSING_PREFIX + " Use the generator, supply IValidationPlanProvider or attach ValidationPlanRegistry. Explicit Unsafe APIs remain available."
        VERIFY.verify_missing_dispatch(17, message)
        for code, text in [(0, message), (1, message), (17, "CONFIG_FAILURE|unrelated failure"),
                           (17, message.replace("Unsafe", "")), (17, message + "\n" + message),
                           (17, VERIFY.GENERATED_OK)]:
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_missing_dispatch(code, text)
        VERIFY.verify_success(0, VERIFY.GENERATED_OK)
        with self.assertRaises(ValueError):
            VERIFY.verify_success(0, message)
        # Restore may succeed while input capture fails. Keep its actual graph
        # and imports before the caller propagates the missing-capture failure.
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary) / "consumer"
            output = Path(temporary) / "retained"
            (folder / "obj").mkdir(parents=True)
            output.mkdir()
            assets = folder / "obj/project.assets.json"
            props = folder / "obj/Control.csproj.nuget.g.props"
            assets.write_text('{"targets": {"net10.0": {}}}')
            props.write_text('<Project><Import Project="actual-package.props" /></Project>')
            evidence = VERIFY.retain_inputs(folder, output, "failed-capture", {})
            with self.assertRaises(ValueError):
                VERIFY.read_inputs(folder)
            self.assertEqual({Path(item["file"]).name.split("-", 1)[1] for item in evidence["files"]},
                             {"project.assets.json", "Control.csproj.nuget.g.props"})
            for item in evidence["files"]:
                self.assertEqual(VERIFY.digest(output / item["file"]), item["sha256"])


if __name__ == "__main__":
    unittest.main()
