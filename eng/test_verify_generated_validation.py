#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Guard regressions for independent generated package acceptance evidence."""

import importlib.util
import copy
import base64
import hashlib
from unittest.mock import patch
import json
from pathlib import Path
import shutil
import tempfile
import unittest
import zipfile

_spec = importlib.util.spec_from_file_location("generated_validation_gate", Path(__file__).with_name("verify-generated-validation.py"))
VERIFY = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(VERIFY)


class GeneratedGateTests(unittest.TestCase):
    def test_published_application_is_retained_before_disposable_runtime_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            publish = root / "publish"
            publish.mkdir()
            output = root / "evidence"
            executable = publish / "GeneratedValidation.Primitives"
            executable.write_bytes(b"actual published executable")
            linked = publish / "GeneratedValidation.Primitives.dll"
            linked.write_bytes(b"actual post-link PE differs from compiler PE")
            (publish / "GeneratedValidation.Primitives.deps.json").write_text('{"published":true}')
            (publish / "unrelated-cache.dll").write_bytes(b"not an application artifact")
            evidence = VERIFY.retain_published_application(executable, "GeneratedValidation.Primitives", output, "Primitives-trimmed")
            shutil.rmtree(publish)
            self.assertTrue(evidence["postLink"])
            self.assertEqual(len(evidence["files"]), 3)
            for record in evidence["files"]:
                path = output / record["file"]
                self.assertEqual(VERIFY.NATIVE.digest(path), record["sha256"])
                self.assertEqual(path.stat().st_size, record["bytes"])
            self.assertEqual((output / evidence["executable"]).read_bytes(), b"actual published executable")
            self.assertEqual((output / "Primitives-trimmed-runtime/GeneratedValidation.Primitives.dll").read_bytes(),
                             b"actual post-link PE differs from compiler PE")
            with self.assertRaises(ValueError):
                VERIFY.retain_published_application(executable, "GeneratedValidation.Primitives", output, "missing")

    def test_precompiled_peer_retains_both_real_normal_call_and_callable_families(self):
        with tempfile.TemporaryDirectory() as temporary:
            binary = Path(temporary) / "Peer.dll"
            binary.write_bytes(b"separately compiled peer")
            prefix = "GeneratedValidation.Precompiled.PeerRules."
            for reactive in (False, True):
                root = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
                calls = []
                for name, opcode, form in (("Rule", "call", "Expression"), ("Callable", "ldftn", "Expression"),
                                           ("DelegateRule", "call", "Func"), ("DelegateCallable", "ldftn", "Func")):
                    selector = "System.Func`2<!!0,!!1>"
                    if form == "Expression":
                        selector = "System.Linq.Expressions.Expression`1<" + selector + ">"
                    calls.append({"method": prefix + name, "opcode": opcode, "owner": root + ".Extensions.ValidatableViewModelExtensions",
                                  "assembly": root, "target": "ValidationRule", "returnType": root + ".Helpers.ValidationHelper",
                                  "parameters": ["!!0", selector, "System.Func`2<!!1,System.Boolean>", "System.String"]})
                evidence = {"completed": True, "path": str(binary), "sha256": VERIFY.NATIVE.digest(binary), "mvid": "actual-MVID",
                            "methods": [{"method": call["method"], "ilSha256": "a" * 64} for call in calls], "normalApiCalls": calls}
                self.assertEqual(VERIFY.verify_precompiled_normal_il(evidence, binary, reactive), calls)
                for key, value in (("opcode", "call"), ("owner", "Generated.Interceptor"), ("assembly", "Other"),
                                   ("target", "ValidationRuleUnsafe"), ("parameters", calls[0]["parameters"])):
                    changed = copy.deepcopy(evidence)
                    changed["normalApiCalls"][3][key] = value
                    with self.subTest(reactive=reactive, key=key), self.assertRaises(ValueError):
                        VERIFY.verify_precompiled_normal_il(changed, binary, reactive)
                for key, value in (("sha256", "stale"), ("mvid", ""), ("methods", []), ("normalApiCalls", calls[:-1]),
                                   ("normalApiCalls", calls + [calls[0]])):
                    with self.subTest(reactive=reactive, key=key), self.assertRaises(ValueError):
                        VERIFY.verify_precompiled_normal_il({**evidence, key: value}, binary, reactive)

    def test_original_caller_il_controls_bind_pe_and_real_array_factory_and_entry(self):
        with tempfile.TemporaryDirectory() as temporary:
            binary = Path(temporary) / "Consumer.dll"
            binary.write_bytes(b"fixture PE identity")
            evidence = {"completed": True, "path": str(binary), "sha256": VERIFY.NATIVE.digest(binary), "mvid": "actual-mvid",
                        "methods": [{"method": "RuleCapabilityCases.IndexedBoundary.Model.Check", "token": 1, "ilSha256": "a" * 64}],
                        "arrayFactories": [], "entryCalls": []}
            VERIFY.verify_expression_il(evidence, binary)
            for key, value in (("sha256", "stale"), ("arrayFactories", None), ("methods", []), ("mvid", "")):
                with self.subTest(key=key), self.assertRaises(ValueError):
                    VERIFY.verify_expression_il({**evidence, key: value}, binary)
            call = {"method": "RuleExpressionParamsNegative.Program.Run", "opcode": "call",
                    "owner": "System.Linq.Expressions.Expression", "target": "NewArrayInit", "assembly": "System.Linq.Expressions",
                    "returnType": "System.Linq.Expressions.NewArrayExpression", "parameters": ["System.Type", "System.Linq.Expressions.Expression[]"]}
            negative = {**evidence, "arrayFactories": [call]}
            VERIFY.verify_expression_il(negative, binary, negative=True)
            with self.assertRaises(ValueError):
                VERIFY.verify_expression_il(negative, binary)
            for key, value in (("owner", "Other.Expression"), ("method", "Dead.Run"), ("target", "NewArrayBounds"),
                               ("assembly", "Fake.Expressions"), ("parameters", ["System.Type", "System.Object[]"])):
                with self.subTest(key=key), self.assertRaises(ValueError):
                    VERIFY.verify_expression_il({**negative, "arrayFactories": [{**call, key: value}]}, binary, negative=True)
            entry = {"method": "Program.Main", "opcode": "call", "owner": "RuleExpressionParamsNegative.Program", "target": "Run"}
            VERIFY.verify_expression_il({**evidence, "entryCalls": [entry]}, binary, host=True)
            for calls in ([], [entry, entry], [{**entry, "method": "Unused.Helper"}], [{**entry, "opcode": "ldftn"}]):
                with self.subTest(calls=calls), self.assertRaises(ValueError):
                    VERIFY.verify_expression_il({**evidence, "entryCalls": calls}, binary, host=True)

    def test_aot_globals_must_be_delivered_from_actual_hashed_compiler_configs(self):
        with tempfile.TemporaryDirectory() as temporary:
            project = Path(temporary) / "App.csproj"
            (project.parent / "obj/generated").mkdir(parents=True)
            semantics = self.semantic_fixture(project)
            manifest = Path(semantics["manifest"]["path"])
            records = [line.split("|", 1) for line in manifest.read_text().splitlines()]
            VERIFY.verify_aot_configuration(records, semantics)
            config = Path(semantics["analyzerConfigs"][0]["path"])
            original = config.read_text()
            for changed in (original.replace("PublishAot =", "MissingPublishAot ="),
                            original.replace("PublishAot =", "PublishAot = true"),
                            original + "build_property.PublishAot =\n",
                            original + "build_property.EnableAotAnalyzer = true\n",
                            original.replace("is_global = true", "is_global = false"),
                            original.replace("is_global = true", "is_global = true\nis_global = false"),
                            original.replace("is_global = true", "is_global = true\n[NeverMatches/*.cs]")):
                config.write_text(changed)
                current = copy.deepcopy(semantics)
                current["analyzerConfigs"][0]["sha256"] = VERIFY.NATIVE.digest(config)
                with self.subTest(config=changed), self.assertRaises(ValueError):
                    VERIFY.verify_aot_configuration(records, current)
            config.write_text(original)
            for key in ("publishAot", "isAotCompatible", "enableAotAnalyzer"):
                with self.subTest(missing=key), self.assertRaises(ValueError):
                    VERIFY.verify_aot_configuration([item for item in records if item[0] != key], semantics)
            stale = copy.deepcopy(semantics)
            stale["analyzerConfigs"][0]["sha256"] = "stale"
            with self.assertRaises(ValueError):
                VERIFY.verify_aot_configuration(records, stale)
            absent = copy.deepcopy(semantics)
            absent["analyzerConfigs"] = []
            with self.assertRaises(ValueError):
                VERIFY.verify_aot_configuration(records, absent)
            records = [[kind, "true" if kind == "publishAot" else value] for kind, value in records]
            with self.assertRaises(ValueError):
                VERIFY.verify_aot_configuration(records, semantics)
            records.append(["define", "GENERATED_GATE_NATIVE"])
            config.write_text(original.replace("PublishAot =", "PublishAot = true"))
            semantics["aotProperties"]["publishAot"] = "true"
            semantics["analyzerConfigs"][0]["sha256"] = VERIFY.NATIVE.digest(config)
            VERIFY.verify_aot_configuration(records, semantics)

    def test_sdk_warning_defaults_require_exact_stage_and_byte_proven_origin(self):
        baseline = ["1701", "1702", "8002"]
        origin = {"verified": True, "archiveSha256": VERIFY.ILLINK_ARCHIVE_SHA256,
                  "targetSha256": VERIFY.ILLINK_TARGET_SHA256}
        VERIFY.verify_warning_policy(baseline, "", "", ["GENERATED_GATE_MANAGED"])
        with self.assertRaises(ValueError):
            VERIFY.verify_warning_policy([*baseline, "IL2121"], "", "", ["GENERATED_GATE_MANAGED"], origin)
        for stage in ("TRIMMED", "NATIVE"):
            defines = [f"GENERATED_GATE_{stage}"]
            flags = VERIFY.NATIVE.strict_positive_publish_flags(stage.lower())
            self.assertIn("-p:PublishTrimmed=true", flags)
            self.assertIn(f"-p:PublishAot={'true' if stage == 'NATIVE' else 'false'}", flags)
            self.assertFalse(any("NoWarn" in flag or "RedundantSuppressions" in flag for flag in flags))
            expected = [*baseline, "IL2121"]
            VERIFY.verify_warning_policy(expected, "true", "", defines, origin)
            for invalid in (None, {}, {**origin, "verified": False}, {**origin, "targetSha256": "wrong"},
                            {**origin, "archiveSha256": "wrong"}):
                with self.subTest(stage=stage, origin=invalid), self.assertRaises(ValueError):
                    VERIFY.verify_warning_policy(expected, "true", "", defines, invalid)
            for trimmed, redundant in (("", ""), ("false", ""), ("true", "false"), ("true", "true")):
                with self.subTest(stage=stage, trimmed=trimmed, redundant=redundant), self.assertRaises(ValueError):
                    VERIFY.verify_warning_policy(expected, trimmed, redundant, defines, origin)
            for code in ("IL2026", "IL3050", "2026", "IL9999"):
                with self.subTest(stage=stage, code=code), self.assertRaises(ValueError):
                    VERIFY.verify_warning_policy(sorted([*expected, code]), "true", "", defines, origin)
            with tempfile.TemporaryDirectory() as temporary:
                project = Path(temporary) / "App.csproj"
                generated = project.parent / "obj/generated/ReactiveUI.Validation.SourceGenerators/Owner/Bridge.cs"
                generated.parent.mkdir(parents=True)
                generated.write_text("// exact bridge")
                semantics = self.semantic_fixture(project)
                manifest = Path(semantics["manifest"]["path"])
                manifest.write_text(manifest.read_text().replace("define|NET10_0", f"define|GENERATED_GATE_{stage}\ndefine|NET10_0")
                                    .replace("publishTrimmed|\n", "publishTrimmed|true\n")
                                    .replace("publishAot|\n", f"publishAot|{'true' if stage == 'NATIVE' else 'false'}\n")
                                    .replace("ilLinkTarget|\n", "ilLinkTarget|actual-target\n") + "nowarn|IL2121\n")
                config = Path(semantics["analyzerConfigs"][0]["path"])
                config.write_text(config.read_text().replace("PublishAot =", f"PublishAot = {'true' if stage == 'NATIVE' else 'false'}"))
                semantics["aotProperties"]["publishAot"] = "true" if stage == "NATIVE" else "false"
                semantics["analyzerConfigs"][0]["sha256"] = VERIFY.NATIVE.digest(config)
                semantics.update(defines=sorted(["NET10_0", *defines]), noWarn=expected,
                                 publishTrimmed="true", ilLinkTarget="actual-target")
                semantics["manifest"]["sha256"] = VERIFY.NATIVE.digest(manifest)
                with patch.object(VERIFY, "verify_sdk_warning_origin", return_value=origin) as reader:
                    VERIFY.verify_semantic_inputs(project, semantics)
                    reader.assert_called_once_with(project, "actual-target")
                semantics["publishTrimmed"] = "false"
                with self.assertRaises(ValueError):
                    VERIFY.verify_semantic_inputs(project, semantics)
        with self.assertRaises(ValueError):
            VERIFY.NATIVE.strict_positive_publish_flags("managed")

    def test_sdk_warning_target_cannot_be_missing_tampered_or_from_another_package(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            project = root / "App.csproj"
            cache = root / "cache"
            package_root = cache / "microsoft.net.illink.tasks/10.0.12"
            target = package_root / "build/Microsoft.NET.ILLink.targets"
            target.parent.mkdir(parents=True)
            target.write_bytes(b"exact pinned SDK default target")
            dll = package_root / "tools/ILLink.dll"
            dll.parent.mkdir()
            dll.write_bytes(b"exact tool bytes")
            archive = package_root / "microsoft.net.illink.tasks.10.0.12.nupkg"
            with zipfile.ZipFile(archive, "w") as output:
                output.write(target, "build/Microsoft.NET.ILLink.targets")
                output.write(dll, "tools/ILLink.dll")
                output.writestr(".signature.p7s", "synthetic signed-package fixture")
            raw_hash = base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode()
            signed_hash = base64.b64encode(hashlib.sha512(b"unsigned content fixture").digest()).decode()
            archive.with_suffix(".nupkg.sha512").write_text(raw_hash)
            (package_root / ".nupkg.metadata").write_text(json.dumps({"contentHash": signed_hash}))
            assets = {"packageFolders": {str(cache): {}}, "libraries": {"Microsoft.NET.ILLink.Tasks/10.0.12": {
                "path": "microsoft.net.illink.tasks/10.0.12", "type": "package",
                "sha512": signed_hash}}}
            graph = root / "obj/project.assets.json"
            graph.parent.mkdir()
            graph.write_text(json.dumps(assets))
            with patch.object(VERIFY, "ILLINK_ARCHIVE_SHA256", VERIFY.NATIVE.digest(archive)), \
                    patch.object(VERIFY, "ILLINK_TARGET_SHA256", VERIFY.NATIVE.digest(target)), \
                    patch.object(VERIFY, "ILLINK_ARCHIVE_SHA512", raw_hash), \
                    patch.object(VERIFY, "ILLINK_CONTENT_SHA512", signed_hash):
                self.assertTrue(VERIFY.verify_sdk_warning_origin(project, str(target))["verified"])
                original_archive = archive.read_bytes()
                archive.write_bytes(original_archive + b"tampered archive")
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(target))
                archive.write_bytes(original_archive)
                assets["libraries"]["Microsoft.NET.ILLink.Tasks/10.0.12"]["sha512"] = raw_hash
                graph.write_text(json.dumps(assets))
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(target))
                assets["libraries"]["Microsoft.NET.ILLink.Tasks/10.0.12"]["sha512"] = signed_hash
                graph.write_text(json.dumps(assets))
                metadata_file = package_root / ".nupkg.metadata"
                metadata_file.write_text(json.dumps({"contentHash": "tampered"}))
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(target))
                metadata_file.write_text(json.dumps({"contentHash": signed_hash}))
                archive.with_suffix(".nupkg.sha512").write_text("tampered")
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(target))
                archive.with_suffix(".nupkg.sha512").write_text(raw_hash)
                dll.write_bytes(b"tampered tool")
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(target))
                dll.write_bytes(b"exact tool bytes")
                other = root / "Microsoft.NET.ILLink.targets"
                other.write_bytes(target.read_bytes())
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(other))
                target.write_bytes(b"tampered extracted target")
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(target))
                target.unlink()
                with self.assertRaises(ValueError):
                    VERIFY.verify_sdk_warning_origin(project, str(target))
                assets["libraries"].clear()
                graph.write_text(json.dumps(assets))
                with self.assertRaises((ValueError, KeyError)):
                    VERIFY.verify_sdk_warning_origin(project, str(target))

    def test_successful_compile_retains_final_manifest_before_semantic_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            app = root / "app"
            app.mkdir()
            project = app / "Fixture.csproj"
            project.write_text("<Project />")
            source = app / "Original.cs"
            source.write_text("actual compiled caller")
            generated = app / "obj/generated/Validation/Bridge.cs"
            generated.parent.mkdir(parents=True)
            generated.write_text("actual compiled bridge")
            (app / "obj/generated-compiler-input.before.txt").write_text(f"source|{source}\ndeclaredNowarn|\n")
            final = "language|14.0\nnullable|enable\nnowarn|1701\nnowarn|1702\nnowarn|8002\n"
            (app / "obj/generated-compiler-input.txt").write_text(final)
            output = root / "retained"
            evidence = VERIFY.retain_failed_compile(project, output, "Primitives-managed", capture="before-semantic")
            self.assertFalse(evidence["accepted"])
            self.assertTrue(evidence["effectiveSdkNoWarnCaptured"])
            shutil.rmtree(app)
            retained = [(output / row["file"]).read_text() for row in evidence["files"]]
            self.assertIn(final, retained)
            self.assertIn("actual compiled caller", retained)
            self.assertEqual((output / evidence["generatedSources"][0]["file"]).read_text(), "actual compiled bridge")
            self.assertTrue(all("before-semantic-inputs" in row["file"] for row in evidence["files"]))

    def test_failed_compile_retains_actual_sources_configs_graph_and_emissions(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            app = root / "app"
            app.mkdir()
            project = app / "Fixture.csproj"
            project.write_text("<Project />")
            source = app / "Broken.cs"
            source.write_text("actual broken caller")
            config = app / "compiler.editorconfig"
            config.write_text("actual compiler properties")
            reference = app / "Reference.dll"
            reference.write_bytes(b"exact selected reference")
            generated = app / "obj/generated/Validation/Bridge.cs"
            generated.parent.mkdir(parents=True)
            generated.write_text("actual partial generated bridge")
            (app / "obj/project.assets.json").write_text('{"actualRestore": true}')
            (app / "obj/generated-compiler-input.before.txt").write_text(
                f"source|{source}\nanalyzerConfig|{config}\nreference|{reference}\ndeclaredNowarn|\n")
            (app / "obj/generated-compiler-input.txt").write_text("stale prior successful-stage defaults")
            output = root / "retained"
            evidence = VERIFY.retain_failed_compile(project, output, "Primitives-managed")
            self.assertFalse(evidence["accepted"])
            self.assertFalse(evidence["effectiveSdkNoWarnCaptured"])
            self.assertTrue(evidence["compilerInputCaptured"])
            self.assertEqual(evidence["referencedBytes"][0]["sha256"], VERIFY.NATIVE.digest(reference))
            shutil.rmtree(app)
            retained = [(output / row["file"]).read_text() for row in evidence["files"]]
            self.assertIn("actual broken caller", retained)
            self.assertIn("actual compiler properties", retained)
            self.assertIn('{"actualRestore": true}', retained)
            self.assertNotIn("stale prior successful-stage defaults", retained)
            self.assertEqual((output / evidence["generatedSources"][0]["file"]).read_text(), "actual partial generated bridge")

    def test_configuration_inventory_rejects_missing_duplicate_failed_and_stale_controls(self):
        records = [{"name": f"Primitives-configuration-{case}", "passed": True,
                    "source": "current", "packageSha256": "exact"} for case in (
                        "generated", "excluded-analyzer", "removed-analyzer", "analyzers-disabled", "excluded-build",
                        "wrong-allowlist", "csharp13", "missing-runtime", "old-runtime", "older-compiler", "collision")]
        VERIFY.verify_configuration_results(records, "Primitives", "current", "exact")
        for changed in ([], records[:-1], [records[0], *records[1:-1], records[0]]):
            with self.assertRaises(ValueError):
                VERIFY.verify_configuration_results(changed, "Primitives", "current", "exact")
        for key, value in (("passed", False), ("source", "stale"), ("packageSha256", "other")):
            changed = copy.deepcopy(records)
            changed[0][key] = value
            with self.assertRaises(ValueError):
                VERIFY.verify_configuration_results(changed, "Primitives", "current", "exact")

    @staticmethod
    def readflow_fixture():
        declarations = []
        for number, (member, condition, return_type) in enumerate((
            ("get_Certain", "NotNull", "string?"), ("get_ReturnCertain", "NotNull", "string?"),
            ("get_Possible", "MaybeNull", "string"), ("get_Number", "NotNull", "int?"),
            ("CertainStorage", "NotNull", "string?"), ("PossibleStorage", "MaybeNull", "string"),
            ("NumberStorage", "NotNull", "int?"),
        )):
            owner = f"global::ReactiveUI.Validation.Generated.Consumer.Accessors.AccessBridge{number}"
            anchor = {"kind": "Method", "name": "Read" if member.startswith("get_") else "Storage",
                      "signature": f"{owner}.{member}", "containingType": owner, "returnType": return_type,
                      "parameters": [{"type": "global::AccessReadFlowSource"}],
                      "attributeContracts": [{"type": "global::System.Runtime.CompilerServices.UnsafeAccessorAttribute",
                                              "assembly": "System.Runtime, Version=10.0.0.0",
                                              "constructorArguments": [{"value": "1" if member.startswith("get_") else "3"}],
                                              "namedArguments": [{"name": "Name", "value": member}]}],
                      "returnAttributes": [{"type": f"global::System.Diagnostics.CodeAnalysis.{condition}Attribute",
                                            "assembly": "System.Runtime, Version=10.0.0.0"}]}
            declarations.append(anchor)
            if not member.startswith("get_"):
                read = copy.deepcopy(anchor)
                read.update(name="Read", signature=f"{owner}.Read", attributeContracts=[])
                declarations.append(read)
        return {"declarations": declarations}

    def test_readflow_requires_exact_selected_members_and_framework_return_contracts(self):
        evidence = VERIFY.verify_readflow_bridges(self.readflow_fixture())
        self.assertEqual(len(evidence), 7)
        self.assertEqual(next(row for row in evidence if row["member"] == "get_Possible")["condition"], "MaybeNull")
        for field, value in (("returnType", "object"), ("returnAttributes", []), ("parameters", [{"type": "global::OtherOwner"}])):
            changed = self.readflow_fixture()
            changed["declarations"][0][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                VERIFY.verify_readflow_bridges(changed)
        changed = self.readflow_fixture()
        changed["declarations"][0]["returnAttributes"][0]["assembly"] = "Consumer, Version=1.0.0.0"
        with self.assertRaises(ValueError):
            VERIFY.verify_readflow_bridges(changed)
        changed = self.readflow_fixture()
        changed["declarations"].append(copy.deepcopy(changed["declarations"][0]))
        with self.assertRaises(ValueError):
            VERIFY.verify_readflow_bridges(changed)

    def test_readflow_control_rejects_success_unrelated_errors_and_wrong_location(self):
        diagnostic = "Negative.cs(40,13): error CS8604: Possible null reference argument for parameter 'value' in 'int AccessReadFlowSource.RequireNonNull(string value)'."
        VERIFY.verify_readflow_failure(1, diagnostic)
        VERIFY.verify_readflow_failure(1, diagnostic + "\n" + diagnostic)
        for code, text in ((0, diagnostic), (1, ""), (1, diagnostic.replace("CS8604", "CS0103")),
                           (1, diagnostic.replace("Negative.cs", "Generated.cs")),
                           (1, diagnostic + "\n" + diagnostic.replace("(40,13)", "(41,13)")),
                           (1, diagnostic.replace("RequireNonNull", "Unrelated"))):
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_readflow_failure(code, text)

    def test_capability_map_requires_all_rows_and_executable_shared_matrix(self):
        mapping = json.loads((VERIFY.ROOT / "eng/verification-fixtures/generated-capability-cases.json").read_text())
        mapping["expectedFinalCases"] = sorted(VERIFY.CASES)
        for row in mapping["rows"]:
            row["runtimeCases"] = [mapping["expectedFinalCases"][0]]
        VERIFY.verify_capability_map(mapping)
        for changed in (dict(mapping, rows=mapping["rows"][:-1]),
                        dict(mapping, rows=mapping["rows"][:-1] + mapping["rows"][:1]),
                        dict(mapping, expectedFinalCases=mapping["expectedFinalCases"][:-1]),
                        dict(mapping, expectedFinalCases=mapping["expectedFinalCases"] + mapping["expectedFinalCases"][:1]),
                        dict(mapping, rows=[dict(mapping["rows"][0], runtimeCases=["unimplemented"])] + mapping["rows"][1:]),
                        dict(mapping, sameBinaryMatrix=dict(mapping["sameBinaryMatrix"], positiveBuildsPerRid=23))):
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                VERIFY.verify_capability_map(changed)

    def test_historical_peer_requires_immutable_old_cohort_and_original_il(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            (folder / "frozen").mkdir()
            (folder / "LegacyCaller.cs").write_text("original normal calls")
            assembly = folder / "frozen/GeneratedValidation.LegacyPeer.Primitives.dll"
            assembly.write_bytes(b"immutable old peer")
            manifest = folder / "frozen/Primitives.compiler-input.txt"
            manifest.write_text("reference|old baseline .15.10 and .5")
            (folder / "Primitives").mkdir()
            project = folder / "Primitives/LegacyPeer.csproj"
            project.write_text("historical project")
            snapshot = folder / "frozen/original.cs"
            snapshot.write_text("original normal calls")
            pins = VERIFY.NATIVE.legacy_pins(VERIFY.PACKAGES.dependency_pins(VERIFY.ROOT))
            libraries = {name + "/" + version: {"type": "package"} for name, version in (
                ("Runic.ReactiveUI.Validation", VERIFY.LEGACY_VERSION), ("Runic.DynamicData", "10.0.0-runic.5"),
                ("ReactiveUI", pins["reactiveui"]), ("ReactiveUI.Core", pins["reactiveui"]))}
            graph = folder / "frozen/Primitives.graph.json"
            graph.write_text(json.dumps({"libraries": libraries, "targets": {"net10.0": {name: {} for name in libraries}}}))
            build_log = folder / "frozen/Primitives.build.log"
            build_log.write_text("Build succeeded. 0 Warning(s) 0 Error(s)")
            peer = {"validationPackageSha256": VERIFY.LEGACY_PACKAGES["Primitives"],
                    "sourceSha256": VERIFY.NATIVE.digest(folder / "LegacyCaller.cs"),
                    "assemblySha256": VERIFY.NATIVE.digest(assembly),
                    "compilerManifestSha256": VERIFY.NATIVE.digest(manifest),
                    "projectSha256": VERIFY.NATIVE.digest(project), "graphSha256": VERIFY.NATIVE.digest(graph),
                    "originalBuildLogSha256": VERIFY.NATIVE.digest(build_log),
                    "positiveWarnings": 0, "positiveErrors": 0, "oldValidationAnalyzerExcluded": True,
                    "originalCompilerSources": [{"file": "frozen/original.cs", "sha256": VERIFY.NATIVE.digest(snapshot)}]}
            provenance = {"source": VERIFY.LEGACY_SOURCE, "validationVersion": VERIFY.LEGACY_VERSION,
                          "dynamicDataVersion": "10.0.0-runic.5", "compiledBeforeFreshCapabilityPackage": True,
                          "sdk": "10.0.401", "language": "14.0", "peers": {"Primitives": peer}}
            path = folder / "provenance.json"
            evidence = {"completed": True, "sha256": peer["assemblySha256"], "validationAssembly": "ReactiveUI.Validation",
                        "newCapabilityReferences": False, "invocationCount": 1, "callableCount": 1, "calls": [{"target": "ValidationRule"}]}
            il = folder / "frozen/Primitives.il.json"
            il.write_text(json.dumps(evidence))
            peer["originalILReportSha256"] = VERIFY.NATIVE.digest(il)
            path.write_text(json.dumps(provenance))
            self.assertEqual(VERIFY.verify_legacy_peer(folder, "Primitives", evidence)[0], assembly)
            for key, value in (("source", "current"), ("validationVersion", "current"), ("dynamicDataVersion", "10.0.0-runic.30"),
                               ("compiledBeforeFreshCapabilityPackage", False), ("sdk", "different"), ("language", "different")):
                path.write_text(json.dumps(dict(provenance, **{key: value})))
                with self.subTest(key=key), self.assertRaises(ValueError):
                    VERIFY.verify_legacy_peer(folder, "Primitives", evidence)
            path.write_text(json.dumps(provenance))
            for key, value in (("completed", False), ("sha256", "stale"), ("newCapabilityReferences", True),
                               ("validationAssembly", "ReactiveUI.Validation.Reactive"), ("invocationCount", 0), ("callableCount", 0), ("calls", [])):
                with self.subTest(key=key), self.assertRaises(ValueError):
                    VERIFY.verify_legacy_peer(folder, "Primitives", dict(evidence, **{key: value}))
            for key, value in (("assemblySha256", "stale"), ("sourceSha256", "stale"), ("compilerManifestSha256", "stale"),
                               ("validationPackageSha256", "current"), ("oldValidationAnalyzerExcluded", False),
                               ("projectSha256", "stale"), ("graphSha256", "stale"), ("originalILReportSha256", "stale"),
                               ("originalBuildLogSha256", "stale"),
                               ("positiveWarnings", 1), ("positiveErrors", 1), ("originalCompilerSources", []),
                               ("originalCompilerSources", [{"file": "frozen/original.cs", "sha256": "stale"}])):
                path.write_text(json.dumps(dict(provenance, peers={"Primitives": dict(peer, **{key: value})})))
                with self.subTest(key=key), self.assertRaises(ValueError):
                    VERIFY.verify_legacy_peer(folder, "Primitives", evidence)

    def test_all_cases_are_required_exactly_once(self):
        records = [json.dumps({"case": name, "passed": True}) for name in VERIFY.CASES]
        VERIFY.verify_runtime("\n".join(records))
        for lines in (records[:-1], records + records[:1], [line.replace('true', 'false') for line in records]):
            with self.subTest(lines=lines), self.assertRaises(ValueError):
                VERIFY.verify_runtime("\n".join(lines))

    def test_negative_requires_expected_actionable_generator_diagnostic(self):
        output = "Negative.cs(42,9): error RUVG001: Unsupported selector. Supply a typed ValidationSelector or registered plan, an explicit observable or ValidationRuleUnsafe."
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
                           (1, output.replace("typed ValidationSelector or registered plan", "another method"))):
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_negative(code, text, "RUVG001")

    def test_current_reflection_setter_requires_ruc_without_false_rdc(self):
        for reactive in (False, True):
            prefix = "ReactiveUI.Binding.Reactive." if reactive else "ReactiveUI.Binding."
            text = f"ReflectionSetter.cs(12,28): error IL2026: Using member '{prefix}ReactiveUIBindingExtensions.BindToUnsafe<TValue,TTarget,TProperty>' which has RequiresUnreferencedCode."
            VERIFY.verify_reflection_setter_failure(1, text, reactive)
            for code, corrupted in ((0, text), (1, text.replace("IL2026", "IL3050")),
                                    (1, text.replace("ReflectionSetter.cs", "Other.cs")),
                                    (1, text.replace("BindToUnsafe", "WhenAnyValueUnsafe")),
                                    (1, text + "\nerror NU1301: feed unavailable"),
                                    (1, text + "\nwarning IL3050: dynamic compilation")):
                with self.subTest(reactive=reactive, corrupted=corrupted), self.assertRaises(ValueError):
                    VERIFY.verify_reflection_setter_failure(code, corrupted, reactive)

    def test_current_observer_warning_is_separate_from_immutable_release_baseline(self):
        for reactive in (False, True):
            root = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
            text = f"ReflectionObserver.cs(6,26): error IL2026: Using member '{root}.Extensions.ValidatableViewModelExtensions.ValidationRuleUnsafe<TModel,TValue>' which has RequiresUnreferencedCode."
            VERIFY.verify_reflection_observer_failure(1, text, reactive)
            for code, corrupted in ((0, text), (1, text.replace("IL2026", "IL3050")),
                                    (1, text.replace("ReflectionObserver.cs", "Other.cs")),
                                    (1, text.replace("ValidationRuleUnsafe", "ValidationRule")),
                                    (1, text + "\nerror CS1002: expected semicolon")):
                with self.subTest(reactive=reactive, corrupted=corrupted), self.assertRaises(ValueError):
                    VERIFY.verify_reflection_observer_failure(code, corrupted, reactive)

    def test_logical_observer_warning_preserves_exact_source_api_and_ruc(self):
        for reactive in (False, True):
            root = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
            owner = root + ".Extensions.ValidatableViewModelExtensions"
            receiver = "extension<TViewModel>(TViewModel)"
            signature = "ValidationRuleUnsafe<TViewModelProp>(Expression<Func<TViewModel, TViewModelProp>>, Func<TViewModelProp, Boolean>, String)"
            text = f"/actual/ReflectionObserver.cs(6,26): error IL2026: Using member '{owner}.{receiver}.{signature}' which has 'RequiresUnreferencedCodeAttribute'."
            VERIFY.verify_reflection_observer_failure(1, text, reactive)
            VERIFY.verify_reflection_observer_failure(1, text.replace(receiver + ".", ""), reactive)
            for corrupted in (
                    text.replace(owner, "Other.Extensions.ValidatableViewModelExtensions"),
                    text.replace("ValidatableViewModelExtensions", "OtherExtensions"),
                    text.replace("ValidationRuleUnsafe", "ValidationRuleUnsafeOther"),
                    text.replace("ValidationRuleUnsafe", "ValidationRule"),
                    text.replace(receiver, "extension<Other>(Other)"),
                    text.replace(receiver, receiver + "." + receiver),
                    text.replace(receiver, "extension<TViewModel>(TViewModel"),
                    text.replace("ReflectionObserver.cs", "Other.cs"),
                    text.replace("IL2026", "IL3050"),
                    text + "\nwarning IL3050: dynamic compilation",
                    text + "\nerror CS1002: expected semicolon"):
                with self.subTest(reactive=reactive, corrupted=corrupted), self.assertRaises(ValueError):
                    VERIFY.verify_reflection_observer_failure(1, corrupted, reactive)
            legacy = text.replace(receiver + ".", "").replace("ValidationRuleUnsafe", "ValidationRuleUnsafeOther")
            with self.assertRaises(ValueError):
                VERIFY.verify_reflection_observer_failure(1, legacy, reactive)

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

    def test_semantic_inventory_requires_18_original_and_11_static_overloads(self):
        dispatch = self.dispatch_fixture()
        self.assertEqual(len(VERIFY.verify_overload_inventory(dispatch, self.catalogue_fixture())), 57)
        for corrupted in (dispatch[:-1], dispatch[1:],
                          [dict(call, interceptor=None) for call in dispatch],
                          [dict(call, signature="same") if call["api"].startswith("ValidationBinding.") else call for call in dispatch]):
            with self.subTest(corrupted=corrupted), self.assertRaises(ValueError):
                VERIFY.verify_overload_inventory(corrupted, self.catalogue_fixture())

    def test_exact_catalogue_cannot_be_replaced_by_registered_or_relabelled_calls(self):
        dispatch = self.dispatch_fixture()
        catalogue = self.catalogue_fixture()
        registered_calls = [dict(next(call for call in dispatch if call["selectorForm"] == form),
                                 mode="RuntimeRegistered", callable=callable, requiresGenerated=False, interceptor=None)
                            for form in ("Expression", "Func") for callable in (False, True)]
        registered = registered_calls[0]
        self.assertEqual(len(VERIFY.verify_overload_inventory(dispatch + registered_calls, catalogue)), 57)
        self.assertEqual(VERIFY.verify_registered_dispatch(registered_calls, catalogue), registered_calls)
        for calls in (registered_calls[:2], registered_calls[2:], registered_calls[::2], registered_calls[1::2], registered_calls[:-1]):
            with self.subTest(calls=calls), self.assertRaises(ValueError):
                VERIFY.verify_registered_dispatch(calls, catalogue)
        missing_kind = [dict(call) for call in registered_calls]
        missing_kind[0].pop("callable")
        with self.assertRaises(ValueError):
            VERIFY.verify_registered_dispatch(missing_kind, catalogue)
        with self.assertRaises(ValueError):
            VERIFY.verify_registered_dispatch(dispatch + [registered], catalogue)
        with self.assertRaises(ValueError):
            VERIFY.verify_registered_dispatch([dict(registered, callable=False)], catalogue)
        for calls, definitions in ((dispatch[1:] + [registered], catalogue),
                                   (dispatch, catalogue[:-1]),
                                   (dispatch, catalogue[:-1] + [catalogue[0]]),
                                   ([dict(call, methodId="unrelated") for call in dispatch], catalogue),
                                   ([dict(call, mode="RuntimeSafe", requiresGenerated=False) for call in dispatch], catalogue),
                                   ([dict(call, parameters=[]) if index == 0 else call for index, call in enumerate(dispatch)], catalogue),
                                   (dispatch + [dict(registered, interceptor="Host.Intercept")], catalogue)):
            with self.subTest(calls=calls, definitions=definitions), self.assertRaises(ValueError):
                VERIFY.verify_overload_inventory(calls, definitions)

    def test_both_selector_families_require_exact_paired_generated_contracts(self):
        dispatch = self.dispatch_fixture()
        catalogue = self.catalogue_fixture()
        inventory = VERIFY.verify_overload_inventory(dispatch, catalogue)
        self.assertEqual({form: sum(row["selectorForm"] == form for row in inventory)
                          for form in ("Expression", "Func", "None")},
                         {"Expression": 28, "Func": 28, "None": 1})
        typed_inline = [dict(call, inlineSelectors=call["selectorForm"] == "Func") for call in dispatch]
        VERIFY.verify_overload_inventory(typed_inline, catalogue)
        with self.assertRaises(ValueError):
            VERIFY.verify_overload_inventory([dict(call, inlineSelectors=True) for call in dispatch], catalogue)
        expressions = [call for call in dispatch if call["selectorForm"] != "Func"]
        registered = [dict(call, mode="RuntimeRegistered", requiresGenerated=False, interceptor=None)
                      for call in dispatch if call["selectorForm"] == "Func"]
        for calls in (expressions, expressions + registered):
            with self.assertRaises(ValueError):
                VERIFY.verify_overload_inventory(calls, catalogue)
        for field, value in (("returnType", "different"), ("arity", 99),
                             ("genericParameterContracts", [{"name": "T", "notNullConstraint": True}]),
                             ("selectorForm", "Expression")):
            corrupted = copy.deepcopy(catalogue)
            next(row for row in corrupted if row["selectorForm"] == "Func")[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                VERIFY.verify_shipping_catalogue(corrupted)

        for mutation in ("nullable", "refKind", "mixedSelectors", "callbackOnly"):
            corrupted = copy.deepcopy(catalogue)
            row = next(row for row in corrupted if row["selectorForm"] == "Func"
                       and row["api"].endswith(".BindValidation")
                       and any(p["name"] == "viewModelProperty" for p in row["parameters"]))
            selector = next(p for p in row["parameters"] if p["name"] == "viewModelProperty")
            if mutation == "nullable": selector["type"] = "global::System.Func<object, string?>"
            elif mutation == "refKind": selector["refKind"] = "Ref"
            elif mutation == "mixedSelectors": selector["type"] = "global::System.Linq.Expressions.Expression<global::System.Func<object, string>>"
            else:
                for parameter in row["parameters"]:
                    if parameter["name"] in VERIFY.SELECTOR_PARAMETERS: parameter["type"] = "object"
                row["parameters"].append({"name": "converter", "type": "global::System.Func<object, string>"})
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                VERIFY.verify_shipping_catalogue(corrupted)

    def test_original_normal_identities_cannot_be_replaced_by_typed_definitions(self):
        baseline = json.loads((VERIFY.ROOT / "eng/verification-fixtures/original-normal-api.json").read_text())
        original = [{"methodId": identity.replace("{ROOT}", "ReactiveUI.Validation"),
                     "selectorForm": "Expression" if "System.Linq.Expressions.Expression" in identity else "None"}
                    for identity in baseline["originalNormalMethodIds"]]
        for root in ("ReactiveUI.Validation", "ReactiveUI.Validation.Reactive"):
            definitions = [{**row, "methodId": row["methodId"].replace("ReactiveUI.Validation", root)} for row in original]
            definitions.append({"methodId": "M:Typed.New", "selectorForm": "Func"})
            VERIFY.verify_preserved_expression_api(definitions, baseline)
            for corrupted in (definitions[1:], definitions[:-2] + [definitions[0]],
                              [{**row, "methodId": row["methodId"] + ".Changed"} for row in definitions],
                              [{**row, "selectorForm": "Func"} for row in definitions]):
                with self.subTest(root=root), self.assertRaises(ValueError):
                    VERIFY.verify_preserved_expression_api(corrupted, baseline)

    def test_all_owned_sources_are_retained_and_guarded_with_semantic_provenance(self):
        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp)
            project = folder / "App.csproj"
            generated = folder / "obj/generated/ReactiveUI.Validation.SourceGenerators/ReactiveUI.Validation.SourceGenerators.ValidationGenerator"
            generated.mkdir(parents=True)
            source = generated / "ValidationInterceptors.g.cs"
            source.write_text("namespace ReactiveUI.Validation.Generated; internal static class Host { }")
            helper = generated / "HostedGenericAccess.g.cs"
            helper.write_text("partial class Model<T> { }")
            peer = generated.parent.parent / "Other.Producer/IncidentalSdkOutput.g.cs"
            peer.parent.mkdir()
            peer.write_text("// peer-owned generated file")
            retained = folder / "Primitives-managed-generated"
            retained.mkdir()
            (retained / "StalePreviousRun.g.cs").write_text("// stale")
            semantics = self.semantic_fixture(project)
            evidence = VERIFY.verify_generated_sources(project, retained, semantics)
            self.assertEqual(len(evidence["overloads"]), 57)
            self.assertEqual(evidence["sourceCount"], 2)
            self.assertEqual(len(evidence["files"]), 2)
            for record in evidence["files"]:
                self.assertEqual(record["sha256"], VERIFY.NATIVE.digest(folder / record["path"]))
            self.assertEqual(list(retained.rglob("IncidentalSdkOutput.g.cs")), [])
            self.assertEqual(list(retained.rglob("StalePreviousRun.g.cs")), [])
            for text in ("expression.Compile();", "view.BindValidationUnsafe();", "#pragma warning disable IL2026",
                         "model.WhenAnyValue(x => x.Name);", "owner.GetField(name);", "owner.GetMethod(name);"):
                helper.write_text(text)
                with self.subTest(text=text), self.assertRaises(ValueError):
                    VERIFY.verify_generated_sources(project, retained, self.semantic_fixture(project))
            helper.write_text("partial class Model<T> { }")
            semantics = self.semantic_fixture(project)
            source.write_text(source.read_text() + "// changed")
            with self.assertRaisesRegex(ValueError, "source bytes"):
                VERIFY.verify_generated_sources(project, retained, semantics)

    def test_semantic_replay_cannot_omit_inputs_or_lie_about_ownership(self):
        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp)
            project = folder / "App.csproj"
            generated = folder / "obj/generated/ReactiveUI.Validation.SourceGenerators/Owner"
            generated.mkdir(parents=True)
            (generated / "Hosted.g.cs").write_text("partial class Host<T> { }")
            for corrupt in (lambda report: report.update(completed=False),
                            lambda report: report.update(originalSources=[]),
                            lambda report: report.update(references=[]),
                            lambda report: report.update(analyzers=[]),
                            lambda report: report["validationAssemblies"][0]["metadata"].update(IsAotCompatible="False"),
                            lambda report: report.update(validationAssemblies=[]),
                            lambda report: report.update(declarations=[]),
                            lambda report: report.update(producerProfiles=[]),
                            lambda report: report.update(noWarn=[]),
                            lambda report: report.update(noWarn=["1701", "1702", "8002", "2026"]),
                            lambda report: report["generatedSources"][0].update(validationOwned=False),
                            lambda report: report["generatedSources"].append(dict(report["generatedSources"][0]))):
                semantics = self.semantic_fixture(project)
                corrupt(semantics)
                with self.subTest(corrupt=corrupt), self.assertRaises(ValueError):
                    VERIFY.verify_generated_sources(project, semantics=semantics)

    @staticmethod
    def dispatch_fixture():
        result = []
        for family in ("rule", "context", "state"):
            for first in (False, True):
                for second in (False, True):
                    if family == "rule":
                        parameters = {"viewModel": "object", "viewModelProperty": "object", "isPropertyValid": "Func<string, bool>", "message": "Func<string, string>" if second else "string"}
                        if first: parameters["context"] = "object"
                        api = "ValidatableViewModelExtensions.ValidationRule"
                    elif family == "context":
                        parameters = {"contextProperty": "object", "viewProperty" if second else "action": "object"}
                        if first: parameters["viewModelProperty"] = "object"
                        api = "ViewForExtensions.BindValidationContext"
                    else:
                        parameters = {"converter": "object", "viewProperty" if second else "onNext": "object"}
                        if first: parameters["modelProperty"] = "object"
                        else: parameters["helperProperty"] = "object"
                        api = "ViewForExtensions.BindValidationState"
                    result.append({"api": api, "parameters": [{"name": name, "type": value} for name, value in parameters.items()], "interceptor": "Host<T>.Intercept<TViewModel,TValue>", "signature": str(len(result)), "typeParameters": []})
        for kind in ("helper", "property", "model"):
            for formatter in (False, True):
                parameters = {"view": "object", "viewProperty": "object"}
                if kind != "model": parameters["viewModelHelperProperty" if kind == "helper" else "viewModelProperty"] = "object"
                if formatter: parameters["formatter"] = "object"
                result.append({"api": "ViewForExtensions.BindValidation", "parameters": [{"name": name, "type": value} for name, value in parameters.items()], "interceptor": "Host.Intercept", "signature": str(len(result)), "typeParameters": []})
        for api, count in (("ForProperty", 5), ("ForValidationHelperProperty", 3), ("ForViewModel", 3)):
            for index in range(count):
                parameters = [{"name": "overload", "type": f"Shape{index}"}]
                if api != "ForViewModel" or index != 2:
                    parameters.append({"name": "viewProperty", "type": "object"})
                result.append({"api": "ValidationBinding." + api, "signature": f"ValidationBinding.{api}<{index}>", "interceptor": "Host.Intercept", "parameters": parameters, "typeParameters": ["TView", "TViewModel"]})
        for index, call in enumerate(result):
            form = "None" if index == 28 else "Expression"
            for parameter in call["parameters"]:
                if parameter["name"] in VERIFY.SELECTOR_PARAMETERS:
                    parameter["type"] = "global::System.Linq.Expressions.Expression<global::System.Func<object, string>>"
            call.update(methodId=f"M:Shipping.Normal.{index}", mode="Generated", normalShippingCall=True, requiresGenerated=True,
                        selectorForm=form, returnType="object", arity=len(call["typeParameters"]),
                        genericParameterContracts=[{"name": name, "notNullConstraint": False} for name in call["typeParameters"]])
        typed = []
        for call in result:
            if call["selectorForm"] == "None":
                continue
            counterpart = copy.deepcopy(call)
            counterpart.update(methodId=call["methodId"] + ".Func", signature=call["signature"] + ".Func", selectorForm="Func")
            for parameter in counterpart["parameters"]:
                if parameter["name"] in VERIFY.SELECTOR_PARAMETERS:
                    parameter["type"] = "global::System.Func<object, string>"
            typed.append(counterpart)
        result.extend(typed)
        return result

    @classmethod
    def catalogue_fixture(cls):
        return [{key: call[key] for key in ("methodId", "api", "signature", "parameters", "typeParameters", "genericParameterContracts", "selectorForm", "returnType", "arity")}
                for call in cls.dispatch_fixture()]

    @classmethod
    def semantic_fixture(cls, project):
        folder = project.parent
        original = folder / "Original.cs"
        reference = folder / "ReactiveUI.Validation.dll"
        original.write_text("// original caller input")
        reference.write_bytes(b"reference bytes")
        config = folder / "Consumer.GeneratedMSBuildEditorConfig.editorconfig"
        config.write_text("is_global = true\nbuild_property.PublishAot = \nbuild_property.IsAotCompatible = \nbuild_property.EnableAotAnalyzer = true\n")
        analyzer = folder / "Analyzer.dll"
        analyzer.write_bytes(b"analyzer bytes")
        manifest = folder / "obj/generated-compiler-input.txt"
        manifest.write_text(f"source|{original}\nreference|{reference}\nanalyzer|{analyzer}\nassembly|Consumer\noutput|Library\ndefine|NET10_0\nlanguage|14.0\nnullable|enable\nallowlist|ReactiveUI.Validation.Generated\nprofile|Reactive=4.2.0\nprofile|Binding=9.1.0\nprofile|Avalonia=\nprofile|Maui=\nnowarn|1701\nnowarn|1702\nnowarn|8002\nredundantSuppressions|\npublishTrimmed|\nilLinkTarget|\npublishAot|\nisAotCompatible|\nenableAotAnalyzer|true\nanalyzerConfig|{config}\n")
        generated_root = folder / "obj/generated"
        return {"completed": True, "assembly": "Consumer", "output": "Library", "defines": ["NET10_0"],
                "language": "14.0", "nullable": "enable", "allowlist": ["ReactiveUI.Validation.Generated"],
                "noWarn": ["1701", "1702", "8002"], "redundantSuppressions": "", "publishTrimmed": "", "ilLinkTarget": "",
                "aotProperties": {"publishAot": "", "isAotCompatible": "", "enableAotAnalyzer": "true"},
                "analyzerConfigs": [{"path": str(config), "sha256": VERIFY.NATIVE.digest(config)}],
                "producerProfiles": ["Avalonia=", "Binding=9.1.0", "Maui=", "Reactive=4.2.0"],
                "manifest": {"path": str(manifest), "sha256": VERIFY.NATIVE.digest(manifest)},
                "originalSources": [{"path": str(original), "sha256": VERIFY.NATIVE.digest(original)}],
                "references": [{"path": str(reference), "sha256": VERIFY.NATIVE.digest(reference)}],
                "validationAssemblies": [{"path": str(reference), "assembly": "ReactiveUI.Validation", "sha256": VERIFY.NATIVE.digest(reference),
                                           "metadata": {"IsAotCompatible": "True", "IsTrimmable": "True"}}],
                "analyzers": [{"path": str(analyzer), "sha256": VERIFY.NATIVE.digest(analyzer)}],
                "generatedSources": [{"path": path.relative_to(generated_root).as_posix(), "sha256": VERIFY.NATIVE.digest(path),
                                      "validationOwned": "ReactiveUI.Validation.SourceGenerators" in path.relative_to(generated_root).parts}
                                     for path in generated_root.rglob("*.cs")],
                "dispatch": cls.dispatch_fixture(), "shippingMethods": cls.catalogue_fixture(), "declarations": [{"signature": "Host<T>.Intercept<TViewModel,TValue>"}]}

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
