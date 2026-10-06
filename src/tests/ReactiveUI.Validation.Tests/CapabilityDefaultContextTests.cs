// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Tests;
#else
namespace ReactiveUI.Validation.Tests;
#endif

/// <summary>Implicit typed context fallback and explicit notification-provider regressions.</summary>
public class CapabilityDefaultContextTests
{
    /// <summary>The bounded context descriptor capacity.</summary>
    private const int RegistryCapacity = 2;

    /// <summary>A model without INPC supports the original interface constraint and snapshot fallback.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task NonNotifyingModelReadsDefaultContextSnapshot()
    {
        using var first = new ValidationContext(ImmediateSequencer.Instance);
        using var second = new ValidationContext(ImmediateSequencer.Instance);
        using var model = new ManualModel(first);
        var selector = ValidationRuntime.DefaultContext<ManualModel>();
        List<IValidationContext?> seen = [];
        using var subscription = selector.Bind(model).Observe().Subscribe(read => seen.Add(read.Value));
        model.Replace(second);
        await Assert.That(seen.Count).IsEqualTo(1);
        await Assert.That(seen[0]).IsSameReferenceAs(first);
        await Assert.That(selector.Bind(model).Read().Value).IsSameReferenceAs(second);
    }

    /// <summary>A view-owner catalog can replace equal-valued contexts using its explicitly supplied event adapter.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task OwnerCatalogFollowsDefaultContextByReference()
    {
        using var first = new EqualContext();
        using var second = new EqualContext();
        using var model = new ManualModel(first);
        using var registry = new ValidationPlanRegistry(RegistryCapacity);
        var owner = new object();
        using var attachment = registry.Attach(owner);
        var descriptor = new ValidationSelector<ManualModel, IValidationContext?>(source => new(
            () => ValidationRead<IValidationContext?>.Present(source.ValidationContext, []),
            [ValidationDependency.Create(() => source, static (current, observer) => current.Changed.Subscribe(observer))],
            ValidationObservationOptions<IValidationContext?>.Default));
        using var registration = registry.RegisterSelector(ValidationPlanRole.DefaultContext, null, descriptor);
        List<IValidationContext?> seen = [];
        using var subscription = ValidationRuntime.DefaultContext<ManualModel>(owner).Bind(model).Observe().Subscribe(read => seen.Add(read.Value));
        model.Replace(second);
        await Assert.That(seen.Count).IsEqualTo(RegistryCapacity);
        await Assert.That(seen[0]).IsSameReferenceAs(first);
        await Assert.That(seen[1]).IsSameReferenceAs(second);
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.Run(() => registry.RegisterSelector(ValidationPlanRole.Context, null, descriptor)));
    }

    /// <summary>Registered model handoff uses identity even when model equality claims replacements are equal.</summary>
    /// <returns>The asynchronous assertion task.</returns>
    [Test]
    public async Task RegisteredEqualModelsStillReplaceCurrentOwner()
    {
        var first = new EqualModel();
        var second = new EqualModel();
        using var view = new ManualView { ViewModel = first };
        using var registry = new ValidationPlanRegistry(RegistryCapacity);
        using var attachment = registry.Attach(view);
        var descriptor = new ValidationSelector<ManualView, EqualModel?>(source => new(
            () => ValidationRead<EqualModel?>.Present(source.ViewModel, []),
            [ValidationDependency.Create(() => source, static (current, observer) => current.Changed.Subscribe(observer))],
            ValidationObservationOptions<EqualModel?>.Default));
        using var registration = registry.RegisterSelector(ValidationPlanRole.Model, null, descriptor);
        List<EqualModel?> seen = [];
        using var subscription = ValidationRuntime.ObserveModels<ManualView, EqualModel>(view).Subscribe(seen.Add);
        view.ViewModel = second;
        view.Changed.OnNext(default);
        await Assert.That(seen.Count).IsEqualTo(RegistryCapacity);
        await Assert.That(seen[0]).IsSameReferenceAs(first);
        await Assert.That(seen[1]).IsSameReferenceAs(second);
    }

    /// <summary>The original validatable interface without a notification-interface constraint.</summary>
    /// <param name="context">The borrowed initial context.</param>
    private sealed class ManualModel(IValidationContext context) : IValidatableViewModel, IDisposable
    {
        /// <inheritdoc/>
        public IValidationContext ValidationContext { get; private set; } = context;

        /// <summary>Gets the explicit caller-supplied replacement notification adapter.</summary>
        public Subject<ValidationInvalidation> Changed { get; } = new();

        /// <summary>Replaces borrowed context and publishes the manual event.</summary>
        /// <param name="replacement">The borrowed replacement context.</param>
        public void Replace(IValidationContext replacement)
        {
            ValidationContext = replacement;
            Changed.OnNext(default);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Changed.Dispose();
    }

    /// <summary>A context whose ordinary equality deliberately cannot identify replacement.</summary>
    private sealed class EqualContext() : ValidationContext(ImmediateSequencer.Instance)
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj) => obj is EqualContext;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => 0;
    }

    /// <summary>A stable view with explicit notification adapters and no INPC requirement.</summary>
    private sealed class ManualView : IViewFor<EqualModel>, IDisposable
    {
        /// <inheritdoc/>
        public EqualModel? ViewModel { get; set; }

        /// <inheritdoc/>
        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value as EqualModel;
        }

        /// <summary>Gets the explicitly supplied view-model notification stream.</summary>
        public Subject<ValidationInvalidation> Changed { get; } = new();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Changed.Dispose();
    }

    /// <summary>A model with intentionally non-identifying equality.</summary>
    private sealed class EqualModel
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj) => obj is EqualModel;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => 0;
    }
}
