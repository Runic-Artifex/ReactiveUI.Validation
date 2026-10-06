// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Typed delegate catalogs remain cold, scoped and distinct from expression registrations.</summary>
public class DelegateCapabilityCatalogTests
{
    /// <summary>The first finite contract identity.</summary>
    private const string FirstSchema = "first-contract";

    /// <summary>The overlapping finite contract identity.</summary>
    private const string SecondSchema = "second-contract";

    /// <summary>The registered property presentation path.</summary>
    private const string PropertyPath = "Model.Value";

    /// <summary>The original factory failure message.</summary>
    private const string FactoryMessage = "delegate factory failure";

    /// <summary>The assigned target output.</summary>
    private const string AssignedOutput = "assigned";

    /// <summary>The capacity required for two independent registrations.</summary>
    private const int PairCapacity = 2;

    /// <summary>The capacity for property, target and implicit selector registrations.</summary>
    private const int FullCapacity = 4;

    /// <summary>A target getter type distinct from the writer output.</summary>
    private enum GetterState
    {
        /// <summary>The initial fixture state.</summary>
        Initial = 0,
    }

    /// <summary>Fresh equal method groups match one exact registration without invoking their bodies.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task EqualMethodGroupsUseOneColdExactRegistration()
    {
        using var source = new DelegateSource();
        Func<DelegateSource, int> first = ReadValue;
        Func<DelegateSource, int> fresh = new(ReadValue);
        Func<DelegateSource, int> other = ReadOtherValue;
        var descriptor = ValueSelector();
        var request = new ValidationPlanRequest(ValidationPlanRole.RuleValue, string.Empty);
        using var registry = new ValidationPlanRegistry(1);
        using var registration = registry.RegisterDelegateSelector(ValidationPlanRole.RuleValue, first, descriptor);
        await Assert.That(ReferenceEquals(first, fresh)).IsFalse();
        await Assert.That(() => registry.RegisterDelegateSelector(ValidationPlanRole.RuleValue, fresh, descriptor)).Throws<InvalidOperationException>();
        await Assert.That(registry.TryGetDelegateSelector(request, fresh, out var selected)).IsTrue();
        await Assert.That(selected).IsSameReferenceAs(descriptor);
        await Assert.That(registry.TryGetDelegateSelector(request, other, out _)).IsFalse();
        await Assert.That(source.ReadCount).IsEqualTo(0);
        await Assert.That(selected!.Bind(source).Read().Value).IsEqualTo(0);
        await Assert.That(source.ReadCount).IsEqualTo(1);
    }

    /// <summary>Equal schema identities coexist in distinct expression and delegate dispatch domains.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task DelegatePatternsAndExpressionPatternsHaveSeparateDomains()
    {
        using var source = new DelegateSource();
        Func<DelegateSource, int> selection = ReadValue;
        Expression<Func<DelegateSource, int>> expression = model => model.Value;
        var descriptor = ValueSelector();
        var request = new ValidationPlanRequest(ValidationPlanRole.Property, FirstSchema);
        using var registry = new ValidationPlanRegistry(PairCapacity);
        using var delegateRegistration = registry.RegisterDelegateSelectorPattern(
            ValidationPlanRole.Property,
            FirstSchema,
            ValidationDelegatePattern.Create(selection),
            _ => descriptor);
        using var expressionRegistration = registry.RegisterSelectorPattern(
            ValidationPlanRole.Property,
            FirstSchema,
            ValidationExpressionPattern.Create(expression),
            _ => descriptor);
        await Assert.That(registry.TryGetDelegateSelector(request, selection, out var fromDelegate)).IsTrue();
        await Assert.That(fromDelegate).IsSameReferenceAs(descriptor);
        await Assert.That(registry.TryGetSelector(request, expression, out var fromExpression)).IsTrue();
        await Assert.That(fromExpression).IsSameReferenceAs(descriptor);
        Func<DelegateSource, long> wrongSlot = static model => model.Value;
        await Assert.That(registry.TryGetDelegateSelector(request, wrongSlot, out _)).IsFalse();
        Func<DelegateSource, int> foreignMethod = ReadOtherValue;
        await Assert.That(registry.TryGetDelegateSelector(request, foreignMethod, out _)).IsFalse();
        await Assert.That(source.ReadCount).IsEqualTo(0);
    }

    /// <summary>Known method-group captures stay live while unknown closure owners and retired scopes fail closed.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task CapturedOwnersAndAssociationLeasesRemainScoped()
    {
        using var source = new DelegateSource();
        var first = new DelegateCapture();
        var second = new DelegateCapture();
        Func<DelegateSource, int> known = first.Read;
        Func<DelegateSource, int> equal = first.Read;
        Func<DelegateSource, int> foreign = second.Read;
        var matcher = ValidationDelegatePattern.Create(known);
        await Assert.That(matcher(equal, out _)).IsTrue();
        await Assert.That(matcher(foreign, out _)).IsFalse();
        var closure = CapturedSelector(first);
        await Assert.That(ValidationDelegatePattern.Create(closure)(CapturedSelector(first), out _)).IsFalse();
        var request = new ValidationPlanRequest(ValidationPlanRole.RuleValue, string.Empty);
        using var registry = new ValidationPlanRegistry(1);
        using (var association = registry.Attach(source))
        {
            using var registration = registry.RegisterDelegateSelector(ValidationPlanRole.RuleValue, known, CaptureSelector(first));
            await Assert.That(ValidationPlanRegistry.TryGetAttached(source)).IsSameReferenceAs(registry);
            await Assert.That(registry.TryGetDelegateSelector(request, equal, out var selected)).IsTrue();
            await Assert.That(registry.TryGetDelegateSelector(request, foreign, out _)).IsFalse();
            await Assert.That(source.ReadCount).IsEqualTo(0);
            await Assert.That(first.ReadCount).IsEqualTo(0);
            var plan = selected!.Bind(source);
            await Assert.That(plan.Read().Value).IsEqualTo(0);
            first.Offset = 1;
            await Assert.That(plan.Read().Value).IsEqualTo(1);
        }

        var reads = source.ReadCount;
        await Assert.That(registry.TryGetDelegateSelector(request, known, out _)).IsFalse();
        await Assert.That(ValidationPlanRegistry.TryGetAttached(source)).IsNull();
        await Assert.That(source.ReadCount).IsEqualTo(reads);
        await Assert.That(second.ReadCount).IsEqualTo(0);
    }

    /// <summary>Ambiguity, invalid positive payloads and factory failures propagate before any selector invocation.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task DelegateAmbiguityAndInvalidFactoriesNeverInvokeSelections()
    {
        using var source = new DelegateSource();
        Func<DelegateSource, int> selection = ReadValue;
        var matcher = ValidationDelegatePattern.Create(selection);
        var request = new ValidationPlanRequest(ValidationPlanRole.RuleValue, string.Empty);
        var factories = 0;
        using var registry = new ValidationPlanRegistry(PairCapacity);
        using (var first = registry.RegisterDelegateSelectorPattern(ValidationPlanRole.RuleValue, FirstSchema, matcher, Factory))
        {
            using var second = registry.RegisterDelegateSelectorPattern(ValidationPlanRole.RuleValue, SecondSchema, matcher, Factory);
            await Assert.That(() => registry.TryGetDelegateSelector(request, selection, out _)).Throws<InvalidOperationException>();
            await Assert.That(factories).IsEqualTo(0);
            await Assert.That(registry.TryGetDelegateSelector(new(ValidationPlanRole.RuleValue, FirstSchema), selection, out _)).IsTrue();
            await Assert.That(factories).IsEqualTo(1);
        }

        using (var invalidPayload = registry.RegisterDelegateSelectorPattern(ValidationPlanRole.RuleValue, FirstSchema, InvalidMatch, Factory))
        {
            await Assert.That(() => registry.TryGetDelegateSelector(request, selection, out _)).Throws<InvalidOperationException>();
            await Assert.That(factories).IsEqualTo(1);
        }

        using (var nullFactory = registry.RegisterDelegateSelectorPattern(ValidationPlanRole.RuleValue, FirstSchema, matcher, static _ => null!))
        {
            await Assert.That(() => registry.TryGetDelegateSelector(request, selection, out _)).Throws<InvalidOperationException>();
        }

        var original = new InvalidOperationException(FactoryMessage);
        using (var throwingFactory = registry.RegisterDelegateSelectorPattern(ValidationPlanRole.RuleValue, FirstSchema, matcher, _ => throw original))
        {
            await Assert.That(CaptureFailure(() => registry.TryGetDelegateSelector(request, selection, out _))).IsSameReferenceAs(original);
        }

        await Assert.That(registry.TryGetDelegateSelector(request, selection, out _)).IsFalse();
        await Assert.That(source.ReadCount).IsEqualTo(0);

        ValidationSelector<DelegateSource, int> Factory(ValidationArguments _)
        {
            factories++;
            return ValueSelector();
        }
    }

    /// <summary>Metadata, implicit roles and target output slots retain exact typed contracts without invoking selectors.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task DelegateMetadataAndImplicitRolesKeepExactClosedSlots()
    {
        using var source = new DelegateSource();
        Func<DelegateSource, int> selection = ReadValue;
        Func<DelegateSource, GetterState> targetSelection = ReadState;
        var path = ValidationPath.Legacy(PropertyPath);
        var metadata = new ValidationSelector<DelegateSource, int>(_ => new(
            static () => throw new InvalidOperationException(FactoryMessage),
            [],
            ValidationObservationOptions<int>.Default,
            () => [path]));
        var target = new ValidationTarget<DelegateSource, string>(model => new(
            () => ValidationTargetAccess<string>.Present(model, value => model.Text = value),
            []));
        var context = new ValidationSelector<DelegateSource, IValidationContext?>(model => new(
            () => ValidationRead<IValidationContext?>.Present(model.Context, []),
            [],
            ValidationObservationOptions<IValidationContext?>.Default));
        var modelSelector = new ValidationSelector<DelegateSource, DelegateSource?>(model => new(
            () => ValidationRead<DelegateSource?>.Present(model, []),
            [],
            ValidationObservationOptions<DelegateSource?>.Default));
        using var registry = new ValidationPlanRegistry(FullCapacity);
        using var propertyRegistration = registry.RegisterDelegateSelector(ValidationPlanRole.Property, selection, metadata);
        using var targetRegistration = registry.RegisterDelegateTarget(ValidationPlanRole.Target, targetSelection, target);
        using var contextRegistration = registry.RegisterDelegateSelector(ValidationPlanRole.DefaultContext, null, context);
        using var modelRegistration = registry.RegisterDelegateSelector(ValidationPlanRole.Model, null, modelSelector);
        await Assert.That(registry.TryGetDelegateSelector(new(ValidationPlanRole.Property, string.Empty), selection, out var selected)).IsTrue();
        IReadOnlyList<ValidationPath>? paths = null;
        using var observation = selected!.Bind(source).ObservePaths().Subscribe(current => paths = current);
        await Assert.That(paths![0]).IsSameReferenceAs(path);
        await Assert.That(registry.TryGetDelegateSelector<DelegateSource, IValidationContext?>(new(ValidationPlanRole.DefaultContext, string.Empty), null, out var selectedContext)).IsTrue();
        await Assert.That(selectedContext!.Bind(source).Read().Value).IsSameReferenceAs(source.Context);
        await Assert.That(registry.TryGetDelegateSelector<DelegateSource, DelegateSource?>(new(ValidationPlanRole.Model, string.Empty), null, out _)).IsTrue();
        await Assert.That(registry.TryGetDelegateSelector<DelegateSource, IValidationContext?>(new(ValidationPlanRole.Context, string.Empty), null, out _)).IsFalse();
        await Assert.That(registry.TryGetDelegateTarget<DelegateSource, GetterState, int>(new(ValidationPlanRole.Target, string.Empty), targetSelection, out _)).IsFalse();
        await Assert.That(registry.TryGetDelegateTarget<DelegateSource, GetterState, string>(new(ValidationPlanRole.Target, string.Empty), targetSelection, out var selectedTarget)).IsTrue();
        await Assert.That(registry.TryGetDelegateTarget<DelegateSource, GetterState, string>(new(ValidationPlanRole.Target, string.Empty), null, out _)).IsFalse();
        using var values = new BehaviorSubject<string>(AssignedOutput);
        using var binding = selectedTarget!.Bind(source).Bind(values);
        await Assert.That(source.Text).IsEqualTo(AssignedOutput);
        await Assert.That(source.ReadCount).IsEqualTo(0);
    }

    /// <summary>Reads the known value only when a bound observation explicitly invokes the legal getter.</summary>
    /// <param name="source">The borrowed current source.</param>
    /// <returns>The current selected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadValue(DelegateSource source) => source.Value;

    /// <summary>Provides a distinct method identity with the same closed signature.</summary>
    /// <param name="source">The borrowed current source.</param>
    /// <returns>The adjusted value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadOtherValue(DelegateSource source) => source.Value + 1;

    /// <summary>Provides an exact target getter signature distinct from its output type.</summary>
    /// <param name="source">The borrowed source.</param>
    /// <returns>The typed getter value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GetterState ReadState(DelegateSource source) => source.State;

    /// <summary>Creates a distinct compiler closure owner on each invocation.</summary>
    /// <param name="capture">The known capture value.</param>
    /// <returns>The delegate with its new closure owner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<DelegateSource, int> CapturedSelector(DelegateCapture capture) => source => capture.Read(source) + capture.Offset;

    /// <summary>Creates current direct source access without retaining a receiver during lookup.</summary>
    /// <returns>The typed descriptor.</returns>
    private static ValidationSelector<DelegateSource, int> ValueSelector() => new(source => new(
        () => ValidationRead<int>.Present(source.Value, []),
        [],
        ValidationObservationOptions<int>.Default));

    /// <summary>Creates legal typed access to a known live capture owner.</summary>
    /// <param name="capture">The borrowed known capture.</param>
    /// <returns>The typed descriptor.</returns>
    private static ValidationSelector<DelegateSource, int> CaptureSelector(DelegateCapture capture) => new(source => new(
        () => ValidationRead<int>.Present(capture.Read(source), []),
        [],
        ValidationObservationOptions<int>.Default));

    /// <summary>Deliberately violates a foreign matcher's positive-result annotation.</summary>
    /// <param name="selection">The supplied selector that must remain uninvoked.</param>
    /// <param name="arguments">The invalid positive null payload.</param>
    /// <returns>True to exercise runtime contract validation.</returns>
    private static bool InvalidMatch(Func<DelegateSource, int> selection, [NotNullWhen(true)] out ValidationArguments? arguments)
    {
        _ = selection;
        arguments = null!;
        return true;
    }

    /// <summary>Captures the original expected factory error without masking unrelated failures.</summary>
    /// <param name="operation">The synchronous catalog operation.</param>
    /// <returns>The exact failure, or null if the operation succeeded.</returns>
    private static InvalidOperationException? CaptureFailure(Action operation)
    {
        try
        {
            operation();
            return null;
        }
        catch (InvalidOperationException error)
        {
            return error;
        }
    }

    /// <summary>A borrowed source whose value getter counts forbidden matching-time invocations.</summary>
    private sealed class DelegateSource : IDisposable
    {
        /// <summary>Gets the fixture-owned context.</summary>
        public ValidationContext Context { get; } = new(ImmediateSequencer.Instance);

        /// <summary>Gets the number of actual value reads.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Gets or sets the target expression's getter value.</summary>
        public GetterState State { get; set; }

        /// <summary>Gets or sets the actual presentation output.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the current value and counts actual getter access.</summary>
        public int Value
        {
            get
            {
                ReadCount++;
                return field;
            }

            set;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Context.Dispose();
    }

    /// <summary>A known capture used as a delegate target without inspecting its Method or Target metadata.</summary>
    private sealed class DelegateCapture
    {
        /// <summary>Gets or sets the live captured argument.</summary>
        public int Offset { get; set; }

        /// <summary>Gets the number of actual capture invocations.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Reads current typed model and capture values only after binding.</summary>
        /// <param name="source">The borrowed current model.</param>
        /// <returns>The current combined value.</returns>
        public int Read(DelegateSource source)
        {
            ReadCount++;
            return source.Value + Offset;
        }
    }
}
