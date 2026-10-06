// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive.Subjects;

#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
namespace ReactiveUI.Validation.Reactive.Tests;
#else
using ReactiveUI.Validation.Capabilities;
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Regressions for cold typed fallbacks and authoritative registered capabilities.</summary>
public class CapabilityFallbackTests
{
    /// <summary>The first receiver's value.</summary>
    private const string FirstValue = "first";

    /// <summary>The current receiver's value.</summary>
    private const string CurrentValue = "current";

    /// <summary>The assigned presentation output.</summary>
    private const string AssignedValue = "assigned";

    /// <summary>The registered schema discriminator.</summary>
    private const string FirstSchema = "first-schema";

    /// <summary>The competing schema discriminator.</summary>
    private const string SecondSchema = "second-schema";

    /// <summary>A failure resembling the normal missing-capability error.</summary>
    private const string MissingMessage = "No typed validation capability is registered for RuleValue.";

    /// <summary>The capacity for role and output slot registrations.</summary>
    private const int CatalogSlots = 4;

    /// <summary>The capacity for competing structural registrations.</summary>
    private const int CompetingSlots = 2;

    /// <summary>The getter type selected by a writable target expression.</summary>
    private enum GetterState
    {
        /// <summary>The initial getter state.</summary>
        Initial = 0,
    }

    /// <summary>An absent selector resolves cold fallback access bound to the current receiver.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task AbsentCapabilitiesKeepFallbackColdAndBindCurrentReceiver()
    {
        var first = new FallbackSource { Value = FirstValue };
        var current = new FallbackSource { Value = CurrentValue };
        var counters = new FallbackCounters();
        var fallback = ValueFallback(counters);
        Expression<Func<FallbackSource, string>> expression = source => source.Value;
        var selected = ValidationRuntime.ResolveSelector(first, null, expression, ValidationPlanRole.RuleValue, string.Empty, fallback);
        await Assert.That(selected).IsSameReferenceAs(fallback);
        await Assert.That(counters.BindCount).IsEqualTo(0);
        await Assert.That(first.ReadCount).IsEqualTo(0);
        var plan = selected.Bind(current);
        await Assert.That(counters.BindCount).IsEqualTo(1);
        await Assert.That(current.ReadCount).IsEqualTo(0);
        await Assert.That(plan.Read().Value).IsEqualTo(CurrentValue);
        await Assert.That(current.ReadCount).IsEqualTo(1);
        await Assert.That(first.ReadCount).IsEqualTo(0);
    }

    /// <summary>Scoped plans win only for their exact selector roles and target output slots.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task RegisteredRolesAndClosedTargetOutputsOverrideFallbacks()
    {
        var source = new FallbackSource();
        var counters = new FallbackCounters();
        var fallback = ValueFallback(counters);
        var targetFallback = TargetFallback(counters);
        Expression<Func<FallbackSource, string>> expression = model => model.Value;
        Expression<Func<FallbackSource, GetterState>> targetExpression = model => model.State;
        using var registry = new ValidationPlanRegistry(CatalogSlots);
        using var association = registry.Attach(source);
        using var wrongRole = registry.RegisterSelector(ValidationPlanRole.Property, expression, ValueSelector());
        var absentRole = ValidationRuntime.ResolveSelector(source, null, expression, ValidationPlanRole.RuleValue, string.Empty, fallback);
        await Assert.That(absentRole).IsSameReferenceAs(fallback);
        var registered = ValueSelector();
        using var correctRole = registry.RegisterSelector(ValidationPlanRole.RuleValue, expression, registered);
        await Assert.That(ValidationRuntime.ResolveSelector(source, null, expression, ValidationPlanRole.RuleValue, string.Empty, fallback)).IsSameReferenceAs(registered);
        var integerTarget = new ValidationTarget<FallbackSource, int>(model => new(
            () => ValidationTargetAccess<int>.Present(model, value => model.Number = value),
            []));
        using var wrongOutput = registry.RegisterTarget(ValidationPlanRole.Target, targetExpression, integerTarget);
        await Assert.That(ValidationRuntime.ResolveTarget(source, targetExpression, string.Empty, targetFallback)).IsSameReferenceAs(targetFallback);
        var registeredTarget = new ValidationTarget<FallbackSource, string>(model => new(
            () => ValidationTargetAccess<string>.Present(model, value => model.Value = value),
            []));
        using var correctOutput = registry.RegisterTarget(ValidationPlanRole.Target, targetExpression, registeredTarget);
        var selected = ValidationRuntime.ResolveTarget(source, targetExpression, string.Empty, targetFallback);
        await Assert.That(selected).IsSameReferenceAs(registeredTarget);
        await Assert.That(counters.BindCount).IsEqualTo(0);
        using var values = new BehaviorSubject<string>(AssignedValue);
        using var binding = selected.Bind(source).Bind(values);
        await Assert.That(source.Value).IsEqualTo(AssignedValue);
    }

    /// <summary>Matched provider errors, null successes and structural conflicts never select fallback access.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task AuthoritativeFailuresNeverSelectFallback()
    {
        var source = new FallbackSource();
        var counters = new FallbackCounters();
        var fallback = ValueFallback(counters);
        Expression<Func<FallbackSource, string>> expression = model => model.Value;
        var original = new InvalidOperationException(MissingMessage);
        var provider = new FaultingProvider(original);
        var providerFailure = CaptureFailure(() => ValidationRuntime.ResolveSelector(source, provider, expression, ValidationPlanRole.RuleValue, string.Empty, fallback));
        await Assert.That(providerFailure).IsSameReferenceAs(original);
        var nullProvider = new FaultingProvider(null);
        await Assert.That(CaptureFailure(() => ValidationRuntime.ResolveSelector(source, nullProvider, expression, ValidationPlanRole.RuleValue, string.Empty, fallback))).IsNotNull();
        using var registry = new ValidationPlanRegistry(CompetingSlots);
        using var association = registry.Attach(source);
        var matcher = ValidationExpressionPattern.Create(expression);
        var first = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, FirstSchema, matcher, Factory);
        var second = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, SecondSchema, matcher, Factory);
        await Assert.That(CaptureFailure(() => ValidationRuntime.ResolveSelector(source, null, expression, ValidationPlanRole.RuleValue, string.Empty, fallback))).IsNotNull();
        await Assert.That(counters.FactoryCount).IsEqualTo(0);
        first.Dispose();
        second.Dispose();
        using var failingFactory = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, FirstSchema, matcher, _ => throw original);
        var factoryFailure = CaptureFailure(() => ValidationRuntime.ResolveSelector(source, null, expression, ValidationPlanRole.RuleValue, string.Empty, fallback));
        await Assert.That(factoryFailure).IsSameReferenceAs(original);
        await Assert.That(counters.BindCount).IsEqualTo(0);

        ValidationSelector<FallbackSource, string> Factory(ValidationArguments _)
        {
            counters.FactoryCount++;
            return ValueSelector();
        }
    }

    /// <summary>Fallback and registered metadata avoid leaf getters, including matched metadata failure.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task MetadataFallbacksAndRegisteredPathsNeverReadLeafValues()
    {
        var source = new FallbackSource();
        var counters = new FallbackCounters();
        var fallbackPath = ValidationPath.Legacy("Fallback.Value");
        var registeredPath = ValidationPath.Legacy("Registered.Value");
        var fallback = new ValidationSelector<FallbackSource, ValidationPath>(_ =>
        {
            counters.BindCount++;
            return new(() => ValidationRead<ValidationPath>.Present(fallbackPath, []), [], ValidationObservationOptions<ValidationPath>.Default);
        });
        Expression<Func<FallbackSource, string>> expression = model => model.ExplosiveValue;
        var absent = ValidationRuntime.ResolvePathSelector(source, null, expression, ValidationPlanRole.Property, string.Empty, fallback);
        await Assert.That(absent).IsSameReferenceAs(fallback);
        await Assert.That(counters.BindCount).IsEqualTo(0);
        await Assert.That(absent.Bind(source).Read().Value).IsSameReferenceAs(fallbackPath);
        using var registry = new ValidationPlanRegistry(1);
        using var association = registry.Attach(source);
        var descriptor = new ValidationSelector<FallbackSource, string>(model => new(
            () => ValidationRead<string>.Present(model.ExplosiveValue, []),
            [],
            ValidationObservationOptions<string>.Default,
            () => [registeredPath]));
        var registration = registry.RegisterSelector(ValidationPlanRole.Property, expression, descriptor);
        var selected = ValidationRuntime.ResolvePathSelector(source, null, expression, ValidationPlanRole.Property, string.Empty, fallback);
        await Assert.That(selected.Bind(source).Read().Value).IsSameReferenceAs(registeredPath);
        registration.Dispose();
        using var incompleteMetadata = registry.RegisterSelector(ValidationPlanRole.Property, expression, ValueSelector());
        var incomplete = ValidationRuntime.ResolvePathSelector(source, null, expression, ValidationPlanRole.Property, string.Empty, fallback);
        await Assert.That(CaptureFailure(() => incomplete.Bind(source))).IsNotNull();
        await Assert.That(counters.BindCount).IsEqualTo(1);
        await Assert.That(source.ReadCount).IsEqualTo(0);
        await Assert.That(source.ExplosiveReads).IsEqualTo(0);
    }

    /// <summary>Creates a fallback recording only actual descriptor binding.</summary>
    /// <param name="counters">The invocation counters.</param>
    /// <returns>The cold fallback descriptor.</returns>
    private static ValidationSelector<FallbackSource, string> ValueFallback(FallbackCounters counters) => new(source =>
    {
        counters.BindCount++;
        return new(() => ValidationRead<string>.Present(source.Value, []), [], ValidationObservationOptions<string>.Default);
    });

    /// <summary>Creates direct value access without retaining a receiver before binding.</summary>
    /// <returns>The typed selector.</returns>
    private static ValidationSelector<FallbackSource, string> ValueSelector() => new(source =>
        new(() => ValidationRead<string>.Present(source.Value, []), [], ValidationObservationOptions<string>.Default));

    /// <summary>Creates a target fallback recording only actual descriptor binding.</summary>
    /// <param name="counters">The invocation counters.</param>
    /// <returns>The cold target descriptor.</returns>
    private static ValidationTarget<FallbackSource, string> TargetFallback(FallbackCounters counters) => new(source =>
    {
        counters.BindCount++;
        return new(() => ValidationTargetAccess<string>.Present(source, value => source.Value = value), []);
    });

    /// <summary>Captures the exact expected failure while allowing unrelated exception types to propagate.</summary>
    /// <param name="operation">The synchronous resolution or binding operation.</param>
    /// <returns>The original error, or null if resolution unexpectedly succeeded.</returns>
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

    /// <summary>Records binding and structural factory effects independently of lookup.</summary>
    private sealed class FallbackCounters
    {
        /// <summary>Gets or sets the number of fallback descriptor bindings.</summary>
        public int BindCount { get; set; }

        /// <summary>Gets or sets the number of matched factory executions.</summary>
        public int FactoryCount { get; set; }
    }

    /// <summary>A borrowed source exposing a value getter that must remain cold during resolution.</summary>
    private sealed class FallbackSource
    {
        /// <summary>Gets or sets the target expression's getter value.</summary>
        public GetterState State { get; set; }

        /// <summary>Gets or sets the value assigned through the distinct integer output slot.</summary>
        public int Number { get; set; }

        /// <summary>Gets or sets the selected value and counts actual getter access.</summary>
        public string Value
        {
            get
            {
                ReadCount++;
                return field;
            }

            set => field = value;
        } = string.Empty;

        /// <summary>Gets the number of actual value reads.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Gets the number of forbidden leaf reads.</summary>
        public int ExplosiveReads { get; private set; }

        /// <summary>Gets a leaf that fails if metadata resolution evaluates it.</summary>
        /// <exception cref="InvalidOperationException">The forbidden leaf getter was evaluated.</exception>
        public string ExplosiveValue
        {
            get
            {
                ExplosiveReads++;
                throw new InvalidOperationException("Metadata lookup evaluated the selected leaf.");
            }
        }
    }

    /// <summary>A foreign provider deliberately exercising failures and invalid positive-null results.</summary>
    /// <param name="failure">The exact failure to propagate, or null to violate the positive-result contract.</param>
    private sealed class FaultingProvider(InvalidOperationException? failure) : IValidationPlanProvider
    {
        /// <inheritdoc/>
        public bool TryGetSelector<TSource, TValue>(
            ValidationPlanRequest request,
            Expression<Func<TSource, TValue>>? expression,
            [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector)
        {
            if (typeof(TSource) != typeof(FallbackSource) || typeof(TValue) != typeof(string) || request.Role != ValidationPlanRole.RuleValue)
            {
                selector = null;
                return false;
            }

            if (failure is not null)
            {
                throw failure;
            }

            // A foreign provider can violate its annotation; the runtime must reject this result without using fallback access.
            selector = null!;
            return true;
        }

        /// <inheritdoc/>
        public bool TryGetTarget<TSource, TValue, TOut>(
            ValidationPlanRequest request,
            Expression<Func<TSource, TValue>>? expression,
            [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target)
        {
            target = null;
            return false;
        }
    }
}
