// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Binding.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Capabilities;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Formatters.Abstractions;
using ReactiveUI.Validation.Reactive.Helpers;
using ReactiveUI.Validation.Reactive.States;
using ReactiveUI.Validation.Reactive.Collections;
using ReactiveUI.Validation.Reactive.Components.Abstractions;
using ReactiveUI.Validation.Reactive.ValidationBindings;
using ReactiveUI.Validation.Reactive.ValidationBindings.Abstractions;
#else
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Capabilities;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Formatters.Abstractions;
using ReactiveUI.Validation.Helpers;
using ReactiveUI.Validation.States;
using ReactiveUI.Validation.Collections;
using ReactiveUI.Validation.Components.Abstractions;
using ReactiveUI.Validation.ValidationBindings;
using ReactiveUI.Validation.ValidationBindings.Abstractions;
#endif
using BindingCapabilityInputs;

/// <summary>Runs the common managed, trimmed and native binding capability corpus.</summary>
internal static class BindingCapabilityScenario
{
    /// <summary>Shared immutable values for the name properties.</summary>
    private static readonly string[] NameProperties = ["Name"];

    /// <summary>Shared immutable values for the other properties.</summary>
    private static readonly string[] OtherProperties = ["Other"];

    /// <summary>Shared immutable values for the name field properties.</summary>
    private static readonly string[] NameFieldProperties = ["NameField"];

    /// <summary>Shared immutable values for the combined properties.</summary>
    private static readonly string[] CombinedProperties = ["Name", "Other"];

    /// <summary>Shared immutable values for the legacy initial trace.</summary>
    private static readonly string[] LegacyInitialTrace = ["empty", "valid", "first|second"];

    /// <summary>Shared immutable values for the legacy membership trace.</summary>
    private static readonly string[] LegacyMembershipTrace = ["valid", "first|second|third"];

    /// <summary>Shared immutable values for the legacy missing trace.</summary>
    private static readonly string[] LegacyMissingTrace = ["empty"];

    /// <summary>Shared immutable values for the legacy reattachment trace.</summary>
    private static readonly string[] LegacyReattachmentTrace = ["empty", "valid", "first|second|third"];

    /// <summary>Shared immutable values for the actual membership trace.</summary>
    private static readonly string[] ActualMembershipTrace = ["first|second|third"];

    /// <summary>Shared immutable values for the default missing trace.</summary>
    private static readonly string[] DefaultMissingTrace = ["present:9", "missing:null"];

    /// <summary>Shared immutable values for the suppressed missing trace.</summary>
    private static readonly string[] SuppressedMissingTrace = ["present:9"];

    /// <summary>Shared immutable values for the fallback missing trace.</summary>
    private static readonly string[] FallbackMissingTrace = ["present:9", "missing:42"];

    /// <summary>Executes the same bounded typed dispatch and ownership assertions in every build mode.</summary>
    internal static void Run()
    {
        StaticInventory();
        ExtensionInventory();
        RawFormatterIdentity();
        DirectAssignment();
        NestedReferenceReplay();
        DynamicTargetSlot();
        StoredPlans();
        LegacySequence();
        MissingOwnerPolicies();
        NullableStoragePolicy();
        RegisteredCallable();
        PrivateModelAccess();
        NullableOperatorStorage();
        SemanticStorageWithTypedAssignment();
        SameSlotPeerOrigins();
        IndependentFieldOrigins();
        ReentrantExternalOrigin();
        NestedGenericPrivateTarget();
        AllowNullSetterInputs();
        DisallowNullTypedPolicy();
        NullableValueInputPolicy();
    }

    /// <summary>Checks static inventory through actual package dispatch.</summary>
    private static void StaticInventory()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "name-required", 41);
        var states = new Current(first);
        using var rule = model.AddObservableRule(states, NameProperties);
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
            ValidationBinding.ForProperty<View, Model, string, string>(view, (Expression<Func<Model, string>>)(x => x.Name), x => x.Message),
            ValidationBinding.ForProperty<View, Model, string, string>(view, (Expression<Func<Model, string>>)(x => x.Name), x => x.Message, null),
            ValidationBinding.ForProperty<View, Model, string, string>(view, (Expression<Func<Model, string>>)(x => x.Name), x => x.Message, null, false),
            ValidationBinding.ForProperty<View, Model, string, Output?>(view, (Expression<Func<Model, string>>)(x => x.Name), static (raw, formatted) => { }, formatter),
            ValidationBinding.ForProperty<View, Model, string, Output?>(view, (Expression<Func<Model, string>>)(x => x.Name), static (raw, formatted) => { }, formatter, false),
            ValidationBinding.ForValidationHelperProperty<View, Model, string>(view, (Expression<Func<Model?, ValidationHelper?>>)(x => x!.Rule), x => x.Message),
            ValidationBinding.ForValidationHelperProperty<View, Model, string>(view, (Expression<Func<Model?, ValidationHelper?>>)(x => x!.Rule), x => x.Message, null),
            ValidationBinding.ForValidationHelperProperty<View, Model, Output?>(view, (Expression<Func<Model?, ValidationHelper?>>)(x => x!.Rule), static (raw, formatted) => { }, formatter),
            ValidationBinding.ForViewModel<View, Model, string>(view, (Expression<Func<View, string>>)(x => x.Message)),
            ValidationBinding.ForViewModel<View, Model, string>(view, (Expression<Func<View, string>>)(x => x.Message), null),
        };
        Require(view.Message == "name-required", "static target initial state");
        Require(formatter.Inputs.Count >= 4, "custom formatter callbacks");
        foreach (var binding in bindings) binding.Dispose();
    }

    /// <summary>Checks extension inventory through actual package dispatch.</summary>
    private static void ExtensionInventory()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "inventory", 42);
        var states = new Current(first);
        using var rule = model.AddObservableRule(states, NameProperties);
        model.Rule = rule;
        var helperCalls = new List<IValidationState>();
        var propertyCalls = new List<IList<IValidationState>>();
        var contextCalls = new List<IValidationState>();
        var contextPropertyCalls = new List<IList<IValidationState>>();
        var expressionHelperCalls = new List<IValidationState>();
        var expressionPropertyCalls = new List<IList<IValidationState>>();
        var expressionContextCalls = new List<IValidationState>();
        var expressionContextPropertyCalls = new List<IList<IValidationState>>();
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
            view.BindValidation(model, (Expression<Func<Model, string>>)(x => x.Name), x => x.Message),
            view.BindValidation(model, (Expression<Func<Model, string>>)(x => x.Name), x => x.Message, null),
            view.BindValidation(model, (Expression<Func<View, string>>)(x => x.Message)),
            view.BindValidation(model, (Expression<Func<View, string>>)(x => x.Message), null),
            view.BindValidation(model, (Expression<Func<Model?, ValidationHelper?>>)(x => x!.Rule), x => x.Message),
            view.BindValidation(model, (Expression<Func<Model?, ValidationHelper?>>)(x => x!.Rule), x => x.Message, null),
            view.BindValidationState(model, (Expression<Func<Model, ValidationHelper?>>)(x => x.Rule), x => x.Presentation,
                static state => state.IsValid ? (Output?)null : new Output(state.Text.ToSingleLine())),
            view.BindValidationState(model, (Expression<Func<Model, ValidationHelper?>>)(x => x.Rule),
                state => { expressionHelperCalls.Add(state); return state; }, static state => { }),
            view.BindValidationState(model, (Expression<Func<Model, string>>)(x => x.Name), x => x.Presentation,
                static raw => raw.Count == 0 ? (Output?)null : new Output(raw[0].Text.ToSingleLine()), true),
            view.BindValidationState(model, (Expression<Func<Model, string>>)(x => x.Name),
                raw => { expressionPropertyCalls.Add(raw); return raw; }, static raw => { }, true),
            view.BindValidationContext(model, (Expression<Func<Model, IValidationContext?>>)(x => x.ValidationContext), x => x.Message),
            view.BindValidationContext(model, (Expression<Func<Model, IValidationContext?>>)(x => x.ValidationContext), x => x.Name, x => x.Message),
            view.BindValidationContext(model, (Expression<Func<Model, IValidationContext?>>)(x => x.ValidationContext), expressionContextCalls.Add),
            view.BindValidationContext(model, (Expression<Func<Model, IValidationContext?>>)(x => x.ValidationContext), x => x.Name, expressionContextPropertyCalls.Add),
        };
        Require(view.Message == "inventory" && view.Presentation?.Text == "inventory", "typed actual initial target values");
        Require(helperCalls.Count == 1 && ReferenceEquals(helperCalls[0], first), "initial complete helper state");
        Require(propertyCalls.Count == 1 && ReferenceEquals(propertyCalls[0][0], first), "initial matching property state");
        Require(contextCalls.Count == 1 && contextPropertyCalls.Count == 1 && ReferenceEquals(contextPropertyCalls[0][0], first), "selected context callbacks");
        Require(expressionHelperCalls.Count == 1 && ReferenceEquals(expressionHelperCalls[0], first), "expression initial complete helper state");
        Require(expressionPropertyCalls.Count == 1 && ReferenceEquals(expressionPropertyCalls[0][0], first), "expression initial matching property state");
        Require(expressionContextCalls.Count == 1 && expressionContextPropertyCalls.Count == 1
            && ReferenceEquals(expressionContextPropertyCalls[0][0], first), "expression selected context callbacks");
        states.Set(new RichState(false, "later", 43));
        Require(view.Message == "later" && view.Presentation?.Text == "later", "typed target updates");
        view.ViewModel = null;
        Require(view.Message is { Length: 0 } && view.Presentation is null && helperCalls.Last().IsValid && propertyCalls.Last().Count == 0, "missing model clear and raw projections");
        foreach (var binding in bindings) binding.Dispose();
        var count = helperCalls.Count;
        var expressionCount = expressionHelperCalls.Count;
        view.ViewModel = model;
        states.Set(new RichState(false, "disposed", 44));
        Require(helperCalls.Count == count && view.Message is { Length: 0 }, "all extension binding lifetimes detached");
        Require(expressionHelperCalls.Count == expressionCount, "all expression extension binding lifetimes detached");
    }

    /// <summary>Checks raw formatter identity through actual package dispatch.</summary>
    private static void RawFormatterIdentity()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "first", 7);
        IValidationState second = new RichState(false, "second", 8);
        var firstStates = new Current(first);
        var secondStates = new Current(second);
        using var firstRule = model.AddObservableRule(firstStates, NameProperties);
        using var secondRule = model.AddObservableRule(secondStates, CombinedProperties);
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
    }

    /// <summary>Checks direct assignment through actual package dispatch.</summary>
    private static void DirectAssignment()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, " initial ", 56));
        using var rule = model.AddObservableRule(states, NameProperties);
        using var setterOnly = view.BindValidation(model, x => x.Name, x => x.SetterOnly);
        using var normalized = view.BindValidation(model, x => x.Name, x => x.NormalizedMessage);
        using var converted = view.BindValidation(model, x => x.Name, x => x.Converted);
        Require(view.Converted.Text == " initial " && ConvertedOutput.Conversions == 1, "user-defined conversion executes once per assignment");
        Require(view.OtherMessage == " initial " && view.Message == "initial" && view.NormalizedWrites == 1, "setter-only access and normalization without reconciliation loop");
        states.Set(new RichState(false, " updated ", 57));
        Require(view.Converted.Text == " updated " && ConvertedOutput.Conversions == 2, "next assignment converts once without reconciliation conversion");
        Require(view.OtherMessage == " updated " && view.Message == "updated" && view.NormalizedWrites == 2, "new source output assigned once to normalized setter");
    }

    /// <summary>Checks nested reference replay through actual package dispatch.</summary>
    private static void NestedReferenceReplay()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var first = view.Panel!;
        var states = new Current(new RichState(false, "initial", 53));
        using var rule = model.AddObservableRule(states, NameProperties);
        using var binding = view.BindValidation(model, x => x.Name, x => x.Panel!.Message);
        Require(first.Message == "initial", "initial nested target");
        var second = new BindingCapabilityInputs.Panel();
        view.Panel = second;
        Require(second.Message == "initial", "equal-by-value replacement replays by reference identity");
        view.Panel = null;
        states.Set(new RichState(false, "cached", 54));
        Require(first.Message == "initial" && second.Message == "initial", "missing target caches and detaches old owners");
        var third = new BindingCapabilityInputs.Panel();
        view.Panel = third;
        Require(third.Message == "cached", "restored target receives latest cached projection");
        binding.Dispose();
        view.Panel = new BindingCapabilityInputs.Panel();
        states.Set(new RichState(false, "disposed", 55));
        Require(view.Panel!.Message is { Length: 0 } && third.Message == "cached", "disposed nested target no replay");
    }

    /// <summary>Checks dynamic target slot through actual package dispatch.</summary>
    private static void DynamicTargetSlot()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "cached", 25));
        using var rule = model.AddObservableRule(states, NameProperties);
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
    }

    /// <summary>Checks stored plans through actual package dispatch.</summary>
    private static void StoredPlans()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "registered", 45));
        using var firstRule = model.AddObservableRule(firstStates, OtherProperties);
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
        var argumentEvaluations = new EvaluationCounter();
        Expression<Func<View, string>> GetTarget() { argumentEvaluations.Increment(); return target; }
        [ValidationRuntimeDispatch]
        IDisposable BindFactory() => view.BindValidation(model, property, GetTarget());
        using var factoryBinding = BindFactory();
        Require(argumentEvaluations.Count == 1 && model.ForbiddenReads == 0 && view.Message == "catalog:registered", "one expression evaluation and metadata-only registered dispatch");
        factoryBinding.Dispose();
        using var binding = view.BindValidation(model, property, target);
        view.Index = 1;
        Require(view.OtherMessage == "catalog:registered" && view.Writes1 == 1, "registered current target key replay");
        var secondModel = new Model();
        var secondStates = new Current(new RichState(false, "replacement", 46));
        using var secondRule = secondModel.AddObservableRule(secondStates, OtherProperties);
        view.ViewModel = secondModel;
        Require(view.OtherMessage == "catalog:replacement" && secondModel.ForbiddenReads == 0, "registered descriptor rebound to current model");
        firstStates.Set(new RichState(false, "detached", 47));
        Require(view.OtherMessage == "catalog:replacement", "old model state source detached");
        binding.Dispose();
        var writes = view.Writes0;
        view.Index = 0;
        Require(view.Writes0 == writes, "stored target dependency detached");
    }

    /// <summary>Checks legacy sequence through actual package dispatch.</summary>
    private static void LegacySequence()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "first", 34));
        var secondStates = new Current(new RichState(false, "second", 35));
        using var first = model.AddObservableRule(firstStates, NameProperties);
        using var second = model.AddObservableRule(secondStates, NameProperties);
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
        Require(events.SequenceEqual(LegacyInitialTrace), "combined legacy initial sequence: " + string.Join(",", events));
        events.Clear();
        var thirdStates = new Current(new RichState(false, "third", 36));
        using var third = model.AddObservableRule(thirdStates, NameProperties);
        Require(events.SequenceEqual(LegacyMembershipTrace), "membership valid seed is singleton, not a mixed synthetic rule list");
        events.Clear();
        view.ViewModel = null;
        Require(events.SequenceEqual(LegacyMissingTrace), "null model legacy empty only");
        events.Clear();
        view.ViewModel = model;
        Require(events.SequenceEqual(LegacyReattachmentTrace), "model reattachment legacy prelude");
        var actual = new List<string>();
        using var modern = ValidationRuntime.Bind(
            ValidationRuntime.ObserveViewProperty<View, Model>(view, contexts, metadata, true, ValidationInitialSequence.Actual),
            raw => actual.Add(Trace(raw)));
        Require(actual.SequenceEqual(ActualMembershipTrace), "modern raw state never receives compatibility seeds");
    }

    /// <summary>Checks missing owner policies through actual package dispatch.</summary>
    private static void MissingOwnerPolicies()
    {
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
        Require(defaults.SequenceEqual(DefaultMissingTrace), "default missing-parent projection");
        Require(suppressed.SequenceEqual(SuppressedMissingTrace), "legacy missing-parent suppression");
        Require(fallback.SequenceEqual(FallbackMissingTrace), "typed missing-parent fallback");
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
    }

    /// <summary>Checks nullable storage policy through actual package dispatch.</summary>
    private static void NullableStoragePolicy()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "nullable", 64));
        using var rule = model.AddObservableRule(states, NameProperties);
        model.Rule = rule;
        Expression<Func<View, string?>> targetExpression = owner => owner.Message;
        var target = new ValidationTarget<View, string?>(owner =>
            new ValidationWritePlan<string?>(() => ValidationTargetAccess<string?>.Present(owner,
                value => owner.Message = value ?? "explicit-fallback"), Array.Empty<ValidationDependency>()));
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var targetRegistration = registry.RegisterTarget<View, string?, string?>(ValidationPlanRole.Target, targetExpression, target);
        using var nullable = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
            x => x.NullableMessage, static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var nonNullableProjection = view.BindValidation(model, x => x.Name, x => x.NullableMessage);
        Expression<Func<Model, ValidationHelper?>> helperExpression = owner => owner.Rule;
        var helperPlan = new ValidationSelector<Model, ValidationHelper?>(owner =>
            new ValidationAccessPlan<ValidationHelper?>(() => ValidationRead<ValidationHelper?>.Present(owner.Rule, Array.Empty<ValidationPath>()),
                new[] { ValidationDependency.PropertyChanged(() => owner, nameof(Model.Rule)) },
                new ValidationObservationOptions<ValidationHelper?>(ValidationMissingOwnerPolicy.DefaultValue,
                    null, ReferenceEqualityComparer.Instance, false)));
        using var helperRegistration = registry.RegisterSelector(ValidationPlanRole.Helper, helperExpression, helperPlan);
        [ValidationRuntimeDispatch]
        IValidationBinding BindPolicy() => view.BindValidationState<View, Model, string?>(model, helperExpression,
            targetExpression, static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var policy = BindPolicy();
        Require(view.Message == "nullable" && view.NullableMessage == "nullable", "nullable and widened actual target contracts");
        model.Rule = null;
        Require(view.Message == "explicit-fallback" && view.NullableMessage is null, "typed target owns explicit nullable narrowing policy");
    }

    /// <summary>Checks registered callable through actual package dispatch.</summary>
    [ValidationRuntimeDispatch]
    private static void RegisteredCallable()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "indirect", 48));
        using var rule = model.AddObservableRule(states, NameProperties);
        Expression<Func<Model, string>> property = owner => owner.Name;
        Expression<Func<View, string>> target = owner => owner.Message;
        var paths = new[] { ValidationPath.Legacy("Name") };
        var selector = new ValidationSelector<Model, string>(owner =>
            new ValidationAccessPlan<string>(() => throw new InvalidOperationException("Property dispatch must use its separate metadata reader."),
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
        Func<Model, string> callableProperty = owner => owner.Name;
        Func<View, string> callableTarget = owner => owner.Message;
        using var callablePropertyRegistration = registry.RegisterDelegateSelector(ValidationPlanRole.Property, callableProperty, selector);
        using var callableTargetRegistration = registry.RegisterDelegateTarget<View, string, string>(ValidationPlanRole.Target, callableTarget, targetPlan);
        Func<View, Func<Model, string>, Func<View, string>, IValidationBinding> callableFactory =
            ValidationBinding.ForProperty<View, Model, string, string>;
        using var callableBinding = callableFactory(view, callableProperty, callableTarget);
        Require(view.Message == "disposed", "registered callable method group initial state");
        states.Set(new RichState(false, "callable", 51));
        Require(view.Message == "callable", "registered callable method group update");
        callableBinding.Dispose();
        states.Set(new RichState(false, "detached", 52));
        Require(view.Message == "callable", "registered callable method group disposal");
        Expression<Func<Model, string>> mixedProperty = owner => owner.MetadataOnly;
        using var mixedRegistration = registry.RegisterSelector(ValidationPlanRole.Property, mixedProperty, selector);
        var metadata = ValidationRuntime.ResolvePathSelector(model, view, mixedProperty, ValidationPlanRole.Property, string.Empty);
        var callableWrite = ValidationRuntime.ResolveTarget<View, string, string>(view, callableTarget, string.Empty);
        using var mixed = callableWrite.Bind(view).Bind(ValidationRuntime.Project(
            ValidationRuntime.ObserveViewProperty<View, Model>(view, ValidationRuntime.DefaultContext<Model>(view), metadata, true, ValidationInitialSequence.Actual),
            static raw => raw.Count == 0 ? string.Empty : raw[0].Text.ToSingleLine()));
        Require(view.Message == "detached" && model.ForbiddenReads == 0, "mixed expression metadata and callable target");
        view.ViewModel = null;
        Require(view.Message is { Length: 0 }, "mixed typed plan model handoff clear");
        view.ViewModel = model;
        Require(view.Message == "detached" && model.ForbiddenReads == 0, "mixed typed plan current model reattachment");
        mixed.Dispose();
        states.Set(new RichState(false, "mixed-disposed", 53));
        Require(view.Message == "detached", "mixed typed plan lifetime detached");
    }

    /// <summary>Checks private model access through actual package dispatch.</summary>
    private static void PrivateModelAccess()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        IValidationState first = new RichState(false, "private", 61);
        var states = new Current(first);
        using var rule = model.AddObservableRule(states, NameProperties);
        model.SetPrivateRule(rule);
        var observed = new List<IValidationState>();
        using var binding = model.BindPrivateHelper(view, observed.Add);
        Require(observed.Count == 1 && ReferenceEquals(observed[0], first), "private helper accessor preserves complete initial state");
        IValidationState next = new RichState(false, "next", 62);
        states.Set(next);
        Require(ReferenceEquals(observed.Last(), next), "private helper state forwarding");
        model.SetPrivateRule(null);
        Require(observed.Last().IsValid, "private helper replacement clears");
        var count = observed.Count;
        states.Set(new RichState(false, "detached", 63));
        Require(observed.Count == count, "private helper old source detached");
        binding.Dispose();
        model.SetPrivateRule(rule);
        Require(observed.Count == count, "private source notification detached on dispose");
    }

    /// <summary>Checks nullable operator storage through actual package dispatch.</summary>
    private static void NullableOperatorStorage()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "operator", 65));
        using var rule = model.AddObservableRule(states, NameProperties);
        using var binding = view.BindValidation(model, x => x.Name, x => x.NullableConverted);
        Require(view.NullableConverted?.Text == "operator", "nullable conversion result assigned to nullable storage");
        view.ViewModel = null;
        Require(view.NullableConverted is null, "null conversion result clears nullable target");
    }

    /// <summary>Checks semantic storage with typed assignment through actual package dispatch.</summary>
    private static void SemanticStorageWithTypedAssignment()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "semantic", 17));
        using var rule = model.AddObservableRule(states, NameFieldProperties);
        using var field = view.BindValidation(model, x => x.NameField, x => x.MessageField);
        using var index = ValidationBinding.ForProperty<View, Model, string, string>(view, x => x.NameField, x => x[1]);
        Expression<Func<View, object>> expression = owner => owner.Message;
        var target = new ValidationTarget<View, string>(owner => new ValidationWritePlan<string>(
            () => ValidationTargetAccess<string>.Present(owner, value => owner.Message = value), Array.Empty<ValidationDependency>()));
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var registration = registry.RegisterTarget<View, object, string>(ValidationPlanRole.Target, expression, target);
        using var converted = view.BindValidation(model, x => x.NameField, expression);
        Require(view.MessageField == "semantic" && view.OtherMessage == "semantic" && view.Message == "semantic", "field/index/conversion initial assignment");
        states.Set(new RichState(false, "updated", 18));
        Require(view.MessageField == "updated" && view.OtherMessage == "updated" && view.Message == "updated", "field/index/conversion updates");
        field.Dispose();
        index.Dispose();
        converted.Dispose();
        states.Set(new RichState(false, "disposed", 19));
        Require(view.MessageField == "updated" && view.OtherMessage == "updated" && view.Message == "updated", "semantic target disposal");
    }

    /// <summary>Checks same slot peer origins through actual package dispatch.</summary>
    [ValidationRuntimeDispatch]
    private static void SameSlotPeerOrigins()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "first", 66));
        var secondStates = new Current(new RichState(false, "second", 67));
        using var firstRule = model.AddObservableRule(firstStates, NameProperties);
        using var secondRule = model.AddObservableRule(secondStates, OtherProperties);
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
        using var first = view.BindValidation(model, nameExpression, errorExpression);
        using var second = view.BindValidation(model, otherExpression, errorExpression);
        Require(view.OwnedState.Error == "second" && view.OwnedState.Kept == 7 && view.StateAssignments == 2, "same-slot initial last writer with no peer replay");
        firstStates.Set(new RichState(false, "next-first", 68));
        Require(view.OwnedState.Error == "next-first" && view.StateAssignments == 3, "source update is the next writer");
        view.OnStateWrite = () => secondStates.Set(new RichState(false, "peer-last", 69));
        firstStates.Set(new RichState(false, "peer-first", 70));
        Require(view.OwnedState.Error == "peer-last" && view.StateAssignments == 5, "reentrant peer update wins without ping-pong");
        view.OwnedState = new WritableState("external", 99);
        Require(view.OwnedState.Error == "peer-last" && view.OwnedState.Kept == 99 && view.StateAssignments == 8, "external epoch replays each peer once in deterministic order");
        first.Dispose();
        second.Dispose();
        view.OwnedState = new WritableState("disposed", 100);
        Require(view.OwnedState.Error == "disposed" && view.StateAssignments == 9, "disposed policy observation detached");
    }

    /// <summary>Checks independent field origins through actual package dispatch.</summary>
    [ValidationRuntimeDispatch]
    private static void IndependentFieldOrigins()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "first", 66));
        var secondStates = new Current(new RichState(false, "second", 67));
        using var firstRule = model.AddObservableRule(firstStates, NameProperties);
        using var secondRule = model.AddObservableRule(secondStates, OtherProperties);
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
        using var first = view.BindValidation(model, nameExpression, errorExpression);
        using var second = view.BindValidationState(model, otherExpression, keptExpression,
            static raw => raw[0].Text.ToSingleLine().Length, true);
        Require(view.OwnedState.Error == "first" && view.OwnedState.Kept == 6 && view.StateAssignments == 2, "independent struct fields preserve peer storage");
        firstStates.Set(new RichState(false, "next-first", 71));
        secondStates.Set(new RichState(false, "long-second", 72));
        Require(view.OwnedState.Error == "next-first" && view.OwnedState.Kept == 11 && view.StateAssignments == 4, "fresh storage writes preserve other field");
        view.OwnedState = new WritableState("external", 101);
        Require(view.OwnedState.Error == "next-first" && view.OwnedState.Kept == 11 && view.StateAssignments == 7, "one cached replay per field after external replacement");
        view.OnStateWrite = () => view.OwnedState = new WritableState("foreign", 202);
        firstStates.Set(new RichState(false, "epoch-write", 73));
        Require(view.OwnedState.Error == "epoch-write" && view.OwnedState.Kept == 11 && view.StateAssignments == 11, "foreign reentrant epoch restores current projections without loops");
    }

    /// <summary>Checks reentrant external origin through actual package dispatch.</summary>
    [ValidationRuntimeDispatch]
    private static void ReentrantExternalOrigin()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var firstStates = new Current(new RichState(false, "first", 66));
        var secondStates = new Current(new RichState(false, "second", 67));
        using var firstRule = model.AddObservableRule(firstStates, NameProperties);
        using var secondRule = model.AddObservableRule(secondStates, OtherProperties);
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
        view.OnStateWrite = () => view.OwnedState = new WritableState("external", 101);
        using var binding = view.BindValidation(model, nameExpression, errorExpression);
        Require(view.OwnedState.Error == "first" && view.OwnedState.Kept == 101 && view.StateAssignments == 3, "declared origin policy reconciles reentrant external storage replacement");
        firstStates.Set(new RichState(false, "updated", 74));
        Require(view.OwnedState.Error == "updated" && view.OwnedState.Kept == 101 && view.StateAssignments == 4, "declared target acquires fresh storage");
        binding.Dispose();
        view.OwnedState = new WritableState("after-disposal", 100);
        Require(view.OwnedState.Error == "after-disposal" && view.StateAssignments == 5, "disposed declared struct target has no replay");
    }

    /// <summary>Checks nested generic private target through actual package dispatch.</summary>
    private static void NestedGenericPrivateTarget()
    {
        var model = new Model();
        var view = new Outer<string>.Inner<int> { ViewModel = model };
        var states = new Current(new RichState(false, "hosted", 51));
        using var rule = model.AddObservableRule(states, NameProperties);
        using var binding = view.Bind(model);
        Require(view.Result == "hosted", "open generic hosted private initial assignment");
        states.Set(new RichState(false, "updated", 52));
        Require(view.Result == "updated", "open generic hosted update");
        view.ViewModel = null;
        Require(view.Result is { Length: 0 }, "open generic hosted missing model clear");
        binding.Dispose();
        view.ViewModel = model;
        Require(view.Result is { Length: 0 }, "open generic hosted disposal");
    }

    /// <summary>Preserves independent getter and AllowNull setter contracts.</summary>
    private static void AllowNullSetterInputs()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "contract", 81));
        using var rule = model.AddObservableRule(states, NameProperties);
        model.Rule = rule;
        using var property = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
            x => x.AllowProperty, static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var parameter = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
            x => x.AllowParameter, static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var field = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
            x => x.AllowField, static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var indexer = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
            x => x[true], static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var indexerParameter = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
            x => x[1m], static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var privateSetter = view.BindPrivateAllow(model);
        Require(view.AllowProperty == "contract" && view.AllowParameter == "contract"
            && view.AllowField == "contract" && view.ParameterIndexerResult == "contract"
            && view.PrivateAllowResult == "contract", "AllowNull receives projected values");
        states.Set(new RichState(true, "", 82));
        string getterContract = view.AllowProperty;
        string parameterGetterContract = view.AllowParameter;
        Require(getterContract.Length == 0 && parameterGetterContract.Length == 0
            && view.AllowField is null && view.ParameterIndexerResult is { Length: 0 }
            && view.PrivateAllowResult is { Length: 0 }, "AllowNull receives null without changing getter annotations");
    }

    /// <summary>Applies a typed null policy and nonnull text to DisallowNull inputs.</summary>
    private static void DisallowNullTypedPolicy()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "contract", 83));
        using var rule = model.AddObservableRule(states, NameProperties);
        model.Rule = rule;
        Expression<Func<View, string?>> expression = owner => owner.DisallowProperty;
        var target = new ValidationTarget<View, string?>(owner =>
            new ValidationWritePlan<string?>(() => ValidationTargetAccess<string?>.Present(owner,
                value => owner.DisallowProperty = value ?? "explicit-fallback"), Array.Empty<ValidationDependency>()));
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var registration = registry.RegisterTarget<View, string?, string?>(ValidationPlanRole.Target, expression, target);
        Expression<Func<Model, ValidationHelper?>> helperExpression = owner => owner.Rule;
        var helperPlan = new ValidationSelector<Model, ValidationHelper?>(owner =>
            new ValidationAccessPlan<ValidationHelper?>(() => ValidationRead<ValidationHelper?>.Present(owner.Rule, Array.Empty<ValidationPath>()),
                new[] { ValidationDependency.PropertyChanged(() => owner, nameof(Model.Rule)) },
                new ValidationObservationOptions<ValidationHelper?>(ValidationMissingOwnerPolicy.DefaultValue,
                    null, ReferenceEqualityComparer.Instance, false)));
        using var helperRegistration = registry.RegisterSelector(ValidationPlanRole.Helper, helperExpression, helperPlan);
        [ValidationRuntimeDispatch]
        IValidationBinding BindPolicy() => view.BindValidationState<View, Model, string?>(model, helperExpression,
            expression, static state => state.IsValid ? null : state.Text.ToSingleLine());
        using var policy = BindPolicy();
        using var parameter = view.BindValidation(model, x => x.Name, x => x.DisallowParameter);
        using var field = view.BindValidation(model, x => x.Name, x => x.DisallowField);
        using var indexer = view.BindValidation(model, x => x.Name, x => x["error"]);
        Require(view.DisallowProperty == "contract" && view.DisallowParameter == "contract"
            && view.DisallowField == "contract", "DisallowNull receives nonnull projections");
        states.Set(new RichState(true, "", 84));
        Require(view.DisallowProperty == "explicit-fallback" && view.DisallowParameter is { Length: 0 }
            && view.DisallowField is { Length: 0 }, "typed target applies the explicit null policy");
    }

    /// <summary>Preserves CLR nullable value getters while typed policies forbid null writes.</summary>
    private static void NullableValueInputPolicy()
    {
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "contract", 85));
        using var rule = model.AddObservableRule(states, NameProperties);
        model.Rule = rule;
        Expression<Func<Model, ValidationHelper?>> helperExpression = owner => owner.Rule;
        var helper = new ValidationSelector<Model, ValidationHelper?>(owner =>
            new ValidationAccessPlan<ValidationHelper?>(() => ValidationRead<ValidationHelper?>.Present(owner.Rule, Array.Empty<ValidationPath>()),
                new[] { ValidationDependency.PropertyChanged(() => owner, nameof(Model.Rule)) },
                new ValidationObservationOptions<ValidationHelper?>(ValidationMissingOwnerPolicy.DefaultValue,
                    null, ReferenceEqualityComparer.Instance, false)));
        Expression<Func<View, int?>> numberExpression = owner => owner.RequiredNumber;
        var payloadExpression = View.PayloadExpression();
        var numberTarget = new ValidationTarget<View, int?>(owner =>
            new ValidationWritePlan<int?>(() => ValidationTargetAccess<int?>.Present(owner,
                value => owner.RequiredNumber = value ?? 0), Array.Empty<ValidationDependency>()));
        var payloadTarget = new ValidationTarget<View, View.ValuePayload?>(owner =>
            new ValidationWritePlan<View.ValuePayload?>(() => ValidationTargetAccess<View.ValuePayload?>.Present(owner,
                value => owner.SetPayload(value ?? new View.ValuePayload(0))), Array.Empty<ValidationDependency>()));
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var helperRegistration = registry.RegisterSelector(ValidationPlanRole.Helper, helperExpression, helper);
        using var numberRegistration = registry.RegisterTarget<View, int?, int?>(ValidationPlanRole.Target, numberExpression, numberTarget);
        using var payloadRegistration = registry.RegisterTarget<View, View.ValuePayload?, View.ValuePayload?>(ValidationPlanRole.Target, payloadExpression, payloadTarget);
        [ValidationRuntimeDispatch]
        IValidationBinding BindNumber() => view.BindValidationState<View, Model, int?>(model, helperExpression,
            numberExpression, static state => state.IsValid ? null : state.Text.ToSingleLine().Length);
        [ValidationRuntimeDispatch]
        IValidationBinding BindPayload() => view.BindValidationState<View, Model, View.ValuePayload?>(model, helperExpression,
            payloadExpression, static state => state.IsValid ? null : new View.ValuePayload(state.Text.ToSingleLine().Length));
        using var number = BindNumber();
        using var payload = BindPayload();
        Require(view.RequiredNumber == 8 && view.PayloadResult?.Number == 8, "typed nullable-value policies receive current projections");
        states.Set(new RichState(true, "", 86));
        int? numberGetter = view.RequiredNumber;
        View.ValuePayload? payloadGetter = view.PayloadResult;
        Require(numberGetter == 0 && payloadGetter?.Number == 0, "DisallowNull value setters receive nonnull policy outputs with nullable getters preserved");
        model.Rule = null;
        Require(view.RequiredNumber == 0 && view.PayloadResult?.Number == 0, "missing helper clears through the declared nonnull value policy");
    }

    /// <summary>Stops the shared corpus on a concrete capability or ownership regression.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
