// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks nullable setter input annotations independently of readable member contracts.</summary>
public sealed partial class BindingCapabilitiesCompilerTests
{
    /// <summary>The independently annotated property, parameter, field and indexer inputs.</summary>
    private const int DisallowInputCount = 4;

    /// <summary>Consumer members with independently annotated getters and setter inputs.</summary>
    private const string SetterContractMembers = """
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public string AllowProperty { get; set => field = value ?? ""; } = "";
        public string AllowParameter
        {
            get;
            [param: System.Diagnostics.CodeAnalysis.AllowNull]
            set => field = value ?? "";
        } = "";
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public string AllowField = "";
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public string this[bool key]
        {
            get => AllowProperty;
            set => AllowProperty = value;
        }
        public string ParameterIndexerResult { get; private set; } = "";
        public string this[decimal key]
        {
            get => ParameterIndexerResult;
            [param: System.Diagnostics.CodeAnalysis.AllowNull]
            set => ParameterIndexerResult = value ?? "";
        }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        private string PrivateAllow { get; set => field = value ?? ""; } = "";
        public string PrivateAllowResult => PrivateAllow;
        public IValidationBinding BindPrivateAllow(Model model) =>
            this.BindValidationState<View, Model, string?>(model, x => x.Rule,
                x => x.PrivateAllow, static state => state.IsValid ? null : state.Text.ToSingleLine());
        [System.Diagnostics.CodeAnalysis.DisallowNull]
        public string? DisallowProperty { get; set; } = "";
        public string? DisallowParameter
        {
            get;
            [param: System.Diagnostics.CodeAnalysis.DisallowNull]
            set;
        } = "";
        [System.Diagnostics.CodeAnalysis.DisallowNull]
        public string? DisallowField = "";
        [System.Diagnostics.CodeAnalysis.DisallowNull]
        public string? this[string key]
        {
            get => DisallowParameter;
            set => DisallowParameter = value;
        }
        """;

    /// <summary>Nullable projections satisfy AllowNull inputs while getters retain nonnullable contracts.</summary>
    private const string AllowNullBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "contract", 81));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
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
            && view.AllowField is null && view.ParameterIndexerResult == ""
            && view.PrivateAllowResult == "", "AllowNull receives null without changing getter annotations");
        return true;
        """;

    /// <summary>A typed coalescing target and nonnullable text projection satisfy DisallowNull input slots.</summary>
    private const string DisallowNullBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "contract", 83));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        model.Rule = rule;
        Expression<Func<View, string?>> expression = owner => owner.DisallowProperty;
        var target = new ValidationTarget<View, string?>(owner =>
            new ValidationWritePlan<string?>(() => ValidationTargetAccess<string?>.Present(owner,
                value => owner.DisallowProperty = value ?? "explicit-fallback"), Array.Empty<ValidationDependency>()));
        using var registry = new ValidationPlanRegistry(8);
        using var attached = registry.Attach(view);
        using var registration = registry.RegisterTarget<View, string?, string?>(ValidationPlanRole.Target, expression, target);
        {{HELPER}}
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
        Require(view.DisallowProperty == "explicit-fallback" && view.DisallowParameter == ""
            && view.DisallowField == "", "typed target applies the explicit null policy");
        return true;
        """;

    /// <summary>Executes property, setter parameter, field, indexer and private bridge AllowNull inputs.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AllowNullSetterInputPreservesGetterContract(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(SetterSource(reactive, AllowNullBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(DescribeDiagnostic)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Rejects automatic nullable writes to DisallowNull inputs and executes explicit safe alternatives.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DisallowNullSetterInputRequiresExplicitPolicy(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var rejected = await host.RunAsync(SetterSource(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            using var property = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
                x => x.DisallowProperty, static _ => null);
            using var parameter = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
                x => x.DisallowParameter, static _ => null);
            using var field = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
                x => x.DisallowField, static _ => null);
            using var indexer = view.BindValidationState<View, Model, string?>(model, x => x.Rule,
                x => x["error"], static _ => null);
            return true;
            """));
        await Assert.That(rejected.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == BindingDiagnosticId
            && diagnostic.GetMessage().Contains(NullablePolicyMessage, StringComparison.Ordinal))).IsEqualTo(DisallowInputCount);
        var result = await host.RunAsync(SetterSource(reactive, DisallowNullBody.Replace("{{HELPER}}", RegisteredHelperSetup, StringComparison.Ordinal)));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(DescribeDiagnostic)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Includes the exact generated statement when a compiler flow-contract regression occurs.</summary>
    /// <param name="diagnostic">The compiler warning or error.</param>
    /// <returns>The diagnostic and its source statement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string DescribeDiagnostic(Diagnostic diagnostic) =>
        $"{diagnostic}{Environment.NewLine}{diagnostic.Location.SourceTree?.GetText().Lines[diagnostic.Location.GetLineSpan().StartLinePosition.Line]}";

    /// <summary>Includes independently annotated setter inputs in the trusted consumer fixture.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <param name="body">The fixture entry point.</param>
    /// <returns>The trusted source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SetterSource(bool reactive, string body) => Source(reactive, body)
        .Replace("public string MessageField", $"{SetterContractMembers}{Environment.NewLine}public string MessageField", StringComparison.Ordinal);
}
