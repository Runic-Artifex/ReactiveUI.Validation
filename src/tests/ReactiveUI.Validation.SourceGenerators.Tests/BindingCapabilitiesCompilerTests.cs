// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Exercises expanded bindings through the real semantic compiler and runtime ownership contracts.</summary>
public sealed partial class BindingCapabilitiesCompilerTests
{
    /// <summary>Compiles and executes every static factory signature in each runtime flavor.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EveryStaticFactoryHasWarningFreeTypedDispatch(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, StaticOverloadsBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Compiles and executes all fourteen expression extension signatures in each flavor.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EveryExtensionHasWarningFreeTypedDispatch(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, ExtensionOverloadsBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Executes stored property and target expressions through scoped typed registration.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StoredSelectorsUseCurrentTypedRegistration(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, StoredSelectorsBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Executes a method group with the explicit typed runtime dispatch contract.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RegisteredMethodGroupKeepsOriginalGenericArity(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, IndirectCallBody).Replace("public static bool Check()", "[ValidationRuntimeDispatch] public static bool Check()", StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Rejects an output conversion that cannot assign the real storage member.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InvalidStorageConversionRequiresExplicitTypedLens(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            using var binding = view.BindValidationState<View, Model, object>(model, x => x.Rule,
                x => x.Message, static state => (object)state.Text.ToSingleLine());
            return true;
            """));
        await Assert.That(result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == BindingDiagnosticId
            && diagnostic.GetMessage().Contains("typed conversion target", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.ValidationSources.Any(static source => source.Text.Contains("target.Bind", StringComparison.Ordinal))).IsFalse();
    }

    /// <summary>Direct target binding does not read selected getters or retry legal normalized writes.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DirectTargetOnlyExecutesSetterAndAllowsNormalization(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, DirectSetterBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Executes nested target replacement, missing-parent cache and reference identity handoff.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NestedTargetIdentityCachesAndReplaysLatestOutput(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, NestedTargetBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Open outer and inner generic slots preserve private legal access in a partial host.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NestedOpenGenericViewHostsPrivateTypedAssignment(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, """
            var model = new Model();
            var view = new Outer<string>.Inner<int> { ViewModel = model };
            var states = new Current(new RichState(false, "hosted", 51));
            using var rule = model.AddObservableRule(states, new[] { "Name" });
            using var binding = view.Bind(model);
            Require(view.Result == "hosted", "open generic hosted private initial assignment");
            states.Set(new RichState(false, "updated", 52));
            Require(view.Result == "updated", "open generic hosted update");
            view.ViewModel = null;
            Require(view.Result == "", "open generic hosted missing model clear");
            binding.Dispose();
            view.ViewModel = model;
            Require(view.Result == "", "open generic hosted disposal");
            return true;
            """).Replace(FixtureDeclaration, HostedView + Environment.NewLine + FixtureDeclaration, StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>A private nested index key read through an erased lexical bridge is restored to its declared type.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OpenGenericViewPrivateIndexKeyRetainsDeclaredType(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, """
            var model = new Model();
            var view = new KeyedView<string> { ViewModel = model };
            var states = new Current(new RichState(false, "keyed", 61));
            using var rule = model.AddObservableRule(states, new[] { "Name" });
            model.Rule = rule;
            using var binding = view.Bind(model);
            Require(view.Indexed() == "keyed", "private index key initial assignment");
            var keyReads = view.KeyReads;
            states.Set(new RichState(false, "rekeyed", 62));
            Require(view.Indexed() == "rekeyed", "private index key update");
            Require(view.KeyReads > keyReads, "each output refreshes the private key before assignment");
            keyReads = view.KeyReads;
            states.Set(new RichState(true, "", 63));
            Require(view.Indexed() == "cleared", "nullable setter input reaches the private indexer");
            view.RaiseKey();
            Require(view.KeyReads > keyReads, "key notification reacquires the private key");
            return true;
            """).Replace(FixtureDeclaration, KeyedView + Environment.NewLine + FixtureDeclaration, StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Concrete selected contexts preserve interface streams and reference replacement.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SelectedConcreteContextUsesInterfaceAndReferenceIdentity(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(
            reactive,
            """
            var model = new Model();
            var view = new View { ViewModel = model };
            var firstStates = new Current(new RichState(false, "first-context", 58));
            using var firstRule = model.AddObservableRule(firstStates, new[] { "Name" });
            model.Selected = (ShadowContext)model.ValidationContext;
            using var binding = view.BindValidationContext(model, x => x.Selected, x => x.Message);
            Require(view.Message == "first-context", "declared interface selects original context stream");
            var secondModel = new Model();
            var secondStates = new Current(new RichState(false, "second-context", 59));
            using var secondRule = secondModel.AddObservableRule(secondStates, new[] { "Name" });
            model.Selected = (ShadowContext)secondModel.ValidationContext;
            Require(view.Message == "second-context", "equal concrete context replacement observed by reference");
            firstStates.Set(new RichState(false, "detached", 60));
            Require(view.Message == "second-context", "old selected context stream detached");
            model.Selected = null;
            Require(view.Message == "", "null selected context clears");
            return true;
            """).Replace(
                "public IValidationContext ValidationContext { get; } = new ValidationContext();",
                """
                public IValidationContext ValidationContext { get; } = new ShadowContext();
                public ShadowContext? Selected
                {
                    get;
                    set
                    {
                        if (ReferenceEquals(field, value)) return;
                        field = value;
                        this.RaisePropertyChanged();
                    }
                }
                """,
                StringComparison.Ordinal)
            .Replace(FixtureDeclaration, $"{ShadowContext}{Environment.NewLine}{FixtureDeclaration}", StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Inferred factories retain anonymous result types and explicit custom notification ownership.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InferredAnonymousFactoryOwnsNonInpcAdapter(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, """
            var owner = new ManualSource();
            var paths = new[] { ValidationPath.Legacy("Value") };
            var selector = ValidationSelector.Create(owner, current => ValidationAccessPlan.Create(
                () => ValidationRead.Present(new { Number = current.Value, Kind = "anonymous" }, paths),
                new[] { ValidationDependency.Create(() => current, static (source, observer) => source.Subscribe(observer)) },
                () => paths));
            var outputs = new List<string>();
            using var binding = ValidationRuntime.Bind(selector.Bind(owner).Observe(),
                read => outputs.Add(read.Value.Kind + ":" + read.Value.Number));
            Require(owner.Listeners == 1 && outputs.SequenceEqual(new[] { "anonymous:0" }), "inferred anonymous initial value and owned adapter");
            owner.Set(7);
            Require(outputs.Last() == "anonymous:7", "non-INPC custom invalidation re-evaluates typed getter");
            binding.Dispose();
            owner.Set(8);
            Require(owner.Listeners == 0 && outputs.Count == 2, "explicit adapter lifetime detached");
            return true;
            """).Replace(FixtureDeclaration, $"{ManualSource}{Environment.NewLine}{FixtureDeclaration}", StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Originally legal private source access is preserved without requiring unrelated partial owners.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PrivateModelSourceUsesLegalTypedAccessBridge(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = Source(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            IValidationState first = new RichState(false, "private", 61);
            var states = new Current(first);
            using var rule = model.AddObservableRule(states, new[] { "Name" });
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
            return true;
            """).Replace("public int ForbiddenReads", $"{PrivateModelMembers}{Environment.NewLine}public int ForbiddenReads", StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Nullable actual storage is accepted and narrowing storage requires a caller-defined policy.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullableTargetsUseExplicitNarrowingPolicy(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            var states = new Current(new RichState(false, "nullable", 64));
            using var rule = model.AddObservableRule(states, new[] { "Name" });
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
            {{HELPER}}
            [ValidationRuntimeDispatch]
            IValidationBinding BindPolicy() => view.BindValidationState<View, Model, string?>(model, helperExpression,
                targetExpression, static state => state.IsValid ? null : state.Text.ToSingleLine());
            using var policy = BindPolicy();
            Require(view.Message == "nullable" && view.NullableMessage == "nullable", "nullable and widened actual target contracts");
            model.Rule = null;
            Require(view.Message == "explicit-fallback" && view.NullableMessage is null, "typed target owns explicit nullable narrowing policy");
            return true;
            """.Replace("{{HELPER}}", RegisteredHelperSetup, StringComparison.Ordinal)));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Rejects nullable outputs narrowed by the getter's implicit reference conversion.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullableNarrowingRequiresTypedStoragePolicy(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            using var binding = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
                x => x.Message, static state => state.IsValid ? null : state.Text.ToSingleLine());
            return true;
            """));
        await Assert.That(result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == BindingDiagnosticId
            && diagnostic.GetMessage().Contains(NullablePolicyMessage, StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Rejects nested nullable contracts whose top-level reference annotation is unchanged.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NestedNullableContractRequiresTypedStoragePolicy(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            using var binding = view.BindValidationState<View, Model, IList<string?>>(model, x => x.Rule,
                x => (IList<string?>)x.NonNullableItems, static state => new List<string?> { null });
            return true;
            """));
        await Assert.That(result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == BindingDiagnosticId
            && diagnostic.GetMessage().Contains(NullablePolicyMessage, StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A nullable user-defined conversion result is accepted only by nullable real storage.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullableConversionResultUsesNullableStorage(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            var states = new Current(new RichState(false, "operator", 65));
            using var rule = model.AddObservableRule(states, new[] { "Name" });
            using var binding = view.BindValidation(model, x => x.Name, x => x.NullableConverted);
            Require(view.NullableConverted?.Text == "operator", "nullable conversion result assigned to nullable storage");
            view.ViewModel = null;
            Require(view.NullableConverted is null, "null conversion result clears nullable target");
            return true;
            """));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Checks conversion operator result annotations independently of the original string projection.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullableConversionResultRequiresStoragePolicy(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            using var binding = view.BindValidation(model, x => x.Name, x => x.NonNullableConverted);
            return true;
            """));
        await Assert.That(result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == BindingDiagnosticId
            && diagnostic.GetMessage().Contains(NullablePolicyMessage, StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Peer writers to one struct slot use deterministic last-writer ordering without replay loops.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExplicitStructPolicyCoordinatesSameSlotAndPeerReentry(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(StorageSource(reactive, """
            using var first = view.BindValidation(model, x => x.Name, errorExpression);
            using var second = view.BindValidation(model, x => x.Other, errorExpression);
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
            return true;
            """));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Different struct fields preserve peer writes and reapply each cached value once after an external epoch.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExplicitStructPolicyCoordinatesFieldsAndForeignReentry(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(StorageSource(reactive, """
            using var first = view.BindValidation(model, x => x.Name, errorExpression);
            using var second = view.BindValidationState(model, x => x.Other, keptExpression,
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
            return true;
            """));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Executes custom formatter, raw-state, model/helper replacement and disposal contracts.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StaticFormattingPreservesStateIdentityAndOwnership(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, StaticProjectionBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Executes newly supported direct writes while validation membership uses metadata only.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SemanticTargetsSupportFieldsIndicesAndConversion(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, SemanticTargetsBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Proves that selection metadata is independent of executable value access.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PropertyMembershipNeverExecutesSelectedGetter(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, MetadataOnlyBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Proves reference owner identity and typed key identity jointly define target replacement.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DynamicTargetKeyReplaysCachedValueExactlyOnce(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, DynamicTargetSlotBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Opaque struct storage requires a declared origin policy instead of guessing notification ownership.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OpaqueStructStorageRequiresExplicitOriginPolicy(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, OpaqueStructBody));
        await Assert.That(result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == BindingDiagnosticId)).IsTrue();
    }

    /// <summary>Checks reentrant writes distinguish external storage replacement from binding feedback.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReentrantStructReplacementPreservesActualOwner(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(StorageSource(reactive, ReentrantStructBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Checks exact combined legacy preludes while preserving modern actual-state defaults.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LegacyInitialSequenceIsExactAndPresentationOnly(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, LegacyInitialBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Executes all explicit missing-parent policies without treating available null as absence.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingOwnerPoliciesPreserveTrueNullLeaves(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(Source(reactive, MissingOwnerBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }
}
