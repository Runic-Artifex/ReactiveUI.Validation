// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks input flow restrictions independently of CLR Nullable value storage.</summary>
public sealed partial class BindingCapabilitiesCompilerTests
{
    /// <summary>The ordinary nullable integer and private nullable custom value setter inputs.</summary>
    private const int NullableValueInputCount = 2;

    /// <summary>Consumer setters whose getters and CLR signatures remain nullable values.</summary>
    private const string NullableValueMembers = """
        public readonly record struct ValuePayload(int Number);
        [System.Diagnostics.CodeAnalysis.DisallowNull]
        public int? RequiredNumber { get; set; } = 0;
        [System.Diagnostics.CodeAnalysis.DisallowNull]
        private ValuePayload? RequiredPayload { get; set; } = new ValuePayload(0);
        public ValuePayload? PayloadResult => RequiredPayload;
        public void SetPayload(ValuePayload value) => RequiredPayload = value;
        public Expression<Func<View, ValuePayload?>> PayloadExpression() => owner => owner.RequiredPayload;
        """;

    /// <summary>The original caller has legal private setter access but cannot supply null to that input.</summary>
    private const string RejectedValueBridge = """
        public IValidationBinding BindRejectedPayload(Model model) =>
            this.BindValidationState<View, Model, ValuePayload?>(model, x => x.Rule,
                x => x.RequiredPayload, static _ => null);
        """;

    /// <summary>Typed policies supply nonnull values without changing the storage or readable nullable contracts.</summary>
    private const string NullableValueBody = """
        var model = new Model();
        var view = new View { ViewModel = model };
        var states = new Current(new RichState(false, "contract", 85));
        using var rule = model.AddObservableRule(states, new[] { "Name" });
        model.Rule = rule;
        Expression<Func<Model, ValidationHelper?>> helperExpression = owner => owner.Rule;
        var helper = new ValidationSelector<Model, ValidationHelper?>(owner =>
            new ValidationAccessPlan<ValidationHelper?>(() => ValidationRead<ValidationHelper?>.Present(owner.Rule, Array.Empty<ValidationPath>()),
                new[] { ValidationDependency.PropertyChanged(() => owner, nameof(Model.Rule)) },
                new ValidationObservationOptions<ValidationHelper?>(ValidationMissingOwnerPolicy.DefaultValue,
                    null, ReferenceEqualityComparer.Instance, false)));
        Expression<Func<View, int?>> numberExpression = owner => owner.RequiredNumber;
        var payloadExpression = view.PayloadExpression();
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
        return true;
        """;

    /// <summary>Rejects null integer and private custom value writes and executes declared nonnull replacement policies.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous assertion work.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DisallowNullNullableValueInputsKeepClrStorage(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var source = NullableValueSource(reactive, """
            var model = new Model();
            var view = new View { ViewModel = model };
            using var number = view.BindValidationState<View, Model, int?>(model, x => x.Rule,
                x => x.RequiredNumber, static _ => null);
            using var payload = view.BindRejectedPayload(model);
            return true;
            """).Replace("public string MessageField", $"{RejectedValueBridge}{Environment.NewLine}public string MessageField", StringComparison.Ordinal);
        var rejected = await host.RunAsync(source);
        await Assert.That(rejected.GeneratorDiagnostics.Count(static diagnostic => diagnostic.Id == BindingDiagnosticId
            && diagnostic.GetMessage().Contains(NullablePolicyMessage, StringComparison.Ordinal))).IsEqualTo(NullableValueInputCount);
        var result = await host.RunAsync(NullableValueSource(reactive, NullableValueBody));
        await Assert.That(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(DescribeDiagnostic)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FixtureType, FixtureMethod)).IsTrue();
    }

    /// <summary>Includes nullable-value setter inputs independently of reference flow annotations.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <param name="body">The fixture entry point.</param>
    /// <returns>The trusted source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string NullableValueSource(bool reactive, string body) => Source(reactive, body)
        .Replace("public string MessageField", $"{NullableValueMembers}{Environment.NewLine}public string MessageField", StringComparison.Ordinal);
}
