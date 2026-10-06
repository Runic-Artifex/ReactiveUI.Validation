// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Disposable = ReactiveUI.Primitives.Disposables.Scope;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Regressions for typed observation completion, errors and capture ownership.</summary>
public class CapabilityObservationTests
{
    /// <summary>The stable observed fixture value.</summary>
    private const int ObservedValue = 7;

    /// <summary>The expected number of provider registrations in paired-provider tests.</summary>
    private const int TwoProviders = 2;

    /// <summary>The second structural path identity.</summary>
    private const int SecondPathKey = 2;

    /// <summary>The value returned by the second owner read.</summary>
    private const int SecondRead = 2;

    /// <summary>The total reads after independently invalidating one of two observations.</summary>
    private const int ThirdRead = 3;

    /// <summary>The fixture provider error message.</summary>
    private const string ProviderErrorMessage = "provider";

    /// <summary>The fixture cleanup error message.</summary>
    private const string CleanupErrorMessage = "cleanup";

    /// <summary>The getter failure operation name.</summary>
    private const string GetterOperation = "getter";

    /// <summary>The owner getter failure operation name.</summary>
    private const string OwnerOperation = "owner";

    /// <summary>The registration failure operation name.</summary>
    private const string SubscribeOperation = "subscribe";

    /// <summary>The comparer failure operation name.</summary>
    private const string ComparerOperation = "comparer";

    /// <summary>Zero-dependency streams complete after their one actual snapshot.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ZeroDependenciesCompleteAfterInitialValue()
    {
        var observer = new RecordingObserver();
        using var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, [])).Observe().Subscribe(observer);
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        await Assert.That(observer.Completions).IsEqualTo(1);
        await Assert.That(observer.Error).IsNull();
    }

    /// <summary>Suppressed missing owners do not suppress completion.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task MissingOwnerSuppressionStillCompletes()
    {
        var observer = new RecordingObserver();
        var options = new ValidationObservationOptions<int>(ValidationMissingOwnerPolicy.Suppress, null, EqualityComparer<int>.Default, false);
        using var subscription = Plan(static () => ValidationRead<int>.Missing([]), options: options).Observe().Subscribe(observer);
        await Assert.That(observer.Values.Count).IsEqualTo(0);
        await Assert.That(observer.Completions).IsEqualTo(1);
    }

    /// <summary>Providers completing during registration return tokens that are disposed before completion delivery.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task SynchronouslyCompletedProvidersReleaseReturnedTokens()
    {
        var first = new NotificationSource { CompleteDuringSubscribe = true };
        var second = new NotificationSource { CompleteDuringSubscribe = true };
        var cleanupAtCompletion = false;
        var observer = new RecordingObserver(completed: () => cleanupAtCompletion = first.Disposals == 1 && second.Disposals == 1);
        using var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [first.Dependency(), second.Dependency()]).Observe().Subscribe(observer);
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        await Assert.That(observer.Completions).IsEqualTo(1);
        await Assert.That(cleanupAtCompletion).IsTrue();
        await Assert.That(first.Listeners + second.Listeners).IsEqualTo(0);
    }

    /// <summary>Equality filtering never hides a provider's terminal completion.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task EqualFinalValueStillCompletes()
    {
        var source = new NotificationSource();
        var observer = new RecordingObserver();
        using var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [source.Dependency()]).Observe().Subscribe(observer);
        source.Invalidate();
        source.Complete();
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        await Assert.That(observer.Completions).IsEqualTo(1);
        await Assert.That(source.Disposals).IsEqualTo(1);
    }

    /// <summary>Producer failures notify the downstream observer after cleaning existing registrations.</summary>
    /// <param name="failure">The failing producer operation.</param>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    [Arguments(GetterOperation)]
    [Arguments(OwnerOperation)]
    [Arguments(SubscribeOperation)]
    [Arguments(ComparerOperation)]
    public async Task ProducerFailuresNotifyAfterCleanup(string failure)
    {
        var expected = new InvalidOperationException(failure);
        var source = new NotificationSource();
        var broken = new NotificationSource();
        var armed = failure is OwnerOperation or SubscribeOperation;
        var errorAfterCleanup = false;
        var observer = new RecordingObserver(error: _ => errorAfterCleanup = source.Listeners == 0);
        var dependency = ValidationDependency.Create(
            () => armed && failure == OwnerOperation ? throw expected : broken,
            (owner, downstream) => armed && failure == SubscribeOperation ? throw expected : owner.Subscribe(downstream));
        var comparer = EqualityComparer<int>.Create((left, right) => armed && failure == ComparerOperation ? throw expected : left == right, static value => value);
        var options = new ValidationObservationOptions<int>(ValidationMissingOwnerPolicy.DefaultValue, null, comparer, false);
        using var subscription = Plan(
            () => armed && failure == GetterOperation ? throw expected : ValidationRead<int>.Present(ObservedValue, []),
            [source.Dependency(), dependency],
            options).Observe().Subscribe(observer);
        armed = true;
        source.Invalidate();
        await Assert.That(ReferenceEquals(observer.Error, expected)).IsTrue();
        await Assert.That(errorAfterCleanup).IsTrue();
        await Assert.That(source.Disposals).IsEqualTo(1);
        await Assert.That(observer.Completions).IsEqualTo(0);
    }

    /// <summary>A provider's explicit error stops every owned registration before delivery.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ProviderErrorNotifiesAfterAllCleanup()
    {
        var first = new NotificationSource();
        var second = new NotificationSource();
        var expected = new InvalidOperationException(ProviderErrorMessage);
        var clean = false;
        var observer = new RecordingObserver(error: _ => clean = first.Listeners + second.Listeners == 0);
        using var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [first.Dependency(), second.Dependency()]).Observe().Subscribe(observer);
        second.Fail(expected);
        await Assert.That(ReferenceEquals(observer.Error, expected)).IsTrue();
        await Assert.That(clean).IsTrue();
        await Assert.That(first.Disposals + second.Disposals).IsEqualTo(TwoProviders);
    }

    /// <summary>Producer and cleanup failures remain ordered and are delivered through OnError.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ProducerAndCleanupFailuresAreReportedTogether()
    {
        var producerError = new InvalidOperationException("producer");
        var cleanupError = new InvalidOperationException(CleanupErrorMessage);
        var source = new NotificationSource { CleanupError = cleanupError };
        var observer = new RecordingObserver();
        var armed = false;
        using var subscription = Plan(() => armed ? throw producerError : ValidationRead<int>.Present(ObservedValue, []), [source.Dependency()]).Observe().Subscribe(observer);
        armed = true;
        source.Invalidate();
        var combined = observer.Error as AggregateException;
        await Assert.That(combined).IsNotNull();
        await Assert.That(ReferenceEquals(combined!.InnerExceptions[0], producerError)).IsTrue();
        await Assert.That(ReferenceEquals(((AggregateException)combined.InnerExceptions[1]).InnerExceptions[0], cleanupError)).IsTrue();
        await Assert.That(source.Listeners).IsEqualTo(0);
    }

    /// <summary>A downstream callback error cleans registrations and propagates without calling OnError.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task DownstreamOnNextFailurePropagatesAfterCleanup()
    {
        var source = new NotificationSource();
        var observer = new RecordingObserver(next: static _ => throw new InvalidOperationException("callback"));
        var plan = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [source.Dependency()]);
        await Assert.That(() => plan.Observe().Subscribe(observer)).Throws<InvalidOperationException>();
        await Assert.That(source.Listeners).IsEqualTo(0);
        await Assert.That(source.Disposals).IsEqualTo(1);
        await Assert.That(observer.Error).IsNull();
    }

    /// <summary>A provider must supply an owned token even if it sends synchronous callbacks.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task NullRegistrationTokenFailsAndCleansOtherProviders()
    {
        var source = new NotificationSource();
        var owner = new object();
        var invalid = ValidationDependency.Create(() => owner, static (_, _) => null!);
        var observer = new RecordingObserver();
        using var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [source.Dependency(), invalid]).Observe().Subscribe(observer);
        await Assert.That(observer.Error is ArgumentNullException).IsTrue();
        await Assert.That(source.Disposals).IsEqualTo(1);
        await Assert.That(source.Listeners).IsEqualTo(0);
    }

    /// <summary>Invalidation during registration schedules a fresh read without duplicate registration.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task SynchronousInvalidationBeforeTokenReturnIsCoalesced()
    {
        var source = new NotificationSource { InvalidateDuringSubscribe = true };
        var observer = new RecordingObserver();
        var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [source.Dependency()]).Observe().Subscribe(observer);
        await Assert.That(source.Subscriptions).IsEqualTo(1);
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        subscription.Dispose();
        await Assert.That(source.Disposals).IsEqualTo(1);
    }

    /// <summary>A synchronous provider error waits for token cleanup and preserves both failures.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task SynchronousProviderErrorWaitsForThrowingTokenCleanup()
    {
        var original = new InvalidOperationException(ProviderErrorMessage);
        var cleanup = new InvalidOperationException(CleanupErrorMessage);
        var source = new NotificationSource { ErrorDuringSubscribe = original, CleanupError = cleanup };
        var clean = false;
        var observer = new RecordingObserver(error: _ => clean = source.Disposals == 1 && source.Listeners == 0);
        using var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [source.Dependency()]).Observe().Subscribe(observer);
        var combined = observer.Error as AggregateException;
        await Assert.That(combined).IsNotNull();
        await Assert.That(ReferenceEquals(combined!.InnerExceptions[0], original)).IsTrue();
        await Assert.That(ReferenceEquals(((AggregateException)combined.InnerExceptions[1]).InnerExceptions[0], cleanup)).IsTrue();
        await Assert.That(clean).IsTrue();
        await Assert.That(observer.Values.Count).IsEqualTo(0);
    }

    /// <summary>A token failure after synchronous provider completion is delivered through OnError.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task SynchronousCompletionWithThrowingTokenFailsObservation()
    {
        var expected = new InvalidOperationException(CleanupErrorMessage);
        var source = new NotificationSource { CompleteDuringSubscribe = true, CleanupError = expected };
        var observer = new RecordingObserver();
        using var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [source.Dependency()]).Observe().Subscribe(observer);
        await Assert.That(ReferenceEquals(observer.Error, expected)).IsTrue();
        await Assert.That(source.Disposals).IsEqualTo(1);
        await Assert.That(observer.Completions).IsEqualTo(0);
    }

    /// <summary>A custom comparer that disposes cannot repopulate the snapshot or deliver again.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ComparerDisposalStopsDelivery()
    {
        var source = new NotificationSource();
        var observer = new RecordingObserver();
        IDisposable? subscription = null;
        var comparer = EqualityComparer<int>.Create(
            (_, _) =>
            {
                subscription!.Dispose();
                return false;
            },
            static value => value);
        var options = new ValidationObservationOptions<int>(ValidationMissingOwnerPolicy.DefaultValue, null, comparer, false);
        subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [source.Dependency()], options).Observe().Subscribe(observer);
        source.Invalidate();
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        await Assert.That(observer.Error).IsNull();
        await Assert.That(source.Disposals).IsEqualTo(1);
    }

    /// <summary>A structural comparer disposing on the first path stops comparison of later paths.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task StructuralPathComparerDisposalStopsFurtherComparison()
    {
        var source = new NotificationSource();
        var observer = new RecordingObserver();
        IDisposable? subscription = null;
        var comparisons = 0;
        var comparer = EqualityComparer<int>.Create(
            (_, _) =>
            {
                comparisons++;
                subscription!.Dispose();
                return true;
            },
            static value => value);
        ValidationPath[] paths = [ValidationPath.Structural("First", 1, comparer), ValidationPath.Structural("Second", SecondPathKey, comparer)];
        subscription = Plan(() => ValidationRead<int>.Present(ObservedValue, paths), [source.Dependency()]).Observe().Subscribe(observer);
        source.Invalidate();
        await Assert.That(comparisons).IsEqualTo(1);
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        await Assert.That(observer.Error).IsNull();
        await Assert.That(source.Disposals).IsEqualTo(1);
    }

    /// <summary>Presentation renames emit even when value and structural identity remain equal.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task StructuralPathDisplayRenameEmitsEqualValue()
    {
        var source = new NotificationSource();
        var observer = new RecordingObserver();
        var path = ValidationPath.Structural("Before", 1, EqualityComparer<int>.Default);
        using var subscription = Plan(() => ValidationRead<int>.Present(ObservedValue, [path]), [source.Dependency()]).Observe().Subscribe(observer);
        path = ValidationPath.Structural("After", 1, EqualityComparer<int>.Default);
        source.Invalidate();
        await Assert.That(observer.Values.SequenceEqual([ObservedValue, ObservedValue])).IsTrue();
        await Assert.That(observer.Reads[0].Paths[0].DisplayPath).IsEqualTo("Before");
        await Assert.That(observer.Reads[1].Paths[0].DisplayPath).IsEqualTo("After");
    }

    /// <summary>External disposal during an erroring reattachment rejects terminal delivery and releases the late token.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ReentrantDisposalDuringProviderErrorReleasesLateToken()
    {
        var first = new NotificationSource();
        var second = new NotificationSource { ErrorDuringSubscribe = new InvalidOperationException(ProviderErrorMessage) };
        var current = first;
        var observer = new RecordingObserver();
        var dependency = ValidationDependency.Create(() => current, static (owner, downstream) => owner.Subscribe(downstream));
        var subscription = Plan(static () => ValidationRead<int>.Present(ObservedValue, []), [dependency]).Observe().Subscribe(observer);
        second.AfterCallbacks = subscription.Dispose;
        current = second;
        first.Invalidate();
        await Assert.That(first.Disposals + second.Disposals).IsEqualTo(TwoProviders);
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        await Assert.That(observer.Error).IsNull();
        await Assert.That(observer.Completions).IsEqualTo(0);
    }

    /// <summary>Owner equality cannot prevent reference replacement, and old callbacks are ignored.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task EqualOwnersAreReplacedByReferenceIdentity()
    {
        var first = new NotificationSource();
        var second = new NotificationSource();
        var current = first;
        var dependency = ValidationDependency.Create(() => current, static (owner, observer) => owner.Subscribe(observer));
        var reads = 0;
        var observer = new RecordingObserver();
        using var subscription = Plan(
            () =>
            {
                reads++;
                return ValidationRead<int>.Present(reads, []);
            },
            [dependency]).Observe().Subscribe(observer);
        var stale = first.CurrentObserver!;
        current = second;
        first.Invalidate();
        stale.OnNext(default);
        await Assert.That(first.Disposals).IsEqualTo(1);
        await Assert.That(second.Subscriptions).IsEqualTo(1);
        await Assert.That(observer.Values.SequenceEqual([1, SecondRead])).IsTrue();
    }

    /// <summary>After-read registrations follow the exact row chosen by one effectful index read.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task AfterReadOwnersFollowOneEffectfulIndexSelection()
    {
        var root = new NotificationSource();
        var first = new NotificationSource();
        var second = new NotificationSource();
        NotificationSource[] rows = [first, second];
        NotificationSource? cachedRow = null;
        var indexReads = 0;
        var registrationsMatchDelivery = true;
        var selected = ValidationDependency.AfterRead(ValidationDependency.Create(
            () => cachedRow,
            static (owner, downstream) => owner.Subscribe(downstream)));
        var observer = new RecordingObserver(next: snapshot =>
        {
            registrationsMatchDelivery &= snapshot.Value == 1
                ? first.Listeners == 1 && second.Listeners == 0
                : second.Listeners == 1 && first.Listeners == 0;
        });
        using var subscription = Plan(
            () =>
            {
                indexReads++;
                var index = (indexReads - 1) % rows.Length;
                cachedRow = rows[index];
                return ValidationRead<int>.Present(index + 1, []);
            },
            [selected, root.Dependency()]).Observe().Subscribe(observer);
        var stale = first.CurrentObserver!;
        await Assert.That(indexReads).IsEqualTo(1);
        root.Invalidate();
        stale.OnNext(default);
        first.Invalidate();
        await Assert.That(indexReads).IsEqualTo(SecondRead);
        await Assert.That(observer.Values.SequenceEqual([1, SecondRead])).IsTrue();
        await Assert.That(registrationsMatchDelivery).IsTrue();
        await Assert.That(first.Disposals).IsEqualTo(1);
        await Assert.That(second.Listeners).IsEqualTo(1);
        await Assert.That(root.Subscriptions).IsEqualTo(1);
    }

    /// <summary>Getter and final-owner failures release root and previous row registrations before OnError.</summary>
    /// <param name="failure">The failing phase operation.</param>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    [Arguments(GetterOperation)]
    [Arguments(OwnerOperation)]
    [Arguments(SubscribeOperation)]
    public async Task AfterReadFailuresReleaseExistingRegistrations(string failure)
    {
        var root = new NotificationSource();
        var first = new NotificationSource();
        var second = new NotificationSource();
        NotificationSource? cachedRow = null;
        var armed = false;
        var expected = new InvalidOperationException(failure);
        var clean = false;
        var selected = ValidationDependency.AfterRead(ValidationDependency.Create(
            () => armed && failure == OwnerOperation ? throw expected : cachedRow,
            (owner, downstream) => armed && failure == SubscribeOperation ? throw expected : owner.Subscribe(downstream)));
        var observer = new RecordingObserver(error: _ => clean = root.Listeners + first.Listeners + second.Listeners == 0);
        using var subscription = Plan(
            () =>
            {
                if (armed && failure == GetterOperation)
                {
                    throw expected;
                }

                cachedRow = armed ? second : first;
                return ValidationRead<int>.Present(ObservedValue, []);
            },
            [root.Dependency(), selected]).Observe().Subscribe(observer);
        armed = true;
        root.Invalidate();
        await Assert.That(ReferenceEquals(observer.Error, expected)).IsTrue();
        await Assert.That(clean).IsTrue();
        await Assert.That(root.Disposals + first.Disposals).IsEqualTo(TwoProviders);
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
    }

    /// <summary>Synchronous after-read provider completion still delivers the cached initial snapshot and completes.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task AfterReadSynchronousCompletionPreservesInitialSnapshot()
    {
        var source = new NotificationSource { CompleteDuringSubscribe = true };
        NotificationSource? cachedOwner = null;
        var selected = ValidationDependency.AfterRead(ValidationDependency.Create(
            () => cachedOwner,
            static (owner, downstream) => owner.Subscribe(downstream)));
        var reads = 0;
        var observer = new RecordingObserver();
        using var subscription = Plan(
            () =>
            {
                reads++;
                cachedOwner = source;
                return ValidationRead<int>.Present(ObservedValue, []);
            },
            [selected]).Observe().Subscribe(observer);
        await Assert.That(reads).IsEqualTo(1);
        await Assert.That(observer.Values.SequenceEqual([ObservedValue])).IsTrue();
        await Assert.That(observer.Completions).IsEqualTo(1);
        await Assert.That(source.Disposals).IsEqualTo(1);
    }

    /// <summary>A reentrant second subscription cannot overwrite the first subscription's cached row owner.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ObservationFactoryIsolatesReentrantCachedOwners()
    {
        var first = new NotificationSource();
        var second = new NotificationSource();
        var factories = 0;
        var reads = 0;
        Action? reenter = null;
        var plan = ValidationAccessPlan.CreateObservation(() =>
        {
            factories++;
            var value = factories;
            NotificationSource? cachedOwner = null;
            var dependency = ValidationDependency.AfterRead(ValidationDependency.Create(
                () => cachedOwner,
                static (owner, downstream) => owner.Subscribe(downstream)));
            return Plan(
                () =>
                {
                    reads++;
                    cachedOwner = value == 1 ? first : second;
                    var nested = reenter;
                    reenter = null;
                    nested?.Invoke();
                    return ValidationRead<int>.Present(value, []);
                },
                [dependency]);
        });
        var firstObserver = new RecordingObserver();
        var secondObserver = new RecordingObserver();
        IDisposable? secondSubscription = null;
        var observable = plan.Observe();
        reenter = () => secondSubscription = observable.Subscribe(secondObserver);
        using var firstSubscription = observable.Subscribe(firstObserver);
        await Assert.That(factories).IsEqualTo(TwoProviders);
        await Assert.That(reads).IsEqualTo(TwoProviders);
        await Assert.That(firstObserver.Values.SequenceEqual([1])).IsTrue();
        await Assert.That(secondObserver.Values.SequenceEqual([SecondRead])).IsTrue();
        await Assert.That(first.Listeners).IsEqualTo(1);
        await Assert.That(second.Listeners).IsEqualTo(1);
        secondSubscription!.Dispose();
        first.Invalidate();
        second.Invalidate();
        await Assert.That(reads).IsEqualTo(ThirdRead);
        await Assert.That(first.Listeners).IsEqualTo(1);
        await Assert.That(second.Listeners).IsEqualTo(0);
        await Assert.That(firstObserver.Error).IsNull();
        await Assert.That(secondObserver.Error).IsNull();
    }

    /// <summary>Metadata subscriptions create independent caches and attach only metadata owners without reading values.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task MetadataObservationFactoryIsolatesCachesWithoutValueReads()
    {
        var first = new NotificationSource();
        var second = new NotificationSource();
        var factories = 0;
        var metadataReads = 0;
        var valueReads = 0;
        var plan = ValidationAccessPlan.CreateObservation(() =>
        {
            factories++;
            var factory = factories;
            NotificationSource? cachedOwner = null;
            var dependency = ValidationDependency.AfterRead(ValidationDependency.Create(
                () => cachedOwner,
                static (owner, downstream) => owner.Subscribe(downstream)));
            return new ValidationAccessPlan<int>(
                () =>
                {
                    valueReads++;
                    throw new InvalidOperationException("Metadata observation accessed the selected value.");
                },
                [],
                ValidationObservationOptions<int>.Default,
                () =>
                {
                    metadataReads++;
                    cachedOwner = factory == 1 ? first : second;
                    return [ValidationPath.Legacy(factory == 1 ? nameof(first) : nameof(second))];
                },
                [dependency]);
        });
        var observable = plan.ObservePaths();
        await Assert.That(factories).IsEqualTo(0);
        var firstObserver = new PathRecordingObserver();
        var secondObserver = new PathRecordingObserver();
        using var firstSubscription = observable.Subscribe(firstObserver);
        using var secondSubscription = observable.Subscribe(secondObserver);
        first.Invalidate();
        await Assert.That(factories).IsEqualTo(TwoProviders);
        await Assert.That(metadataReads).IsEqualTo(ThirdRead);
        await Assert.That(valueReads).IsEqualTo(0);
        await Assert.That(firstObserver.Paths.SequenceEqual([nameof(first), nameof(first)])).IsTrue();
        await Assert.That(secondObserver.Paths.SequenceEqual([nameof(second)])).IsTrue();
        await Assert.That(first.Listeners).IsEqualTo(1);
        await Assert.That(second.Listeners).IsEqualTo(1);
        await Assert.That(firstObserver.Error).IsNull();
        await Assert.That(secondObserver.Error).IsNull();
    }

    /// <summary>Retaining a disposed token does not retain caller getter, dependency or observer captures.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    [SuppressMessage(
        "Performance",
        "PSH1021:Avoid forced garbage collection",
        Justification = "This test requires deterministic collection to prove a retained disposed token does not root caller captures.")]
    public async Task DisposedSubscriptionReleasesCapturesWhileTokenSurvives()
    {
        var captured = CreateCapturedSubscription();
        captured.Subscription.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Assert.That(captured.Getter.TryGetTarget(out _)).IsFalse();
        await Assert.That(captured.Dependency.TryGetTarget(out _)).IsFalse();
        await Assert.That(captured.Observer.TryGetTarget(out _)).IsFalse();
        GC.KeepAlive(captured.Subscription);
    }

    /// <summary>Creates a typed plan with explicit defaults.</summary>
    /// <param name="read">The getter.</param>
    /// <param name="dependencies">The optional dependency adapters.</param>
    /// <param name="options">The optional observation policies.</param>
    /// <returns>The bound plan.</returns>
    private static ValidationAccessPlan<int> Plan(Func<ValidationRead<int>> read, ValidationDependency[]? dependencies = null, ValidationObservationOptions<int>? options = null) =>
        new(read, dependencies ?? [], options ?? ValidationObservationOptions<int>.Default);

    /// <summary>Creates captures in a separate frame so the GC assertion does not retain locals.</summary>
    /// <returns>The retained token and weak capture references.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IDisposable Subscription, WeakReference<object> Getter, WeakReference<object> Dependency, WeakReference<object> Observer) CreateCapturedSubscription()
    {
        var getterOwner = new object();
        var dependencyOwner = new NotificationSource();
        var observerOwner = new object();
        var observer = new RecordingObserver(next: _ => GC.KeepAlive(observerOwner));
        var dependency = dependencyOwner.Dependency();
        var subscription = Plan(
            () =>
            {
                GC.KeepAlive(getterOwner);
                return ValidationRead<int>.Present(ObservedValue, []);
            },
            [dependency]).Observe().Subscribe(observer);
        return (subscription, new(getterOwner), new(dependencyOwner), new(observerOwner));
    }

    /// <summary>A controllable provider whose value equality deliberately ignores identity.</summary>
    private sealed class NotificationSource
    {
        /// <summary>The active registration callback.</summary>
        private IObserver<ValidationInvalidation>? _observer;

        /// <summary>Gets or sets whether registration immediately completes.</summary>
        public bool CompleteDuringSubscribe { get; set; }

        /// <summary>Gets or sets whether registration immediately invalidates.</summary>
        public bool InvalidateDuringSubscribe { get; set; }

        /// <summary>Gets or sets an error sent before the registration token returns.</summary>
        public Exception? ErrorDuringSubscribe { get; set; }

        /// <summary>Gets or sets an external callback before the registration token returns.</summary>
        public Action? AfterCallbacks { get; set; }

        /// <summary>Gets or sets the optional disposal failure.</summary>
        public Exception? CleanupError { get; set; }

        /// <summary>Gets the total acquired registration count.</summary>
        public int Subscriptions { get; private set; }

        /// <summary>Gets the total disposed registration count.</summary>
        public int Disposals { get; private set; }

        /// <summary>Gets the active listener count.</summary>
        public int Listeners => _observer is null ? 0 : 1;

        /// <summary>Gets the current callback for stale-generation tests.</summary>
        public IObserver<ValidationInvalidation>? CurrentObserver => _observer;

        /// <summary>Creates an adapter borrowing this provider.</summary>
        /// <returns>The typed adapter.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValidationDependency Dependency() => ValidationDependency.Create(() => this, static (owner, observer) => owner.Subscribe(observer));

        /// <summary>Registers one observer and returns owned cleanup.</summary>
        /// <param name="observer">The provider observer.</param>
        /// <returns>The cleanup token.</returns>
        public IDisposable Subscribe(IObserver<ValidationInvalidation> observer)
        {
            Subscriptions++;
            _observer = observer;
            if (InvalidateDuringSubscribe)
            {
                observer.OnNext(default);
            }

            if (CompleteDuringSubscribe)
            {
                observer.OnCompleted();
            }

            if (ErrorDuringSubscribe is { } error)
            {
                observer.OnError(error);
            }

            AfterCallbacks?.Invoke();

            return Disposable.Create(this, static owner =>
            {
                owner.Disposals++;
                owner._observer = null;
                if (owner.CleanupError is { } cleanupError)
                {
                    throw cleanupError;
                }
            });
        }

        /// <summary>Invalidates the active observer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate() => _observer?.OnNext(default);

        /// <summary>Completes the active provider.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete() => _observer?.OnCompleted();

        /// <summary>Fails the active provider.</summary>
        /// <param name="error">The producer failure.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fail(Exception error) => _observer?.OnError(error);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is NotificationSource;

        /// <inheritdoc />
        public override int GetHashCode() => 0;
    }

    /// <summary>Records metadata paths and any terminal error without accessing selected values.</summary>
    private sealed class PathRecordingObserver : IObserver<IReadOnlyList<ValidationPath>>
    {
        /// <summary>Gets the delivered presentation paths.</summary>
        public List<string> Paths { get; } = [];

        /// <summary>Gets the terminal metadata error.</summary>
        public Exception? Error { get; private set; }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNext(IReadOnlyList<ValidationPath> value) => Paths.Add(value[0].DisplayPath);

        /// <inheritdoc />
        public void OnError(Exception error) => Error = error;

        /// <inheritdoc />
        public void OnCompleted()
        {
        }
    }

    /// <summary>A direct observer that records terminal notifications without reactive wrappers.</summary>
    /// <param name="next">The optional delivery action.</param>
    /// <param name="error">The optional error action.</param>
    /// <param name="completed">The optional completion action.</param>
    private sealed class RecordingObserver(Action<ValidationRead<int>>? next = null, Action<Exception>? error = null, Action? completed = null) : IObserver<ValidationRead<int>>
    {
        /// <summary>The optional terminal error callback.</summary>
        private readonly Action<Exception>? _onError = error;

        /// <summary>Gets the delivered actual values.</summary>
        public List<int> Values { get; } = [];

        /// <summary>Gets the complete delivered snapshots.</summary>
        public List<ValidationRead<int>> Reads { get; } = [];

        /// <summary>Gets the terminal error.</summary>
        public Exception? Error { get; private set; }

        /// <summary>Gets the number of completion notifications.</summary>
        public int Completions { get; private set; }

        /// <inheritdoc />
        public void OnNext(ValidationRead<int> value)
        {
            Values.Add(value.Value);
            Reads.Add(value);
            next?.Invoke(value);
        }

        /// <inheritdoc />
        public void OnError(Exception error)
        {
            Error = error;
            _onError?.Invoke(error);
        }

        /// <inheritdoc />
        public void OnCompleted()
        {
            Completions++;
            completed?.Invoke();
        }
    }
}
