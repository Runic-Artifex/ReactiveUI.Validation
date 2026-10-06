#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Positive and negative IL shape controls for built-in registry attachment dispatch."""

import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location("verify_attached_registry_dispatch", Path(__file__).with_name("verify-attached-registry-dispatch.py"))
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)


def sample():
    """Mirror InspectIl's MethodDef and generic MethodSpec rendering for both flavors."""
    lines = []
    for namespace in VERIFY.NAMESPACES:
        lines.append(f"ASSEMBLY {namespace}.dll SHA256 fixture")
        for site, operation in VERIFY.SITES.items():
            lines.extend([
                f"METHOD {namespace}.ValidationRuntime.{site}(Object)",
                f" IL_0010 callvirt {namespace}.IValidationPlanProvider.{operation}(Object)<!!0,!!1>",
                f" IL_0020 call {namespace}.ValidationPlanRegistry.TryGetAttached(Object)",
                f" IL_0030 callvirt {namespace}.ValidationPlanRegistry.{operation}(Object)<!!0,!!1>",
            ])
    return "\n".join(lines)


class AttachedRegistryDispatchTests(unittest.TestCase):
    """The gate rejects interface regression without rejecting custom providers."""

    def test_concrete_and_custom_provider_calls_pass(self):
        self.assertEqual(len(VERIFY.verify(sample())), 6)

    def test_old_two_interface_calls_fail(self):
        text = sample().replace("ValidationPlanRegistry.TryGetSelector", "IValidationPlanProvider.TryGetSelector")
        with self.assertRaisesRegex(ValueError, "concrete registry"):
            VERIFY.verify(text)

    def test_missing_flavor_fails(self):
        text = sample().split("ASSEMBLY ReactiveUI.Validation.Reactive")[0]
        with self.assertRaisesRegex(ValueError, "exactly one inspected"):
            VERIFY.verify(text)

    def test_non_generic_class_call_fails(self):
        text = sample().replace("ValidationPlanRegistry.TryGetTarget(Object)<!!0,!!1>", "ValidationPlanRegistry.TryGetTarget(Object)")
        with self.assertRaisesRegex(ValueError, "generic MethodSpec"):
            VERIFY.verify(text)

    def test_erasing_custom_provider_branch_fails(self):
        text = "\n".join(line for line in sample().splitlines() if ".IValidationPlanProvider." not in line)
        with self.assertRaisesRegex(ValueError, "custom-provider interface"):
            VERIFY.verify(text)


if __name__ == "__main__":
    unittest.main()
