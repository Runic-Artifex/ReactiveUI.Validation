#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Strict actual-package acceptance of generated familiar Validation call syntax."""

import argparse
import base64
import hashlib
import json
import platform
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parent.parent
# Share strict package provenance/graph checks, never the historical expectations.
import importlib.util
_spec = importlib.util.spec_from_file_location("native_validation_gate", ROOT / "eng/verify-native-validation.py")
NATIVE = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(NATIVE)
PACKAGES = NATIVE.PACKAGES
DEPENDENCIES = NATIVE.DEPENDENCIES
CASES = {"initial-field-and-text", "nested-null-and-replacement", "helper-model-rich-state",
         "context-replacement", "nested-target-handoff", "property-state-membership", "rows-owned-observable-results",
         "latest-requests-owner-scheduling", "stable-rows-source-ownership", "removed-pending-row-cancellation",
         "domain-presentation-independence", "nullable-output-stack-snapshots", "typed-formatter-resolver-lifetimes", "precompiled-fresh-expressions-callables", "historical-peer-unchanged-original-api", "static-factory-raw-nullable-projections", "selected-static-access-owner-replay", "typed-providers-components-host-adapter", "rules-generics-private-captures-indices", "real-producer-commands-collections-oaph", "virtual-error-hook-order-and-disposal", "peer-generated-registered-callers", "bindings-generics-storage-policies"}
NEGATIVES = {"NEGATIVE_OPAQUE_SELECTOR": "RUVG001", "NEGATIVE_UNMARKED_INIT": "RUVG006",
             "NEGATIVE_UNMARKED_READONLY": "RUVG006", "NEGATIVE_GET_ONLY_TARGET": "RUVG006",
             "NEGATIVE_INVALID_ASSIGNMENT": "RUVG006", "NEGATIVE_VALUE_VIEW": "RUVG006"}
LEGACY_SOURCE = "2550376b230ccfb78beb8d3b4ced8866b65d809e"
LEGACY_VERSION = "8.1.0-runic.0.790.17.15.10"
ILLINK_VERSION = "10.0.12"
ILLINK_ARCHIVE_SHA256 = "37aa984da51fdc01cc7d57a8647893392f3539cdd5a1d32ce4982be5309bc08b"
ILLINK_TARGET_SHA256 = "f1bbe7e9e79badb900a594691fe4c6738e906d911ee3e50975434d09035a2568"
# NuGet's signed-package content hash excludes signature bytes. Independently
# derived from this exact archive by the locked SDK's PackageArchiveReader.
ILLINK_CONTENT_SHA512 = "xi+BDjFpW+Sb+MHFHaH6Y/gV9I8BluFwRXc1QyCdoZbIK26eNiBeFuMTe/FMwc33G1wdHCyDg7CVTmb8OdQrMQ=="
ILLINK_ARCHIVE_SHA512 = "opT5P1p+CG70xGaveTgq3Q5OZKd7MZ0Rs10x4Tewl6bcPbu4SLo4F0n6RBCInvBKxoR10T11+X0M9dgjKEfucw=="
LEGACY_PACKAGES = {
    "Primitives": "b68cfe33d5f1bf4aeee612bd2baaffdb25f1a0a918d6594db65e592a844ac800",
    "Reactive": "c1c921c16ee79018fcaa4b881c810f850d3018fde70400064b73a0308bd028e6",
}


def verify_configuration_results(results, flavor, source, package_hash):
    expected = {f"{flavor}-configuration-{case}" for case in (
        "generated", "excluded-analyzer", "removed-analyzer", "analyzers-disabled", "excluded-build",
        "wrong-allowlist", "csharp13", "missing-runtime", "old-runtime", "older-compiler", "collision")}
    if len(results) != len(expected) or {result.get("name") for result in results} != expected:
        raise ValueError("Packed configuration acceptance must retain every distinct required control")
    if any(result.get("passed") is not True or result.get("source") != source
           or result.get("packageSha256") != package_hash for result in results):
        raise ValueError("Packed configuration evidence must identify the current accepted source and package bytes")
    return results


def verify_capability_map(mapping):
    expected_ids = {f"{prefix}{number:02d}" for prefix, count in (("C", 6), ("S", 11), ("P", 7), ("R", 11), ("A", 8))
                    for number in range(1, count + 1)}
    rows = mapping["rows"]
    if len(rows) != 43 or {row["id"] for row in rows} != expected_ids:
        raise ValueError("Acceptance map must retain all 43 distinct approved capability rows")
    final_cases = mapping["expectedFinalCases"]
    if len(final_cases) != len(set(final_cases)) or set(final_cases) != CASES:
        raise ValueError("Acceptance binary must implement every final mapped case exactly once")
    for row in rows:
        if not row["runtimeCases"] or not set(row["runtimeCases"]).issubset(CASES):
            raise ValueError(f"Capability {row['id']} has no executable acceptance case")
    matrix = mapping["sameBinaryMatrix"]
    if (matrix["flavors"] != ["Primitives", "Reactive"] or matrix["stages"] != ["managed", "full-trim", "native"]
            or matrix["rids"] != ["linux-x64", "win-x64"] or matrix["positiveBuildsPerRid"] != 6):
        raise ValueError("Capability acceptance must use the shared six-build binary matrix on both RIDs")
    return mapping


def verify_legacy_peer(folder, flavor, evidence):
    provenance = json.loads((folder / "provenance.json").read_text())
    peer = provenance["peers"][flavor]
    assembly = folder / "frozen" / f"GeneratedValidation.LegacyPeer.{flavor}.dll"
    graph_path = folder / "frozen" / f"{flavor}.graph.json"
    original_il = folder / "frozen" / f"{flavor}.il.json"
    if (provenance["source"] != LEGACY_SOURCE or provenance["validationVersion"] != LEGACY_VERSION
            or provenance["dynamicDataVersion"] != "10.0.0-runic.5"
            or provenance["compiledBeforeFreshCapabilityPackage"] is not True
            or provenance["sdk"] != "10.0.401" or provenance["language"] != "14.0"
            or peer["validationPackageSha256"] != LEGACY_PACKAGES[flavor]
            or peer["sourceSha256"] != NATIVE.digest(folder / "LegacyCaller.cs")
            or peer["assemblySha256"] != NATIVE.digest(assembly)
            or peer["compilerManifestSha256"] != NATIVE.digest(folder / "frozen" / f"{flavor}.compiler-input.txt")
            or peer["projectSha256"] != NATIVE.digest(folder / flavor / "LegacyPeer.csproj")
            or peer["graphSha256"] != NATIVE.digest(graph_path)
            or peer["originalILReportSha256"] != NATIVE.digest(original_il)
            or peer["originalBuildLogSha256"] != NATIVE.digest(folder / "frozen" / f"{flavor}.build.log")
            or peer["positiveWarnings"] != 0 or peer["positiveErrors"] != 0
            or peer["oldValidationAnalyzerExcluded"] is not True
            or not peer["originalCompilerSources"]):
        raise ValueError("Historical ABI fixture provenance is stale or uses the current cohort")
    for item in peer["originalCompilerSources"]:
        path = (folder / item["file"]).resolve()
        if not path.is_relative_to((folder / "frozen").resolve()) or NATIVE.digest(path) != item["sha256"]:
            raise ValueError("Historical peer original compiler source snapshot is stale")
    graph = json.loads(graph_path.read_text())
    NATIVE.verify_native_graph(graph, flavor == "Reactive", NATIVE.legacy_pins(PACKAGES.dependency_pins(ROOT)), LEGACY_VERSION)
    verify_no_roslyn_runtime(graph)
    root = "ReactiveUI.Validation.Reactive" if flavor == "Reactive" else "ReactiveUI.Validation"
    if (evidence.get("completed") is not True or evidence.get("sha256") != peer["assemblySha256"]
            or evidence.get("validationAssembly") != root or evidence.get("newCapabilityReferences") is not False
            or evidence.get("invocationCount", 0) < 1 or evidence.get("callableCount", 0) < 1
            or not evidence.get("calls")):
        raise ValueError("Historical peer must retain original normal API invocation and callable IL")
    retained = json.loads(original_il.read_text())
    for key in ("sha256", "assembly", "validationAssembly", "newCapabilityReferences", "invocationCount", "callableCount", "calls", "assemblyReferences"):
        if evidence.get(key) != retained.get(key):
            raise ValueError("Historical peer metadata changed from its original inspection")
    return assembly, peer


def verify_runtime(output):
    records = [json.loads(line) for line in output.splitlines() if line.startswith('{')]
    cases = [record for record in records if "case" in record]
    if len(cases) != len(CASES) or {record["case"] for record in cases} != CASES:
        raise ValueError("Generated consumer must report exactly every required application case once")
    if any(record.get("passed") is not True for record in cases):
        raise ValueError("Generated application consumer reported a failed case")


def verify_negative(code, output, diagnostic):
    if code == 0:
        raise ValueError("Unsupported generated selector unexpectedly compiled")
    errors = [line for line in output.splitlines() if re.search(r"\berror (?:[A-Z]+\d+|:)", line)]
    parsed = []
    for line in errors:
        match = re.search(r"(?:^|[\\/])Negative\.cs\((\d+),\d+\): error ([A-Z]+\d+):", line)
        if match is None or match[2] not in {diagnostic, "RUVG005"}:
            raise ValueError(f"Negative consumer failed for an unexpected reason; expected {diagnostic}")
        parsed.append((match[1], match[2], line))
    primary_lines = {number for number, code, _ in parsed if code == diagnostic}
    if len(primary_lines) != 1 or any(number not in primary_lines for number, _, _ in parsed):
        raise ValueError("Expected a primary selector diagnostic; secondary dispatch errors must identify the same Negative.cs invocation line")
    if any(not re.search(r"typed .*?(?:selector|target|factory)|registered", line, re.IGNORECASE)
           for _, code, line in parsed if code == diagnostic):
        raise ValueError("Unsupported call diagnostic must offer a typed selector/target or registered alternative")
    if any("observable" not in line.lower() or "Unsafe" not in line for _, code, line in parsed if code == "RUVG005"):
        raise ValueError("Missing dispatch must offer explicit observable and Unsafe alternatives")


def verify_readflow_bridges(semantics):
    """Pin each actual private read bridge and its independent output contract."""
    specifications = {
        "get_Certain": ("NotNull", "string?"), "get_ReturnCertain": ("NotNull", "string?"),
        "get_Possible": ("MaybeNull", "string"), "get_Number": ("NotNull", "int?"),
        "CertainStorage": ("NotNull", "string?"), "PossibleStorage": ("MaybeNull", "string"),
        "NumberStorage": ("NotNull", "int?"),
    }
    declarations = semantics.get("declarations", [])
    evidence = []
    for member, (condition, return_type) in specifications.items():
        anchors = []
        for declaration in declarations:
            if declaration.get("kind") != "Method" or not declaration.get("parameters"):
                continue
            if declaration["parameters"][0].get("type") != "global::AccessReadFlowSource":
                continue
            for attribute in declaration.get("attributeContracts", []):
                if attribute.get("type") != "global::System.Runtime.CompilerServices.UnsafeAccessorAttribute":
                    continue
                if not attribute.get("assembly", "").startswith(("System.Runtime,", "System.Private.CoreLib,")):
                    continue
                named = {argument["name"]: argument.get("value") for argument in attribute.get("namedArguments", [])}
                constructor = attribute.get("constructorArguments", [])
                if named.get("Name") == member and len(constructor) == 1 and constructor[0].get("value") == ("1" if member.startswith("get_") else "3"):
                    anchors.append(declaration)
        if len(anchors) != 1:
            raise ValueError(f"Expected one actual selected read accessor for {member}")
        anchor = anchors[0]
        reads = [row for row in declarations if row.get("kind") == "Method" and row.get("name") == "Read"
                 and row.get("containingType") == anchor.get("containingType")]
        if len(reads) != 1:
            raise ValueError(f"Expected one actual read wrapper for {member}")
        for method in {row["signature"]: row for row in (anchor, reads[0])}.values():
            canonical = {"global::System.String": "string", "global::System.String?": "string?",
                         "global::System.Int32?": "int?", "global::System.Nullable<global::System.Int32>": "int?"}
            if canonical.get(method.get("returnType"), method.get("returnType")) != return_type:
                raise ValueError(f"Selected accessor changed the declared return type of {member}")
            attributes = method.get("returnAttributes", [])
            expected = f"global::System.Diagnostics.CodeAnalysis.{condition}Attribute"
            if not any(attribute.get("type") == expected and attribute.get("assembly", "").startswith(("System.Runtime,", "System.Private.CoreLib,")) for attribute in attributes):
                raise ValueError(f"Selected accessor lost its exact framework {condition} read contract: {member}")
            other = "MaybeNull" if condition == "NotNull" else "NotNull"
            if any(attribute.get("type") == f"global::System.Diagnostics.CodeAnalysis.{other}Attribute" for attribute in attributes):
                raise ValueError(f"Selected accessor invented a conflicting read contract: {member}")
        evidence.append({"member": member, "condition": condition, "returnType": return_type,
                         "accessor": anchor, "read": reads[0]})
    return evidence


def verify_readflow_failure(code, output):
    """Require the actual emitted MaybeNull bridge to fail its nonnull input use."""
    if code == 0:
        raise ValueError("Actual MaybeNull accessor unexpectedly satisfied a nonnull input contract")
    errors = [line for line in output.splitlines() if re.search(r"\berror (?:[A-Z]+\d+|:)", line)]
    locations = []
    for line in errors:
        match = re.search(r"(?:^|[\\/])Negative\.cs\((\d+),\d+\): error CS8604:", line)
        if match is None or "RequireNonNull" not in line:
            raise ValueError("Read-flow control failed outside the exact CS8604 nonnull-input invocation")
        locations.append(match[1])
    if len(set(locations)) != 1:
        raise ValueError("Expected exactly one MaybeNull argument diagnostic location")



def verify_ruc_failure(code, output, filename, api_prefix, logical_receiver=None):
    """Require precise current reflection provenance, without invented dynamic compilation."""
    if code == 0:
        raise ValueError("Current reflection route unexpectedly passed strict trimming analysis")
    if re.search(r"\b(?:warning|error) IL3050\b", output):
        raise ValueError("Current reflection route must not claim dynamic compilation")
    errors = [line for line in output.splitlines() if re.search(r"\berror (?:[A-Z]+\d+|:)", line)]
    matched = []
    prefixes = [api_prefix]
    if logical_receiver is not None:
        owner, method = api_prefix.rsplit(".", 1)
        prefixes.append(f"{owner}.{logical_receiver}.{method}")
    pattern = rf"(?:^|[\\/]){re.escape(filename)}\((\d+),\d+\): error (IL\d+): Using member '([^']+)'"
    for line in errors:
        match = re.search(pattern, line)
        if match is None or match[2] != "IL2026" or not any(
                match[3].startswith(prefix) and
                (len(match[3]) == len(prefix) or match[3][len(prefix)] in "<(")
                for prefix in prefixes):
            raise ValueError(f"Unexpected current reflection failure: {line}")
        matched.append(match[1])
    if len(set(matched)) != 1:
        raise ValueError("Expected precise current reflection IL2026 at its sole invocation")


def verify_reflection_setter_failure(code, output, reactive):
    prefix = "ReactiveUI.Binding.Reactive." if reactive else "ReactiveUI.Binding."
    verify_ruc_failure(code, output, "ReflectionSetter.cs", prefix + "ReactiveUIBindingExtensions.BindToUnsafe")


def verify_reflection_observer_failure(code, output, reactive):
    root = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
    verify_ruc_failure(code, output, "ReflectionObserver.cs",
                       root + ".Extensions.ValidatableViewModelExtensions.ValidationRuleUnsafe",
                       "extension<TViewModel>(TViewModel)")


def verify_generator_assets(package):
    """The shipping pair itself must carry the analyzer and namespace opt-in."""
    with zipfile.ZipFile(package) as archive:
        for name in archive.namelist():
            if name.startswith(("lib/", "runtimes/")) and ("Microsoft.CodeAnalysis" in name or "ReactiveUI.Validation.SourceGenerators" in name):
                raise ValueError("Roslyn and generator assemblies must never be runtime assets")
            if name.endswith(".nuspec"):
                metadata = ET.fromstring(archive.read(name))
                if any(node.tag.rsplit("}", 1)[-1] == "dependency" and node.get("id", "").lower().startswith("microsoft.codeanalysis") for node in metadata.iter()):
                    raise ValueError("Shipping package must not depend on Roslyn runtime packages")
        analyzers = [name for name in archive.namelist() if name.startswith("analyzers/") and name.endswith(".dll")]
        expected = [name for name in analyzers if name.endswith("/ReactiveUI.Validation.SourceGenerators.dll")]
        if len(expected) != 1 or not expected[0].startswith("analyzers/dotnet/"):
            raise ValueError("Validation package must bundle exactly one Roslyn generator analyzer")
        if len(archive.read(expected[0])) < 1024:
            raise ValueError("Bundled analyzer is not a built assembly")
        props = [name for name in archive.namelist() if name.startswith("buildTransitive/") and name.endswith(".props")]
        if len(props) != 1:
            raise ValueError("Validation package must carry exactly one transitive interceptor opt-in props")
        nodes = list(ET.fromstring(archive.read(props[0])).iter("InterceptorsNamespaces"))
        if len(nodes) != 1 or "$(InterceptorsNamespaces)" not in (nodes[0].text or "") or "ReactiveUI.Validation.Generated" not in (nodes[0].text or "").split(";"):
            raise ValueError("Package must append its exact generated namespace to the interceptor allowlist")
        return {"analyzer": expected[0], "props": props[0]}


def verify_no_roslyn_runtime(assets):
    for name in assets["libraries"]:
        if name.split("/", 1)[0].lower().startswith("microsoft.codeanalysis"):
            raise ValueError("Roslyn package leaked into external consumer dependency graph")
    for target in assets["targets"].values():
        for library in target.values():
            for group in ("compile", "runtime", "runtimeTargets"):
                for asset in library.get(group, {}):
                    if "Microsoft.CodeAnalysis" in asset or "ReactiveUI.Validation.SourceGenerators" in asset:
                        raise ValueError("Compiler or generator assembly leaked into compile/runtime assets")


SELECTOR_PARAMETERS = {"viewModelProperty", "modelProperty", "viewProperty", "contextProperty",
                       "helperProperty", "viewModelHelperProperty"}
EXPRESSION_TYPE = "global::System.Linq.Expressions.Expression<"
FUNC_TYPE = "global::System.Func<"


def verify_preserved_expression_api(shipping_methods, baseline):
    """Bind preserved CLR identities to the actual prior package's normal API catalogue."""
    expected = set(baseline["originalNormalMethodIds"])
    if len(expected) != 29 or len(baseline["originalNormalMethodIds"]) != 29:
        raise ValueError("The immutable original normal API identity contract must contain 29 definitions")
    actual = [method["methodId"].replace("ReactiveUI.Validation.Reactive", "{ROOT}")
              .replace("ReactiveUI.Validation", "{ROOT}")
              for method in shipping_methods if method.get("selectorForm") in {"Expression", "None"}]
    if len(actual) != 29 or set(actual) != expected:
        raise ValueError("Actual shipping metadata changed or removed an original normal CLR method identity")


def selector_contract(method):
    """Derive selector kind and pairing from complete actual formal parameter types."""
    forms = set()
    parameters = []
    for parameter in method["parameters"]:
        value = parameter["type"]
        if parameter["name"] in SELECTOR_PARAMETERS:
            if value.startswith(EXPRESSION_TYPE) and value.endswith(">"):
                value = value[len(EXPRESSION_TYPE):-1]
                if not value.startswith(FUNC_TYPE):
                    raise ValueError("An Expression selector must wrap the actual System.Func type")
                forms.add("Expression")
            elif value.startswith(FUNC_TYPE):
                forms.add("Func")
        parameters.append((parameter["name"], value, parameter.get("refKind", "None")))
    if len(forms) > 1:
        raise ValueError("Normal selector overloads must use a coherent Expression or Func family")
    form = next(iter(forms), "None")
    if method.get("selectorForm") != form:
        raise ValueError("Recorded selector form differs from actual shipping formal parameter types")
    return (method["api"], method["returnType"], method["arity"],
            tuple(method["typeParameters"]), json.dumps(method["genericParameterContracts"], sort_keys=True), tuple(parameters))


def verify_shipping_catalogue(shipping_methods):
    """Preserve 29 original definitions and pair every applicable typed normal definition."""
    catalogue = {method["methodId"]: method for method in shipping_methods}
    if len(catalogue) != len(shipping_methods) or any(not key for key in catalogue):
        raise ValueError("Actual shipping method definitions must have unique complete identities")
    if len({method["signature"] for method in shipping_methods}) != len(shipping_methods):
        raise ValueError("Shipping method definitions must have distinct complete signatures")
    contracts = {form: {} for form in ("Expression", "Func", "None")}
    for method in shipping_methods:
        contract = selector_contract(method)
        family = contracts[method["selectorForm"]]
        if contract in family:
            raise ValueError("A selector family contains duplicate complete formal contracts")
        family[contract] = method
    if len(contracts["Expression"]) != 28 or len(contracts["None"]) != 1:
        raise ValueError("All 29 original definitions must remain: 28 Expression and one selector-free factory")
    if set(contracts["Func"]) != set(contracts["Expression"]):
        raise ValueError("Every original selector contract must have exactly its coherent Func counterpart")
    selector_free = next(iter(contracts["None"].values()))
    if selector_free["api"] != "ValidationBinding.ForViewModel":
        raise ValueError("Only the original callback ForViewModel factory is selector-free")
    return catalogue


def verify_shipping_dispatch(dispatch, shipping_methods):
    """Validate every normal call against exact shipping definitions, preserving dispatch modes."""
    catalogue = verify_shipping_catalogue(shipping_methods)
    covered = set()
    for call in dispatch:
        if call.get("mode") == "RuntimeSafe":
            if call.get("normalShippingCall") or call.get("requiresGenerated"):
                raise ValueError("A shipping normal method cannot be relabeled RuntimeSafe")
            continue
        if call.get("mode") not in {"Generated", "RuntimeRegistered"} or call.get("normalShippingCall") is not True:
            raise ValueError("Every normal call must record its exact generated or registered dispatch mode")
        method = catalogue.get(call.get("methodId"))
        if method is None or any(call.get(key) != method.get(key) for key in (
                "api", "signature", "parameters", "typeParameters", "genericParameterContracts", "selectorForm", "returnType", "arity")):
            raise ValueError("Consumer dispatch differs from its exact shipping method definition")
        if call.get("inlineSelectors") is True and call["selectorForm"] != "Func":
            raise ValueError("Ordinary inline normal selectors did not select the typed Func definition")
        if call["mode"] == "RuntimeRegistered":
            if call.get("requiresGenerated") is not False or call.get("interceptor"):
                raise ValueError("Explicit registered dispatch must retain its normal definition without an interceptor")
            continue
        if call.get("requiresGenerated") is not True or not call.get("interceptor"):
            raise ValueError("A normal generated call has no final consumer interceptor")
        covered.add(call["methodId"])
    return catalogue, covered


def verify_registered_dispatch(dispatch, shipping_methods):
    catalogue, covered = verify_shipping_dispatch(dispatch, shipping_methods)
    normal = [call for call in dispatch if call.get("normalShippingCall")]
    if covered or not normal or any(call["mode"] != "RuntimeRegistered" for call in normal):
        raise ValueError("Precompiled peer must deliberately use registered normal APIs")
    for selector_form in ("Expression", "Func"):
        family = [call for call in normal if call["selectorForm"] == selector_form]
        if not any(call.get("callable") is True for call in family) or not any(call.get("callable") is False for call in family):
            raise ValueError(f"Precompiled peer must retain real {selector_form} normal invocations and callable method groups")
    return normal


def verify_precompiled_normal_il(evidence, binary, reactive):
    """Require real original API calls and method groups in the separately compiled peer."""
    if evidence.get("completed") is not True or Path(evidence.get("path", "")).resolve() != binary.resolve() or evidence.get("sha256") != NATIVE.digest(binary):
        raise ValueError("Fresh peer IL evidence must bind the actual separately compiled binary")
    root = "ReactiveUI.Validation.Reactive" if reactive else "ReactiveUI.Validation"
    owner = root + ".Extensions.ValidatableViewModelExtensions"
    prefix = "GeneratedValidation.Precompiled.PeerRules."
    expected = {prefix + "Rule": ("call", "Expression"), prefix + "Callable": ("ldftn", "Expression"),
                prefix + "DelegateRule": ("call", "Func"), prefix + "DelegateCallable": ("ldftn", "Func")}
    bodies = evidence.get("methods", [])
    if (not evidence.get("mvid") or not isinstance(bodies, list)
            or any(sum(body.get("method") == method and re.fullmatch(r"[a-f0-9]{64}", body.get("ilSha256", "")) is not None
                       for body in bodies) != 1 for method in expected)):
        raise ValueError("Fresh peer normal calls must bind unique actual method bodies and MVID")
    calls = evidence.get("normalApiCalls")
    if not isinstance(calls, list) or len(calls) != len(expected):
        raise ValueError("Fresh peer must retain all four normal Expression/Func call and callable bodies")
    observed = set()
    for call in calls:
        method = call.get("method")
        if method not in expected or method in observed:
            raise ValueError("Fresh peer normal API bodies are duplicated or unexpected")
        opcode, form = expected[method]
        parameters = call.get("parameters", [])
        if (call.get("opcode") != opcode or call.get("owner") != owner or call.get("assembly") != root
                or call.get("target") != "ValidationRule" or call.get("returnType") != root + ".Helpers.ValidationHelper"
                or len(parameters) != 4 or parameters[0] != "!!0" or parameters[2] != "System.Func`2<!!1,System.Boolean>"
                or parameters[3] != "System.String"):
            raise ValueError("Fresh peer does not retain the exact original normal Validation API reference")
        selector = "System.Func`2<!!0,!!1>"
        if form == "Expression":
            selector = "System.Linq.Expressions.Expression`1<" + selector + ">"
        if parameters[1] != selector:
            raise ValueError("Fresh peer normal API selector form differs from its actual callable body")
        observed.add(method)
    return calls


def verify_overload_inventory(dispatch, shipping_methods):
    """Require every exact shipping definition and the original typed shape inventory."""
    catalogue, covered = verify_shipping_dispatch(dispatch, shipping_methods)
    if covered != set(catalogue):
        raise ValueError(f"External consumer did not intercept every exact shipping overload: missing {sorted(set(catalogue) - covered)}")
    inventory = {}
    factories = {}
    for call in dispatch:
        if call.get("mode") in {"RuntimeRegistered", "RuntimeSafe"}:
            continue
        if call.get("requiresGenerated") is True and not call.get("interceptor"):
            raise ValueError("A normal generated call has no final consumer interceptor")
        if not call.get("interceptor"):
            continue  # Supplied-observable overloads are ordinary safe runtime calls.
        parameters = {parameter["name"]: parameter["type"] for parameter in call["parameters"]}
        names = set(parameters)
        api = call["api"]
        form = call["selectorForm"]
        if api.startswith("ValidationBinding."):
            factories[call["signature"]] = call
        elif api.endswith(".ValidationRule") and "isPropertyValid" in names:
            inventory[(form, "rule", "context" in names, "Func<" in parameters.get("message", ""))] = call
        elif api.endswith(".BindValidationContext"):
            inventory[(form, "context", "viewModelProperty" in names, "viewProperty" in names)] = call
        elif api.endswith(".BindValidationState"):
            inventory[(form, "state", "modelProperty" in names, "viewProperty" in names)] = call
        elif api.endswith(".BindValidation"):
            source_kind = "helper" if "viewModelHelperProperty" in names else "property" if "viewModelProperty" in names else "model"
            inventory[(form, "text", source_kind, "formatter" in names)] = call
    original = {(family, first, second) for family in ("rule", "context", "state") for first in (False, True) for second in (False, True)}
    original |= {("text", kind, formatter) for kind in ("helper", "property", "model") for formatter in (False, True)}
    expected = {(form, *shape) for form in ("Expression", "Func") for shape in original}
    if not expected.issubset(inventory.keys()):
        raise ValueError(f"External generator consumer did not dispatch every original overload: missing {sorted(expected - inventory.keys())}")
    for form in ("Expression", "Func", "None"):
        counts = {api: sum(call["api"] == api and call["selectorForm"] == form for call in factories.values()) for api in (
            "ValidationBinding.ForProperty", "ValidationBinding.ForValidationHelperProperty", "ValidationBinding.ForViewModel")}
        required = {"ValidationBinding.ForProperty": 5, "ValidationBinding.ForValidationHelperProperty": 3, "ValidationBinding.ForViewModel": 2} if form != "None" else {
            "ValidationBinding.ForProperty": 0, "ValidationBinding.ForValidationHelperProperty": 0, "ValidationBinding.ForViewModel": 1}
        if counts != required:
            raise ValueError(f"External consumer did not dispatch every {form} static factory: {counts}")
    typed = []
    for form, family, first, second in sorted(expected):
        if family == "rule":
            typed.append({"api": "ValidationRule", "selectorForm": form, "context": "explicit" if first else "default", "message": "dynamic" if second else "static"})
        elif family == "context":
            typed.append({"api": "BindValidationContext", "selectorForm": form, "selection": "property" if first else "aggregate", "destination": "target" if second else "callback"})
        elif family == "state":
            typed.append({"api": "BindValidationState", "selectorForm": form, "selection": "property" if first else "helper", "destination": "target" if second else "callback"})
        else:
            typed.append({"api": "BindValidation", "selectorForm": form, "selection": first, "formatter": "explicit" if second else "default"})
    for shape, record in zip(sorted(expected), typed):
        record.update({key: inventory[shape][key] for key in ("methodId", "signature", "parameters", "typeParameters")})
    typed.extend({"api": call["api"], "selectorForm": call["selectorForm"], "methodId": call["methodId"], "signature": signature, "parameters": call["parameters"], "typeParameters": call["typeParameters"]}
                 for signature, call in sorted(factories.items()))
    return typed


def verify_sdk_warning_origin(project, target_path):
    """Bind the actual imported SDK suppression default to its graph and archive."""
    assets_path = project.parent / "obj/project.assets.json"
    assets = json.loads(assets_path.read_text())
    package_id = "Microsoft.NET.ILLink.Tasks"
    library = assets.get("libraries", {}).get(f"{package_id}/{ILLINK_VERSION}")
    if not library or library.get("type") != "package":
        raise ValueError("SDK warning defaults require the exact locked ILLink task package graph")
    roots = [Path(base) / library["path"] for base in assets["packageFolders"]]
    archives = [root / f"microsoft.net.illink.tasks.{ILLINK_VERSION}.nupkg" for root in roots]
    present = [path for path in archives if path.is_file()]
    if len(present) != 1 or NATIVE.digest(present[0]) != ILLINK_ARCHIVE_SHA256:
        raise ValueError("SDK task archive differs from the exact immutable input")
    archive = present[0]
    raw_sha512 = base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode()
    metadata = json.loads((archive.parent / ".nupkg.metadata").read_text())
    if (raw_sha512 != ILLINK_ARCHIVE_SHA512
            or archive.with_suffix(".nupkg.sha512").read_text().strip() != raw_sha512
            or metadata.get("contentHash") != ILLINK_CONTENT_SHA512
            or library.get("sha512") != ILLINK_CONTENT_SHA512):
        raise ValueError("SDK task raw archive and signed NuGet content hash provenance differ")
    with zipfile.ZipFile(archive) as package:
        dlls = [name for name in package.namelist() if name.casefold().endswith(".dll")]
        if ".signature.p7s" not in package.namelist() or not dlls:
            raise ValueError("Exact SDK task must retain its signed archive and compiler tools")
        for name in dlls:
            extracted = archive.parent / name
            if not extracted.is_file() or NATIVE.digest(extracted) != hashlib.sha256(package.read(name)).hexdigest():
                raise ValueError(f"SDK task extracted DLL differs from its exact signed archive: {name}")
    target = Path(target_path)
    matches = [root for root in roots if target.resolve() == (root / "build/Microsoft.NET.ILLink.targets").resolve()]
    if len(matches) != 1 or not target.is_file() or NATIVE.digest(target) != ILLINK_TARGET_SHA256:
        raise ValueError("Actual ILLink default target must match the exact locked SDK package")
    if archive.parent.resolve() != matches[0].resolve():
        raise ValueError("Actual SDK import and verified archive use different package roots")
    with zipfile.ZipFile(archive) as package:
        if package.read("build/Microsoft.NET.ILLink.targets") != target.read_bytes():
            raise ValueError("Extracted SDK default target differs from its verified archive")
    return {"verified": True, "id": package_id, "version": ILLINK_VERSION,
            "archive": str(archive), "archiveSha256": ILLINK_ARCHIVE_SHA256,
            "archiveSha512": raw_sha512, "signedContentSha512": ILLINK_CONTENT_SHA512,
            "target": str(target), "targetSha256": ILLINK_TARGET_SHA256,
            "graphSha256": NATIVE.digest(assets_path), "defaultDiagnostic": "IL2121"}


def verify_warning_policy(no_warn, publish_trimmed, redundant_suppressions, defines, sdk_origin=None):
    """Allow only the byte-proven SDK default, never authored IL suppression."""
    publishing = bool({"GENERATED_GATE_TRIMMED", "GENERATED_GATE_NATIVE"} & set(defines))
    if publish_trimmed != ("true" if publishing else "") or redundant_suppressions != "":
        raise ValueError("Actual compiler policy must match its exact locked SDK publish stage")
    expected = ["1701", "1702", "8002", "IL2121"] if publishing else ["1701", "1702", "8002"]
    if no_warn != expected:
        raise ValueError("Actual CoreCompile warning policy exceeds the exact locked SDK stage defaults")
    if publishing and (not sdk_origin or sdk_origin.get("verified") is not True
                       or sdk_origin.get("archiveSha256") != ILLINK_ARCHIVE_SHA256
                       or sdk_origin.get("targetSha256") != ILLINK_TARGET_SHA256):
        raise ValueError("SDK IL2121 default requires verified actual archive and imported target bytes")



def verify_aot_configuration(records, semantics):
    """Bind analyzer-visible AOT globals to actual MSBuild inputs and config bytes."""
    properties = {"publishAot": "PublishAot", "isAotCompatible": "IsAotCompatible", "enableAotAnalyzer": "EnableAotAnalyzer"}
    expected = {}
    for key, property_name in properties.items():
        values = [value for kind, value in records if kind == key]
        if len(values) != 1 or values[0] not in {"", "true", "false"}:
            raise ValueError("Missing or invalid actual MSBuild AOT property capture")
        expected[key] = values[0]
    if semantics.get("aotProperties") != expected:
        raise ValueError("Semantic replay changed actual AOT property values")
    stages = {value.removeprefix("GENERATED_GATE_") for kind, value in records
              if kind == "define" and value.startswith("GENERATED_GATE_")}
    expected_aot = "true" if "NATIVE" in stages else "false" if "TRIMMED" in stages else ""
    if expected["publishAot"] != expected_aot or expected["enableAotAnalyzer"] != "true":
        raise ValueError("Actual AOT analyzer properties do not match the executed compiler stage")
    paths = sorted({value for kind, value in records if kind == "analyzerConfig"})
    evidence = semantics.get("analyzerConfigs", [])
    if not paths or sorted(item["path"] for item in evidence) != paths or len(evidence) != len(paths):
        raise ValueError("Semantic replay omitted or duplicated actual analyzer configs")
    delivered = {key: [] for key in properties}
    for item in evidence:
        path = Path(item["path"])
        if NATIVE.digest(path) != item["sha256"]:
            raise ValueError("Semantic replay has stale analyzer config bytes")
        text = re.split(r"(?m)^\s*\[", path.read_text(encoding="utf-8-sig"), maxsplit=1)[0]
        global_values = re.findall(r"(?m)^\s*is_global\s*=([^\r\n]*)$", text)
        if not global_values:
            continue
        if [value.strip() for value in global_values] != ["true"]:
            raise ValueError("Compiler config global-scope identity is missing or ambiguous")
        for key, property_name in properties.items():
            matches = re.findall(r"(?m)^\s*build_property\." + property_name + r"\s*=([^\r\n]*)$", text)
            delivered[key].extend(value.strip() for value in matches)
    if any(delivered[key] != [expected[key]] for key in properties):
        raise ValueError("Packed assets did not deliver the exact actual AOT globals to the compiler")
    return {"properties": expected, "configs": evidence}


def verify_semantic_inputs(project, semantics, require_declarations=True):
    """Reject incomplete or stale replay before trusting any overload/signature claim."""
    generated_root = project.parent / "obj/generated"
    manifest = project.parent / "obj/generated-compiler-input.txt"
    if semantics.get("completed") is not True or semantics.get("manifest", {}).get("sha256") != NATIVE.digest(manifest):
        raise ValueError("Semantic replay must match the completed current compiler manifest")
    lines = [line.split("|", 1) for line in manifest.read_text(encoding="utf-8-sig").splitlines() if line]
    if any(len(record) != 2 or record[0] not in {"source", "reference", "analyzer", "define", "assembly", "output", "language", "nullable", "allowlist", "profile", "nowarn", "redundantSuppressions", "publishTrimmed", "ilLinkTarget", "publishAot", "isAotCompatible", "enableAotAnalyzer", "analyzerConfig"} for record in lines):
        raise ValueError("Semantic replay has a malformed exact compiler input manifest")
    for kind in ("assembly", "output", "language", "nullable"):
        values = [value for category, value in lines if category == kind]
        if len(values) != 1 or semantics.get(kind) != values[0]:
            raise ValueError(f"Semantic replay changed the exact compiler {kind}")
    if semantics.get("defines") != sorted({value for category, value in lines if category == "define"}):
        raise ValueError("Semantic replay changed exact consumer preprocessor symbols")
    if semantics.get("allowlist") != sorted({value for category, value in lines if category == "allowlist"}):
        raise ValueError("Semantic replay changed exact consumer interceptor allowlist")
    no_warn = sorted({value for category, value in lines if category == "nowarn"})
    policy = {}
    for kind in ("redundantSuppressions", "publishTrimmed", "ilLinkTarget"):
        values = [value for category, value in lines if category == kind]
        if len(values) != 1 or semantics.get(kind) != values[0]:
            raise ValueError("Semantic replay changed the exact captured SDK warning policy")
        policy[kind] = values[0]
    if semantics.get("noWarn") != no_warn:
        raise ValueError("Semantic replay changed the exact captured warning policy")
    sdk_origin = verify_sdk_warning_origin(project, policy["ilLinkTarget"]) if policy["publishTrimmed"] == "true" else None
    verify_warning_policy(no_warn, policy["publishTrimmed"], policy["redundantSuppressions"], semantics["defines"], sdk_origin)
    verify_aot_configuration(lines, semantics)
    profiles = sorted({value for category, value in lines if category == "profile"})
    if semantics.get("producerProfiles") != profiles or profiles != ["Avalonia=", "Binding=9.1.0", "Maui=", "Reactive=4.2.0"]:
        raise ValueError("Real package transitive targets did not resolve the exact producer profiles from analyzer assets")
    for kind, key in (("source", "originalSources"), ("reference", "references"), ("analyzer", "analyzers")):
        expected = sorted({value for category, value in lines if category == kind})
        evidence = semantics.get(key, [])
        if sorted(record["path"] for record in evidence) != expected or not expected:
            raise ValueError(f"Semantic replay omitted or invented exact compiler {kind} inputs")
        for record in evidence:
            if NATIVE.digest(Path(record["path"])) != record["sha256"]:
                raise ValueError(f"Semantic replay contains stale compiler {kind} bytes")
    assemblies = semantics.get("validationAssemblies", [])
    if len(assemblies) != 1 or assemblies[0].get("assembly") not in {"ReactiveUI.Validation", "ReactiveUI.Validation.Reactive"}:
        raise ValueError("Semantic replay must identify exactly the matching shipping core assembly")
    core = assemblies[0]
    if core.get("sha256") != NATIVE.digest(Path(core["path"])) or core["path"] not in {record["path"] for record in semantics["references"]}:
        raise ValueError("Shipping core metadata does not match actual compiler reference bytes")
    if any(str(core.get("metadata", {}).get(key)).lower() != "true" for key in ("IsAotCompatible", "IsTrimmable")):
        raise ValueError("Final shipping assembly must carry the audited AOT and trimming contract")
    expected_sources = {path.relative_to(generated_root).as_posix(): NATIVE.digest(path) for path in generated_root.rglob("*.cs")}
    actual_sources = {record["path"]: record["sha256"] for record in semantics.get("generatedSources", [])}
    if actual_sources != expected_sources or len(actual_sources) != len(semantics.get("generatedSources", [])):
        raise ValueError("Semantic replay omitted, duplicated, or invented emitted source bytes")
    for record in semantics["generatedSources"]:
        owned = "ReactiveUI.Validation.SourceGenerators" in Path(record["path"]).parts
        if record.get("validationOwned") is not owned:
            raise ValueError("Semantic replay assigned an emitted source to the wrong generator owner")
    if require_declarations and not semantics.get("declarations"):
        raise ValueError("Semantic replay must retain the generated semantic declaration inventory")
    return sdk_origin



def retain_compiler_inputs(project, semantics, retained):
    """Keep caller/SDK source bytes after disposable projects are removed."""
    retained.mkdir(parents=True, exist_ok=True)
    records = []
    source_inputs = [*semantics["originalSources"], *semantics["analyzerConfigs"]]
    if semantics.get("publishTrimmed") == "true":
        source_inputs.append({"path": semantics["ilLinkTarget"]})
    for index, source in enumerate(source_inputs):
        destination = retained / "original" / f"{index:03}-{Path(source['path']).name}"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source["path"], destination)
        records.append({"originalPath": source["path"], "path": f"{retained.name}/{destination.relative_to(retained).as_posix()}",
                        "sha256": NATIVE.digest(destination)})
    generated_root = project.parent / "obj/generated"
    for source in semantics["generatedSources"]:
        destination = retained / "generated" / source["path"]
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(generated_root / source["path"], destination)
        records.append({"originalPath": str(generated_root / source["path"]),
                        "path": f"{retained.name}/{destination.relative_to(retained).as_posix()}", "sha256": NATIVE.digest(destination)})
    return records


def retain_failed_compile(project, output, stem, capture="failed"):
    """Keep unaccepted compiler inputs before build or semantic failures remove the project."""
    retained = output / f"{stem}-{capture}-inputs"
    retained.mkdir(parents=True, exist_ok=True)
    manifest = project.parent / "obj/generated-compiler-input.before.txt"
    records = []
    if manifest.is_file():
        records = [line.split("|", 1) for line in manifest.read_text(encoding="utf-8-sig").splitlines() if "|" in line]
    final_manifest = project.parent / "obj/generated-compiler-input.txt"
    final_captured = capture == "before-semantic" and final_manifest.is_file()
    candidates = [project, manifest, project.parent / "obj/project.assets.json"]
    if final_captured:
        candidates.append(final_manifest)
    candidates += sorted((project.parent / "obj").glob("*.nuget.g.*"))
    candidates += [Path(value) for kind, value in records if kind in {"source", "analyzerConfig", "compilerTarget", "ilLinkTarget"}]
    candidates += [project.parent.parent / "Directory.Build.props", project.parent.parent / "Directory.Build.targets"]
    files = []
    for index, path in enumerate(dict.fromkeys(candidates)):
        if not path.is_file():
            continue
        destination = retained / f"{index:03}-{path.name}"
        shutil.copyfile(path, destination)
        files.append({"originalPath": str(path), "file": str(destination.relative_to(output)), "sha256": NATIVE.digest(destination)})
    generated = []
    generated_root = project.parent / "obj/generated"
    for path in sorted(generated_root.rglob("*.cs")):
        destination = output / f"{stem}-{capture}-generated" / path.relative_to(generated_root)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)
        generated.append({"file": str(destination.relative_to(output)), "sha256": NATIVE.digest(destination)})
    return {"accepted": False, "preCoreCompile": True, "effectiveSdkNoWarnCaptured": final_captured,
            "compilerInputCaptured": manifest.is_file(), "records": records, "files": files, "generatedSources": generated,
            "referencedBytes": [{"kind": kind, "path": value, "sha256": NATIVE.digest(Path(value))}
                                for kind, value in records if kind in {"reference", "analyzer"} and Path(value).is_file()]}



def retain_published_application(executable, assembly, output, stem):
    """Retain actual post-link application bytes before executing a disposable publish."""
    if not executable.is_file():
        raise ValueError("Published application executable is missing before runtime")
    destination = output / f"{stem}-runtime"
    destination.mkdir(parents=True, exist_ok=True)
    paths = [executable, *[executable.parent / f"{assembly}{suffix}"
                          for suffix in (".dll", ".deps.json", ".runtimeconfig.json", ".pdb", ".dbg")]]
    records = []
    for path in sorted(set(paths)):
        if not path.is_file():
            continue
        retained = destination / path.name
        shutil.copyfile(path, retained)
        records.append({"file": str(retained.relative_to(output)), "sha256": NATIVE.digest(retained),
                        "bytes": retained.stat().st_size, "publishedPath": str(path)})
    return {"postLink": True, "executable": str((destination / executable.name).relative_to(output)),
            "files": records}


def verify_generated_sources(project, retained=None, semantics=None):
    generated_root = project.parent / "obj/generated"
    owned = sorted(path for path in generated_root.rglob("*.cs") if "ReactiveUI.Validation.SourceGenerators" in path.relative_to(generated_root).parts)
    if not owned:
        raise ValueError("External package consumer did not emit Validation-owned sources")
    if semantics is None:
        raise ValueError("Generated acceptance requires actual semantic dispatch evidence")
    sdk_origin = verify_semantic_inputs(project, semantics)
    inventory = verify_overload_inventory(semantics.get("dispatch", []), semantics.get("shippingMethods", []))
    for path in owned:
        source = path.read_text()
        if re.search(r"\.Compile\s*\(|\.Get(?:Propert(?:y|ies)|Fields?|Methods?|Members?|Constructors?)\s*\(|"
                     r"\.SetValue\s*\(|Activator\.CreateInstance\s*\(|Unsafe\s*\(|WhenAnyValue(?:Unsafe)?\s*\(|"
                     r"#pragma\s+warning\s+disable|SuppressMessage|DynamicDependency", source):
            raise ValueError(f"Generated Validation source hides reflection or diagnostic suppression: {path}")
    files = []
    if retained is not None:
        if retained.exists():
            shutil.rmtree(retained)
        for path in owned:
            destination = retained / path.relative_to(generated_root)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, destination)
            files.append({"path": f"{retained.name}/{destination.relative_to(retained).as_posix()}", "sha256": NATIVE.digest(destination)})
    return {"sourceCount": len(owned), "overloads": inventory, "files": files,
            "dispatch": semantics["dispatch"], "shippingMethods": semantics["shippingMethods"], "declarations": semantics["declarations"],
            "sdkWarningPolicy": sdk_origin,
            "aotConfiguration": {"properties": semantics["aotProperties"], "configs": semantics["analyzerConfigs"]}}



def verify_expression_il(evidence, binary, negative=False, host=False):
    """Require actual caller IL rather than inferring construction from generated getters."""
    methods = evidence.get("methods", [])
    if (evidence.get("completed") is not True or evidence.get("sha256") != NATIVE.digest(binary)
            or Path(evidence.get("path", "")).resolve() != binary.resolve() or not evidence.get("mvid")
            or not methods or len({method["token"] for method in methods}) != len(methods)
            or any(not re.fullmatch(r"[0-9a-f]{64}", method.get("ilSha256", "")) for method in methods)):
        raise ValueError("Expression construction proof must bind every inspected method to actual current PE bytes")
    factories = evidence.get("arrayFactories")
    if not isinstance(factories, list):
        raise ValueError("Expression construction proof omitted the actual factory inventory")
    if host:
        calls = evidence.get("entryCalls", [])
        expected = {"method": "Program.Main", "opcode": "call", "owner": "RuleExpressionParamsNegative.Program", "target": "Run"}
        if factories or len(calls) != 1 or any(calls[0].get(key) != value for key, value in expected.items()):
            raise ValueError("Native negative Main must directly invoke the unchanged retained Expression peer Run")
    elif negative:
        if len(factories) != 1:
            raise ValueError("Old Expression peer must retain exactly the original expanded-array factory")
        call = factories[0]
        expected = {"method": "RuleExpressionParamsNegative.Program.Run", "opcode": "call",
                    "owner": "System.Linq.Expressions.Expression", "target": "NewArrayInit",
                    "assembly": "System.Linq.Expressions", "returnType": "System.Linq.Expressions.NewArrayExpression",
                    "parameters": ["System.Type", "System.Linq.Expressions.Expression[]"]}
        if any(call.get(key) != value for key, value in expected.items()):
            raise ValueError("Old Expression peer's actual factory owner, method or formal signature is incorrect")
    elif factories or sum(method["method"] == "RuleCapabilityCases.IndexedBoundary.Model.Check" for method in methods) != 1:
        raise ValueError("Positive normal Func and preconstructed Expression params callers must retain no dynamic array factory")
    return evidence


def guard_consumer_sources(folder):
    NATIVE.guard_consumer_sources(folder)
    for path in folder.rglob("*.cs"):
        if set(path.relative_to(folder).parts) & {"bin", "obj", "artifacts", "packages"}:
            continue
        guard_positive_source(path)


def guard_positive_source(path):
    source = path.read_text()
    if re.search(r"#pragma\s+warning\s+disable|SuppressMessage|DynamicDependency|DynamicallyAccessedMembers|RequiresUnreferencedCode|RequiresDynamicCode", source):
        raise ValueError(f"Suppression, preservation or hidden analysis boundary: {path}")
    if re.search(r"\b(?:ValidationRule|BindValidation|BindValidationState|BindValidationContext)Unsafe\s*\(", source):
        raise ValueError("Generated acceptance consumer must exercise normal call names")


def write_report(output, report):
    (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")


def initialize_report(output, report):
    """Invalidate prior success before any input or toolchain check can fail."""
    report["completed"] = False
    write_report(output, report)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", choices=("linux-x64", "win-x64"), required=True)
    parser.add_argument("--mode", choices=("all", "managed", "trimmed", "native"), default="all")
    parser.add_argument("--package-feed", type=Path, help="Use exact same-SHA CI package artifact; otherwise pack current source")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/verification/generated-gates")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    report = {"source": None, "dirty": None, "sdk": None, "rid": args.rid, "mode": args.mode,
              "dynamicDataSha256": DEPENDENCIES.DIGESTS, "packages": {}, "checks": []}
    initialize_report(output, report)
    if platform.system() != ("Windows" if args.rid == "win-x64" else "Linux") or platform.machine().lower() not in {"x86_64", "amd64"}:
        raise ValueError("Execution requires a matching x64 host for the selected RID")
    sdk = subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip()
    if sdk != json.loads((ROOT / "global.json").read_text())["sdk"]["version"]:
        raise ValueError("Pinned SDK is required")
    if subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip():
        raise ValueError("Strict generated acceptance requires a clean current source checkout")
    source = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    report.update({"source": source, "dirty": False, "sdk": sdk})
    write_report(output, report)
    print(f"Available space: {shutil.disk_usage(output).free // (1024 ** 3)} GiB", flush=True)
    fixture = ROOT / "examples/GeneratedValidation"
    guard_consumer_sources(fixture)
    capability_map_path = ROOT / "eng/verification-fixtures/generated-capability-cases.json"
    capability_map = verify_capability_map(json.loads(capability_map_path.read_text()))
    shutil.copyfile(capability_map_path, output / "capability-cases.json")
    report["capabilityMap"] = {"file": "capability-cases.json", "sha256": NATIVE.digest(capability_map_path),
                              "rows": capability_map["rows"], "cases": sorted(CASES)}
    original_api_path = ROOT / "eng/verification-fixtures/original-normal-api.json"
    original_api = json.loads(original_api_path.read_text())
    shutil.copyfile(original_api_path, output / original_api_path.name)
    report["originalNormalApi"] = {"file": original_api_path.name, "sha256": NATIVE.digest(original_api_path),
                                   "originalSource": original_api["source"], "originalPackageSha256": original_api["packageSha256"]}
    shared_fixture_paths = [ROOT / "eng/verification-fixtures" / name for name in (
        "RuleCapabilityScenario.cs", "ProducerInteropScenario.cs", "ErrorHookScenario.cs", "PeerGeneratedCallerScenario.cs",
        "BindingCapabilityScenario.cs", "BindingCapabilityScenario.Types.cs")]
    for shared_fixture in shared_fixture_paths:
        guard_positive_source(shared_fixture)
    report["sharedFixtureSources"] = [{"file": str(path.relative_to(ROOT)), "sha256": NATIVE.digest(path)} for path in shared_fixture_paths]
    write_report(output, report)
    pins = PACKAGES.dependency_pins(ROOT)
    for package_id, expected in DEPENDENCIES.DIGESTS.items():
        package = ROOT / "artifacts/dependencies" / f"{package_id}.{pins[package_id.casefold()]}.nupkg"
        if NATIVE.digest(package) != expected:
            raise ValueError("DynamicData feed must match the SHA-verified immutable release")
    with tempfile.TemporaryDirectory(prefix="work-", dir=output) as temporary:
        work = Path(temporary)
        feed = args.package_feed.resolve() if args.package_feed else work / "candidate-feed"
        if not args.package_feed:
            NATIVE.run(["dotnet", "pack", "ReactiveUI.Validation.slnx", "-c", "Release", "-m:2", "-warnaserror", "-o", feed], output / "candidate-pack.log", ROOT / "src")
            retained = output / "candidate-packages" / source
            retained.mkdir(parents=True, exist_ok=True)
            for package in feed.glob("*.nupkg"):
                shutil.copyfile(package, retained / package.name)
            feed = retained
        inspector_output = output / "semantic-inspector"
        NATIVE.run(["dotnet", "build", ROOT / "eng/GeneratedSourceInspector/GeneratedSourceInspector.csproj",
                    "-c", "Release", "-m:2", "-warnaserror", "-o", inspector_output], output / "semantic-inspector.build.log", ROOT / "src")
        inspector = inspector_output / "GeneratedSourceInspector.dll"
        report["semanticInspector"] = {"source": source, "sha256": NATIVE.digest(inspector),
                                       "compilerManifest": "generated-compiler-input.txt", "roslynVersion": pins["microsoft.codeanalysis.csharp"]}
        write_report(output, report)
        configuration_path = ROOT / "eng/verify-generated-configuration.py"
        configuration_spec = importlib.util.spec_from_file_location("generated_configuration_gate", configuration_path)
        configuration = importlib.util.module_from_spec(configuration_spec)
        configuration_spec.loader.exec_module(configuration)
        metadata_project = ROOT / "eng/tools/InspectIl/InspectIl.csproj"
        metadata_output = output / "metadata-inspector"
        NATIVE.run(["dotnet", "build", metadata_project, "-c", "Release", "-m:2", "-warnaserror", "-o", metadata_output],
                   output / "metadata-inspector.build.log", ROOT)
        metadata_inspector = metadata_output / "InspectIl.dll"
        shutil.copyfile(metadata_inspector, output / "MetadataInspector.dll")
        metadata_inputs = output / "metadata-inspector-inputs"
        metadata_inputs.mkdir(exist_ok=True)
        metadata_sources = []
        for path in sorted([metadata_project, *metadata_project.parent.glob("*.cs")]):
            shutil.copyfile(path, metadata_inputs / path.name)
            metadata_sources.append({"file": f"{metadata_inputs.name}/{path.name}", "sha256": NATIVE.digest(path)})
        report["configurationTool"] = {"source": source, "file": str(configuration_path.relative_to(ROOT)),
                                       "sha256": NATIVE.digest(configuration_path)}
        report["metadataInspector"] = {"source": source, "assembly": "MetadataInspector.dll",
                                       "sha256": NATIVE.digest(metadata_inspector), "inputs": metadata_sources}
        write_report(output, report)
        peer_generator_project = ROOT / "eng/PeerGeneratedCaller/PeerGeneratedCaller.csproj"
        peer_generator_output = output / "peer-generator"
        NATIVE.run(["dotnet", "build", peer_generator_project, "-c", "Release", "-m:2", "-warnaserror", "-o", peer_generator_output],
                   output / "peer-generator.build.log", ROOT)
        peer_generator = peer_generator_output / "Runic.Validation.PeerGeneratedCaller.dll"
        shutil.copyfile(peer_generator, output / peer_generator.name)
        peer_generator_inputs = output / "peer-generator-inputs"
        peer_generator_inputs.mkdir(exist_ok=True)
        generator_sources = []
        for path in sorted(peer_generator_project.parent.glob("*.cs")):
            shutil.copyfile(path, peer_generator_inputs / path.name)
            generator_sources.append({"file": f"{peer_generator_inputs.name}/{path.name}", "sha256": NATIVE.digest(path)})
        shutil.copyfile(peer_generator_project, peer_generator_inputs / peer_generator_project.name)
        report["peerGenerator"] = {"source": source, "assembly": peer_generator.name, "sha256": NATIVE.digest(peer_generator),
                                   "projectSha256": NATIVE.digest(peer_generator_project), "sources": generator_sources}
        write_report(output, report)
        expression_path = ROOT / "eng/verify-expression-params.py"
        expression_spec = importlib.util.spec_from_file_location("expression_params_controls", expression_path)
        expression_controls = importlib.util.module_from_spec(expression_spec)
        expression_spec.loader.exec_module(expression_controls)
        report["expressionControlTool"] = {"source": source, "sha256": NATIVE.digest(expression_path)}
        for reactive, package_id, version, package in NATIVE.packed_versions(feed, pins, source):
            flavor = "Reactive" if reactive else "Primitives"
            folder = work / flavor
            shutil.copytree(fixture, folder, ignore=shutil.ignore_patterns("bin", "obj", "artifacts", ".editorconfig"))
            project = folder / flavor / "Examples.csproj"
            config = NATIVE.write_config(folder, feed)
            cache = output / "packages"
            cached = cache / package_id.lower() / version
            cached_package = cached / f"{package_id.lower()}.{version}.nupkg"
            package_hash = NATIVE.digest(package)
            if cached.exists() and (not cached_package.exists() or NATIVE.digest(cached_package) != package_hash):
                shutil.rmtree(cached)
            report["packages"][flavor] = {"id": package_id, "version": version, "sha256": package_hash, **verify_generator_assets(package)}
            flags = ["-c", "Release", "-m:2", "-warnaserror", "--configfile", config,
                     f"-p:ValidationVersion={version}", f"-p:RestorePackagesPath={cache}",
                     f"-p:CapabilityFixtureRoot={ROOT / 'eng/verification-fixtures'}",
                     "-p:EnableTrimAnalyzer=true", "-p:EnableAotAnalyzer=true", "-p:TreatWarningsAsErrors=true",
                     "-p:RunAnalyzers=true", "-p:RunAnalyzersDuringBuild=true", "-p:TrimmerSingleWarn=false",
                     "-p:ILLinkTreatWarningsAsErrors=true", "-p:IlcTreatWarningsAsErrors=true",
                     "-p:SuppressTrimAnalysisWarnings=false", "-p:SuppressAotAnalysisWarnings=false"]
            peer_project = project.parent / "Precompiled/Precompiled.csproj"
            peer_assembly = peer_project.parent / f"bin/Release/net10.0/GeneratedValidation.Precompiled.{flavor}.dll"
            _, peer_build = NATIVE.run(["dotnet", "build", peer_project, *flags], output / f"{flavor}-precompiled.build.log", folder)
            if re.search(r"\bwarning [A-Z]+\d+\b", peer_build):
                raise ValueError("Precompiled consumer emitted a warning")
            peer_assets = json.loads((peer_project.parent / "obj/project.assets.json").read_text())
            NATIVE.verify_native_graph(peer_assets, reactive, pins, version)
            verify_no_roslyn_runtime(peer_assets)
            PACKAGES.verify_restored_bytes(peer_assets, package_id, version, package_hash)
            peer_semantic = output / f"{flavor}-precompiled.semantic.json"
            peer_manifest = peer_project.parent / "obj/generated-compiler-input.txt"
            NATIVE.run(["dotnet", inspector, peer_manifest, peer_project.parent / "obj/generated", peer_semantic, "registered"],
                       output / f"{flavor}-precompiled.semantic.log", folder)
            peer_evidence = json.loads(peer_semantic.read_text())
            verify_preserved_expression_api(peer_evidence["shippingMethods"], original_api)
            verify_semantic_inputs(peer_project, peer_evidence, require_declarations=False)
            verify_registered_dispatch(peer_evidence["dispatch"], peer_evidence["shippingMethods"])
            retained_peer = output / f"GeneratedValidation.Precompiled.{flavor}.dll"
            shutil.copyfile(peer_assembly, retained_peer)
            peer_il_report = output / f"{flavor}-precompiled.original-il.json"
            NATIVE.run(["dotnet", inspector, "expression-il", retained_peer, peer_il_report],
                       output / f"{flavor}-precompiled.original-il.log", folder)
            peer_il = json.loads(peer_il_report.read_text())
            peer_original_calls = verify_precompiled_normal_il(peer_il, retained_peer, reactive)
            shutil.copyfile(peer_manifest, output / f"{flavor}-precompiled.compiler-input.txt")
            (output / f"{flavor}-precompiled.graph.json").write_text(json.dumps(peer_assets, indent=2))
            report["checks"].append({"name": f"{flavor}-precompiled", "passed": True, "source": source,
                                     "packageSha256": package_hash, "assembly": retained_peer.name,
                                     "assemblySha256": NATIVE.digest(retained_peer), "semanticReport": peer_semantic.name,
                                     "originalIlReport": peer_il_report.name, "originalApiCalls": peer_original_calls,
                                     "dispatch": peer_evidence["dispatch"],
                                     "compilerSources": retain_compiler_inputs(peer_project, peer_evidence, output / f"{flavor}-precompiled-inputs")})
            write_report(output, report)
            # The app consumes this independently compiled binary; no project source or compiler graph is inherited.
            legacy_folder = ROOT / "eng/verification-fixtures/LegacyPeer"
            legacy_assembly = legacy_folder / "frozen" / f"GeneratedValidation.LegacyPeer.{flavor}.dll"
            legacy_semantic = output / f"{flavor}-historical-peer.il.json"
            NATIVE.run(["dotnet", inspector, "legacy-pe", legacy_assembly, legacy_semantic],
                       output / f"{flavor}-historical-peer.il.log", folder)
            legacy_assembly, legacy_provenance = verify_legacy_peer(legacy_folder, flavor, json.loads(legacy_semantic.read_text()))
            retained_legacy = output / legacy_assembly.name
            shutil.copyfile(legacy_assembly, retained_legacy)
            shutil.copyfile(legacy_folder / "provenance.json", output / "historical-peer.provenance.json")
            shutil.copyfile(legacy_folder / "frozen" / f"{flavor}.compiler-input.txt", output / f"{flavor}-historical-peer.compiler-input.txt")
            shutil.copyfile(legacy_folder / "LegacyCaller.cs", output / "historical-peer.source.cs")
            shutil.copyfile(legacy_folder / "frozen" / f"{flavor}.graph.json", output / f"{flavor}-historical-peer.graph.json")
            shutil.copyfile(legacy_folder / "frozen" / f"{flavor}.il.json", output / f"{flavor}-historical-peer.original-il.json")
            shutil.copyfile(legacy_folder / "frozen" / f"{flavor}.build.log", output / f"{flavor}-historical-peer.original-build.log")
            historical_inputs = output / f"{flavor}-historical-peer-inputs"
            historical_inputs.mkdir(exist_ok=True)
            retained_sources = []
            for item in legacy_provenance["originalCompilerSources"]:
                retained = historical_inputs / Path(item["file"]).name
                shutil.copyfile(legacy_folder / item["file"], retained)
                retained_sources.append({"file": str(retained.relative_to(output)), "sha256": NATIVE.digest(retained),
                                         "originalPath": item["originalPath"]})
            shutil.copyfile(legacy_folder / flavor / "LegacyPeer.csproj", historical_inputs / "LegacyPeer.csproj")
            report["checks"].append({"name": f"{flavor}-historical-peer", "passed": True,
                                     "compiledAgainstSource": LEGACY_SOURCE, "compiledAgainstVersion": LEGACY_VERSION,
                                     "compiledAgainstDynamicData": "10.0.0-runic.5", "assembly": retained_legacy.name,
                                     "assemblySha256": NATIVE.digest(retained_legacy), "originalApiIL": legacy_semantic.name,
                                     "provenance": legacy_provenance, "originalCompilerSources": retained_sources,
                                     "currentPackageSha256": package_hash})
            write_report(output, report)
            flags += [f"-p:PrecompiledConsumerPath={peer_assembly}", f"-p:LegacyPrecompiledConsumerPath={legacy_assembly}"]
            flags += [f"-p:PeerGeneratedCallerAnalyzerPath={peer_generator}"]
            configuration_results = configuration.run_packed_cases(
                root=ROOT, work=work, output=output, source=source, reactive=reactive,
                package_id=package_id, version=version, package=package, package_hash=package_hash,
                cache=cache, config=config, pins=pins, run=NATIVE.run,
                verify_restored_bytes=PACKAGES.verify_restored_bytes, metadata_inspector=metadata_inspector)
            report["checks"].extend(verify_configuration_results(configuration_results, flavor, source, package_hash))
            write_report(output, report)
            stages = ["managed", "trimmed", "native"] if args.mode == "all" else [args.mode]
            for stage in stages:
                stem = f"{flavor}-{stage}"
                publish = folder / f"publish-{stage}"
                generated_root = project.parent / "obj/generated"
                if generated_root.exists():
                    shutil.rmtree(generated_root)
                command = ["dotnet", "build" if stage == "managed" else "publish", project, *flags,
                           f"-p:GeneratedVerificationStage={stage.upper()}"]
                rid = None
                if stage != "managed":
                    rid = args.rid
                    command += ["-r", rid, "--self-contained", "true", "-o", publish,
                                *NATIVE.strict_positive_publish_flags(stage)]
                code, content = NATIVE.run(command, output / f"{stem}.build.log", folder, expected=True)
                if code != 0 or re.search(r"\bwarning [A-Z]+\d+\b", content):
                    failure = retain_failed_compile(project, output, stem)
                    failure.update({"source": source, "packageSha256": package_hash, "stage": stage,
                                    "flavor": flavor, "exitCode": code, "log": f"{stem}.build.log"})
                    report["failedCompile"] = failure
                    write_report(output, report)
                    if code != 0:
                        raise RuntimeError(f"Command failed ({code}); retained partial inputs: {stem}-failed-inputs")
                    raise ValueError("Successful generated consumer emitted a warning")
                assets = json.loads((project.parent / "obj/project.assets.json").read_text())
                NATIVE.verify_native_graph(assets, reactive, pins, version, rid)
                verify_no_roslyn_runtime(assets)
                PACKAGES.verify_restored_bytes(assets, package_id, version, package_hash)
                dynamic_data = PACKAGES.flavor_ids(reactive)[1]
                PACKAGES.verify_restored_bytes(assets, dynamic_data, pins[dynamic_data.casefold()], DEPENDENCIES.DIGESTS[dynamic_data])
                (output / f"{stem}.graph.json").write_text(json.dumps(assets, indent=2))
                semantic_report = output / f"{stem}.semantic.json"
                manifest = project.parent / "obj/generated-compiler-input.txt"
                pending = retain_failed_compile(project, output, stem, capture="before-semantic")
                pending.update({"source": source, "packageSha256": package_hash, "stage": stage,
                                "flavor": flavor, "buildExitCode": code})
                report["pendingSemantic"] = pending
                shutil.copyfile(manifest, output / f"{stem}.compiler-input.txt")
                write_report(output, report)
                NATIVE.run(["dotnet", inspector, manifest, project.parent / "obj/generated", semantic_report],
                           output / f"{stem}.semantic.log", folder)
                semantics = json.loads(semantic_report.read_text())
                verify_preserved_expression_api(semantics["shippingMethods"], original_api)
                generated_evidence = verify_generated_sources(project, output / f"{stem}-generated", semantics)
                generated_evidence["readContracts"] = verify_readflow_bridges(semantics)
                shutil.copyfile(manifest, output / f"{stem}.compiler-input.txt")
                generated_evidence["semanticReport"] = semantic_report.name
                generated_evidence["compilerSources"] = retain_compiler_inputs(project, semantics, output / f"{stem}-inputs")
                generated_evidence.update({"source": source, "packageSha256": package_hash, "flavor": flavor, "stage": stage})
                assembly = f"GeneratedValidation.{flavor}"
                binary = project.parent / "bin/Release/net10.0" / (rid or "") / f"{assembly}.dll"
                il_report = output / f"{stem}.expression-il.json"
                NATIVE.run(["dotnet", inspector, "expression-il", binary, il_report], output / f"{stem}.expression-il.log", folder)
                generated_evidence["expressionConstruction"] = verify_expression_il(json.loads(il_report.read_text()), binary)
                shutil.copyfile(binary, output / f"{stem}-inputs" / f"{assembly}.dll")
                generated_evidence["expressionConstruction"]["retainedAssembly"] = f"{stem}-inputs/{assembly}.dll"
                runtime = ["dotnet", project.parent / f"bin/Release/net10.0/{assembly}.dll"] if stage == "managed" else [publish / (assembly + (".exe" if args.rid == "win-x64" else ""))]
                if stage == "native":
                    runtime += ["--require-aot"]
                if stage != "managed":
                    runtime_artifacts = retain_published_application(Path(runtime[0]), assembly, output, stem)
                    pending["runtimeArtifacts"] = runtime_artifacts
                    generated_evidence["runtimeArtifacts"] = runtime_artifacts
                    write_report(output, report)
                _, runtime_output = NATIVE.run(runtime, output / f"{stem}.runtime.log", folder)
                verify_runtime(runtime_output)
                print(runtime_output, flush=True)
                report["checks"].append({"name": stem, "passed": True, "generatedSources": generated_evidence["sourceCount"], "generated": generated_evidence})
                report.pop("pendingSemantic", None)
                write_report(output, report)
            expression_results = expression_controls.run_cases(
                root=ROOT, work=work, output=output, source=source, reactive=reactive,
                package_id=package_id, version=version, package_hash=package_hash, cache=cache,
                config=config, rid=args.rid, native=args.mode in {"all", "native"}, inspector=inspector,
                gate=sys.modules[__name__])
            report["checks"].extend(expression_results)
            write_report(output, report)
            # Keep the exact original source paths and selected accessor identities.
            # The control adds no Validation invocation and consumes the emitted Read method.
            read_contract = next(record for record in generated_evidence["readContracts"] if record["member"] == "get_Possible")
            template_path = ROOT / "eng/verification-fixtures/AccessorReadFlowNegative.cs.template"
            template = template_path.read_text()
            placeholder = "__RUNIC_MAYBE_NULL_BRIDGE__"
            if template.count(placeholder) != 1:
                raise ValueError("Accessor read-flow control must select exactly the verified emitted bridge")
            if re.search(r"\b(?:ValidationRule|BindValidation|BindValidationContext|BindValidationState)\s*\(", template):
                raise ValueError("Read-flow control must consume the existing bridge without adding Validation calls")
            snippet = template.replace(placeholder, read_contract["read"]["containingType"])
            negative_source = folder / "Negative.cs"
            source_text = negative_source.read_text()
            anchor = "    private sealed class DiagnosticView"
            if source_text.count(anchor) != 1:
                raise ValueError("Read-flow negative must retain the original negative fixture")
            prefix, suffix = source_text.split(anchor)
            closing = prefix.rfind("    }")
            if closing < 0:
                raise ValueError("Read-flow control requires the original Unsupported method")
            negative_source.write_text(prefix[:closing] + snippet + prefix[closing:] + anchor + suffix)
            shutil.copyfile(negative_source, output / f"{flavor}-readflow-Negative.cs")
            code, content = NATIVE.run(["dotnet", "build", project, *flags,
                                       f"-p:GeneratedVerificationStage={stages[-1].upper()}",
                                       "-p:NegativeCase=NEGATIVE_MAYBE_NULL_GETTER"],
                                      output / f"{flavor}-readflow.build.log", folder, expected=True)
            verify_readflow_failure(code, content)
            accessor_file = project.parent / "obj/generated" / read_contract["accessor"]["file"]
            original_accessor = next(record for record in semantics["generatedSources"] if record["path"] == read_contract["accessor"]["file"])
            if NATIVE.digest(accessor_file) != original_accessor["sha256"]:
                raise ValueError("Read-flow control changed the verified selected accessor source")
            readflow_generated = output / f"{flavor}-readflow-generated"
            shutil.copytree(project.parent / "obj/generated", readflow_generated)
            report["checks"].append({"name": f"{flavor}-readflow", "passed": True, "diagnostic": "CS8604",
                                     "source": source, "packageSha256": package_hash,
                                     "templateSha256": NATIVE.digest(template_path), "bridge": read_contract,
                                     "generatedFiles": [{"path": str(path.relative_to(output)), "sha256": NATIVE.digest(path)}
                                                        for path in sorted(readflow_generated.rglob("*.cs"))]})
            write_report(output, report)
            setter_folder = work / f"{flavor}-reflection-setter"
            shutil.copytree(fixture, setter_folder, ignore=shutil.ignore_patterns("bin", "obj", "artifacts", ".editorconfig"))
            shutil.copyfile(ROOT / "eng/verification-fixtures/ReflectionSetter/ReflectionSetter.cs", setter_folder / "ReflectionSetter.cs")
            setter_project = setter_folder / flavor / "Examples.csproj"
            code, content = NATIVE.run(["dotnet", "build", setter_project, *flags],
                                       output / f"{flavor}-reflection-setter.build.log", setter_folder, expected=True)
            verify_reflection_setter_failure(code, content, reactive)
            report["checks"].append({"name": f"{flavor}-reflection-setter", "diagnostic": "IL2026", "forbiddenDiagnostic": "IL3050", "passed": True})
            write_report(output, report)
            observer_folder = work / f"{flavor}-reflection-observer"
            shutil.copytree(fixture, observer_folder, ignore=shutil.ignore_patterns("bin", "obj", "artifacts", ".editorconfig"))
            shutil.copyfile(ROOT / "eng/verification-fixtures/ReflectionObserver/ReflectionObserver.cs", observer_folder / "ReflectionObserver.cs")
            observer_project = observer_folder / flavor / "Examples.csproj"
            code, content = NATIVE.run(["dotnet", "build", observer_project, *flags],
                                       output / f"{flavor}-reflection-observer.build.log", observer_folder, expected=True)
            verify_reflection_observer_failure(code, content, reactive)
            report["checks"].append({"name": f"{flavor}-reflection-observer", "diagnostic": "IL2026", "forbiddenDiagnostic": "IL3050", "passed": True})
            write_report(output, report)
            # Fresh folders avoid previous RID/negative outputs influencing compiler evidence.
            for negative, diagnostic in NEGATIVES.items():
                negative_folder = work / f"{flavor}-{negative}"
                shutil.copytree(fixture, negative_folder, ignore=shutil.ignore_patterns("bin", "obj", "artifacts", ".editorconfig"))
                negative_project = negative_folder / flavor / "Examples.csproj"
                stem = f"{flavor}-{negative}"
                code, content = NATIVE.run(["dotnet", "build", negative_project, *flags, f"-p:NegativeCase={negative}"], output / f"{stem}.build.log", negative_folder, expected=True)
                verify_negative(code, content, diagnostic)
                report["checks"].append({"name": stem, "diagnostic": diagnostic, "passed": True})
                write_report(output, report)
    report["completed"] = True
    write_report(output, report)
    print(f"Generated package acceptance passed: {output / 'results.json'}", flush=True)


if __name__ == "__main__":
    main()
