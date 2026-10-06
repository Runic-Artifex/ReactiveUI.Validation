// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

using Disposable = ReactiveUI.Primitives.Disposables.Scope;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Verifies that each domain output refreshes the current assignment without inventing target notifications.</summary>
public class CapabilityWriteRefreshTests
{
    /// <summary>The first domain output.</summary>
    private const string FirstOutput = "first";

    /// <summary>The replacement domain output.</summary>
    private const string SecondOutput = "second";

    /// <summary>The latest cached output while the target is missing.</summary>
    private const string CachedOutput = "cached";

    /// <summary>The selected keyed slot display name.</summary>
    private const string SlotPath = "Output";

    /// <summary>The number of complete domain outputs in the reentrant fixture.</summary>
    private const int OutputCount = 2;

    /// <summary>The initial target watch seed plus two domain output resolutions.</summary>
    private const int SeedAndTwoOutputs = 3;

    /// <summary>The initial seed, two outputs and one genuine target invalidation.</summary>
    private const int SeedOutputsAndNotification = 4;

    /// <summary>Silent plain replacements wait for a domain output, while declared recovery notifications replay the latest cached output.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task PlainReplacementMissingRecoveryAndDisposalUseCurrentTarget()
    {
        var first = new TargetOwner();
        var second = new TargetOwner();
        var recovered = new TargetOwner();
        var root = new PlainRoot { Owner = first };
        var notifications = new NotificationSource();
        using var values = new Subject<string>();
        var plan = new ValidationWritePlan<string>(
            () => root.Owner is { } current
                ? ValidationTargetAccess<string>.Present(current, value => current.Output = value)
                : ValidationTargetAccess<string>.Missing(),
            [ValidationDependency.Create(() => notifications, static (owner, observer) => owner.Subscribe(observer))]);
        using (var binding = plan.Bind(values))
        {
            values.OnNext(FirstOutput);
            root.Owner = second;
            await Assert.That(second.Writes.Count).IsEqualTo(0);
            values.OnNext(SecondOutput);
            await Assert.That(first.Writes.SequenceEqual([FirstOutput])).IsTrue();
            await Assert.That(second.Writes.SequenceEqual([SecondOutput])).IsTrue();
            root.Owner = null;
            values.OnNext(FirstOutput);
            values.OnNext(CachedOutput);
            root.Owner = recovered;
            await Assert.That(recovered.Writes.Count).IsEqualTo(0);
            notifications.Invalidate();
            await Assert.That(recovered.Writes.SequenceEqual([CachedOutput])).IsTrue();
            await Assert.That(first.GetterReads + second.GetterReads + recovered.GetterReads).IsEqualTo(0);
        }

        values.OnNext(SecondOutput);
        notifications.Invalidate();
        await Assert.That(recovered.Writes.SequenceEqual([CachedOutput])).IsTrue();
        await Assert.That(notifications.Disposals).IsEqualTo(1);
    }

    /// <summary>After-read dependencies follow one sampled owner and key per resolution, and stale owner events cannot refresh the target.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task OutputRefreshPreservesAtomicAfterReadOwnerAndKey()
    {
        var first = new TargetOwner();
        var second = new TargetOwner();
        var root = new PlainRoot { Owner = first };
        TargetOwner? cachedOwner = null;
        var keyReads = 0;
        var registrationsMatchAssignment = true;
        var selected = ValidationDependency.AfterRead(ValidationDependency.Create(
            () => cachedOwner,
            static (owner, observer) => owner.Notifications.Subscribe(observer)));
        var plan = new ValidationWritePlan<string>(
            () =>
            {
                cachedOwner = root.Owner;
                var owner = cachedOwner!;
                keyReads++;
                var key = root.Key;
                return ValidationTargetAccess<string>.Present(
                    owner,
                    ValidationPath.Structural(SlotPath, key, EqualityComparer<int>.Default),
                    value =>
                    {
                        registrationsMatchAssignment &= owner.Notifications.Listeners == 1
                            && (ReferenceEquals(owner, first) ? second : first).Notifications.Listeners == 0;
                        owner.AssignSlot(key, value);
                    });
            },
            [selected]);
        using var values = new Subject<string>();
        using (var binding = plan.Bind(values))
        {
            await Assert.That(keyReads).IsEqualTo(1);
            var stale = first.Notifications.CurrentObserver!;
            values.OnNext(FirstOutput);
            root.Owner = second;
            root.Key = 1;
            values.OnNext(SecondOutput);
            await Assert.That(keyReads).IsEqualTo(SeedAndTwoOutputs);
            await Assert.That(first.Notifications.Disposals).IsEqualTo(1);
            await Assert.That(second.Notifications.Listeners).IsEqualTo(1);
            stale.OnNext(default);
            first.Notifications.Invalidate();
            await Assert.That(keyReads).IsEqualTo(SeedAndTwoOutputs);
            second.Notifications.Invalidate();
            await Assert.That(keyReads).IsEqualTo(SeedOutputsAndNotification);
            await Assert.That(first.Slots.Count).IsEqualTo(1);
            await Assert.That(second.Slots.Count).IsEqualTo(1);
            await Assert.That(first.Slots[0].Key).IsEqualTo(0);
            await Assert.That(second.Slots[0].Key).IsEqualTo(1);
            await Assert.That(second.Slots[0].Value).IsEqualTo(SecondOutput);
            await Assert.That(registrationsMatchAssignment).IsTrue();
        }

        await Assert.That(second.Notifications.Disposals).IsEqualTo(1);
    }

    /// <summary>A later resolve failure releases both admitted subscriptions before propagating the original exception.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task LaterResolveFailureReleasesAccessAndOutputTokens()
    {
        var owner = new TargetOwner();
        var notifications = new NotificationSource();
        var values = new CountedValues();
        var expected = new InvalidOperationException("target resolve failure");
        var fail = false;
        var plan = new ValidationWritePlan<string>(
            () => fail
                ? throw expected
                : ValidationTargetAccess<string>.Present(owner, value => owner.Output = value),
            [ValidationDependency.Create(() => notifications, static (source, observer) => source.Subscribe(observer))]);
        using var binding = plan.Bind(values);
        values.Emit(FirstOutput);
        fail = true;
        Exception? failure = null;
        try
        {
            values.Emit(SecondOutput);
        }
        catch (InvalidOperationException error)
        {
            failure = error;
        }

        await Assert.That(failure).IsSameReferenceAs(expected);
        await Assert.That(notifications.Listeners).IsEqualTo(0);
        await Assert.That(notifications.Disposals).IsEqualTo(1);
        await Assert.That(values.Disposals).IsEqualTo(1);
        values.Emit(CachedOutput);
        await Assert.That(owner.Writes.SequenceEqual([FirstOutput])).IsTrue();
    }

    /// <summary>A reentrant setter's source output is assigned to its fresh plain owner exactly once.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ReentrantOutputDrainsFreshOwnerWithoutDuplicatePresentation()
    {
        var first = new TargetOwner();
        var second = new TargetOwner();
        var root = new PlainRoot { Owner = first };
        using var values = new Subject<string>();
        var conversions = 0;
        var projected = ValidationRuntime.Project(values, value =>
        {
            conversions++;
            return value;
        });
        var reenter = true;
        first.DuringWrite = _ =>
        {
            if (reenter)
            {
                reenter = false;
                root.Owner = second;
                values.OnNext(SecondOutput);
            }
        };
        var plan = new ValidationWritePlan<string>(
            () =>
            {
                var current = root.Owner!;
                return ValidationTargetAccess<string>.Present(current, value => current.Output = value);
            },
            []);
        using var binding = plan.Bind(projected);
        values.OnNext(FirstOutput);
        await Assert.That(first.Writes.SequenceEqual([FirstOutput])).IsTrue();
        await Assert.That(second.Writes.SequenceEqual([SecondOutput])).IsTrue();
        await Assert.That(conversions).IsEqualTo(OutputCount);
        await Assert.That(first.GetterReads + second.GetterReads).IsEqualTo(0);
    }

    /// <summary>A root with no automatic owner or key notification.</summary>
    private sealed class PlainRoot
    {
        /// <summary>Gets or sets the silently replaceable target.</summary>
        public TargetOwner? Owner { get; set; }

        /// <summary>Gets or sets the silently replaceable slot argument.</summary>
        public int Key { get; set; }
    }

    /// <summary>A typed target whose ordinary leaf getter must never run during assignment.</summary>
    private sealed class TargetOwner
    {
        /// <summary>Gets the complete ordinary setter trace.</summary>
        public List<string> Writes { get; } = [];

        /// <summary>Gets the complete keyed assignment trace.</summary>
        public List<(int Key, string Value)> Slots { get; } = [];

        /// <summary>Gets the explicitly declared target owner notifications.</summary>
        public NotificationSource Notifications { get; } = new();

        /// <summary>Gets the number of forbidden ordinary leaf getter reads.</summary>
        public int GetterReads { get; private set; }

        /// <summary>Gets or sets a caller-owned reentrant setter callback.</summary>
        public Action<string>? DuringWrite { get; set; }

        /// <summary>Gets or sets the ordinary target; reads fail to expose accidental leaf inspection.</summary>
        public string Output
        {
            get
            {
                GetterReads++;
                throw new InvalidOperationException("An ordinary assignment must not inspect its selected leaf getter.");
            }

            set
            {
                Writes.Add(value);
                DuringWrite?.Invoke(value);
            }
        }

        /// <summary>Assigns the exact key sampled by the current access snapshot.</summary>
        /// <param name="key">The sampled slot argument.</param>
        /// <param name="value">The complete output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AssignSlot(int key, string value) => Slots.Add((key, value));
    }

    /// <summary>A counted provider retaining its observer to exercise deliberately late invalidations.</summary>
    private sealed class NotificationSource
    {
        /// <summary>Gets the retained observer, including after registration disposal.</summary>
        public IObserver<ValidationInvalidation>? CurrentObserver { get; private set; }

        /// <summary>Gets the current owned registration count.</summary>
        public int Listeners { get; private set; }

        /// <summary>Gets the number of released registrations.</summary>
        public int Disposals { get; private set; }

        /// <summary>Attaches an exact owned observer registration.</summary>
        /// <param name="observer">The invalidation observer.</param>
        /// <returns>The owned registration cleanup.</returns>
        public IDisposable Subscribe(IObserver<ValidationInvalidation> observer)
        {
            CurrentObserver = observer;
            Listeners++;
            return Disposable.Create(this, static owner =>
            {
                owner.Listeners--;
                owner.Disposals++;
            });
        }

        /// <summary>Emits an explicit provider invalidation, even after disposal.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate() => CurrentObserver?.OnNext(default);
    }

    /// <summary>A counted output producer retaining its observer to verify post-failure detachment.</summary>
    private sealed class CountedValues : IObservable<string>
    {
        /// <summary>The retained output observer.</summary>
        private IObserver<string>? _observer;

        /// <summary>Gets the number of released output registrations.</summary>
        public int Disposals { get; private set; }

        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<string> observer)
        {
            _observer = observer;
            return Disposable.Create(this, static owner => owner.Disposals++);
        }

        /// <summary>Emits a complete domain value even after the registration is released.</summary>
        /// <param name="value">The complete output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Emit(string value) => _observer?.OnNext(value);
    }
}
