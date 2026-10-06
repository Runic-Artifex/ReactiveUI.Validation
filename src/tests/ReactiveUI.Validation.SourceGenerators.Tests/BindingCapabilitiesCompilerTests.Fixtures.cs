// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Provides isolated typed compiler inputs for binding capability regressions.</summary>
public sealed partial class BindingCapabilitiesCompilerTests
{
    /// <summary>The runtime fixture entry type.</summary>
    private const string FixtureType = "Fixture";

    /// <summary>The runtime fixture entry method.</summary>
    private const string FixtureMethod = "Check";

    /// <summary>The declaration replaced when adding a trusted fixture type.</summary>
    private const string FixtureDeclaration = "public static class Fixture";

    /// <summary>The actionable binding capability diagnostic.</summary>
    private const string BindingDiagnosticId = "RUVG006";

    /// <summary>The diagnostic fragment identifying an explicit nullable storage policy.</summary>
    private const string NullablePolicyMessage = "nullable-value policy";

    /// <summary>Consumer types shared by the static factory and semantic target fixtures.</summary>
    private const string FixtureTemplate = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Linq.Expressions;
        using {{ui}};
        using {{ui}}.Builder;
        using {{binding}};
        using {{root}}.Abstractions;
        using {{root}}.Capabilities;
        using {{root}}.Collections;
        using {{root}}.Components.Abstractions;
        using {{root}}.Contexts;
        using {{root}}.Extensions;
        using {{root}}.Formatters.Abstractions;
        using {{root}}.Helpers;
        using {{root}}.States;
        using {{root}}.ValidationBindings;
        using {{root}}.ValidationBindings.Abstractions;
        public sealed class Model : ReactiveObject, IValidatableViewModel
        {
            public string Name { get; set; } = "";
            public string Other { get; set; } = "";
            public string NameField = "";
            public int ForbiddenReads { get; private set; }
            public string MetadataOnly
            {
                get
                {
                    ForbiddenReads++;
                    throw new InvalidOperationException("Metadata filtering read the selected value.");
                }
            }
            public ValidationHelper? Rule { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            public SourceParent? Parent { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            public IValidationContext ValidationContext { get; } = new ValidationContext();
        }
        public sealed class View : ReactiveObject, IViewFor<Model>
        {
            public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
            public string Message { get; set; } = "";
            public string? NullableMessage { get; set; }
            public IList<string> NonNullableItems { get; set; } = new List<string>();
            public string OtherMessage { get; set; } = "";
            public string SetterOnly
            {
                get => throw new InvalidOperationException("A target binding read its selected leaf.");
                set => OtherMessage = value;
            }
            public int NormalizedWrites { get; private set; }
            public string NormalizedMessage
            {
                get => Message;
                set
                {
                    if (++NormalizedWrites > 10) throw new InvalidOperationException("Normalized setter reconciliation loop.");
                    Message = value.Trim();
                }
            }
            public string MessageField = "";
            public Output? Presentation { get; set; }
            public ConvertedOutput Converted { get; set; }
            public NullableConvertedOutput? NullableConverted { get; set; }
            public NullableConvertedOutput NonNullableConverted { get; set; } = new("");
            public int Index { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            public int Writes0 { get; private set; }
            public int Writes1 { get; private set; }
            public int StateAssignments { get; private set; }
            public Action? OnStateWrite { get; set; }
            public WritableState State
            {
                get;
                set
                {
                    StateAssignments++;
                    if (StateAssignments > 10) throw new InvalidOperationException("Struct write-back notification loop.");
                    field = value;
                    var reenter = OnStateWrite;
                    OnStateWrite = null;
                    reenter?.Invoke();
                    this.RaisePropertyChanged();
                }
            } = new("", 7);
            public string this[int index]
            {
                get => index == 0 ? Message : OtherMessage;
                set
                {
                    if (index == 0)
                    {
                        Writes0++;
                        Message = value;
                    }
                    else
                    {
                        Writes1++;
                        OtherMessage = value;
                    }
                }
            }
            public Panel? Panel
            {
                get;
                set
                {
                    if (ReferenceEquals(field, value)) return;
                    field = value;
                    this.RaisePropertyChanged();
                }
            } = new();
        }
        public sealed class Panel
        {
            public string Message { get; set; } = "";
            public string? NullableMessage { get; set; }
            public IList<string> NonNullableItems { get; set; } = new List<string>();
            public override bool Equals(object? other) => other is Panel;
            public override int GetHashCode() => 0;
        }
        public readonly record struct Output(string Text);
        public readonly record struct ConvertedOutput(string Text)
        {
            public static int Conversions;
            public static implicit operator ConvertedOutput(string text)
            {
                Conversions++;
                return new ConvertedOutput(text);
            }
        }
        public sealed class NullableConvertedOutput(string text)
        {
            public string Text { get; } = text;
            public static implicit operator NullableConvertedOutput?(string value) =>
                value.Length == 0 ? null : new NullableConvertedOutput(value);
        }
        public record struct WritableState(string Error, int Kept);
        public sealed class SourceParent : ReactiveObject
        {
            public int? Number { get; set => this.RaiseAndSetIfChanged(ref field, value); }
        }
        public sealed record RichState(bool IsValid, string Code, int Revision) : IValidationState
        {
            public IValidationText Text => ValidationText.Create(Code);
        }
        public sealed class Formatter : IValidationTextFormatter<Output?>
        {
            public readonly List<IValidationText> Inputs = new();
            public Output? Format(IValidationText text)
            {
                Inputs.Add(text);
                return text.Count == 0 ? null : new Output("formatted:" + text.ToSingleLine());
            }
        }
        public sealed class Current(IValidationState initial) : IObservable<IValidationState>
        {
            private readonly List<IObserver<IValidationState>> _observers = new();
            private IValidationState _current = initial;
            public int Subscribers => _observers.Count;
            public IDisposable Subscribe(IObserver<IValidationState> observer)
            {
                _observers.Add(observer);
                observer.OnNext(_current);
                return new Cleanup(() => _observers.Remove(observer));
            }
            public void Set(IValidationState state)
            {
                _current = state;
                foreach (var observer in _observers.ToArray()) observer.OnNext(state);
            }
        }
        public sealed class Cleanup(Action cleanup) : IDisposable
        {
            public void Dispose() => cleanup();
        }
        public static class Fixture
        {
            public static bool Check()
            {
                RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                {{body}}
            }
            private static void Require(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }
        }
        """;

    /// <summary>An event-based owner deliberately independent of INPC.</summary>
    private const string ManualSource = """
        public sealed class ManualSource
        {
            public event Action? Changed;
            public int Value { get; private set; }
            public int Listeners { get; private set; }
            public void Set(int value) { Value = value; Changed?.Invoke(); }
            public IDisposable Subscribe(IObserver<ValidationInvalidation> observer)
            {
                Action handler = () => observer.OnNext(default);
                Changed += handler;
                Listeners++;
                return new Cleanup(() => { Changed -= handler; Listeners--; });
            }
        }
        """;

    /// <summary>A declared setter storage policy distinguishes generated peers from external replacements.</summary>
    private const string ExplicitStorageMembers = """
        public ValidationStoragePolicy StatePolicy { get; } = new();
        private WritableState _ownedState = new("", 7);
        public WritableState OwnedState
        {
            get => _ownedState;
            set => SetOwnedState(value, null);
        }
        public void SetOwnedState(WritableState value, object? origin)
        {
            if (++StateAssignments > 20) throw new InvalidOperationException("Peer struct write-back notification loop.");
            _ownedState = value;
            StatePolicy.RecordChange(origin);
            var reenter = OnStateWrite;
            OnStateWrite = null;
            reenter?.Invoke();
            this.RaisePropertyChanged(nameof(OwnedState));
        }
        """;

    /// <summary>Current source streams and explicit typed storage targets for the peer-write fixtures.</summary>
    private const string ExplicitStorageSetup = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "first", 66));
        var secondStates = new Current(new RichState(false, "second", 67));
        using var firstRule = model.AddObservableRule(firstStates, new[] { "Name" });
        using var secondRule = model.AddObservableRule(secondStates, new[] { "Other" });
        Expression<Func<View, string>> errorExpression = owner => owner.OwnedState.Error;
        Expression<Func<View, int>> keptExpression = owner => owner.OwnedState.Kept;
        var errorPath = ValidationPath.Legacy("OwnedState.Error");
        var keptPath = ValidationPath.Legacy("OwnedState.Kept");
        var errorTarget = new ValidationTarget<View, string>(owner =>
        {
            var receipt = owner.StatePolicy.CreateReceipt(errorPath);
            var checks = 0;
            var dependency = ValidationDependency.Create(() => owner, (current, observer) =>
            {
                System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
                {
                    if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(View.OwnedState)) observer.OnNext(default);
                };
                current.PropertyChanged += handler;
                return new Cleanup(() => current.PropertyChanged -= handler);
            });
            return new ValidationWritePlan<string>(() => ValidationTargetAccess<string>.Present(owner, errorPath,
                value => owner.StatePolicy.Write(receipt, () => owner.OwnedState, value,
                    static (storage, output) => { storage.Error = output; return storage; }, owner.SetOwnedState), _ =>
            {
                if (++checks > 100) throw new InvalidOperationException("Deferred error writer reconciliation loop.");
                return receipt.IsCurrent();
            }), new[] { dependency, ValidationDependency.Writer(receipt) });
        });
        var keptTarget = new ValidationTarget<View, int>(owner =>
        {
            var receipt = owner.StatePolicy.CreateReceipt(keptPath);
            var checks = 0;
            var dependency = ValidationDependency.Create(() => owner, (current, observer) =>
            {
                System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
                {
                    if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(View.OwnedState)) observer.OnNext(default);
                };
                current.PropertyChanged += handler;
                return new Cleanup(() => current.PropertyChanged -= handler);
            });
            return new ValidationWritePlan<int>(() => ValidationTargetAccess<int>.Present(owner, keptPath,
                value => owner.StatePolicy.Write(receipt, () => owner.OwnedState, value,
                    static (storage, output) => { storage.Kept = output; return storage; }, owner.SetOwnedState), _ =>
            {
                if (++checks > 100) throw new InvalidOperationException("Deferred kept writer reconciliation loop.");
                return receipt.IsCurrent();
            }), new[] { dependency, ValidationDependency.Writer(receipt) });
        });
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var errorRegistration = registry.RegisterTarget<View, string, string>(ValidationPlanRole.Target, errorExpression, errorTarget);
        using var keptRegistration = registry.RegisterTarget<View, int, int>(ValidationPlanRole.Target, keptExpression, keptTarget);
        Expression<Func<Model, string>> nameExpression = owner => owner.Name;
        Expression<Func<Model, string>> otherExpression = owner => owner.Other;
        ValidationSelector<Model, string> Metadata(string path) => new(owner =>
            new ValidationAccessPlan<string>(() => throw new InvalidOperationException("Metadata plan read a selected leaf."),
                Array.Empty<ValidationDependency>(), ValidationObservationOptions<string>.Default,
                () => new[] { ValidationPath.Legacy(path) }));
        using var nameRegistration = registry.RegisterSelector(ValidationPlanRole.Property, nameExpression, Metadata("Name"));
        using var otherRegistration = registry.RegisterSelector(ValidationPlanRole.Property, otherExpression, Metadata("Other"));
        """;

    /// <summary>A scoped helper registration used by explicit nullable target policies.</summary>
    private const string RegisteredHelperSetup = """
        Expression<Func<Model, ValidationHelper?>> helperExpression = owner => owner.Rule;
        var helperPlan = new ValidationSelector<Model, ValidationHelper?>(owner =>
            new ValidationAccessPlan<ValidationHelper?>(() => ValidationRead<ValidationHelper?>.Present(owner.Rule, Array.Empty<ValidationPath>()),
                new[] { ValidationDependency.PropertyChanged(() => owner, nameof(Model.Rule)) },
                new ValidationObservationOptions<ValidationHelper?>(ValidationMissingOwnerPolicy.DefaultValue,
                    null, ReferenceEqualityComparer.Instance, false)));
        using var helperRegistration = registry.RegisterSelector(ValidationPlanRole.Helper, helperExpression, helperPlan);
        """;

    /// <summary>A lexically hosted target inside an open nested generic view.</summary>
    private const string HostedView = """
        public partial class Outer<TOuter> where TOuter : class
        {
            public sealed partial class Inner<T> : ReactiveObject, IViewFor<Model> where T : struct
            {
                private string PrivateMessage { get; set; } = "";
                public string Result => PrivateMessage;
                public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
                object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
                public IDisposable Bind(Model model) => this.BindValidation(model, x => x.Name, x => x.PrivateMessage);
            }
        }
        """;

    /// <summary>An open generic partial view whose private indexer key has a private nested type.</summary>
    private const string KeyedView = """
        public sealed partial class KeyedView<TPrefix> : ReactiveObject, IViewFor<Model> where TPrefix : class
        {
            private readonly Marker _key = new();
            private Owner<Marker>? CurrentOwner { get; set => this.RaiseAndSetIfChanged(ref field, value); } = new();
            private int _keyReads;
            private Marker Key { get { _keyReads++; return _key; } }
            public int KeyReads => _keyReads;
            public void RaiseKey() => this.RaisePropertyChanged(nameof(Key));
            public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
            public IDisposable Bind(Model model) => this.BindValidationState(model, source => source.Rule,
                view => view.CurrentOwner![view.Key], static state => state.IsValid ? null : state.Text.ToSingleLine());
            public string? Indexed() => CurrentOwner![_key];
            private class ConstraintBase { }
            private sealed class Marker : ConstraintBase { }
            private sealed class Owner<U> : ReactiveObject where U : ConstraintBase
            {
                private readonly Dictionary<U, string?> _values = [];
                [GeneratedValidationAccess]
                internal string? this[U key]
                {
                    get => _values.TryGetValue(key, out var value) ? value : null;
                    [param: global::System.Diagnostics.CodeAnalysis.AllowNull]
                    set => _values[key] = value ?? "cleared";
                }
            }
        }
        """;

    /// <summary>Original legal private model selection with an unrelated nonpartial view receiver.</summary>
    private const string PrivateModelMembers = """
        private ValidationHelper? PrivateRule => Rule;
        public void SetPrivateRule(ValidationHelper? helper)
        {
            Rule = helper;
            this.RaisePropertyChanged(nameof(PrivateRule));
        }
        public IValidationBinding BindPrivateHelper(View view, Action<IValidationState> onNext) =>
            view.BindValidationState(this, owner => owner.PrivateRule, static state => state, onNext);
        """;

    /// <summary>A concrete selected context whose unrelated hidden stream must not affect interface dispatch.</summary>
    private const string ShadowContext = """
        public sealed class ShadowContext : ValidationContext
        {
            private new IObservable<IValidationState> ValidationStatusChange =>
                throw new InvalidOperationException("Selected context used a concrete hidden stream.");
            public override bool Equals(object? other) => other is ShadowContext;
            public override int GetHashCode() => 0;
        }
        """;

    /// <summary>All eleven public static binding overloads, including nullable custom formatter outputs.</summary>
    private const string StaticOverloadsBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "name-required", 41);
        var states = new Current(first);
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        model.Rule = rule;
        var formatter = new Formatter();
        var bindings = new List<IDisposable>
        {
            ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.Name, x => x.Message),
            ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.Name, x => x.Message, null),
            ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.Name, x => x.Message, null, false),
            ValidationBinding.ForProperty<View, Model, string, Output?>(view, x => x.Name, static (raw, formatted) => { }, formatter),
            ValidationBinding.ForProperty<View, Model, string, Output?>(view, x => x.Name, static (raw, formatted) => { }, formatter, false),
            ValidationBinding.ForValidationHelperProperty<View, Model, string>(view, x => x!.Rule, x => x.Message),
            ValidationBinding.ForValidationHelperProperty<View, Model, string>(view, x => x!.Rule, x => x.Message, null),
            ValidationBinding.ForValidationHelperProperty<View, Model, Output?>(view, x => x!.Rule, static (raw, formatted) => { }, formatter),
            ValidationBinding.ForViewModel<View, Model, string>(view, x => x.Message),
            ValidationBinding.ForViewModel<View, Model, string>(view, x => x.Message, null),
            ValidationBinding.ForViewModel<View, Model, Output?>(view, static formatted => { }, formatter),
        };
        Require(view.Message == "name-required", "static target initial state");
        Require(formatter.Inputs.Count >= 4, "custom formatter callbacks");
        foreach (var binding in bindings) binding.Dispose();
        return true;
        """;

    /// <summary>All fourteen familiar extension signatures use actual typed initial state.</summary>
    private const string ExtensionOverloadsBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "inventory", 42);
        var states = new Current(first);
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        model.Rule = rule;
        var helperCalls = new List<IValidationState>();
        var propertyCalls = new List<IList<IValidationState>>();
        var contextCalls = new List<IValidationState>();
        var contextPropertyCalls = new List<IList<IValidationState>>();
        var bindings = new List<IDisposable>
        {
            view.BindValidation(model, x => x.Name, x => x.Message),
            view.BindValidation(model, x => x.Name, x => x.Message, null),
            view.BindValidation(model, x => x.Message),
            view.BindValidation(model, x => x.Message, null),
            view.BindValidation(model, x => x!.Rule, x => x.Message),
            view.BindValidation(model, x => x!.Rule, x => x.Message, null),
            view.BindValidationState(model, x => x.Rule, x => x.Presentation,
                static state => state.IsValid ? (Output?)null : new Output(state.Text.ToSingleLine())),
            view.BindValidationState(model, x => x.Rule,
                state => { helperCalls.Add(state); return state; }, static state => { }),
            view.BindValidationState(model, x => x.Name, x => x.Presentation,
                static raw => raw.Count == 0 ? (Output?)null : new Output(raw[0].Text.ToSingleLine()), true),
            view.BindValidationState(model, x => x.Name,
                raw => { propertyCalls.Add(raw); return raw; }, static raw => { }, true),
            view.BindValidationContext(model, x => x.ValidationContext, x => x.Message),
            view.BindValidationContext(model, x => x.ValidationContext, x => x.Name, x => x.Message),
            view.BindValidationContext(model, x => x.ValidationContext, contextCalls.Add),
            view.BindValidationContext(model, x => x.ValidationContext, x => x.Name, contextPropertyCalls.Add),
        };
        Require(view.Message == "inventory" && view.Presentation?.Text == "inventory", "typed actual initial target values");
        Require(helperCalls.Count == 1 && ReferenceEquals(helperCalls[0], first), "initial complete helper state");
        Require(propertyCalls.Count == 1 && ReferenceEquals(propertyCalls[0][0], first), "initial matching property state");
        Require(contextCalls.Count == 1 && contextPropertyCalls.Count == 1 && ReferenceEquals(contextPropertyCalls[0][0], first), "selected context callbacks");
        states.Set(new RichState(false, "later", 43));
        Require(view.Message == "later" && view.Presentation?.Text == "later", "typed target updates");
        view.ViewModel = null;
        Require(view.Message == "" && view.Presentation is null && helperCalls.Last().IsValid && propertyCalls.Last().Count == 0, "missing model clear and raw projections");
        foreach (var binding in bindings) binding.Dispose();
        var count = helperCalls.Count;
        view.ViewModel = model;
        states.Set(new RichState(false, "disposed", 44));
        Require(helperCalls.Count == count && view.Message == "", "all extension binding lifetimes detached");
        return true;
        """;

    /// <summary>Static property/helper callbacks retain raw identity and ordered nullable formatted values.</summary>
    private const string StaticProjectionBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "first", 7);
        IValidationState second = new RichState(false, "second", 8);
        var firstStates = new Current(first);
        var secondStates = new Current(second);
        using var firstRule = model.AddObservableRule(firstStates, new[] { "Name" });
        using var secondRule = model.AddObservableRule(secondStates, new[] { "Name", "Other" });
        model.Rule = firstRule;
        var formatter = new Formatter();
        IList<IValidationState>? latestRaw = null;
        IList<Output?>? latestFormatted = null;
        IValidationState? latestHelper = null;
        Output? latestHelperOutput = null;
        using var property = ValidationBinding.ForProperty<View, Model, string, Output?>(
            formatter: formatter, strict: false, viewModelProperty: x => x.Name, view: view,
            action: (raw, formatted) => { latestRaw = raw; latestFormatted = formatted; });
        using var helper = ValidationBinding.ForValidationHelperProperty<View, Model, Output?>(
            view, x => x!.Rule, (raw, formatted) => { latestHelper = raw; latestHelperOutput = formatted; }, formatter);
        Require(latestRaw!.Count == 2 && ReferenceEquals(latestRaw[0], first) && ReferenceEquals(latestRaw[1], second), "raw state identity and rule order");
        Require(latestFormatted![0]?.Text == "formatted:first" && latestFormatted[1]?.Text == "formatted:second", "ordered FormatAll outputs");
        Require(ReferenceEquals(latestHelper, first) && latestHelperOutput?.Text == "formatted:first", "helper raw state identity");
        IValidationState changed = new RichState(false, "first", 9);
        firstStates.Set(changed);
        Require(ReferenceEquals(latestRaw![0], changed) && ReferenceEquals(latestHelper, changed), "same-message custom state update");
        model.Rule = null;
        Require(latestHelper!.IsValid && latestHelperOutput is null, "null helper passes valid text through custom formatter");
        view.ViewModel = null;
        Require(latestRaw!.Count == 0 && latestFormatted!.Count == 0, "missing model empty projection");
        view.ViewModel = model;
        Require(latestRaw!.Count == 2 && ReferenceEquals(latestRaw[0], changed), "reattached current rule states");
        helper.Dispose();
        property.Dispose();
        firstStates.Set(new RichState(false, "disposed", 10));
        Require(ReferenceEquals(latestRaw![0], changed), "disposed callbacks remain unchanged");
        return true;
        """;

    /// <summary>Field metadata, writable fields, constant index targets and terminal conversion assignment.</summary>
    private const string SemanticTargetsBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "semantic", 17));
        using var rule = model.AddObservableRule(states, new[] { "NameField" });
        using var field = view.BindValidation(model, x => x.NameField, x => x.MessageField);
        using var index = ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.NameField, x => x[1]);
        using var converted = view.BindValidation(model, x => x.NameField, x => (object)x.Message);
        Require(view.MessageField == "semantic" && view.OtherMessage == "semantic" && view.Message == "semantic",
            $"field/index/conversion initial assignment: field={view.MessageField}, index={view.OtherMessage}, converted={view.Message}");
        states.Set(new RichState(false, "updated", 18));
        Require(view.MessageField == "updated" && view.OtherMessage == "updated" && view.Message == "updated", "field/index/conversion updates");
        field.Dispose();
        index.Dispose();
        converted.Dispose();
        states.Set(new RichState(false, "disposed", 19));
        Require(view.MessageField == "updated" && view.OtherMessage == "updated" && view.Message == "updated", "semantic target disposal");
        return true;
        """;

    /// <summary>Direct targets need only legal setter access and may normalize their assigned value.</summary>
    private const string DirectSetterBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, " initial ", 56));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        using var setterOnly = view.BindValidation(model, x => x.Name, x => x.SetterOnly);
        using var normalized = view.BindValidation(model, x => x.Name, x => x.NormalizedMessage);
        using var converted = view.BindValidation(model, x => x.Name, x => x.Converted);
        Require(view.Converted.Text == " initial " && ConvertedOutput.Conversions == 1, "user-defined conversion executes once per assignment");
        Require(view.OtherMessage == " initial " && view.Message == "initial" && view.NormalizedWrites == 1, "setter-only access and normalization without reconciliation loop");
        states.Set(new RichState(false, " updated ", 57));
        Require(view.Converted.Text == " updated " && ConvertedOutput.Conversions == 2, "next assignment converts once without reconciliation conversion");
        Require(view.OtherMessage == " updated " && view.Message == "updated" && view.NormalizedWrites == 2, "new source output assigned once to normalized setter");
        return true;
        """;

    /// <summary>Metadata filtering never reads the selected property value.</summary>
    private const string MetadataOnlyBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "metadata", 23));
        using var rule = model.AddObservableRule(states, new[] { "MetadataOnly" });
        var formatter = new Formatter();
        IList<IValidationState>? observed = null;
        using var text = view.BindValidation(model, x => x.MetadataOnly, x => x.Message);
        using var callback = ValidationBinding.ForProperty<View, Model, string, Output?>(view, x => x.MetadataOnly,
            (raw, formatted) => observed = raw, formatter);
        Require(model.ForbiddenReads == 0 && view.Message == "metadata" && observed!.Count == 1, "initial metadata observation");
        states.Set(new RichState(false, "later", 24));
        Require(model.ForbiddenReads == 0 && view.Message == "later" && observed!.Count == 1, "state update metadata observation");
        view.ViewModel = null;
        view.ViewModel = model;
        Require(model.ForbiddenReads == 0 && view.Message == "later", "model replacement metadata observation");
        return true;
        """;

    /// <summary>Changing a target key replays the latest projection once even when its owner is unchanged.</summary>
    private const string DynamicTargetSlotBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "cached", 25));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        using var binding = view.BindValidation(model, x => x.Name, x => x[x.Index]);
        Require(view.Message == "cached" && view.Writes0 == 1 && view.Writes1 == 0, "initial target slot");
        view.Index = 1;
        Require(view.OtherMessage == "cached" && view.Writes0 == 1 && view.Writes1 == 1, "changed key cached replay");
        view.RaisePropertyChanged(nameof(View.Index));
        Require(view.Writes0 == 1 && view.Writes1 == 1, "unchanged key invalidation is not a new slot");
        view.Index = 0;
        Require(view.Writes0 == 2 && view.Writes1 == 1, "return to old slot cached replay");
        states.Set(new RichState(false, "updated", 26));
        Require(view.Message == "updated" && view.OtherMessage == "cached" && view.Writes0 == 3, "current slot receives next source output");
        binding.Dispose();
        view.Index = 1;
        Require(view.Writes0 == 3 && view.Writes1 == 1, "disposed slot observation detached");
        return true;
        """;

    /// <summary>Nested target owners use reference identity and retain cached output during absence.</summary>
    private const string NestedTargetBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var first = view.Panel!;
        var states = new Current(new RichState(false, "initial", 53));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        using var binding = view.BindValidation(model, x => x.Name, x => x.Panel!.Message);
        Require(first.Message == "initial", "initial nested target");
        var second = new Panel();
        view.Panel = second;
        Require(second.Message == "initial", "equal-by-value replacement replays by reference identity");
        view.Panel = null;
        states.Set(new RichState(false, "cached", 54));
        Require(first.Message == "initial" && second.Message == "initial", "missing target caches and detaches old owners");
        var third = new Panel();
        view.Panel = third;
        Require(third.Message == "cached", "restored target receives latest cached projection");
        binding.Dispose();
        view.Panel = new Panel();
        states.Set(new RichState(false, "disposed", 55));
        Require(view.Panel!.Message == "" && third.Message == "cached", "disposed nested target no replay");
        return true;
        """;

    /// <summary>An opaque writable struct parent lacks the origin information required for safe replay.</summary>
    private const string OpaqueStructBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        using var binding = view.BindValidation(model, x => x.Name, x => x.State.Error);
        return true;
        """;

    /// <summary>Reentrant external replacement wins storage ownership and receives the current cached output.</summary>
    private const string ReentrantStructBody = """
        view.OnStateWrite = () => view.OwnedState = new WritableState("external", 101);
        using var binding = view.BindValidation(model, x => x.Name, errorExpression);
        Require(view.OwnedState.Error == "first" && view.OwnedState.Kept == 101 && view.StateAssignments == 3, "declared origin policy reconciles reentrant external storage replacement");
        firstStates.Set(new RichState(false, "updated", 74));
        Require(view.OwnedState.Error == "updated" && view.OwnedState.Kept == 101 && view.StateAssignments == 4, "declared target acquires fresh storage");
        binding.Dispose();
        view.OwnedState = new WritableState("after-disposal", 100);
        Require(view.OwnedState.Error == "after-disposal" && view.StateAssignments == 5, "disposed declared struct target has no replay");
        return true;
        """;

    /// <summary>Stored expression plans bind current models and target keys without invoking selected getters.</summary>
    private const string StoredSelectorsBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "registered", 45));
        using var firstRule = model.AddObservableRule(firstStates, new[] { "Other" });
        Expression<Func<Model, string>> property = owner => owner.MetadataOnly;
        Expression<Func<View, string>> target = owner => owner[owner.Index];
        var paths = new[] { ValidationPath.Legacy("Other") };
        var selector = new ValidationSelector<Model, string>(owner =>
            new ValidationAccessPlan<string>(
                () => throw new InvalidOperationException("Property-role registration must use metadata reader."),
                Array.Empty<ValidationDependency>(), ValidationObservationOptions<string>.Default, () => paths));
        var targetPlan = new ValidationTarget<View, string>(owner =>
            new ValidationWritePlan<string>(() =>
            {
                var key = owner.Index;
                return ValidationTargetAccess<string>.Present(owner,
                    ValidationPath.Structural("Item[]", key, EqualityComparer<int>.Default), value => owner[key] = "catalog:" + value);
            }, new[] { ValidationDependency.PropertyChanged(() => owner, nameof(View.Index)) }));
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var propertyRegistration = registry.RegisterSelector(ValidationPlanRole.Property, property, selector);
        using var targetRegistration = registry.RegisterTarget<View, string, string>(ValidationPlanRole.Target, target, targetPlan);
        var argumentEvaluations = 0;
        Expression<Func<View, string>> GetTarget() { argumentEvaluations++; return target; }
        [ValidationRuntimeDispatch]
        IDisposable BindFactory() => view.BindValidation(model, property, GetTarget());
        using var factoryBinding = BindFactory();
        Require(argumentEvaluations == 1 && model.ForbiddenReads == 0 && view.Message == "catalog:registered", "one expression evaluation and metadata-only registered dispatch");
        factoryBinding.Dispose();
        using var binding = view.BindValidation(model, property, target);
        view.Index = 1;
        Require(view.OtherMessage == "catalog:registered" && view.Writes1 == 1, "registered current target key replay");
        var secondModel = new Model();
        var secondStates = new Current(new RichState(false, "replacement", 46));
        using var secondRule = secondModel.AddObservableRule(secondStates, new[] { "Other" });
        view.ViewModel = secondModel;
        Require(view.OtherMessage == "catalog:replacement" && secondModel.ForbiddenReads == 0, "registered descriptor rebound to current model");
        firstStates.Set(new RichState(false, "detached", 47));
        Require(view.OtherMessage == "catalog:replacement", "old model state source detached");
        binding.Dispose();
        var writes = view.Writes0;
        view.Index = 0;
        Require(view.Writes0 == writes, "stored target dependency detached");
        return true;
        """;

    /// <summary>An explicitly registered indirect call preserves the original static generic arity.</summary>
    private const string IndirectCallBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "indirect", 48));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        Expression<Func<Model, string>> property = owner => owner.Name;
        Expression<Func<View, string>> target = owner => owner.Message;
        var paths = new[] { ValidationPath.Legacy("Name") };
        var selector = new ValidationSelector<Model, string>(owner =>
            new ValidationAccessPlan<string>(() => ValidationRead<string>.Present(owner.Name, paths),
                Array.Empty<ValidationDependency>(), ValidationObservationOptions<string>.Default, () => paths));
        var targetPlan = new ValidationTarget<View, string>(owner =>
            new ValidationWritePlan<string>(() => ValidationTargetAccess<string>.Present(owner, value => owner.Message = value),
                Array.Empty<ValidationDependency>()));
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var propertyRegistration = registry.RegisterSelector(ValidationPlanRole.Property, property, selector);
        using var targetRegistration = registry.RegisterTarget<View, string, string>(ValidationPlanRole.Target, target, targetPlan);
        Func<View, Expression<Func<Model, string>>, Expression<Func<View, string>>, IValidationBinding> factory =
            ValidationBinding.ForProperty<View, Model, string, string>;
        using var binding = factory(view, property, target);
        Require(view.Message == "indirect", "registered method group initial state");
        states.Set(new RichState(false, "updated", 49));
        Require(view.Message == "updated", "registered method group update");
        binding.Dispose();
        states.Set(new RichState(false, "disposed", 50));
        Require(view.Message == "updated", "registered method group disposal");
        return true;
        """;

    /// <summary>Compatibility presentation has one combined valid seed per membership switch.</summary>
    private const string LegacyInitialBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "first", 34));
        var secondStates = new Current(new RichState(false, "second", 35));
        using var first = model.AddObservableRule(firstStates, new[] { "Name" });
        using var second = model.AddObservableRule(secondStates, new[] { "Name" });
        var path = ValidationPath.Legacy("Name");
        var paths = new[] { path };
        var contexts = new ValidationSelector<Model, IValidationContext?>(owner =>
            new ValidationAccessPlan<IValidationContext?>(
                () => ValidationRead<IValidationContext?>.Present(owner.ValidationContext, paths),
                new[] { ValidationDependency.PropertyChanged(() => owner, "ValidationContext") },
                ValidationObservationOptions<IValidationContext?>.Default));
        var metadata = new ValidationSelector<Model, ValidationPath>(owner =>
            new ValidationAccessPlan<ValidationPath>(
                () => ValidationRead<ValidationPath>.Present(path, paths),
                Array.Empty<ValidationDependency>(), ValidationObservationOptions<ValidationPath>.Default,
                () => paths));
        var events = new List<string>();
        string Trace(IList<IValidationState> raw) => raw.Count == 0 ? "empty"
            : raw.Count == 1 && ReferenceEquals(raw[0], ValidationState.Valid) ? "valid"
            : string.Join("|", raw.Select(state => ((RichState)state).Code));
        using var binding = ValidationRuntime.Bind(
            ValidationRuntime.ObserveViewProperty<View, Model>(view, contexts, metadata, true,
                ValidationInitialSequence.LegacyEmpty | ValidationInitialSequence.LegacyValid),
            raw => events.Add(Trace(raw)));
        Require(events.SequenceEqual(new[] { "empty", "valid", "first|second" }), "combined legacy initial sequence: " + string.Join(",", events));
        events.Clear();
        var thirdStates = new Current(new RichState(false, "third", 36));
        using var third = model.AddObservableRule(thirdStates, new[] { "Name" });
        Require(events.SequenceEqual(new[] { "valid", "first|second|third" }), "membership valid seed is singleton, not a mixed synthetic rule list");
        events.Clear();
        view.ViewModel = null;
        Require(events.SequenceEqual(new[] { "empty" }), "null model legacy empty only");
        events.Clear();
        view.ViewModel = model;
        Require(events.SequenceEqual(new[] { "empty", "valid", "first|second|third" }), "model reattachment legacy prelude");
        var actual = new List<string>();
        using var modern = ValidationRuntime.Bind(
            ValidationRuntime.ObserveViewProperty<View, Model>(view, contexts, metadata, true, ValidationInitialSequence.Actual),
            raw => actual.Add(Trace(raw)));
        Require(actual.SequenceEqual(new[] { "first|second|third" }), "modern raw state never receives compatibility seeds");
        return true;
        """;

    /// <summary>Missing owners, available null leaves and fallback values remain distinct observations.</summary>
    private const string MissingOwnerBody = """
        var oldParent = new SourceParent { Number = 9 };
        var model = new Model { Parent = oldParent };
        var paths = new[] { ValidationPath.Legacy("Parent.Number") };
        ValidationSelector<Model, int?> Make(ValidationMissingOwnerPolicy policy, Func<int?>? fallback) =>
            new(owner => new ValidationAccessPlan<int?>(
                () =>
                {
                    var parent = owner.Parent;
                    return parent is null ? ValidationRead<int?>.Missing(paths) : ValidationRead<int?>.Present(parent.Number, paths);
                },
                new[]
                {
                    ValidationDependency.PropertyChanged(() => owner, nameof(Model.Parent)),
                    ValidationDependency.PropertyChanged(() => owner.Parent, nameof(SourceParent.Number)),
                },
                new ValidationObservationOptions<int?>(policy, fallback, EqualityComparer<int?>.Default, false),
                () => paths));
        string Render(ValidationRead<int?> read) => (read.HasOwner ? "present:" : "missing:") + (read.Value?.ToString() ?? "null");
        var defaults = new List<string>();
        var suppressed = new List<string>();
        var fallback = new List<string>();
        using var defaultBinding = ValidationRuntime.Bind(Make(ValidationMissingOwnerPolicy.DefaultValue, null).Bind(model).Observe(), read => defaults.Add(Render(read)));
        using var suppressBinding = ValidationRuntime.Bind(Make(ValidationMissingOwnerPolicy.Suppress, null).Bind(model).Observe(), read => suppressed.Add(Render(read)));
        using var fallbackBinding = ValidationRuntime.Bind(Make(ValidationMissingOwnerPolicy.Fallback, static () => 42).Bind(model).Observe(), read => fallback.Add(Render(read)));
        model.Parent = null;
        Require(defaults.SequenceEqual(new[] { "present:9", "missing:null" }), "default missing-parent projection");
        Require(suppressed.SequenceEqual(new[] { "present:9" }), "legacy missing-parent suppression");
        Require(fallback.SequenceEqual(new[] { "present:9", "missing:42" }), "typed missing-parent fallback");
        oldParent.Number = 10;
        Require(defaults.Count == 2 && suppressed.Count == 1 && fallback.Count == 2, "old parent subscriptions detached");
        model.Parent = new SourceParent { Number = null };
        Require(defaults.Last() == "present:null" && suppressed.Last() == "present:null" && fallback.Last() == "present:null", "available null leaf is not a missing parent");
        Require(defaults.Count == 3 && suppressed.Count == 2 && fallback.Count == 3, "equal null value does not erase owner presence change");
        defaultBinding.Dispose();
        suppressBinding.Dispose();
        fallbackBinding.Dispose();
        model.Parent!.Number = 11;
        Require(defaults.Count == 3 && suppressed.Count == 2 && fallback.Count == 3, "disposed source observation detached");
        return true;
        """;

    /// <summary>Supplies authored origin recording and typed current-storage assignment for peer writes.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <param name="body">The bounded peer-write assertions.</param>
    /// <returns>The compiler input.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string StorageSource(bool reactive, string body) =>
        Source(reactive, $"{ExplicitStorageSetup}{Environment.NewLine}{body}"
            .Replace("x => x.Name", "nameExpression", StringComparison.Ordinal)
            .Replace("x => x.Other", "otherExpression", StringComparison.Ordinal))
            .Replace("public static bool Check()", "[ValidationRuntimeDispatch] public static bool Check()", StringComparison.Ordinal)
            .Replace("public string MessageField", $"{ExplicitStorageMembers}{Environment.NewLine}public string MessageField", StringComparison.Ordinal);

    /// <summary>Builds a fixture with one isolated flavor and its actual namespace contracts.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <param name="body">The trusted fixture entry point.</param>
    /// <returns>The compiler input.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Source(bool reactive, string body) => FixtureTemplate
        .Replace("{{ui}}", reactive ? "ReactiveUI.Reactive" : "ReactiveUI", StringComparison.Ordinal)
        .Replace("{{binding}}", reactive ? "ReactiveUI.Binding.Reactive" : "ReactiveUI.Binding", StringComparison.Ordinal)
        .Replace("{{root}}", reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation", StringComparison.Ordinal)
        .Replace("{{body}}", body, StringComparison.Ordinal);
}
