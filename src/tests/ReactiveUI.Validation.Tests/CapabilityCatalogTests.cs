// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
namespace ReactiveUI.Validation.Reactive.Tests;
#else
using ReactiveUI.Validation.Capabilities;
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Finite typed catalog, argument schema and association lifetime regressions.</summary>
public class CapabilityCatalogTests
{
    /// <summary>The first selected value and schema identity.</summary>
    private const string FirstValue = "first";

    /// <summary>The second selected value and schema identity.</summary>
    private const string SecondValue = "second";

    /// <summary>The capacity needed for competing registrations.</summary>
    private const int MultipleEntries = 2;

    /// <summary>The fixture property getter type.</summary>
    private enum CatalogState
    {
        /// <summary>The ready fixture state.</summary>
        Ready = 0,
    }

    /// <summary>The reference API carried by a stable boxed value.</summary>
    private interface IBox
    {
        /// <summary>Gets the fixture identity value.</summary>
        int Identity { get; }
    }

    /// <summary>Fresh equivalent member paths bind each current receiver.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task FreshMemberExpressionsBindCurrentReceivers()
    {
        using var registry = new ValidationPlanRegistry(1);
        Expression<Func<CatalogSource, string>> exemplar = source => source.Value;
        using var registration = registry.RegisterSelectorPattern(
            ValidationPlanRole.RuleValue,
            "value",
            ValidationExpressionPattern.Create(exemplar),
            static _ => ValueSelector());
        Expression<Func<CatalogSource, string>> first = source => source.Value;
        Expression<Func<CatalogSource, string>> second = source => source.Value;
        var request = new ValidationPlanRequest(ValidationPlanRole.RuleValue, string.Empty);
        await Assert.That(registry.TryGetSelector(request, first, out var selector)).IsTrue();
        await Assert.That(selector!.Bind(new CatalogSource { Value = FirstValue }).Read().Value).IsEqualTo(FirstValue);
        await Assert.That(registry.TryGetSelector(request, second, out selector)).IsTrue();
        await Assert.That(selector!.Bind(new CatalogSource { Value = SecondValue }).Read().Value).IsEqualTo(SecondValue);
    }

    /// <summary>Known index positions return invocation-local typed constants without caching.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task FreshIndexArgumentsRemainInvocationLocal()
    {
        using var registry = new ValidationPlanRegistry(1);
        Expression<Func<CatalogSource, string>> exemplar = source => source[0];
        var call = (MethodCallExpression)exemplar.Body;
        var argument = new ValidationArgument<int>();
        var matcher = ValidationExpressionPattern.Create(exemplar, ValidationExpressionBinding.Constant((ConstantExpression)call.Arguments[0], argument));
        using var registration = registry.RegisterSelectorPattern(
            ValidationPlanRole.Property,
            "index",
            matcher,
            arguments => IndexSelector(arguments.Get(argument)));
        Expression<Func<CatalogSource, string>> first = source => source[0];
        Expression<Func<CatalogSource, string>> second = source => source[1];
        var request = new ValidationPlanRequest(ValidationPlanRole.Property, "index");
        var source = new CatalogSource();
        await Assert.That(registry.TryGetSelector(request, first, out var selector)).IsTrue();
        await Assert.That(selector!.Bind(source).Read().Value).IsEqualTo("zero");
        await Assert.That(registry.TryGetSelector(request, second, out selector)).IsTrue();
        await Assert.That(selector!.Bind(source).Read().Value).IsEqualTo("one");
    }

    /// <summary>Captured values require matching closure identity or an explicit typed extractor.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task UnknownClosuresFailWhileKnownCaptureReadsRemainLive()
    {
        var firstCapture = new KnownCapture();
        var exemplar = CaptureExpression(firstCapture);
        var matcher = ValidationExpressionPattern.Create(exemplar);
        var foreignCapture = new KnownCapture();
        var foreign = CaptureExpression(foreignCapture);
        await Assert.That(matcher(foreign, out _)).IsFalse();
        using var registry = new ValidationPlanRegistry(1);
        using var registration = registry.RegisterSelectorPattern(
            ValidationPlanRole.Property,
            "capture",
            matcher,
            _ => new(source => new(
                () => ValidationRead<string>.Present(source[firstCapture.Index], []),
                [],
                ValidationObservationOptions<string>.Default)));
        var request = new ValidationPlanRequest(ValidationPlanRole.Property, "capture");
        await Assert.That(registry.TryGetSelector(request, exemplar, out var selector)).IsTrue();
        var plan = selector!.Bind(new());
        await Assert.That(plan.Read().Value).IsEqualTo("zero");
        firstCapture.Index = 1;
        await Assert.That(plan.Read().Value).IsEqualTo("one");
    }

    /// <summary>Explicit legal typed extraction accepts only the registered member and known owner type.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ExplicitTypedExtractorsSupportCurrentCaptureArguments()
    {
        var argument = new ValidationArgument<int>();
        Expression<Func<KnownCapture, int>> captureMember = capture => capture.Index;
        var expected = Expression.MakeMemberAccess(Expression.Constant(new KnownCapture()), ((MemberExpression)captureMember.Body).Member);
        var source = Expression.Parameter(typeof(CatalogSource), "source");
        var exemplar = Expression.Lambda<Func<CatalogSource, string>>(
            Expression.Call(source, ((MethodCallExpression)IndexExpression().Body).Method, expected),
            source);
        var binding = ValidationExpressionBinding.Extract(expected, argument, Extract);
        var matcher = ValidationExpressionPattern.Create(exemplar, binding);
        var capture = new KnownCapture { Index = 1 };
        var actualMember = Expression.MakeMemberAccess(Expression.Constant(capture), expected.Member);
        var current = Expression.Lambda<Func<CatalogSource, string>>(Expression.Call(source, ((MethodCallExpression)exemplar.Body).Method, actualMember), source);
        await Assert.That(matcher(current, out var payload)).IsTrue();
        await Assert.That(payload!.Get(argument)).IsEqualTo(1);
        capture.Index = 0;
        await Assert.That(matcher(current, out payload)).IsTrue();
        await Assert.That(payload!.Get(argument)).IsEqualTo(0);

        bool Extract(Expression expression, out int value)
        {
            if (expression is MemberExpression member && Equals(member.Member, expected.Member) && member.Expression is ConstantExpression { Value: KnownCapture known })
            {
                value = known.Index;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>All competing schemas are matched before either factory executes.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task OverlapRejectsBeforeFactoriesRun()
    {
        using var registry = new ValidationPlanRegistry(MultipleEntries);
        Expression<Func<CatalogSource, string>> expression = source => source.Value;
        var matcher = ValidationExpressionPattern.Create(expression);
        var factories = 0;
        using var first = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, FirstValue, matcher, Factory);
        using var second = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, SecondValue, matcher, Factory);
        await Assert.That(() => registry.TryGetSelector(new(ValidationPlanRole.RuleValue, string.Empty), expression, out _)).Throws<InvalidOperationException>();
        await Assert.That(factories).IsEqualTo(0);
        await Assert.That(registry.TryGetSelector(new(ValidationPlanRole.RuleValue, SecondValue), expression, out _)).IsTrue();
        await Assert.That(factories).IsEqualTo(1);

        ValidationSelector<CatalogSource, string> Factory(ValidationArguments _)
        {
            factories++;
            return ValueSelector();
        }
    }

    /// <summary>Instance plans require exact expression identity, role and generic source slots.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task InstancePlansUseExactClosedSlotsAndReleaseCapacity()
    {
        using var registry = new ValidationPlanRegistry(1);
        Expression<Func<CatalogSource, string>> expression = source => source.Value;
        var registration = registry.RegisterSelector(ValidationPlanRole.RuleValue, expression, ValueSelector());
        await Assert.That(() => registry.RegisterSelector(ValidationPlanRole.RuleValue, expression, ValueSelector())).Throws<InvalidOperationException>();
        var request = new ValidationPlanRequest(ValidationPlanRole.RuleValue, string.Empty);
        await Assert.That(registry.TryGetSelector(request, expression, out _)).IsTrue();
        Expression<Func<CatalogSource, string>> fresh = source => source.Value;
        await Assert.That(registry.TryGetSelector(request, fresh, out _)).IsFalse();
        await Assert.That(registry.TryGetSelector(new(ValidationPlanRole.Property, string.Empty), expression, out _)).IsFalse();
        Expression<Func<object, string>> wrongSlot = source => source.ToString()!;
        await Assert.That(registry.TryGetSelector(request, wrongSlot, out _)).IsFalse();
        registration.Dispose();
        await Assert.That(registry.TryGetSelector(request, expression, out _)).IsFalse();
        using var replacement = registry.RegisterSelector(ValidationPlanRole.RuleValue, fresh, ValueSelector());
        await Assert.That(registry.TryGetSelector(request, fresh, out _)).IsTrue();
    }

    /// <summary>Attachment identity supports equal owners and a stable interface box.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task OwnerAssociationsUseReferenceIdentityAndStableBoxes()
    {
        using var registry = new ValidationPlanRegistry(MultipleEntries);
        var first = new EqualOwner();
        var second = new EqualOwner();
        using var lease = registry.Attach(first);
        await Assert.That(ReferenceEquals(ValidationPlanRegistry.TryGetAttached(first), registry)).IsTrue();
        await Assert.That(ValidationPlanRegistry.TryGetAttached(second)).IsNull();
        Box value = default;
        IBox box = value;
        using var boxedLease = registry.Attach(box);
        await Assert.That(ReferenceEquals(ValidationPlanRegistry.TryGetAttached(box), registry)).IsTrue();
        IBox fresh = value;
        await Assert.That(ValidationPlanRegistry.TryGetAttached(fresh)).IsNull();
    }

    /// <summary>Built-in attachments expose the sealed registry for direct typed selector and target dispatch.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task BuiltInAttachmentsResolveConcreteTypedSlotsAndReleaseOnlyTheirLease()
    {
        using var registry = new ValidationPlanRegistry(MultipleEntries);
        var source = new CatalogSource { Value = FirstValue };
        Expression<Func<CatalogSource, string>> expression = current => current.Value;
        Expression<Func<CatalogSource, CatalogState>> targetExpression = current => current.State;
        var selector = ValueSelector();
        var target = new ValidationTarget<CatalogSource, string>(current => new(
            () => ValidationTargetAccess<string>.Present(current, value => current.Value = value),
            []));
        using var ruleRegistration = registry.RegisterSelector(ValidationPlanRole.RuleValue, expression, selector);
        using var targetRegistration = registry.RegisterTarget(ValidationPlanRole.Target, targetExpression, target);
        using (registry.Attach(source))
        {
            // The helper's concrete return type locks this lookup contract at compile time.
            var attached = GetConcreteAttachment(source);
            await Assert.That(attached).IsSameReferenceAs(registry);
            await Assert.That(attached!.TryGetSelector(new(ValidationPlanRole.RuleValue, string.Empty), expression, out var selected)).IsTrue();
            await Assert.That(selected).IsSameReferenceAs(selector);
            await Assert.That(selected!.Bind(source).Read().Value).IsEqualTo(FirstValue);
            await Assert.That(attached.TryGetTarget<CatalogSource, CatalogState, string>(new(ValidationPlanRole.Target, string.Empty), targetExpression, out var selectedTarget)).IsTrue();
            await Assert.That(selectedTarget).IsSameReferenceAs(target);
            await Assert.That(attached.TryGetTarget<CatalogSource, CatalogState, int>(new(ValidationPlanRole.Target, string.Empty), targetExpression, out _)).IsFalse();
            using var values = new BehaviorSubject<string>(SecondValue);
            using var binding = selectedTarget!.Bind(source).Bind(values);
            await Assert.That(source.Value).IsEqualTo(SecondValue);
        }

        await Assert.That(ValidationPlanRegistry.TryGetAttached(source)).IsNull();
        await Assert.That(registry.TryGetSelector(new(ValidationPlanRole.RuleValue, string.Empty), expression, out _)).IsTrue();
    }

    /// <summary>Registry disposal does not root weakly associated owners.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    [SuppressMessage("Performance", "PSH1021:Avoid forced garbage collection", Justification = "Deterministic weak owner lifetime proof requires explicit collection in this regression.")]
    public async Task AttachedOwnersAreWeakAndOldLeasesCannotRemoveNewAssociations()
    {
        using var registry = new ValidationPlanRegistry(1);
        var owner = new EqualOwner();
        var oldLease = registry.Attach(owner);
        oldLease.Dispose();
        var currentLease = registry.Attach(owner);
        oldLease.Dispose();
        await Assert.That(ReferenceEquals(ValidationPlanRegistry.TryGetAttached(owner), registry)).IsTrue();
        currentLease.Dispose();
        var weakOwner = CreateWeakOwner(registry);

        // Explicit collection is necessary to prove that the scoped catalog does not retain these weak lifetimes.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Assert.That(weakOwner.TryGetTarget(out _)).IsFalse();
    }

    /// <summary>Unknown nodes and invalid/default contracts fail deterministically.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task InvalidContractsAndUnknownShapesFailClosed()
    {
        using var registry = new ValidationPlanRegistry(1);
        Expression<Func<CatalogSource, string>> expression = source => source.Value;
        var matcher = ValidationExpressionPattern.Create(expression);
        Expression<Func<CatalogSource, string>> method = source => source.ToString()!;
        await Assert.That(matcher(method, out _)).IsFalse();
        await Assert.That(() => registry.TryGetSelector(default, expression, out _)).Throws<ArgumentNullException>();
        await Assert.That(static () => new ValidationPlanRegistry(0)).Throws<ArgumentOutOfRangeException>();
        var argument = new ValidationArgument<int>();
        var payload = new ValidationArguments().Add(argument, 1);
        await Assert.That(() => payload.Add(argument, MultipleEntries)).Throws<InvalidOperationException>();
        await Assert.That(() => payload.Get(new ValidationArgument<int>())).Throws<KeyNotFoundException>();
    }

    /// <summary>Only explicit implicit-model registrations match null expressions.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ImplicitModelRequiresItsOwnTypedRegistration()
    {
        using var registry = new ValidationPlanRegistry(1);
        var selector = ValueSelector();
        await Assert.That(() => registry.RegisterSelector(ValidationPlanRole.Property, null, selector)).Throws<ArgumentNullException>();
        using var registration = registry.RegisterSelector(ValidationPlanRole.Model, null, selector);
        await Assert.That(registry.TryGetSelector<CatalogSource, string>(new(ValidationPlanRole.Model, string.Empty), null, out _)).IsTrue();
        await Assert.That(registry.TryGetSelector<CatalogSource, string>(new(ValidationPlanRole.Property, string.Empty), null, out _)).IsFalse();
    }

    /// <summary>A target's getter type and assignment output type remain distinct closed slots.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task TargetGetterAndAssignmentSlotsStayTyped()
    {
        using var registry = new ValidationPlanRegistry(1);
        Expression<Func<CatalogSource, CatalogState>> expression = source => source.State;
        var descriptor = new ValidationTarget<CatalogSource, string>(source => new(
            () => ValidationTargetAccess<string>.Present(source, value => source.Value = value),
            []));
        using var registration = registry.RegisterTarget(ValidationPlanRole.Target, expression, descriptor);
        var request = new ValidationPlanRequest(ValidationPlanRole.Target, string.Empty);
        await Assert.That(registry.TryGetTarget<CatalogSource, CatalogState, string>(request, expression, out var target)).IsTrue();
        var source = new CatalogSource();
        using var values = new BehaviorSubject<string>("assigned");
        using var binding = target!.Bind(source).Bind(values);
        await Assert.That(source.Value).IsEqualTo("assigned");
        await Assert.That(registry.TryGetTarget<CatalogSource, CatalogState, int>(request, expression, out _)).IsFalse();
    }

    /// <summary>Registry disposal clears retained factory captures even when its registration lease survives.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    [SuppressMessage("Performance", "PSH1021:Avoid forced garbage collection", Justification = "Explicit collection proves capture release while the registration lease remains alive.")]
    public async Task DisposalClearsRegistrationCaptures()
    {
        var registry = new ValidationPlanRegistry(1);
        var captured = CreateCapturedRegistration(registry);
        registry.Dispose();

        // Explicit collection is necessary to prove that the scoped catalog does not retain these weak lifetimes.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Assert.That(captured.Owner.TryGetTarget(out _)).IsFalse();
        captured.Lease.Dispose();
    }

    /// <summary>Creates a factory capture outside the caller's JIT lifetime.</summary>
    /// <param name="registry">The catalog owning the factory registration.</param>
    /// <returns>The captured owner identity and its registration lease.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<EqualOwner> Owner, IDisposable Lease) CreateCapturedRegistration(ValidationPlanRegistry registry)
    {
        var owner = new EqualOwner();
        Expression<Func<CatalogSource, string>> expression = source => source.Value;
        var lease = registry.RegisterSelectorPattern(ValidationPlanRole.RuleValue, "capture-lifetime", ValidationExpressionPattern.Create(expression), _ =>
        {
            GC.KeepAlive(owner);
            return ValueSelector();
        });
        return (new(owner), lease);
    }

    /// <summary>Requires the attached lookup to return a concrete registry through an implicit conversion.</summary>
    /// <param name="owner">The attached owner identity.</param>
    /// <returns>The sealed registry, or null when no association exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValidationPlanRegistry? GetConcreteAttachment(object owner) => ValidationPlanRegistry.TryGetAttached(owner);

    /// <summary>Creates direct member access for each current source.</summary>
    /// <returns>The typed selector.</returns>
    private static ValidationSelector<CatalogSource, string> ValueSelector() =>
        new(source => new ValidationAccessPlan<string>(() => ValidationRead<string>.Present(source.Value, []), [], ValidationObservationOptions<string>.Default));

    /// <summary>Creates indexed access with an invocation-local argument.</summary>
    /// <param name="index">The current typed argument.</param>
    /// <returns>The typed selector.</returns>
    private static ValidationSelector<CatalogSource, string> IndexSelector(int index) =>
        new(source => new ValidationAccessPlan<string>(() => ValidationRead<string>.Present(source[index], []), [], ValidationObservationOptions<string>.Default));

    /// <summary>Supplies opaque indexer member identity.</summary>
    /// <returns>The finite exemplar expression.</returns>
    private static Expression<Func<CatalogSource, string>> IndexExpression() => source => source[0];

    /// <summary>Creates the same compiler closure shape with a distinct owner on each call.</summary>
    /// <param name="capture">The current known captured value.</param>
    /// <returns>The expression with its distinct compiler closure.</returns>
    private static Expression<Func<CatalogSource, string>> CaptureExpression(KnownCapture capture) => source => source[capture.Index];

    /// <summary>Creates an attached owner outside the caller's JIT lifetime.</summary>
    /// <param name="registry">The catalog owning the weak association.</param>
    /// <returns>The weak owner identity.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<EqualOwner> CreateWeakOwner(ValidationPlanRegistry registry)
    {
        var owner = new EqualOwner();
        _ = registry.Attach(owner);
        return new(owner);
    }

    /// <summary>A value represented through its stable interface box.</summary>
    private readonly record struct Box : IBox
    {
        /// <inheritdoc/>
        public int Identity => 0;
    }

    /// <summary>An explicitly supported capture whose typed access is legal.</summary>
    private sealed class KnownCapture
    {
        /// <summary>Gets or sets the current index argument.</summary>
        public int Index { get; set; }
    }

    /// <summary>A source with member and indexer selectors.</summary>
    private sealed class CatalogSource
    {
        /// <summary>Gets or sets the typed property getter value.</summary>
        public CatalogState State { get; set; }

        /// <summary>Gets or sets the current selected text.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Gets the fixture value at the selected index.</summary>
        /// <param name="index">The typed index argument.</param>
        /// <returns>The current selected value.</returns>
        public string this[int index] => index == 0 ? "zero" : "one";
    }

    /// <summary>An owner whose equality deliberately ignores object identity.</summary>
    private sealed class EqualOwner
    {
        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is EqualOwner;

        /// <inheritdoc/>
        public override int GetHashCode() => 0;
    }
}
