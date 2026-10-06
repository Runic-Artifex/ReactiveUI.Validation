// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks normal predicate APIs over custom truth and explicit static object conversions.</summary>
public sealed partial class RuleCapabilityCompilerTests
{
    /// <summary>The custom conditional operator token.</summary>
    private const string TruthOperatorToken = "__TRUTH_OPERATOR__";

    /// <summary>The value that skips the second operand.</summary>
    private const string SkipTruthToken = "__SKIP_TRUTH__";

    /// <summary>The value that activates the second operand.</summary>
    private const string ActiveTruthToken = "__ACTIVE_TRUTH__";

    /// <summary>The original normal API source retaining custom operator effects and owner handoff.</summary>
    private const string NormalTruthSource = """
        using System;
        using System.ComponentModel;
        using System.Linq;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Components.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public struct Flag : IEquatable<Flag>
        {
            public bool Value;
            public static int Combines;
            public static int TruthChecks;
            public static bool operator true(Flag value) { TruthChecks++; return value.Value; }
            public static bool operator false(Flag value) { TruthChecks++; return !value.Value; }
            public static Flag operator &(Flag left, Flag right) { Combines++; return new Flag { Value = left.Value && right.Value }; }
            public static Flag operator |(Flag left, Flag right) { Combines++; return new Flag { Value = left.Value || right.Value }; }
            public bool Equals(Flag other) => Value == other.Value;
            public override bool Equals(object? other) => other is Flag value && Equals(value);
            public override int GetHashCode() => Value.GetHashCode();
        }
        public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            private PropertyChangedEventHandler? _changed;
            private Model? _other;
            private Flag _flag;
            public int Listeners { get; private set; }
            public int OtherReads { get; private set; }
            public int FlagReads { get; private set; }
            public Action? BeforeFlagRead { get; set; }
            public Flag Flag { get { FlagReads++; BeforeFlagRead?.Invoke(); return _flag; } set { _flag = value; } }
            public Model? Primary { get; set; }
            public Model? Other { get { OtherReads++; return _other; } set { _other = value; } }
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public new event PropertyChangedEventHandler? PropertyChanged
            {
                add { _changed += value; Listeners++; }
                remove { _changed -= value; Listeners--; }
            }
            public void Signal(string name) => _changed?.Invoke(this, new(name));
            public static bool Check()
            {
                var model = new Model();
                using var context = model.ValidationContext;
                var left = new Model { Flag = new Flag { Value = __SKIP_TRUTH__ } };
                var right = new Model { Flag = new Flag { Value = true } };
                model.Primary = left; model.Other = right; Flag.Combines = 0; Flag.TruthChecks = 0;
                using var rule = model.ValidationRule(x => x.Primary!.Flag __TRUTH_OPERATOR__ x.Other!.Flag, value => value.Value, "truth required");
                var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
                if (rule.IsValid != __SKIP_TRUTH__ || model.OtherReads != 0 || right.FlagReads != 0 || right.Listeners != 0
                    || Flag.Combines != 0 || Flag.TruthChecks != 1 || component.PropertyCount != 1) return false;
                left.Flag = new Flag { Value = __ACTIVE_TRUTH__ }; left.Signal(nameof(Flag));
                if (!rule.IsValid || model.OtherReads != 1 || right.FlagReads != 1 || right.Listeners != 1
                    || Flag.Combines != 1 || Flag.TruthChecks != 2 || component.PropertyCount != 2) return false;
                left.Flag = new Flag { Value = __SKIP_TRUTH__ }; left.Signal(nameof(Flag));
                if (right.Listeners != 0 || component.PropertyCount != 1 || Flag.Combines != 1 || Flag.TruthChecks != 3) return false;
                rule.Dispose();
                if (model.Listeners != 0 || left.Listeners != 0 || right.Listeners != 0) return false;
                left.Flag = new Flag { Value = __ACTIVE_TRUTH__ }; model.Other = null; Flag.Combines = 0;
                using var missing = model.ValidationRule(x => x.Primary!.Flag __TRUTH_OPERATOR__ x.Other!.Flag, value => value.Value, "missing");
                if (missing.IsValid || Flag.Combines != 0) return false;
                missing.Dispose();
                model.Other = right; var error = new InvalidOperationException("user error"); right.BeforeFlagRead = () => throw error;
                try
                {
                    using var failed = model.ValidationRule(x => x.Primary!.Flag __TRUTH_OPERATOR__ x.Other!.Flag, value => value.Value, "error");
                    return false;
                }
                catch (InvalidOperationException actual)
                {
                    return ReferenceEquals(actual, error) && Flag.Combines == 0 && model.Listeners == 0
                        && left.Listeners == 0 && right.Listeners == 0 && !context.Validations.Items.Any();
                }
            }
        }
        """;

    /// <summary>The normal static-object conversion avoids generated DLR dispatch and retains controlled cast failure.</summary>
    private const string StaticObjectSource = """
        using System;
        using System.ComponentModel;
        using System.Linq;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            private PropertyChangedEventHandler? _changed;
            public dynamic Payload { get; set; } = "payload";
            public int Listeners { get; private set; }
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public new event PropertyChangedEventHandler? PropertyChanged
            {
                add { _changed += value; Listeners++; }
                remove { _changed -= value; Listeners--; }
            }
            public void Signal() => _changed?.Invoke(this, new(nameof(Payload)));
            public static bool Check()
            {
                var model = new Model();
                using var context = model.ValidationContext;
                using var rule = model.ValidationRule(x => (string)(object)x.Payload, value => value == "ok", "object required");
                if (rule.IsValid || model.Listeners != 1) return false;
                model.Payload = "ok"; model.Signal();
                if (!rule.IsValid || !context.GetIsValid()) return false;
                rule.Dispose(); model.Payload = 7;
                try
                {
                    using var failed = model.ValidationRule(x => (string)(object)x.Payload, value => value == "ok", "cast");
                    return false;
                }
                catch (InvalidCastException)
                {
                    return model.Listeners == 0 && !context.Validations.Items.Any();
                }
            }
        }
        """;

    /// <summary>Normal rules preserve custom lazy truth operators, atomic metadata and failed registration cleanup.</summary>
    /// <param name="reactive">Whether to use System.Reactive packages.</param>
    /// <param name="or">Whether the operator is conditional OR.</param>
    /// <returns>The asynchronous compiler/runtime assertion.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task NormalCustomTruthOperatorsKeepEffectsAndOwnership(bool reactive, bool or)
    {
        var source = ExpandFixture(NormalTruthSource, reactive)
            .Replace(TruthOperatorToken, or ? "||" : "&&", StringComparison.Ordinal)
            .Replace(SkipTruthToken, or ? "true" : "false", StringComparison.Ordinal)
            .Replace(ActiveTruthToken, or ? "false" : "true", StringComparison.Ordinal);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Normal rules use the explicit static object bridge and preserve exact cast failure cleanup.</summary>
    /// <param name="reactive">Whether to use System.Reactive packages.</param>
    /// <returns>The asynchronous compiler/runtime assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NormalStaticObjectBridgeAvoidsDynamicDispatch(bool reactive)
    {
        var source = ExpandFixture(StaticObjectSource, reactive);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(string.Join("\n", result.ValidationSources.Select(static generated => generated.Text))).DoesNotContain("RuntimeBinder");
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }
}
