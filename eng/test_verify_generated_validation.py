#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Guard regressions for independent generated package acceptance evidence."""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

_spec = importlib.util.spec_from_file_location("generated_validation_gate", Path(__file__).with_name("verify-generated-validation.py"))
VERIFY = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(VERIFY)


class GeneratedGateTests(unittest.TestCase):
    def test_all_cases_are_required_exactly_once(self):
        records = [json.dumps({"case": name, "passed": True}) for name in VERIFY.CASES]
        VERIFY.verify_runtime("\n".join(records))
        for lines in (records[:-1], records + records[:1], [line.replace('true', 'false') for line in records]):
            with self.subTest(lines=lines), self.assertRaises(ValueError):
                VERIFY.verify_runtime("\n".join(lines))

    def test_negative_requires_expected_actionable_generator_diagnostic(self):
        output = "Negative.cs(42,9): error RUVG001: Unsupported selector. Use an explicit observable or ValidationRuleUnsafe."
        VERIFY.verify_negative(1, output, "RUVG001")
        secondary = "Negative.cs(42,25): error RUVG005: No generated interceptor. Use an explicit observable or choose Unsafe."
        VERIFY.verify_negative(1, output + "\n" + secondary, "RUVG001")
        for code, text in ((0, output), (1, "error NU1301: Unable to load feed"),
                           (1, output.replace("RUVG001", "CS1234")),
                           (1, secondary),
                           (1, output + "\n" + secondary.replace("42,25", "43,25")),
                           (1, output + "\n" + secondary.replace("Negative.cs", "Other.cs")),
                           (1, output + "\n" + secondary.replace("RUVG005", "RUVG002")),
                           (1, output.replace("Negative.cs", "WrongNegative.cs")),
                           (1, output + "\nApp.cs(1,1): error CS1002: ; expected"),
                           (1, output.replace("observable", "alternative")),
                           (1, output.replace("ValidationRuleUnsafe", "another method"))):
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_negative(code, text, "RUVG001")

    def test_package_must_bundle_built_analyzer_and_transitive_allowlist(self):
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "candidate.nupkg"
            analyzer = "analyzers/dotnet/roslyn5.0/cs/ReactiveUI.Validation.SourceGenerators.dll"
            props = "buildTransitive/Runic.ReactiveUI.Validation.props"
            optin = '<Project><PropertyGroup><InterceptorsNamespaces>$(InterceptorsNamespaces);ReactiveUI.Validation.Generated</InterceptorsNamespaces></PropertyGroup></Project>'
            for assets in ({analyzer: b"assembly" * 200, props: optin}, {},
                           {analyzer: b"assembly" * 200}, {analyzer: b"stub", props: optin},
                           {analyzer: b"assembly" * 200, props: optin.replace('$(InterceptorsNamespaces);', '')},
                           {analyzer: b"assembly" * 200, props: optin.replace('ReactiveUI.Validation.Generated', 'Other.Generated')}):
                with zipfile.ZipFile(package, "w") as archive:
                    for name, content in assets.items():
                        archive.writestr(name, content)
                if assets == {analyzer: b"assembly" * 200, props: optin}:
                    VERIFY.verify_generator_assets(package)
                else:
                    with self.subTest(assets=list(assets)), self.assertRaises(ValueError):
                        VERIFY.verify_generator_assets(package)

    def test_external_analyzer_must_emit_all_interceptors_without_reflection(self):
        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp)
            project = folder / "App.csproj"
            generated = folder / "obj/generated/ReactiveUI.Validation.SourceGenerators/ReactiveUI.Validation.SourceGenerators.ValidationGenerator"
            generated.mkdir(parents=True)
            source = generated / "ValidationInterceptors.g.cs"
            marker = '[global::System.Runtime.CompilerServices.InterceptsLocation(1, "site")]'
            headers = []
            for family in ("rule", "context", "state"):
                for first in (False, True):
                    for second in (False, True):
                        if family == "rule":
                            parameters = ["object @viewModel", "bool @isPropertyValid", ("global::System.Func<string, string>" if second else "string") + " @message"]
                            if first: parameters.insert(1, "object @context")
                        elif family == "context":
                            parameters = ["object @contextProperty", "object @viewProperty" if second else "object @action"]
                            if first: parameters.append("object @viewModelProperty")
                        else:
                            parameters = ["object @converter", "object @viewProperty" if second else "object @onNext"]
                            if first: parameters.append("object @modelProperty")
                        headers.append(f"internal static object Intercept{len(headers)}({', '.join(parameters)})")
            for kind in ("helper", "property", "model"):
                for formatter in (False, True):
                    parameters = ["object @view", "object @viewProperty"]
                    if kind != "model": parameters.append("object @viewModelHelperProperty" if kind == "helper" else "object @viewModelProperty")
                    if formatter: parameters.append("object @formatter")
                    headers.append(f"internal static object Intercept{len(headers)}({', '.join(parameters)})")
            normal = 'namespace ReactiveUI.Validation.Generated;\n' + '\n'.join(marker + '\n' + header for header in headers)
            source.write_text(normal)
            retained = folder / "Primitives-managed-generated"
            retained.mkdir()
            (retained / "StalePreviousRun.g.cs").write_text("// stale")
            incidental = generated / "IncidentalSdkOutput.g.cs"
            incidental.write_text("// unrelated SDK output")
            evidence = VERIFY.verify_generated_sources(project, retained)
            self.assertEqual(len(evidence["overloads"]), 18)
            self.assertEqual(evidence["sourceCount"], 1)
            self.assertEqual(len(evidence["files"]), 1)
            record = evidence["files"][0]
            copied = folder / record["path"]
            self.assertEqual(copied.read_text(), normal)
            self.assertEqual(record["sha256"], VERIFY.NATIVE.digest(copied))
            self.assertEqual(list(retained.rglob("IncidentalSdkOutput.g.cs")), [])
            self.assertEqual(list(retained.rglob("StalePreviousRun.g.cs")), [])
            for text in (normal.replace(marker, '', 1), normal + 'expression.Compile();', normal + 'view.BindValidationUnsafe();', normal + '#pragma warning disable IL2026', normal + 'model.WhenAnyValue(x => x.Name);', normal.replace("@context", "@unrelated")):
                source.write_text(text)
                with self.subTest(text=text[-100:]), self.assertRaises(ValueError):
                    VERIFY.verify_generated_sources(project)

    def test_roslyn_cannot_be_a_consumer_dependency_or_runtime_asset(self):
        normal = {"libraries": {"ReactiveUI/26.0.1": {}}, "targets": {"net10.0": {"ReactiveUI/26.0.1": {"runtime": {"lib/net10.0/ReactiveUI.dll": {}}}}}}
        VERIFY.verify_no_roslyn_runtime(normal)
        for graph in ({"libraries": {"Microsoft.CodeAnalysis.CSharp/5.0.0": {}}, "targets": {}},
                      {"libraries": {}, "targets": {"net10.0": {"Library/1.0": {"compile": {"Microsoft.CodeAnalysis.dll": {}}}}}},
                      {"libraries": {}, "targets": {"net10.0": {"Library/1.0": {"runtime": {"ReactiveUI.Validation.SourceGenerators.dll": {}}}}}}):
            with self.subTest(graph=graph), self.assertRaises(ValueError):
                VERIFY.verify_no_roslyn_runtime(graph)

    def test_positive_consumer_cannot_switch_to_unsafe_or_suppressions(self):
        for text in ('view.BindValidationUnsafe();', 'model.ValidationRuleUnsafe();', '#pragma warning disable IL3050'):
            with tempfile.TemporaryDirectory() as temp:
                folder = Path(temp)
                (folder / "Program.cs").write_text(text)
                with self.subTest(text=text), self.assertRaises(ValueError):
                    VERIFY.guard_consumer_sources(folder)

    def test_incomplete_report_replaces_prior_success_and_keeps_partial_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp)
            (output / "results.json").write_text(json.dumps({"completed": True, "checks": [{"name": "old"}]}))
            report = {"source": "current", "checks": []}
            VERIFY.initialize_report(output, report)
            report["checks"].append({"name": "current-first", "passed": True})
            VERIFY.write_report(output, report)
            actual = json.loads((output / "results.json").read_text())
            self.assertFalse(actual["completed"])
            self.assertEqual(actual["checks"], [{"name": "current-first", "passed": True}])


if __name__ == "__main__":
    unittest.main()
