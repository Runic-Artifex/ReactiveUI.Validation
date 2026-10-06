// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks callable overload selection against retained expression and registered contracts.</summary>
public sealed partial class BindingCapabilitiesCompilerTests
{
    /// <summary>The number of distinct binding overloads with selectors in each normal family.</summary>
    private const int SelectorBindingDefinitionCount = 24;

    /// <summary>The generated inventory for both normal selector families.</summary>
    private const string CallableAndExpressionInventoryBody = """
        {
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
        {
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
        return true;
        """;

    /// <summary>Scoped expression and callable registrations compose without selector conversion.</summary>
    private const string RegisteredCallableAndMixedBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "indirect", 48));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
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
        return true;
        """;

    /// <summary>Contravariant target storage with a deliberately incompatible derived shadow.</summary>
    private const string ContravariantTargetTypes = """
        public class BaseBindingView : ReactiveObject
        {
            public string? Status { get; set; }
        }
        public sealed class DerivedBindingView : BaseBindingView, IViewFor<Model>
        {
            public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
            object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
            public new string? Status
            {
                get => throw new InvalidOperationException("The derived target getter must not run.");
                set => throw new InvalidOperationException("The derived shadow setter must not run.");
            }
        }
        """;

    /// <summary>Original base storage must receive every current helper projection.</summary>
    private const string ContravariantTargetBody = """
        var model = new Model();
        var view = new DerivedBindingView { ViewModel = model };
        var states = new Current(new RichState(false, "base-initial", 71));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        model.Rule = rule;
        Func<BaseBindingView, string?> target = owner => owner.Status;
        using var binding = view.BindValidationState<DerivedBindingView, Model, string?>(model,
            owner => owner.Rule, target, static state => state.IsValid ? null : state.Text.ToSingleLine());
        Require(((BaseBindingView)view).Status == "base-initial", "contravariant target base initial assignment");
        states.Set(new RichState(false, "base-update", 72));
        Require(((BaseBindingView)view).Status == "base-update", "contravariant target base state update");
        view.ViewModel = null;
        Require(((BaseBindingView)view).Status is null, "contravariant target null model clear");
        view.ViewModel = model;
        Require(((BaseBindingView)view).Status == "base-update", "contravariant target current model reattachment");
        binding.Dispose();
        states.Set(new RichState(false, "detached", 73));
        Require(((BaseBindingView)view).Status == "base-update", "contravariant target source disposed");
        return true;
        """;

    /// <summary>A typed converter and explicitly selected setter provide a supported narrowing alternative.</summary>
    private const string ContravariantTypedCallbackBody = """
        var model = new Model();
        var view = new DerivedBindingView { ViewModel = model };
        var states = new Current(new RichState(false, "typed-policy", 74));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        model.Rule = rule;
        using var binding = view.BindValidationState<DerivedBindingView, Model, string?>(model,
            owner => owner.Rule, static state => state.IsValid ? null : state.Text.ToSingleLine(),
            value => ((BaseBindingView)view).Status = value);
        Require(((BaseBindingView)view).Status == "typed-policy", "explicit typed converter and base setter initial state");
        states.Set(new RichState(false, "typed-update", 75));
        Require(((BaseBindingView)view).Status == "typed-update", "explicit typed converter source update");
        view.ViewModel = null;
        Require(((BaseBindingView)view).Status is null, "explicit typed converter null model clear");
        binding.Dispose();
        view.ViewModel = model;
        Require(((BaseBindingView)view).Status is null, "explicit typed converter callback disposed");
        return true;
        """;

    /// <summary>Executes every callable binding and its original expression counterpart.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CallablePriorityPreservesEveryExpressionBindingDefinition(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, CallableAndExpressionInventoryBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        var methods = SelectedBindingMethods(result);
        var expressions = methods.Where(static method => method.Parameters.Any(static parameter => parameter.Type is INamedTypeSymbol { Name: "Expression" })).ToArray();
        var callables = methods.Where(static method => method.GetAttributes().Any(static attribute => attribute.AttributeClass?.Name == "OverloadResolutionPriorityAttribute")).ToArray();
        await Assert.That(expressions.Length).IsEqualTo(SelectorBindingDefinitionCount);
        await Assert.That(callables.Length).IsEqualTo(SelectorBindingDefinitionCount);
        var expressionDefinitions = expressions.Select(static method => method.OriginalDefinition.GetDocumentationCommentId()).Distinct(StringComparer.Ordinal).Count();
        var callableDefinitions = callables.Select(static method => method.OriginalDefinition.GetDocumentationCommentId()).Distinct(StringComparer.Ordinal).Count();
        await Assert.That(expressionDefinitions).IsEqualTo(SelectorBindingDefinitionCount);
        await Assert.That(callableDefinitions).IsEqualTo(SelectorBindingDefinitionCount);
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Executes stored callable method groups and mixed expression metadata with a callable target.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RegisteredCallableAndMixedSelectorsPreserveOwnership(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, RegisteredCallableAndMixedBody).Replace("public static bool Check()", "[ValidationRuntimeDispatch] public static bool Check()", StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Preserves the selected base setter through a contravariant stored delegate.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ContravariantStoredTargetPreservesBaseMemberDispatch(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, ContravariantTargetBody).Replace(FixtureDeclaration, $"{ContravariantTargetTypes}\n{FixtureDeclaration}", StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>A wider delegate return does not grant an inverse conversion into its actual storage.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ContravariantTargetStillRejectsUnsafeOutputNarrowing(bool reactive)
    {
        const string body = """
            var model = new Model();
            var view = new DerivedBindingView { ViewModel = model };
            Func<BaseBindingView, object?> target = owner => owner.Status;
            using var binding = view.BindValidationState<DerivedBindingView, Model, object?>(model,
                owner => owner.Rule, target, static state => (object?)state.Text.ToSingleLine());
            return true;
            """;
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, body).Replace(FixtureDeclaration, $"{ContravariantTargetTypes}\n{FixtureDeclaration}", StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == BindingDiagnosticId
            && diagnostic.GetMessage().Contains("typed conversion target", StringComparison.Ordinal))).IsTrue();
        var alternativeSource = Source(reactive, ContravariantTypedCallbackBody)
            .Replace(FixtureDeclaration, $"{ContravariantTargetTypes}\n{FixtureDeclaration}", StringComparison.Ordinal);
        var alternative = await host.RunAsync(alternativeSource);
        await Assert.That(alternative.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(alternative.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await alternative.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(alternative.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Reads compiler-selected binding symbols from the original consumer syntax.</summary>
    /// <param name="result">The final generated compilation.</param>
    /// <returns>The actual selected original methods, including generic arguments.</returns>
    private static List<IMethodSymbol> SelectedBindingMethods(CapabilityCompilation result)
    {
        var methods = new List<IMethodSymbol>();
        foreach (var tree in result.Compilation.SyntaxTrees.Where(static tree => tree.FilePath == "CapabilityCaller.cs"))
        {
            var model = result.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                if (method.ContainingType.Name is "ViewForExtensions" or "ValidationStateBindingExtensions" or "ValidationContextBindingExtensions" or "ValidationBinding")
                {
                    methods.Add(method.ReducedFrom ?? method);
                }
            }
        }

        return methods;
    }
}
