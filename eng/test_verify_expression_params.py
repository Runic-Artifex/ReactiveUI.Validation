#!/usr/bin/env python3
# Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
"""Reject substituted diagnostics in the original Expression construction controls."""
import importlib.util
from pathlib import Path
import unittest

_spec = importlib.util.spec_from_file_location("expression_params", Path(__file__).with_name("verify-expression-params.py"))
VERIFY = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(VERIFY)


class ExpressionParamsTests(unittest.TestCase):
    def test_current_warning_requires_precise_actionable_source_without_linker_substitution(self):
        message = ("/work/ExpressionParams.cs(27,46): error RUVG011: This retained Expression selector constructs an array "
                   "or calls a RequiresDynamicCode factory before interception. Use the normal Func overload with the same inline lambda. "
                   "The interceptor cannot remove original argument construction; NativeAOT IL3050 remains applicable.")
        VERIFY.verify_construction_warning(1, message)
        VERIFY.verify_construction_warning(1, message + "\n" + message)
        for abnormal in (7, 139, -11, 134, -6):
            with self.subTest(exit=abnormal), self.assertRaises(ValueError):
                VERIFY.verify_construction_warning(abnormal, message)
        for code, text in ((0, message), (1, ""), (1, message.replace("ExpressionParams.cs", "Other.cs")),
                           (1, message.replace("RUVG011", "RUVG001")), (1, message.replace("normal Func overload", "Unsafe route")),
                           (1, message + "\nOther.cs(2,4): error CS0246: Unknown type"),
                           (1, message + "\nOther.cs(2,4): warning CS0067: Unused event"),
                           (1, message + "\nExpressionParams.cs(26): AOT analysis error IL3050: NewArrayInit")):
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_construction_warning(code, text)
        for fatal in ("Unhandled exception", "Process terminated", "Fatal error", "Segmentation fault", "Stack overflow",
                      "You must install or update .NET", "could not load file", "could not load type"):
            with self.subTest(fatal=fatal), self.assertRaises(ValueError):
                VERIFY.verify_construction_warning(1, message + "\n" + fatal)

    def test_original_native_failure_requires_reachable_exact_framework_array_factory(self):
        message = ("ExpressionParams.cs(26): AOT analysis error IL3050: RuleExpressionParamsNegative.Program.Run(): "
                   "Using member 'System.Linq.Expressions.Expression.NewArrayInit(Type,Expression[])' which has "
                   "'RequiresDynamicCodeAttribute' can break functionality when AOT compiling.")
        cascade = 'Microsoft.NETCore.Native.targets(342,5): error MSB3077: The command "ilc" exited with code -1.'
        VERIFY.verify_native_construction_failure(1, message)
        VERIFY.verify_native_construction_failure(1, message + "\n" + cascade + "\n" + message)
        for abnormal in (7, 139, -11, 134, -6):
            with self.subTest(exit=abnormal), self.assertRaises(ValueError):
                VERIFY.verify_native_construction_failure(abnormal, message)
        for code, text in ((0, message), (1, ""), (1, cascade), (1, message.replace("IL3050", "IL2026")),
                           (1, message.replace("Program.Run()", "Unused.Run()")),
                           (1, message.replace("NewArrayInit", "NewArrayBounds")),
                           (1, message.replace("System.Linq.Expressions.Expression.", "Fake.Expression.")),
                           (1, message.replace("Expression[]", "Object[]")),
                           (1, message + "\nOther.cs(3): warning IL2026: Reflection"),
                           (1, message + "\nOther.cs(3): error CS0246: Unknown type")):
            with self.subTest(code=code, text=text), self.assertRaises(ValueError):
                VERIFY.verify_native_construction_failure(code, text)
        for fatal in ("Unhandled exception", "Process terminated", "Fatal error", "Segmentation fault", "Stack overflow",
                      "You must install or update .NET", "could not load file", "could not load type"):
            with self.subTest(fatal=fatal), self.assertRaises(ValueError):
                VERIFY.verify_native_construction_failure(1, message + "\n" + fatal)


if __name__ == "__main__":
    unittest.main()
