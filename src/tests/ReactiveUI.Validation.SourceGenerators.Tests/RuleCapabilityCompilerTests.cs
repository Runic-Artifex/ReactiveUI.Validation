// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks typed rule lowering against actual compiler and runtime contracts.</summary>
public sealed partial class RuleCapabilityCompilerTests
{
    /// <summary>The ordinary consumer fixture entry type.</summary>
    private const string ModelEntryType = "Model";

    /// <summary>The lexical/factory consumer fixture entry type.</summary>
    private const string FactoryEntryType = "Fixture";

    /// <summary>The consumer's public Boolean entry method.</summary>
    private const string EntryMethod = "Check";

    /// <summary>The primitive validation namespace.</summary>
    private const string PrimitiveValidationRoot = "ReactiveUI.Validation";

    /// <summary>The System.Reactive validation namespace.</summary>
    private const string ReactiveValidationRoot = "ReactiveUI.Validation.Reactive";

    /// <summary>The primitive UI namespace.</summary>
    private const string PrimitiveUiRoot = "ReactiveUI";

    /// <summary>The System.Reactive UI namespace.</summary>
    private const string ReactiveUiRoot = "ReactiveUI.Reactive";

    /// <summary>The validation namespace template token.</summary>
    private const string ValidationRootToken = "__VALIDATION_ROOT__";

    /// <summary>The UI namespace template token.</summary>
    private const string UiRootToken = "__UI_ROOT__";

    /// <summary>The missing-owner policy template token.</summary>
    private const string MissingOwnerPolicyToken = "__MISSING_OWNER_POLICY__";

    /// <summary>The quoted expected-value template token.</summary>
    private const string ExpectedValueToken = "__EXPECTED_VALUE__";

    /// <summary>The optional selector declaration template token.</summary>
    private const string SelectorDeclarationToken = "__SELECTOR_DECLARATION__";

    /// <summary>The selector value template token.</summary>
    private const string SelectorValueToken = "__SELECTOR_VALUE__";

    /// <summary>The consumer source for DynamicIndexerTracksArgumentAndCollectionChanges.</summary>
    private const string DynamicIndexerTracksArgumentAndCollectionChangesSource = """
        using System;
        using __UI_ROOT__;
        using System.Collections.ObjectModel;
        using System.Collections.Specialized;
        using System.ComponentModel;
        using System.Linq;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Capabilities;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            private PropertyChangedEventHandler? _changed;
            public TrackingCollection? Values { get; set; } = new() { 0, 7, 7 };
            public int Index { get; set; }
            public int Listeners { get; private set; }
            public new event PropertyChangedEventHandler? PropertyChanged
            {
                add { _changed += value; Listeners++; }
                remove { _changed -= value; Listeners--; }
            }
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public void Invalidate(string name) => _changed?.Invoke(this, new(name));
            public static bool Check()
            {
                var model = new Model();
                using var rule = model.ValidationRule(x => x.Values![x.Index], value => value > 5, value => "item:" + value);
                if (rule.IsValid || rule.Message.ToSingleLine() != "item:0") return false;
                model.Index = 1; model.Invalidate(nameof(Index));
                if (!rule.IsValid) return false;
                var component = model.ValidationContext.Validations.Items.OfType<IValidationPathComponent>().Single();
                var previousPaths = component.ValidationPaths.ToArray();
                model.Index = 2; model.Invalidate(nameof(Index));
                if (!rule.IsValid || previousPaths.SequenceEqual(component.ValidationPaths)) return false;
                model.Values![2] = 3;
                if (rule.IsValid || rule.Message.ToSingleLine() != "item:3") return false;
                var previous = model.Values!;
                model.Values = null; model.Invalidate(nameof(Values));
                if (rule.IsValid || rule.Message.ToSingleLine() != "item:0") return false;
                model.Index = 0; model.Invalidate(nameof(Index));
                model.Values = new TrackingCollection { 8 }; model.Invalidate(nameof(Values));
                if (!rule.IsValid) return false;
                previous[0] = 100;
                var bounds = new Model { Index = 99 };
                try
                {
                    using var failed = bounds.ValidationRule(x => x.Values![x.Index], value => value > 5, "bounds");
                    return false;
                }
                catch (ArgumentOutOfRangeException) { }
                return rule.IsValid && model.ValidationContext.GetIsValid() && previous.Listeners == 0
                    && bounds.Listeners == 0 && bounds.Values!.Listeners == 0 && !bounds.ValidationContext.Validations.Items.Any();
            }
        }
        public sealed class TrackingCollection : ObservableCollection<int>, INotifyCollectionChanged
        {
            private NotifyCollectionChangedEventHandler? _changed;
            public int Listeners { get; private set; }
            public new event NotifyCollectionChangedEventHandler? CollectionChanged
            {
                add { _changed += value; Listeners++; }
                remove { _changed -= value; Listeners--; }
            }
            protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
            {
                base.OnCollectionChanged(args);
                _changed?.Invoke(this, args);
            }
        }
        """;

    /// <summary>The consumer source for FieldsConversionsComputedValuesAndStoredSelectors.</summary>
    private const string FieldsConversionsComputedValuesAndStoredSelectorsSource = """
        using System;
        using __UI_ROOT__;
        using System.ComponentModel;
        using System.Linq;
        using System.Linq.Expressions;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Components.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            public CountValue Count;
            public int Bonus { get; set; }
            public new event PropertyChangedEventHandler? PropertyChanged;
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
            public static bool Check()
            {
                CountValue.Conversions = 0;
                var model = new Model { Count = 2, Bonus = 3 };
                __SELECTOR_DECLARATION__
                using var rule = model.ValidationRule(__SELECTOR_VALUE__, value => value == 9L, value => "sum:" + value);
                if (rule.IsValid || rule.Message.ToSingleLine() != "sum:5" || CountValue.Conversions != 1) return false;
                var component = model.ValidationContext.Validations.Items.OfType<IPropertyValidationComponent>().Single();
                if (!component.ContainsPropertyName("Count") || !component.ContainsPropertyName("Bonus")) return false;
                if (component.ContainsPropertyName("Count", true)) return false;
                model.Count = 6;
                model.Invalidate(nameof(Count));
                if (!rule.IsValid || !model.ValidationContext.GetIsValid() || CountValue.Conversions != 2) return false;
                model.Bonus = 4;
                model.Invalidate(nameof(Bonus));
                if (rule.IsValid || rule.Message.ToSingleLine() != "sum:10" || CountValue.Conversions != 3) return false;
                rule.Dispose();
                model.Count = 100;
                model.Invalidate(nameof(Count));
                return !model.ValidationContext.Validations.Items.Any() && CountValue.Conversions == 3;
            }
        }
        public readonly struct CountValue(int value)
        {
            public int Value { get; } = value;
            public static int Conversions { get; set; }
            public static implicit operator CountValue(int value) => new(value);
            public static implicit operator long(CountValue value) { Conversions++; return value.Value; }
        }
        """;

    /// <summary>The consumer source for AnonymousProjectionAndFileLocalFactoryPreserveRichState.</summary>
    private const string AnonymousProjectionAndFileLocalFactoryPreserveRichStateSource = """
        using System;
        using __UI_ROOT__;
        using System.ComponentModel;
        using System.Linq;
        using __VALIDATION_ROOT__.Capabilities;
        using __VALIDATION_ROOT__.Collections;
        using __VALIDATION_ROOT__.Components.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Helpers;
        using __VALIDATION_ROOT__.States;
        file sealed class LocalModel : INotifyPropertyChanged
        {
            public string Text { get; set; } = "same";
            public int Revision { get; set; } = 1;
            public IValidationState? Latest { get; private set; }
            public event PropertyChangedEventHandler? PropertyChanged;
            public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Revision)));
            public ValidationHelper Register(IValidationContext context)
                => RegisterCore(context, source => new { source.Text, source.Revision },
                    value => new RichState(false, value.Text, value.Revision));
            private ValidationHelper RegisterCore<TValue>(IValidationContext context, Func<LocalModel,TValue> read, Func<TValue,IValidationState> validate)
            {
                var paths = new[] { ValidationPath.Legacy(nameof(Text)), ValidationPath.Legacy(nameof(Revision)) };
                var selector = new ValidationSelector<LocalModel,TValue>(source => new ValidationAccessPlan<TValue>(
                    () => ValidationRead<TValue>.Present(read(source), paths),
                    new[] { ValidationDependency.PropertyChanged(() => source, nameof(Text)), ValidationDependency.PropertyChanged(() => source, nameof(Revision)) },
                    ValidationObservationOptions<TValue>.Default));
                return ValidationRuntime.RegisterRule(this, context, selector, value => { var state = validate(value); Latest = state; return state; });
            }
        }
        public readonly record struct RichState(bool IsValid, string Code, int Revision) : IValidationState
        {
            public IValidationText Text => ValidationText.Create(Code);
        }
        public sealed class Capture : IObserver<IValidationState>
        {
            public IValidationState? Latest { get; private set; }
            public void OnNext(IValidationState value) => Latest = value;
            public void OnError(Exception error) => throw error;
            public void OnCompleted() { }
        }
        public static class Fixture
        {
            public static bool Check()
            {
                using var context = new ValidationContext();
                var model = new LocalModel();
                using var rule = model.Register(context);
                var capture = new Capture();
                using var subscription = rule.ValidationChanged.Subscribe(capture);
                if (!ReferenceEquals(capture.Latest, model.Latest) || ((RichState)capture.Latest!).Revision != 1) return false;
                var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
                if (!component.ContainsPropertyName("Text") || !component.ContainsPropertyName("Revision") || component.ContainsPropertyName("Text", true)) return false;
                model.Revision = 2; model.Invalidate();
                return ReferenceEquals(capture.Latest, model.Latest) && ((RichState)capture.Latest!).Revision == 2
                    && !rule.IsValid && !context.GetIsValid();
            }
        }
        """;

    /// <summary>The consumer source for TypedFactoryMissingOwnerPolicies.</summary>
    private const string TypedFactoryMissingOwnerPoliciesSource = """
        using System;
        using __UI_ROOT__;
        using System.Collections.Generic;
        using System.ComponentModel;
        using System.Linq;
        using __VALIDATION_ROOT__.Components.Abstractions;
        using __VALIDATION_ROOT__.Capabilities;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.States;
        public sealed class Model : INotifyPropertyChanged
        {
            public Model? Child { get; set; }
            public string? Name { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
            public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
            public static bool Check()
            {
                using var context = new ValidationContext();
                var model = new Model();
                var paths = new[] { ValidationPath.Legacy("Child.Name") };
                var inputs = new List<string>();
                var missingOwnerPolicy = ValidationMissingOwnerPolicy.__MISSING_OWNER_POLICY__;
                var selector = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                    () => source.Child is null ? ValidationRead<string?>.Missing(paths) : ValidationRead<string?>.Present(source.Child.Name, paths),
                    new[] { ValidationDependency.PropertyChanged(() => source, nameof(Child)),
                        ValidationDependency.PropertyChanged(() => source.Child, nameof(Name)) },
                    new ValidationObservationOptions<string?>(missingOwnerPolicy, () => "fallback", EqualityComparer<string?>.Default, false)));
                using var rule = ValidationRuntime.RegisterRule(model, context, selector, value =>
                {
                    inputs.Add(value ?? "<null>");
                    return new ValidationState(value == "ok", value ?? "<null>");
                });
                var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
                if (!component.ContainsPropertyName("Child.Name", true)) return false;
                model.Child = new Model(); model.Invalidate(nameof(Child));
                if (rule.IsValid || context.GetIsValid()) return false;
                model.Child.Name = "ok"; model.Child.Invalidate(nameof(Name));
                if (!rule.IsValid || !context.GetIsValid()) return false;
                model.Child = null; model.Invalidate(nameof(Child));
                return string.Join(",", inputs) == __EXPECTED_VALUE__
                    && rule.IsValid == (missingOwnerPolicy == ValidationMissingOwnerPolicy.Suppress);
            }
        }
        """;

    /// <summary>The consumer source for NonpartialPrivateFactoryUsesCurrentCapturedRoot.</summary>
    private const string NonpartialPrivateFactoryUsesCurrentCapturedRootSource = """
        using System;
        using __UI_ROOT__;
        using System.ComponentModel;
        using __VALIDATION_ROOT__.Capabilities;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Helpers;
        using __VALIDATION_ROOT__.States;
        public static class Fixture
        {
            private sealed class Model : INotifyPropertyChanged
            {
                private string? Secret { get; set; }
                public event PropertyChangedEventHandler? PropertyChanged;
                public void Set(string value) { Secret = value; Invalidate(nameof(Secret)); }
                public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
                private string Compute(Model current) => Secret + ":" + current.Secret;
                public ValidationHelper Register(IValidationContext context, Func<Model> captured)
                {
                    var paths = new[] { ValidationPath.Legacy("Secret"), ValidationPath.Legacy("Captured.Secret") };
                    var selector = new ValidationSelector<Model, string?>(source => new ValidationAccessPlan<string?>(
                        () => ValidationRead<string?>.Present(source.Compute(captured()), paths),
                        new[] {
                            ValidationDependency.PropertyChanged(() => source, nameof(Secret)),
                            ValidationDependency.PropertyChanged(() => source, "Captured"),
                            ValidationDependency.PropertyChanged(() => captured(), nameof(Secret)) },
                        ValidationObservationOptions<string?>.Default));
                    Func<Model, IValidationContext, ValidationSelector<Model,string?>, Func<string?,IValidationState>, ValidationHelper> callable = ValidationRuntime.RegisterRule;
                    return callable(this, context, selector,
                        value => new ValidationState(value == "source:ok", value ?? "null"));
                }
            }
            public static bool Check()
            {
                using var context = new ValidationContext();
                var source = new Model(); source.Set("source");
                var previous = new Model(); previous.Set("bad");
                var current = previous;
                var capturedReads = 0;
                using var rule = source.Register(context, () => { capturedReads++; return current; });
                if (rule.IsValid || rule.Message.ToSingleLine() != "source:bad") return false;
                current = new Model(); current.Set("ok");
                source.Invalidate("Captured");
                if (!rule.IsValid) return false;
                var afterHandoff = capturedReads;
                previous.Set("obsolete");
                if (!rule.IsValid || capturedReads != afterHandoff) return false;
                current.Set("new");
                return !rule.IsValid && rule.Message.ToSingleLine() == "source:new";
            }
        }
        """;

    /// <summary>The consumer source for PredicateShapesCaptureContextsAndDeliverMissingParents.</summary>
    private const string PredicateShapesCaptureContextsAndDeliverMissingParentsSource = """
        using System;
        using __UI_ROOT__;
        using System.ComponentModel;
        using System.Linq;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Components.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            private string? _name;
            private Model? _child;
            public new event PropertyChangedEventHandler? PropertyChanged;
            public string? Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
            public Model? Child { get => _child; set { _child = value; PropertyChanged?.Invoke(this, new(nameof(Child))); } }
            public IValidationContext CurrentContext { get; set; }
            public int ContextReads { get; private set; }
            IValidationContext IValidatableViewModel.ValidationContext { get { ContextReads++; return CurrentContext; } }
            private IValidationContext ValidationContext => throw new InvalidOperationException("shadow getter must not run");
            public Model(IValidationContext context) => CurrentContext = context;
            public static bool Check()
            {
                using var original = new ValidationContext();
                using var selected = new ValidationContext();
                using var replacement = new ValidationContext();
                var model = new Model(original);
                var predicateCalls = 0;
                var messageCalls = 0;
                var seen = "unset";
                using var one = model.ValidationRule(x => x.Name, value => value == "ok", "required");
                using var two = model.ValidationRule(x => x.Child!.Name,
                    value => { predicateCalls++; seen = value; model.CurrentContext = replacement; return value == "ok"; },
                    value => { messageCalls++; return value is null ? "missing" : "bad:" + value; });
                using var three = model.ValidationRule(selected, x => x.Name, value => value == "ok", "selected");
                using var four = model.ValidationRule(selected, x => x.Child!.Name, value => value == "ok", value => "selected:" + value);
                Require(!one.IsValid && !two.IsValid && !three.IsValid && !four.IsValid, "actual invalid initial state");
                Require(predicateCalls == 1 && messageCalls == 1 && seen is null, "missing parent delivers default once");
                Require(model.ContextReads == 2, "selected context does not query model context");
                Require(original.Validations.Items.Count() == 2 && selected.Validations.Items.Count() == 2, "registration context ownership");
                var nested = original.Validations.Items.OfType<IPropertyValidationComponent>().Single(rule => rule.ContainsPropertyName("Child.Name"));
                Require(!nested.ContainsPropertyName("Name"), "full root metadata path");
                Require(nested.ContainsPropertyName("Child.Name", true), "one selected leaf remains an exclusive property rule");
                model.CurrentContext = replacement;
                model.Name = "ok";
                model.Child = new Model(replacement) { Name = "ok" };
                Require(one.IsValid && two.IsValid && three.IsValid && four.IsValid, "current synchronous validity");
                Require(predicateCalls == 2 && messageCalls == 1, "dynamic message executes only while invalid");
                model.Child.Name = "bad";
                Require(!two.IsValid && two.Message.ToSingleLine() == "bad:bad", "child value and dynamic message");
                model.Child = null;
                Require(!two.IsValid && seen is null && two.Message.ToSingleLine() == "missing", "disappearing parent delivers default");
                Require(model.ContextReads == 2 && replacement.Validations.Items.Count() == 0, "context selection stays captured");
                one.Dispose(); two.Dispose(); three.Dispose(); four.Dispose();
                Require(!original.Validations.Items.Any() && !selected.Validations.Items.Any(), "helpers remove from original contexts");
                return true;
            }
            private static void Require(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }
        }
        """;

    /// <summary>Typed factories support plain sources using declared notification adapters.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NonNotifyingFactoryUsesDeclaredInvalidation(bool reactive)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var source = $$"""
            using System;
            using System.Linq;
            using {{root}}.Capabilities;
            using {{root}}.Contexts;
            using {{root}}.States;
            public sealed class Model
            {
                private int _value;
                private event Action? Changed;
                public int Value => _value;
                public int Listeners { get; private set; }
                public void Set(int value) { _value = value; Changed?.Invoke(); }
                public IDisposable Subscribe(IObserver<ValidationInvalidation> observer)
                {
                    Action handler = () => observer.OnNext(default);
                    Changed += handler; Listeners++;
                    return new Token(() => { Changed -= handler; Listeners--; });
                }
                public static bool Check()
                {
                    var model = new Model();
                    using var context = new ValidationContext();
                    var selector = new ValidationSelector<Model, int>(current =>
                        new ValidationAccessPlan<int>(
                            () => ValidationRead<int>.Present(current.Value, new[] { ValidationPath.Legacy(nameof(Value)) }),
                            new[] { ValidationDependency.Create(() => current, static (owner, observer) => owner.Subscribe(observer)) },
                            ValidationObservationOptions<int>.Default));
                    var rule = ValidationRuntime.RegisterRule(model, context, selector,
                        value => value > 5 ? ValidationState.Valid : new ValidationState(false, "plain:" + value));
                    if (rule.IsValid || model.Listeners != 1) { rule.Dispose(); return false; }
                    model.Set(9);
                    var valid = rule.IsValid && context.GetIsValid();
                    rule.Dispose(); model.Set(0);
                    return valid && model.Listeners == 0 && !context.Validations.Items.Any();
                }
            }
            public sealed class Token(Action cleanup) : IDisposable
            {
                private Action? _cleanup = cleanup;
                public void Dispose() { var callback = _cleanup; _cleanup = null; callback?.Invoke(); }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Normal generic methods preserve selected interface/base dispatch despite a new shadow.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <param name="baseConstraint">Whether selection comes from a base-class constraint.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task NormalGenericConstraintSelectsDeclaredMember(bool reactive, bool baseConstraint)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var ui = reactive ? ReactiveUiRoot : PrimitiveUiRoot;
        var source = $$"""
            using System;
            using {{ui}};
            using System.ComponentModel;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            using {{root}}.Helpers;
            public interface IName : INotifyPropertyChanged { string? Name { get; } }
            public class NameBase : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
            {
                private string? _name;
                public virtual string? Name => _name;
                public new event PropertyChangedEventHandler? PropertyChanged;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Set(string value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); }
            }
            public sealed class Model : NameBase, IName
            {
                public new string? Name => throw new InvalidOperationException("new shadow getter");
                string? IName.Name => base.Name;
            }
            public static class Factory
            {
                public static ValidationHelper Attach<T>(T model) where T : {{(baseConstraint ? "NameBase" : "class, IName, global::ReactiveUI.IReactiveObject, IValidatableViewModel")}}
                    => model.ValidationRule(x => x.Name, value => value == "ok", "required");
            }
            public static class Fixture
            {
                public static bool Check()
                {
                    var model = new Model();
                    using var rule = Factory.Attach(model);
                    if (rule.IsValid) return false;
                    model.Set("ok");
                    return rule.IsValid && model.ValidationContext.GetIsValid();
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FactoryEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Retains checked conversion failures and rolls back initial registration ownership.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CheckedConversionFailureCleansInitialRuleRegistration(bool reactive)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var ui = reactive ? ReactiveUiRoot : PrimitiveUiRoot;
        var source = $$"""
            using System;
            using {{ui}};
            using System.ComponentModel;
            using System.Linq;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
            {
                private PropertyChangedEventHandler? _changed;
                public int Count { get; set; } = 300;
                public int ListenerCount { get; private set; }
                public new event PropertyChangedEventHandler? PropertyChanged
                {
                    add { _changed += value; ListenerCount++; }
                    remove { _changed -= value; ListenerCount--; }
                }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Invalidate() => _changed?.Invoke(this, new(nameof(Count)));
                public static bool Check()
                {
                    var model = new Model();
                    try
                    {
                        using var failed = model.ValidationRule(x => checked((byte)x.Count), value => value > 5, "byte");
                        return false;
                    }
                    catch (OverflowException) { }
                    if (model.ListenerCount != 0 || model.ValidationContext.Validations.Items.Any()) return false;
                    model.Count = 9;
                    using var rule = model.ValidationRule(x => checked((byte)x.Count), value => value > 5, "byte");
                    return rule.IsValid && model.ListenerCount == 1 && model.ValidationContext.GetIsValid();
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Callable generic factories retain constrained interface dispatch without a lexical model host.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GenericCallableFactoryUsesConstrainedInterface(bool reactive)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var ui = reactive ? ReactiveUiRoot : PrimitiveUiRoot;
        var source = $$"""
            using System;
            using {{ui}};
            using System.ComponentModel;
            using {{root}}.Capabilities;
            using {{root}}.Contexts;
            using {{root}}.Helpers;
            using {{root}}.States;
            public interface IName : INotifyPropertyChanged { string? Name { get; } }
            public sealed class Model : IName
            {
                private string? _name;
                private string? Name => throw new InvalidOperationException("private shadow");
                string? IName.Name => _name;
                public event PropertyChangedEventHandler? PropertyChanged;
                public void Set(string value) { _name = value; PropertyChanged?.Invoke(this, new("Name")); }
                public static bool Check()
                {
                    using var context = new ValidationContext();
                    var model = new Model();
                    Func<Model,IValidationContext,ValidationHelper> attach = Factory.Register<Model>;
                    using var rule = attach(model, context);
                    if (rule.IsValid) return false;
                    model.Set("ok");
                    return rule.IsValid && context.GetIsValid();
                }
            }
            public static class Factory
            {
                public static ValidationHelper Register<TSource>(TSource source, IValidationContext context) where TSource : class, IName
                {
                    var selector = new ValidationSelector<TSource,string?>(owner => new ValidationAccessPlan<string?>(
                        () => ValidationRead<string?>.Present(((IName)owner).Name, new[] { ValidationPath.Legacy("Name") }),
                        new[] { ValidationDependency.PropertyChanged(() => owner, "Name") }, ValidationObservationOptions<string?>.Default));
                    return ValidationRuntime.RegisterRule(source, context, selector, value => new ValidationState(value == "ok", "required"));
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Explicit destinations support notification sources without a model context interface.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExplicitContextDoesNotRequireModelContext(bool reactive)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var ui = reactive ? ReactiveUiRoot : PrimitiveUiRoot;
        var source = $$"""
            using System;
            using {{ui}};
            using System.ComponentModel;
            using System.Linq;
            using {{root}}.Capabilities;
            using {{root}}.Contexts;
            using {{root}}.States;
            public sealed class Model : INotifyPropertyChanged
            {
                public int Count { get; set; }
                public event PropertyChangedEventHandler? PropertyChanged;
                public void Set(int value) { Count = value; PropertyChanged?.Invoke(this, new(nameof(Count))); }
                public static bool Check()
                {
                    using var context = new ValidationContext();
                    var model = new Model();
                    var selector = new ValidationSelector<Model,int>(source => new ValidationAccessPlan<int>(
                        () => ValidationRead<int>.Present(source.Count, new[] { ValidationPath.Legacy(nameof(Count)) }),
                        new[] { ValidationDependency.PropertyChanged(() => source, nameof(Count)) }, ValidationObservationOptions<int>.Default));
                    using var one = ValidationRuntime.RegisterRule(model, context, selector, value => new ValidationState(value > 0, "positive"));
                    using var two = ValidationRuntime.RegisterRule(model, context, selector, value => new ValidationState(value > 0, "count:" + value));
                    if (one.IsValid || two.IsValid || context.GetIsValid()) return false;
                    model.Set(1);
                    if (!one.IsValid || !two.IsValid || !context.GetIsValid()) return false;
                    one.Dispose(); two.Dispose();
                    return !context.Validations.Items.Any();
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Follows dynamic index arguments, collection changes and collection replacement.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DynamicIndexerTracksArgumentAndCollectionChanges(bool reactive)
    {
        var source = ExpandFixture(DynamicIndexerTracksArgumentAndCollectionChangesSource, reactive);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Compatibility presentation seeds leave the current rule and admission state invalid.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LegacyPresentationSeedDoesNotFabricateDomainValidity(bool reactive)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var ui = reactive ? ReactiveUiRoot : PrimitiveUiRoot;
        var source = $$"""
            using System;
            using {{ui}};
            using System.Collections.Generic;
            using System.ComponentModel;
            using {{root}}.Abstractions;
            using {{root}}.Capabilities;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            using {{root}}.States;
            public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
            {
                public string? Value { get; set; }
                public new event PropertyChangedEventHandler? PropertyChanged;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Value)));
                public static bool Check()
                {
                    var model = new Model();
                    using var rule = model.ValidationRule(x => x.Value, value => value == "ok", "required");
                    var selector = new ValidationSelector<Model,IObservable<IValidationState>?>(source =>
                        new ValidationAccessPlan<IObservable<IValidationState>?>(
                            () => ValidationRead<IObservable<IValidationState>?>.Present(rule.ValidationChanged, Array.Empty<ValidationPath>()),
                            Array.Empty<ValidationDependency>(), ValidationObservationOptions<IObservable<IValidationState>?>.Default));
                    var capture = new Capture(model.ValidationContext);
                    using var presentation = ValidationRuntime.ObserveState(model, selector, ValidationInitialSequence.LegacyValid).Subscribe(capture);
                    return string.Join(",", capture.Values) == "True,False" && !capture.DomainWasValid && !rule.IsValid && !model.ValidationContext.GetIsValid();
                }
            }
            public sealed class Capture(IValidationContext context) : IObserver<IValidationState>
            {
                public List<bool> Values { get; } = new();
                public bool DomainWasValid { get; private set; }
                public void OnNext(IValidationState state) { Values.Add(state.IsValid); DomainWasValid |= context.GetIsValid(); }
                public void OnError(Exception error) => throw error;
                public void OnCompleted() { }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Uses inference to retain anonymous projections and complete boxed custom states.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AnonymousProjectionAndFileLocalFactoryPreserveRichState(bool reactive)
    {
        var source = ExpandFixture(AnonymousProjectionAndFileLocalFactoryPreserveRichStateSource, reactive);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FactoryEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Reads the selected interface implementation and preserves nullable conversions.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InterfaceCastsAndNullInterfaceParents(bool reactive)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var ui = reactive ? ReactiveUiRoot : PrimitiveUiRoot;
        var source = $$"""
            using System;
            using {{ui}};
            using System.ComponentModel;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public interface IAmount : INotifyPropertyChanged { int Amount { get; } }
            public sealed class Model : ReactiveObject, IAmount, IValidatableViewModel
            {
                private int _amount;
                private int Amount => throw new InvalidOperationException("private shadow");
                int IAmount.Amount => _amount;
                public IAmount? Child { get; set; }
                public new event PropertyChangedEventHandler? PropertyChanged;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public void Set(int amount) { _amount = amount; PropertyChanged?.Invoke(this, new("Amount")); }
                public void Replace(IAmount? child) { Child = child; PropertyChanged?.Invoke(this, new(nameof(Child))); }
                public static bool Check()
                {
                    var model = new Model();
                    long? observed = -1;
                    using var direct = model.ValidationRule(x => (long?)((IAmount)x).Amount, value => value == 9L, "amount");
                    using var nested = model.ValidationRule(x => (long?)x.Child!.Amount,
                        value => { observed = value; return value == 9L; }, "child");
                    if (direct.IsValid || nested.IsValid || observed is not null) return false;
                    model.Set(9);
                    var child = new Model(); child.Set(9); model.Replace(child);
                    if (!direct.IsValid || !nested.IsValid || observed != 9L) return false;
                    child.Set(3);
                    if (nested.IsValid || observed != 3L) return false;
                    model.Replace(null);
                    return !nested.IsValid && observed is null;
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Distinguishes absent owners from actual null leaves under each explicit policy.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <param name="policy">The missing-owner policy selected by the caller.</param>
    /// <param name="expected">The exact validation input sequence.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false, "DefaultValue", "<null>,<null>,ok,<null>")]
    [Arguments(true, "DefaultValue", "<null>,<null>,ok,<null>")]
    [Arguments(false, "Suppress", "<null>,ok")]
    [Arguments(true, "Suppress", "<null>,ok")]
    [Arguments(false, "Fallback", "fallback,<null>,ok,fallback")]
    [Arguments(true, "Fallback", "fallback,<null>,ok,fallback")]
    public async Task TypedFactoryMissingOwnerPolicies(bool reactive, string policy, string expected)
    {
        var source = ExpandFixture(TypedFactoryMissingOwnerPoliciesSource, reactive)
            .Replace(MissingOwnerPolicyToken, policy, StringComparison.Ordinal)
            .Replace(ExpectedValueToken, Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(expected, true), StringComparison.Ordinal);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Supplied lexical factories observe current captures without exposing private shapes.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NonpartialPrivateFactoryUsesCurrentCapturedRoot(bool reactive)
    {
        var source = ExpandFixture(NonpartialPrivateFactoryUsesCurrentCapturedRootSource, reactive);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FactoryEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Keeps generic decomposition and private access inside legal partial hosts.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <param name="valueType">Whether the lexical owner constrains its value to a struct.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task NestedGenericPrivateRuleUsesLexicalHost(bool reactive, bool valueType)
    {
        var root = reactive ? ReactiveValidationRoot : PrimitiveValidationRoot;
        var ui = reactive ? ReactiveUiRoot : PrimitiveUiRoot;
        var source = $$"""
            using System;
            using {{ui}};
            using System.ComponentModel;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            using {{root}}.Helpers;
            public partial class Outer<T> where T : {{(valueType ? "struct" : "class")}}
            {
                private sealed partial class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
                {
                    private T? Secret { get; set; }
                    public new event PropertyChangedEventHandler? PropertyChanged;
                    public IValidationContext ValidationContext { get; } = new ValidationContext();
                    public ValidationHelper Attach() => this.ValidationRule(x => x.Secret, value => value is not null, "private required");
                    public void Set(T value) { Secret = value; PropertyChanged?.Invoke(this, new(nameof(Secret))); }
                }
                public static bool Check(T current)
                {
                    var model = new Model();
                    using var rule = model.Attach();
                    if (rule.IsValid || rule.Message.ToSingleLine() != "private required") return false;
                    model.Set(current);
                    return rule.IsValid && model.ValidationContext.GetIsValid();
                }
            }
            public static class Fixture { public static bool Check() => Outer<{{(valueType ? "int" : "string")}}>.Check({{(valueType ? "9" : "\"current\"")}}); }
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FactoryEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Retains semantic conversion, field/computation dependencies and finite selector provenance.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <param name="declaration">The selector's optional local declaration.</param>
    /// <param name="selector">The source selector syntax.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false, "", "x => (long)x.Count + x.Bonus")]
    [Arguments(true, "", "x => (long)x.Count + x.Bonus")]
    [Arguments(false, "Expression<Func<Model,long>> selected = x => (long)x.Count + x.Bonus;", "selected")]
    [Arguments(true, "Expression<Func<Model,long>> selected = x => (long)x.Count + x.Bonus;", "selected")]
    public async Task FieldsConversionsComputedValuesAndStoredSelectors(bool reactive, string declaration, string selector)
    {
        var source = ExpandFixture(FieldsConversionsComputedValuesAndStoredSelectorsSource, reactive)
            .Replace(SelectorDeclarationToken, declaration, StringComparison.Ordinal)
            .Replace(SelectorValueToken, selector, StringComparison.Ordinal);
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Preserves all predicate shapes, null delivery and captured registration ownership.</summary>
    /// <param name="reactive">Whether to exercise the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PredicateShapesCaptureContextsAndDeliverMissingParents(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        foreach (var expression in new[] { false, true })
        {
            var source = ExpandFixture(PredicateShapesCaptureContextsAndDeliverMissingParentsSource, reactive);
            if (expression)
            {
                source = "using System.Linq.Expressions;" + Environment.NewLine + source
                    .Replace("x => x.Name", "(Expression<Func<Model,string?>>)(x => x.Name)", StringComparison.Ordinal)
                    .Replace("x => x.Child!.Name", "(Expression<Func<Model,string?>>)(x => x.Child!.Name)", StringComparison.Ordinal);
            }

            var result = await host.RunAsync(source);
            await Assert.That(result.GeneratorDiagnostics).IsEmpty();
            await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
            await Assert.That(await CountSelectedRuleSelectorsAsync(result.Compilation, expression)).IsEqualTo(PredicateRuleForms);
            await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
        }
    }

    /// <summary>Expands the flavor-specific namespace tokens in consumer source.</summary>
    /// <param name="source">The consumer template.</param>
    /// <param name="reactive">Whether to use System.Reactive namespaces.</param>
    /// <returns>The exact consumer source for the selected flavor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ExpandFixture(string source, bool reactive) => source
        .Replace(ValidationRootToken, reactive ? ReactiveValidationRoot : PrimitiveValidationRoot, StringComparison.Ordinal)
        .Replace(UiRootToken, reactive ? ReactiveUiRoot : PrimitiveUiRoot, StringComparison.Ordinal);
}
