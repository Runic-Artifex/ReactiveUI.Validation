// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using Disposable = ReactiveUI.Primitives.Disposables.Scope;

#if REACTIVE_SHIM
using System.Reactive.Linq;
#else
using Observable = ReactiveUI.Primitives.Signals.Signal;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Typed access, metadata and stable storage write-back regressions.</summary>
public class CapabilityAccessTests
{
    /// <summary>The named fixture integer 2.</summary>
    private const int FixtureTwo = 2;

    /// <summary>The named fixture integer 3.</summary>
    private const int FixtureThree = 3;

    /// <summary>The named fixture integer 4.</summary>
    private const int FixtureFour = 4;

    /// <summary>The named fixture integer 5.</summary>
    private const int FixtureFive = 5;

    /// <summary>The named fixture integer 7.</summary>
    private const int FixtureSeven = 7;

    /// <summary>The named fixture integer 8.</summary>
    private const int FixtureEight = 8;

    /// <summary>The named fixture integer 9.</summary>
    private const int FixtureNine = 9;

    /// <summary>The named fixture text Error.</summary>
    private const string ErrorPath = "Error";

    /// <summary>The named fixture text first.</summary>
    private const string FirstOutput = "first";

    /// <summary>The named fixture text second.</summary>
    private const string SecondOutput = "second";

    /// <summary>The named fixture text LOWER.</summary>
    private const string NormalizedLower = "LOWER";

    /// <summary>The named fixture text initial.</summary>
    private const string InitialOutput = "initial";

    /// <summary>The named fixture text Errors.</summary>
    private const string ErrorsPath = "Errors";

    /// <summary>The named fixture text cached.</summary>
    private const string CachedOutput = "cached";

    /// <summary>The named fixture text Marker.</summary>
    private const string MarkerPath = "Marker";

    /// <summary>A new output is assigned without invoking optional structural comparers.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ForcedOutputsDoNotInvokeSlotComparers()
    {
        var owner = new object();
        var comparer = new ThrowingComparer();
        List<string> assigned = [];
        var plan = new ValidationWritePlan<string>(
            () => ValidationTargetAccess<string>.Present(
                owner,
                ValidationPath.Structural(ErrorPath, 1, comparer),
                assigned.Add),
            []);
        using var values = new Subject<string>();
        using var binding = plan.Bind(values);
        values.OnNext(FirstOutput);
        values.OnNext(SecondOutput);
        await Assert.That(assigned).IsEquivalentTo([FirstOutput, SecondOutput]);
        await Assert.That(comparer.Calls).IsEqualTo(0);
    }

    /// <summary>Failure in either cleanup cannot hide the other cleanup or repeat ownership release.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task BindingDisposalPreservesBothCleanupFailures()
    {
        var owner = new object();
        var accessError = new InvalidOperationException("access cleanup");
        var valueError = new InvalidOperationException("value cleanup");
        var attempts = 0;
        var plan = new ValidationWritePlan<string>(
            () => ValidationTargetAccess<string>.Present(owner, static _ => { }),
            [ValidationDependency.Create(() => owner, (_, _) => Disposable.Create(accessError, error =>
            {
                attempts++;
                throw error;
            }))]);
        var values = Observable.Create<string>(_ => Disposable.Create(valueError, error =>
        {
            attempts++;
            throw error;
        }));
        var binding = plan.Bind(values);
        AggregateException? failure = null;
        try
        {
            binding.Dispose();
        }
        catch (AggregateException error)
        {
            failure = error;
        }

        await Assert.That(failure).IsNotNull();
        await Assert.That(((AggregateException)failure!.InnerExceptions[0]).InnerExceptions[0]).IsSameReferenceAs(accessError);
        await Assert.That(failure.InnerExceptions[1]).IsSameReferenceAs(valueError);
        binding.Dispose();
        await Assert.That(attempts).IsEqualTo(FixtureTwo);
    }

    /// <summary>Ordinary targets write normalized setters without reading a throwing leaf getter or looping on notifications.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task OrdinaryTargetsNeverReadTheirSelectedGetter()
    {
        var owner = new NormalizingOwner();
        using var values = new Subject<string>();
        var plan = new ValidationWritePlan<string>(
            () => ValidationTargetAccess<string>.Present(owner, value => owner.Output = value),
            [ValidationDependency.PropertyChanged(() => owner, nameof(owner.Output))]);
        using var binding = plan.Bind(values);
        values.OnNext("lower");
        await Assert.That(owner.Stored).IsEqualTo(NormalizedLower);
        await Assert.That(owner.Writes).IsEqualTo(1);
    }

    /// <summary>Owned lens origins distinguish normalization and reentrant external replacement without repeated writes.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task NormalizedLensesUseOwnedWriteOrigins()
    {
        var cell = new ValidationCell<Storage>(new(1, string.Empty));
        var writes = 0;
        var lens = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            (storage, output) =>
            {
                writes++;
                return storage with { Error = output.ToUpperInvariant() };
            });
        using var values = new Subject<string>();
        using var binding = lens.Target().Bind(cell).Bind(values);
        values.OnNext("lower");
        await Assert.That(cell.Value.Error).IsEqualTo(NormalizedLower);
        await Assert.That(writes).IsEqualTo(1);
        await Assert.That(cell.LastWriteOrigin).IsNotNull();
        cell.Value = new(FixtureTwo, "external");
        await Assert.That(cell.Value).IsEqualTo(new(FixtureTwo, NormalizedLower));
        await Assert.That(writes).IsEqualTo(FixtureTwo);
        await Assert.That(cell.Revision).IsEqualTo(FixtureThree);
    }

    /// <summary>Explicit enclosing normalization receipts compare complete storage without selected getter access.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ExplicitStorageReceiptsDeclareNormalization()
    {
        var current = new Storage(1, string.Empty);
        var receipt = new ValidationStorageReceipt<Storage>(() => current, EqualityComparer<Storage>.Default);
        await Assert.That(receipt.IsCurrent()).IsFalse();
        var expected = new Storage(1, "UPPER");
        receipt.Capture(expected);
        current = expected;
        await Assert.That(receipt.IsCurrent()).IsTrue();
        current = new(FixtureTwo, "external replacement");
        await Assert.That(receipt.IsCurrent()).IsFalse();
    }

    /// <summary>Metadata observation uses only its own dependency owners and never value branch gates.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task MetadataDependenciesDoNotEvaluateValueOwners()
    {
        var plan = new ValidationAccessPlan<string>(
            static () => throw new InvalidOperationException("leaf getter"),
            [ValidationDependency.Create<object>(static () => throw new InvalidOperationException("value branch gate"), static (_, _) => Disposable.Empty)],
            ValidationObservationOptions<string>.Default,
            static () => [ValidationPath.Legacy("Name")],
            []);
        List<string> paths = [];
        using var subscription = plan.ObservePaths().Subscribe(snapshot => paths.Add(snapshot[0].DisplayPath));
        await Assert.That(paths).IsEquivalentTo(["Name"]);
    }

    /// <summary>Presentation metadata renames emit while structural matching and state identity remain stable.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task SameStructuralIdentityCanRenameDisplayMetadata()
    {
        using var changes = new Subject<ValidationInvalidation>();
        var owner = new object();
        var display = "A";
        var selector = new ValidationSelector<object, int>(_ => new(
            () => ValidationRead<int>.Present(1, [ValidationPath.Structural(display, 1, EqualityComparer<int>.Default)]),
            [ValidationDependency.Create(() => owner, (_, observer) => changes.Subscribe(observer))],
            ValidationObservationOptions<int>.Default));
        var state = new ValidationState(false, "same");
        using var component = new SelectorValidation<object, int>(owner, selector, _ => state);
        List<string> names = [];
        List<IValidationState> states = [];
        using var paths = component.ValidationPathsChanged.Subscribe(snapshot => names.Add(snapshot[0].DisplayPath));
        using var values = component.ValidationStatusChange.Subscribe(states.Add);
        display = "B";
        changes.OnNext(default);
        await Assert.That(names).IsEquivalentTo(["A", "B"]);
        await Assert.That(component.ContainsPropertyName("A", false)).IsFalse();
        await Assert.That(component.ContainsPropertyName("B", true)).IsTrue();
        await Assert.That(states.Count).IsEqualTo(FixtureTwo);
        await Assert.That(states[0]).IsSameReferenceAs(state);
        await Assert.That(states[1]).IsSameReferenceAs(state);
    }

    /// <summary>Inferred factories support anonymous sources and selected values without naming their types.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task AnonymousTypesUseInferredFactories()
    {
        var source = new { Value = FixtureSeven };
        var selector = ValidationSelector.Create(source, model => ValidationAccessPlan.Create(
            () => ValidationRead.Present(new { model.Value, Label = "private shape" }, []),
            []));
        await Assert.That(selector.Bind(source).Read().Value.Value).IsEqualTo(FixtureSeven);
        await Assert.That(selector.Bind(source).Read().Value.Label).IsEqualTo("private shape");
        var output = new { Error = InitialOutput };
        var cell = ValidationCell.Create(output);
        var target = ValidationTarget.Create(cell, storage => ValidationWritePlan.Create(
            () => ValidationTargetAccess.Present(storage, output, value => storage.Value = value),
            []));
        using var binding = target.Bind(cell).Bind(Observable.Return(new { Error = "assigned" }));
        await Assert.That(cell.Value.Error).IsEqualTo("assigned");
    }

    /// <summary>Suppressing missing values keeps property metadata without invoking the validation projection.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task SuppressedMissingOwnersKeepPropertyMembership()
    {
        var calls = 0;
        var selector = new ValidationSelector<object, string?>(static _ => new(
            static () => ValidationRead<string?>.Missing([ValidationPath.Legacy("Editor.Name")]),
            [],
            new(ValidationMissingOwnerPolicy.Suppress, null, EqualityComparer<string?>.Default, false)));
        using var component = new SelectorValidation<object, string?>(new(), selector, _ =>
        {
            calls++;
            return ValidationState.Valid;
        });
        await Assert.That(component.PropertyCount).IsEqualTo(1);
        await Assert.That(component.ContainsPropertyName("Editor.Name", true)).IsTrue();
        await Assert.That(component.IsValid).IsFalse();
        await Assert.That(calls).IsEqualTo(0);
    }

    /// <summary>All three direct typed constructor counterparts preserve message behavior and metadata.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task DirectTypedComponentsPreserveConstructorSemantics()
    {
        var model = new IndexedOwner();
        var selector = new ValidationSelector<IndexedOwner, string?>(static _ => new(
            static () => ValidationRead<string?>.Present("value", [ValidationPath.Legacy(ErrorsPath)]),
            [],
            ValidationObservationOptions<string?>.Default));
        using var constant = new BasePropertyValidation<IndexedOwner, string>(model, selector, static _ => false, "constant");
        using var dynamic = new BasePropertyValidation<IndexedOwner, string>(model, selector, static _ => false, static value => value!);
        using var complete = new BasePropertyValidation<IndexedOwner, string>(model, selector, static _ => false, static (value, valid) => $"{value}:{valid}");
        await Assert.That(constant.Text!.ToSingleLine()).IsEqualTo("constant");
        await Assert.That(dynamic.Text!.ToSingleLine()).IsEqualTo("value");
        await Assert.That(complete.Text!.ToSingleLine()).IsEqualTo("value:False");
        IPropertyValidationComponent metadata = complete;
        await Assert.That(metadata.ContainsPropertyName(ErrorsPath, true)).IsTrue();
        await Assert.That(((IValidationPathComponent)metadata).ValidationPaths[0].DisplayPath).IsEqualTo(ErrorsPath);
    }

    /// <summary>Structural equality is symmetric and cannot alias legacy display metadata.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task StructuralDomainsAreSymmetricAndLegacyPathsValidated()
    {
        var ordinal = ValidationPath.Structural("Name", "name", StringComparer.Ordinal);
        var insensitive = ValidationPath.Structural("Name", "name", StringComparer.OrdinalIgnoreCase);
        var equal = ValidationPath.Structural("OtherDisplay", "name", StringComparer.Ordinal);
        await Assert.That(ordinal.Equals(insensitive)).IsFalse();
        await Assert.That(insensitive.Equals(ordinal)).IsFalse();
        await Assert.That(ordinal.Equals(equal)).IsTrue();
        await Assert.That(ordinal.GetHashCode()).IsEqualTo(equal.GetHashCode());
        await Assert.That(ValidationPath.Legacy("Name").IsLegacy).IsTrue();
        await Assert.That(ordinal.IsLegacy).IsFalse();
        await Assert.That(ordinal.Equals(ValidationPath.Legacy("name"))).IsFalse();
        await Assert.That(static () => ValidationPath.Legacy("Editor..Name")).Throws<ArgumentException>();
    }

    /// <summary>Read snapshots copy caller metadata and retain owner-presence distinctions.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ReadSnapshotsCopyPathsAndDistinguishLeafNull()
    {
        var original = ValidationPath.Legacy("Name");
        var paths = new[] { original };
        var present = ValidationRead<string?>.Present(null, paths);
        paths[0] = ValidationPath.Legacy("Other");
        await Assert.That(present.Paths[0]).IsSameReferenceAs(original);
        await Assert.That(present.HasOwner).IsTrue();
        await Assert.That(default(ValidationRead<string?>).HasOwner).IsFalse();
        await Assert.That(present.Equals(ValidationRead<string?>.Missing([original]))).IsFalse();
    }

    /// <summary>Two structural slots with one display name are not exclusive.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task StructuralDuplicateDisplayPathsRemainNonexclusive()
    {
        var paths = new[]
        {
            ValidationPath.Structural(ErrorsPath, 0, EqualityComparer<int>.Default),
            ValidationPath.Structural(ErrorsPath, 1, EqualityComparer<int>.Default),
        };
        var selector = new ValidationSelector<object, int>(_ => new(() => ValidationRead<int>.Present(1, paths), [], ValidationObservationOptions<int>.Default));
        using var rule = new SelectorValidation<object, int>(new(), selector, static _ => ValidationState.Valid);
        await Assert.That(rule.PropertyCount).IsEqualTo(FixtureTwo);
        await Assert.That(rule.ContainsPropertyName(ErrorsPath, true)).IsFalse();
        await Assert.That(rule.ContainsPath(paths[0], true)).IsFalse();
        await Assert.That(rule.ContainsPath(paths[0], false)).IsTrue();
    }

    /// <summary>Disposal within validation cannot repopulate component metadata or state.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ProjectionMayDisposeItsComponent()
    {
        using var source = new Subject<ValidationInvalidation>();
        var owner = new object();
        var selector = new ValidationSelector<object, int>(_ => new(
            static () => ValidationRead<int>.Present(1, [ValidationPath.Legacy("Name")]),
            [ValidationDependency.Create(() => owner, (_, observer) => source.Subscribe(observer))],
            ValidationObservationOptions<int>.Default));
        SelectorValidation<object, int>? rule = null;
        rule = new(owner, selector, _ =>
        {
            rule!.Dispose();
            return ValidationState.Valid;
        });
        using (rule)
        {
            await Assert.That(rule.IsValid).IsFalse();
            await Assert.That(rule.PropertyCount).IsEqualTo(0);
            await Assert.That(source.HasObservers).IsFalse();
        }
    }

    /// <summary>Changing an index replays cached output once while unrelated invalidation does not assign again.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task IndexedTargetsReplayOnlyChangedSlotsAndAdoptFreshAssignments()
    {
        var source = new IndexedOwner();
        using var values = new Subject<string>();
        var writes = 0;
        var plan = new ValidationWritePlan<string>(
            () =>
        {
            var index = source.Index;
            return ValidationTargetAccess<string>.Present(source, ValidationPath.Structural(ErrorsPath, index, EqualityComparer<int>.Default), value =>
            {
                writes++;
                source.Errors[index] = value;
            });
        },
            [ValidationDependency.PropertyChanged(() => source, nameof(source.Index))]);
        using var binding = plan.Bind(values);
        values.OnNext(CachedOutput);
        source.Index = 1;
        source.Invalidate();
        await Assert.That(source.Errors[0]).IsEqualTo(CachedOutput);
        await Assert.That(source.Errors[1]).IsEqualTo(CachedOutput);
        await Assert.That(writes).IsEqualTo(FixtureTwo);
        values.OnNext("fresh");
        await Assert.That(source.Errors[1]).IsEqualTo("fresh");
        await Assert.That(source.Errors[0]).IsEqualTo(CachedOutput);
    }

    /// <summary>External and reentrant struct storage replacements replay output without looping on owned writes.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task LensReplaysExternalStorageAndReentrantReplacement()
    {
        var cell = new ValidationCell<Storage>(new(1, string.Empty));
        var writes = 0;
        var lens = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            (storage, value) =>
            {
                writes++;
                return storage with { Error = value };
            });
        using var values = new Subject<string>();
        using var binding = lens.Target().Bind(cell).Bind(values);
        values.OnNext(CachedOutput);
        await Assert.That(writes).IsEqualTo(1);
        cell.Value = new(FixtureTwo, "reset");
        await Assert.That(cell.Value).IsEqualTo(new(FixtureTwo, CachedOutput));
        await Assert.That(writes).IsEqualTo(FixtureTwo);
        var replace = true;
        cell.PropertyChanged += (_, _) =>
        {
            if (replace && cell.Value.Error == "next")
            {
                replace = false;
                cell.Value = new(FixtureThree, "reentrant reset");
            }
        };
        values.OnNext("next");
        await Assert.That(cell.Value).IsEqualTo(new(FixtureThree, "next"));
        await Assert.That(writes).IsEqualTo(FixtureFour);
    }

    /// <summary>Direct external mutation during replacement construction fails before stale copy-back.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task LensRejectsDirectExternalMutationDuringConstruction()
    {
        var cell = new ValidationCell<Storage>(new(1, InitialOutput));
        var replace = true;
        var writes = 0;
        var lens = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            (storage, value) =>
            {
                writes++;
                if (replace)
                {
                    replace = false;
                    cell.Value = new(FixtureTwo, "reentrant");
                }

                return storage with { Error = value };
            });
        using var values = new Subject<string>();
        using var binding = lens.Target().Bind(cell).Bind(values);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => values.OnNext(CachedOutput)));
        await Assert.That(cell.Value).IsEqualTo(new(FixtureTwo, "reentrant"));
        await Assert.That(writes).IsEqualTo(1);
    }

    /// <summary>Nested peer writes serialize current copy-back without repeating transformations.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task LensRetriesDifferentSlotPeerButPreservesLaterSameSlotPeer()
    {
        var cell = new ValidationCell<Storage>(new(0, InitialOutput));
        using var markers = new Subject<int>();
        using var errors = new Subject<string>();
        using var competingErrors = new Subject<string>();
        var triggerDifferent = false;
        var triggerSame = false;
        var marker = new ValidationLens<Storage, int>(
            static storage => ValidationRead<int>.Present(storage.Marker, [ValidationPath.Legacy(MarkerPath)]),
            static (storage, value) => storage with { Marker = value });
        var error = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            (storage, value) =>
            {
                if (triggerDifferent)
                {
                    markers.OnNext(FixtureNine);
                }

                if (triggerSame)
                {
                    triggerSame = false;
                    competingErrors.OnNext("later peer");
                }

                return storage with { Error = value };
            });
        var competing = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            static (storage, value) => storage with { Error = value });
        using var markerBinding = marker.Target().Bind(cell).Bind(markers);
        using var errorBinding = error.Target().Bind(cell).Bind(errors);
        using var competingBinding = competing.Target().Bind(cell).Bind(competingErrors);
        markers.OnNext(1);
        errors.OnNext(FirstOutput);
        triggerDifferent = true;
        errors.OnNext("next");
        await Assert.That(cell.Value).IsEqualTo(new(FixtureNine, "next"));
        triggerDifferent = false;
        triggerSame = true;
        errors.OnNext("older constructing output");
        await Assert.That(cell.Value).IsEqualTo(new(FixtureNine, "later peer"));
    }

    /// <summary>Peer writers of one slot preserve the latest writer without replay loops.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task CellPeerSameSlotUsesLatestWriter()
    {
        var cell = new ValidationCell<Storage>(new(FixtureSeven, InitialOutput));
        var writes = 0;
        var lens = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            (storage, value) =>
            {
                writes++;
                return storage with { Error = value };
            });
        using var first = new Subject<string>();
        using var second = new Subject<string>();
        var sharedPlan = lens.Target().Bind(cell);
        using var bindingA = sharedPlan.Bind(first);
        using var bindingB = sharedPlan.Bind(second);
        first.OnNext(FirstOutput);
        second.OnNext(SecondOutput);
        await Assert.That(writes).IsEqualTo(FixtureTwo);
        await Assert.That(cell.Value).IsEqualTo(new(FixtureSeven, SecondOutput));
        cell.Value = new(FixtureEight, "external");
        await Assert.That(writes).IsEqualTo(FixtureFour);
        await Assert.That(cell.Value).IsEqualTo(new(FixtureEight, SecondOutput));
        first.OnNext("new first");
        await Assert.That(writes).IsEqualTo(FixtureFive);
        await Assert.That(cell.Value.Error).IsEqualTo("new first");
        await Assert.That(cell.StoragePolicy.ExternalRevision).IsEqualTo(1);
    }

    /// <summary>Different structural slots copy back current peer fields once per external epoch.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task CellPeerDifferentSlotsPreserveCurrentStorage()
    {
        var cell = new ValidationCell<Storage>(new(0, InitialOutput));
        var markerWrites = 0;
        var errorWrites = 0;
        var marker = new ValidationLens<Storage, int>(
            static storage => ValidationRead<int>.Present(storage.Marker, [ValidationPath.Legacy(MarkerPath)]),
            (storage, value) =>
            {
                markerWrites++;
                return storage with { Marker = value };
            });
        var error = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            (storage, value) =>
            {
                errorWrites++;
                return storage with { Error = value.ToUpperInvariant() };
            });
        using var markers = new Subject<int>();
        using var errors = new Subject<string>();
        using var bindingA = marker.Target().Bind(cell).Bind(markers);
        using var bindingB = error.Target().Bind(cell).Bind(errors);
        markers.OnNext(FixtureNine);
        errors.OnNext(CachedOutput);
        cell.Value = new(1, "reset");
        await Assert.That(cell.Value).IsEqualTo(new(FixtureNine, "CACHED"));
        await Assert.That(markerWrites).IsEqualTo(FixtureTwo);
        await Assert.That(errorWrites).IsEqualTo(FixtureTwo);
        cell.Invalidate();
        await Assert.That(markerWrites).IsEqualTo(FixtureThree);
        await Assert.That(errorWrites).IsEqualTo(FixtureThree);
    }

    /// <summary>Explicit caller storage policy recognizes only its own peers and records external epochs before notification.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task ExplicitStoragePolicyRejectsForeignOrigins()
    {
        var policy = new ValidationStoragePolicy();
        var first = policy.CreateReceipt(ValidationPath.Legacy("State.Error"));
        var second = policy.CreateReceipt();
        first.Capture();
        policy.RecordChange(first.Origin);
        second.Capture();
        policy.RecordChange(second.Origin);
        await Assert.That(first.IsCurrent()).IsTrue();
        await Assert.That(second.IsCurrent()).IsTrue();
        policy.RecordChange(new ValidationStoragePolicy().CreateReceipt().Origin);
        await Assert.That(first.IsCurrent()).IsFalse();
        await Assert.That(policy.ExternalRevision).IsEqualTo(1);
        first.Capture();
        policy.RecordChange(first.Origin);
        await Assert.That(first.IsCurrent()).IsTrue();
        await Assert.That(second.IsCurrent()).IsFalse();
        await Assert.That(policy.Revision).IsEqualTo(FixtureFour);
    }

    /// <summary>Coalesced peer writes retain latest admission order and release disposed queued writers.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task StorageExecutorCoalescesLatestAdmissionAndCancelsDisposedWriter()
    {
        var policy = new ValidationStoragePolicy();
        using var active = policy.CreateReceipt();
        using var first = policy.CreateReceipt();
        using var second = policy.CreateReceipt();
        var canceled = policy.CreateReceipt();
        List<string> writes = [];
        policy.Execute(active.Origin, () =>
        {
            policy.Execute(first.Origin, () => writes.Add(FirstOutput));
            policy.Execute(second.Origin, () => writes.Add(SecondOutput));
            policy.Execute(first.Origin, () => writes.Add(CachedOutput));
            policy.Execute(canceled.Origin, () => writes.Add(InitialOutput));
            canceled.Dispose();
        });
        await Assert.That(writes.SequenceEqual([SecondOutput, CachedOutput])).IsTrue();
        await Assert.That(first.Origin.SlotIdentity).IsNull();
    }

    /// <summary>Disposal inside an active transformation prevents subsequent copy-back.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task LensActiveDisposalStopsCommit()
    {
        var cell = new ValidationCell<Storage>(new(1, InitialOutput));
        Action? dispose = null;
        var lens = new ValidationLens<Storage, string>(
            static storage => ValidationRead<string>.Present(storage.Error, [ValidationPath.Legacy(ErrorPath)]),
            (storage, value) =>
            {
                dispose?.Invoke();
                return storage with { Error = value };
            });
        using var values = new Subject<string>();
        using var binding = lens.Target().Bind(cell).Bind(values);
        dispose = binding.Dispose;
        values.OnNext(CachedOutput);
        await Assert.That(cell.Value.Error).IsEqualTo(InitialOutput);
        await Assert.That(cell.Revision).IsEqualTo(0);
    }

    /// <summary>Queued failing and discarded writers release their own source subscriptions before draining failure.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task QueuedWriterFailureReleasesAllAdmittedBindings()
    {
        var cell = new CountedStorageOwner();
        using var first = new Subject<string>();
        using var second = new Subject<string>();
        using var third = new Subject<string>();
        var failure = new InvalidOperationException("queued transform failure");
        var firstWrites = 0;
        var thirdWrites = 0;
        var firstPlan = CountedWritePlan(
            cell,
            (storage, value) =>
            {
                firstWrites++;
                second.OnNext(SecondOutput);
                third.OnNext(CachedOutput);
                return storage with { Error = value };
            });
        var secondPlan = CountedWritePlan(
            cell,
            (_, _) => throw failure);
        var thirdPlan = CountedWritePlan(
            cell,
            (storage, value) =>
            {
                thirdWrites++;
                return storage with { Error = value };
            });
        using var firstBinding = firstPlan.Bind(first);
        using var secondBinding = secondPlan.Bind(second);
        using var thirdBinding = thirdPlan.Bind(third);
        await Assert.That(cell.Registrations).IsEqualTo(FixtureThree);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => first.OnNext(FirstOutput)));
        await Assert.That(thrown).IsSameReferenceAs(failure);
        await Assert.That(first.HasObservers).IsFalse();
        await Assert.That(second.HasObservers).IsFalse();
        await Assert.That(third.HasObservers).IsFalse();
        await Assert.That(cell.Registrations).IsEqualTo(0);
        cell.SetExternal(new(1, InitialOutput));
        second.OnNext(CachedOutput);
        await Assert.That(firstWrites).IsEqualTo(1);
        await Assert.That(thirdWrites).IsEqualTo(0);
    }

    /// <summary>Retained origins from disposed writers count as external replacements.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task CellDisposedOriginAdvancesExternalEpoch()
    {
        var cell = new ValidationCell<Storage>(new(0, InitialOutput));
        ValidationWriteOrigin origin;
        using (var receipt = cell.StoragePolicy.CreateReceipt())
        {
            origin = receipt.Origin;
            cell.Set(new(1, FirstOutput), origin);
        }

        cell.Set(new(FixtureTwo, SecondOutput), origin);
        await Assert.That(cell.Value).IsEqualTo(new(FixtureTwo, SecondOutput));
        await Assert.That(cell.StoragePolicy.ExternalRevision).IsEqualTo(1);
        await Assert.That(cell.Revision).IsEqualTo(FixtureTwo);
    }

    /// <summary>Creates independently owned typed writers with counted source and receipt registrations.</summary>
    /// <param name="owner">The explicitly owned complete storage.</param>
    /// <param name="transform">The replacement construction contract.</param>
    /// <returns>The cold write plan.</returns>
    private static ValidationWritePlan<string> CountedWritePlan(CountedStorageOwner owner, Func<Storage, string, Storage> transform) => new(() =>
    {
        var receipt = owner.Policy.CreateReceipt(ValidationPath.Legacy(ErrorPath));
        return new(
            () => ValidationTargetAccess<string>.Present(
                owner,
                value => owner.Policy.Write(receipt, () => owner.Value, value, transform, owner.Set),
                _ => receipt.IsCurrent()),
            [ValidationDependency.PropertyChanged(() => owner, nameof(owner.Value)), ValidationDependency.Writer(receipt)]);
    });

    /// <summary>Immutable value storage containing unrelated data.</summary>
    /// <param name="Marker">The unrelated value that must survive write-back.</param>
    /// <param name="Error">The presentation target.</param>
    private readonly record struct Storage(int Marker, string Error);

    /// <summary>An authored origin-aware INPC adapter with exact registration counts.</summary>
    private sealed class CountedStorageOwner : INotifyPropertyChanged
    {
        /// <summary>The registered storage listeners.</summary>
        private PropertyChangedEventHandler? _changed;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _changed += value;
                Registrations++;
            }

            remove
            {
                _changed -= value;
                Registrations--;
            }
        }

        /// <summary>Gets the complete current storage.</summary>
        public Storage Value { get; private set; } = new(0, InitialOutput);

        /// <summary>Gets the explicit writer admission policy.</summary>
        public ValidationStoragePolicy Policy { get; } = new();

        /// <summary>Gets the number of currently owned INPC registrations.</summary>
        public int Registrations { get; private set; }

        /// <summary>Commits complete peer storage before publishing its recorded origin.</summary>
        /// <param name="value">The replacement storage.</param>
        /// <param name="origin">The policy-owned peer writer identity.</param>
        public void Set(Storage value, ValidationWriteOrigin origin)
        {
            Value = value;
            Policy.RecordChange(origin);
            _changed?.Invoke(this, new(nameof(Value)));
        }

        /// <summary>Commits an externally authored replacement epoch.</summary>
        /// <param name="value">The complete replacement storage.</param>
        public void SetExternal(Storage value)
        {
            Value = value;
            Policy.RecordChange(null);
            _changed?.Invoke(this, new(nameof(Value)));
        }
    }

    /// <summary>Reference-owned indexed target metadata.</summary>
    private sealed class IndexedOwner : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets the target storage.</summary>
        public string[] Errors { get; } = [string.Empty, string.Empty];

        /// <summary>Gets or sets the current index.</summary>
        public int Index
        {
            get;
            set
            {
                field = value;
                Invalidate();
            }
        }

        /// <summary>Raises a current-index invalidation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Index)));
    }

    /// <summary>A setter-only target with observable normalization.</summary>
    private sealed class NormalizingOwner : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets the actual normalized storage.</summary>
        public string Stored { get; private set; } = string.Empty;

        /// <summary>Gets the number of completed assignments.</summary>
        public int Writes { get; private set; }

        /// <summary>Gets or sets normalized output; reading this selected leaf is intentionally unsupported.</summary>
        public string Output
        {
            get => throw new InvalidOperationException("The selected leaf getter must never be read.");
            set
            {
                Stored = value.ToUpperInvariant();
                Writes++;
                PropertyChanged?.Invoke(this, new(nameof(Output)));
            }
        }
    }

    /// <summary>A comparer whose execution is observable and intentionally unsupported.</summary>
    private sealed class ThrowingComparer : IEqualityComparer<int>
    {
        /// <summary>Gets the number of attempted comparisons.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public bool Equals(int x, int y)
        {
            Calls++;
            throw new InvalidOperationException("A forced output must not compare target slots.");
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(int obj) => obj;
    }
}
