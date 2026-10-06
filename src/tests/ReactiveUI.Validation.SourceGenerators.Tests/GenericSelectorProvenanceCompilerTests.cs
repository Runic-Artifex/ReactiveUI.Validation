// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Executes finite constructed selector provenance through real normal-call interception.</summary>
public sealed class GenericSelectorProvenanceCompilerTests
{
    /// <summary>The consumer entry type.</summary>
    private const string EntryType = "Probe";

    /// <summary>The consumer entry method.</summary>
    private const string EntryMethod = "Check";

    /// <summary>The System.Reactive validation namespace.</summary>
    private const string ReactiveRoot = "ReactiveUI.Validation.Reactive";

    /// <summary>The Primitives validation namespace.</summary>
    private const string PrimitiveRoot = "ReactiveUI.Validation";

    /// <summary>The System.Reactive UI namespace.</summary>
    private const string ReactiveUi = "ReactiveUI.Reactive";

    /// <summary>The Primitives UI namespace.</summary>
    private const string PrimitiveUi = "ReactiveUI";

    /// <summary>The validation namespace fixture token.</summary>
    private const string ValidationRootToken = "__VALIDATION_ROOT__";

    /// <summary>The UI namespace fixture token.</summary>
    private const string UiRootToken = "__UI_ROOT__";

    /// <summary>The finite selector fixture token.</summary>
    private const string SelectorToken = "__SELECTOR__";

    /// <summary>The notification member fixture token.</summary>
    private const string MemberToken = "__MEMBER__";

    /// <summary>The constructed selector update consumer.</summary>
    private const string ConstructedSource = """
        using System;
        using System.ComponentModel;
        using System.Linq.Expressions;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public class Model<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            public T Value { get; set; } = default!;
            public T[] Values { get; set; } = new T[2];
            public Count? Number { get; set; }
            public int Index { get; set; }
            public new event PropertyChangedEventHandler? PropertyChanged;
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public void Invalidate(string property) => PropertyChanged?.Invoke(this, new(property));
        }
        public static class Selectors<T>
        {
            public static readonly Expression<Func<Model<T>,T>> Value = x => x.Value;
            public static readonly Expression<Func<Model<T>,T>> Indexed = x => x.Values[x.Index];
            public static readonly Expression<Func<Model<T>,object?>> Boxed = x => (object?)x.Value!;
            public static readonly Expression<Func<Model<T>,int?>> Lifted = x => x.Number + x.Number;
            public static class Nested<TUnused>
            {
                public static Expression<Func<Model<T>,T>> Value { get { return x => x.Value; } }
            }
        }
        public static class Selectors
        {
            public static Expression<Func<Model<T>,T>> Value<T>() { return x => x.Value; }
        }
        public static class StructSelectors
        {
            public static Expression<Func<Model<T>,T?>> Nullable<T>() where T : struct => x => (T?)x.Value;
        }
        public readonly struct Count(int value)
        {
            public int Value { get; } = value;
            public static Count operator +(Count left, Count right) => new(left.Value + right.Value + 1);
            public static implicit operator int(Count value) => value.Value;
        }
        public static class Probe
        {
            public static bool Check()
            {
                var model = new Model<int>();
                using var rule = model.ValidationRule(__SELECTOR__, value => Equals(value,7), "bad");
                if (rule.IsValid) return false;
                model.Value = 7;
                model.Values[0] = 7;
                model.Number = new Count(3);
                model.Invalidate("__MEMBER__");
                if (!rule.IsValid || !model.ValidationContext.GetIsValid()) return false;
                model.Value = 8;
                model.Values[0] = 8;
                model.Number = new Count(4);
                model.Invalidate("__MEMBER__");
                return !rule.IsValid && !model.ValidationContext.GetIsValid();
            }
        }
        """;

    /// <summary>The constrained member, operator and conversion consumer.</summary>
    private const string ConstraintSource = """
        using System;
        using System.ComponentModel;
        using System.Linq.Expressions;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public class Base : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            public int Value { get; set; }
            public new event PropertyChangedEventHandler? PropertyChanged;
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Value)));
        }
        public sealed class Model : Base
        {
            public new int Value { get; set; } = 99;
        }
        public static class Selectors
        {
            public static Expression<Func<T,int>> Value<T>() where T : Base => x => x.Value;
            public static Expression<Func<AmountModel<T>,int>> Sum<T>() where T : Amount => x => x.Value + x.Value;
            public static Expression<Func<AmountModel<T>,int>> Negate<T>() where T : Amount => x => -x.Value;
            public static Expression<Func<AmountModel<T>,int>> Converted<T>() where T : Amount => x => x.Value;
            public static Expression<Func<AmountModel<T>,byte>> Checked<T>() where T : Amount => x => checked((byte)(int)x.Value);
        }
        public class Amount(int tag)
        {
            public int Tag { get; } = tag;
            public static int operator +(Amount left, Amount right) => left.Tag + right.Tag;
            public static int operator -(Amount value) => -value.Tag;
            public static implicit operator int(Amount value) => value.Tag;
        }
        public sealed class DerivedAmount(int tag) : Amount(tag)
        {
            public static int operator +(DerivedAmount left, DerivedAmount right) => 999;
            public static int operator -(DerivedAmount value) => 999;
            public static implicit operator int(DerivedAmount value) => 999;
        }
        public sealed class AmountModel<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel where T : Amount
        {
            public T Value { get; set; } = default!;
            public new event PropertyChangedEventHandler? PropertyChanged;
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Value)));
        }
        public static class Probe
        {
            public static bool Check()
            {
                var model = new Model();
                using var rule = model.ValidationRule(Selectors.Value<Model>(), value => value == 7, "bad");
                if (rule.IsValid) return false;
                ((Base)model).Value = 7; model.Invalidate();
                if (!rule.IsValid || model.Value != 99) return false;
                var amount = new AmountModel<DerivedAmount> { Value = new DerivedAmount(3) };
                using var sum = amount.ValidationRule(Selectors.Sum<DerivedAmount>(), value => value == 14, "sum");
                using var negate = amount.ValidationRule(Selectors.Negate<DerivedAmount>(), value => value == -7, "negative");
                using var converted = amount.ValidationRule(Selectors.Converted<DerivedAmount>(), value => value == 7, "convert");
                using var narrow = amount.ValidationRule(Selectors.Checked<DerivedAmount>(), value => value == 7, "checked");
                if (sum.IsValid || negate.IsValid || converted.IsValid || narrow.IsValid) return false;
                amount.Value = new DerivedAmount(7); amount.Invalidate();
                if (!sum.IsValid || !negate.IsValid || !converted.IsValid || !narrow.IsValid) return false;
                if (amount.Value + amount.Value != 999 || -amount.Value != 999 || (int)amount.Value != 999) return false;
                try { amount.Value = new DerivedAmount(256); amount.Invalidate(); return false; }
                catch (OverflowException) { return true; }
            }
        }
        """;

    /// <summary>Specializes declaring and method slots throughout typed reads, indexing and conversions.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <param name="selector">The finite constructed selector argument.</param>
    /// <param name="member">The member whose notification refreshes the operation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false, "Selectors<int>.Value", "Value")]
    [Arguments(true, "Selectors<int>.Value", "Value")]
    [Arguments(false, "Selectors.Value<int>()", "Value")]
    [Arguments(true, "Selectors.Value<int>()", "Value")]
    [Arguments(false, "Selectors<int>.Indexed", "Values")]
    [Arguments(true, "Selectors<int>.Indexed", "Values")]
    [Arguments(false, "Selectors<int>.Boxed", "Value")]
    [Arguments(true, "Selectors<int>.Boxed", "Value")]
    [Arguments(false, "StructSelectors.Nullable<int>()", "Value")]
    [Arguments(true, "StructSelectors.Nullable<int>()", "Value")]
    [Arguments(false, "Selectors<int>.Nested<string>.Value", "Value")]
    [Arguments(true, "Selectors<int>.Nested<string>.Value", "Value")]
    [Arguments(false, "Selectors<int>.Lifted", "Number")]
    [Arguments(true, "Selectors<int>.Lifted", "Number")]
    public async Task ConstructedSelectorsKeepClosedTypedUpdates(bool reactive, string selector, string member)
    {
        var source = FlavorSource(ConstructedSource, reactive);
        source = source.Replace(SelectorToken, selector, StringComparison.Ordinal).Replace(MemberToken, member, StringComparison.Ordinal);
        await AssertSourceAsync(reactive, source);
    }

    /// <summary>Retains nullable reference slots rather than exposing an undefined factory parameter.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullableConstructedSelectorRetainsActualContract(bool reactive)
    {
        var root = reactive ? ReactiveRoot : PrimitiveRoot;
        var source = $$"""
            using System;
            using System.ComponentModel;
            using System.Linq.Expressions;
            using {{(reactive ? ReactiveUi : PrimitiveUi)}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public class Model<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel where T : class?
            {
                public T? Value { get; set; }
                public new event PropertyChangedEventHandler? PropertyChanged;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Value)));
            }
            public static class Selectors<T> where T : class?
            {
                public static readonly Expression<Func<Model<T>,T?>> Value = x => x.Value;
            }
            public static class Probe
            {
                public static bool Check()
                {
                    var model = new Model<string?>();
                    using var rule = model.ValidationRule(Selectors<string?>.Value, value => value == "ok", "bad");
                    if (rule.IsValid) return false;
                    model.Value = "ok"; model.Invalidate();
                    if (!rule.IsValid) return false;
                    model.Value = null; model.Invalidate();
                    return !rule.IsValid;
                }
            }
            """;
        await AssertSourceAsync(reactive, source);
    }

    /// <summary>Preserves a constrained declaration's base dispatch when the actual class hides its member.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConstructedConstraintKeepsOriginalMemberDispatch(bool reactive)
    {
        var source = FlavorSource(ConstraintSource, reactive);
        await AssertSourceAsync(reactive, source);
    }

    /// <summary>Freezes declaration-sensitive constants and keeps generic reference equality after closing to string.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConstructedConstantsAndReferenceEqualityKeepDeclarationSemantics(bool reactive)
    {
        var root = reactive ? ReactiveRoot : PrimitiveRoot;
        var source = $$"""
            using System;
            using System.ComponentModel;
            using System.Linq.Expressions;
            using {{(reactive ? ReactiveUi : PrimitiveUi)}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed class Model<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel where T : class
            {
                public T Left { get; set; } = default!;
                public T Right { get; set; } = default!;
                public new event PropertyChangedEventHandler? PropertyChanged;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Right)));
            }
            public static class Selectors
            {
                public static Expression<Func<Model<T>,bool>> Same<T>() where T : class => x => x.Left == x.Right;
                public static Expression<Func<Model<T>,string?>> Name<T>() where T : class => x => nameof(T) + ":" + nameof(x.Left);
            }
            public static class Probe
            {
                public static bool Check()
                {
                    var model = new Model<string> { Left = new string('x',3), Right = new string('x',3) };
                    using var same = model.ValidationRule(Selectors.Same<string>(), value => value, "identity");
                    using var name = model.ValidationRule(Selectors.Name<string>(), value => value == "T:Left", "name");
                    if (same.IsValid || !name.IsValid || model.Left != model.Right) return false;
                    model.Right = model.Left; model.Invalidate();
                    return same.IsValid && name.IsValid;
                }
            }
            """;
        await AssertSourceAsync(reactive, source);
    }

    /// <summary>Selects the actual runtime and UI flavor in a fixed consumer template.</summary>
    /// <param name="source">The fixed source template.</param>
    /// <param name="reactive">The selected runtime flavor.</param>
    /// <returns>The flavor-specific consumer source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FlavorSource(string source, bool reactive) => source
        .Replace(ValidationRootToken, reactive ? ReactiveRoot : PrimitiveRoot, StringComparison.Ordinal)
        .Replace(UiRootToken, reactive ? ReactiveUi : PrimitiveUi, StringComparison.Ordinal);

    /// <summary>Verifies actual generation, warning-free compilation, dispatch and runtime changes.</summary>
    /// <param name="reactive">The selected runtime flavor.</param>
    /// <param name="source">The exact source consumer.</param>
    /// <returns>The asynchronous assertions.</returns>
    private static async Task AssertSourceAsync(bool reactive, string source)
    {
        using var admissionHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var admitted = await admissionHost.RunAsync(source);
        await Assert.That(string.Join("\n", admitted.CompilationDiagnostics
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(string.Join("\n", result.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(string.Join("\n", result.CompilationDiagnostics
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ValidationSources.IsEmpty).IsFalse();
        await Assert.That(result.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
    }
}
