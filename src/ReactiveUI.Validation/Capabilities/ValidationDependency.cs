// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A typed owner and registration adapter; no member discovery is performed.</summary>
[System.Diagnostics.DebuggerDisplay("ValidationDependency")]
public sealed class ValidationDependency
{
    /// <summary>The bound _owner contract.</summary>
    private readonly Func<object?> _owner;

    /// <summary>The bound _subscribe contract.</summary>
    private readonly Func<object, IObserver<ValidationInvalidation>, IDisposable> _subscribe;

    /// <summary>Initializes a new instance of the <see cref="ValidationDependency"/> class.</summary>
    /// <param name="owner">The typed owner getter.</param>
    /// <param name="subscribe">The typed registration adapter.</param>
    private ValidationDependency(Func<object?> owner, Func<object, IObserver<ValidationInvalidation>, IDisposable> subscribe)
    {
        _owner = owner;
        _subscribe = subscribe;
    }

    /// <summary>Gets whether the owner getter consumes the current read snapshot.</summary>
    internal bool RequiresAfterRead { get; private init; }

    /// <summary>Creates a dependency with an explicit typed notification adapter.</summary>
    /// <typeparam name="TOwner">The reference-owned notification source.</typeparam>
    /// <param name="currentOwner">Gets the current owner, or null.</param>
    /// <param name="subscribe">Attaches to that exact owner and returns owned cleanup.</param>
    /// <returns>The dependency.</returns>
    public static ValidationDependency Create<TOwner>(
        Func<TOwner?> currentOwner,
        Func<TOwner, IObserver<ValidationInvalidation>, IDisposable> subscribe)
        where TOwner : class
    {
        ArgumentExceptionHelper.ThrowIfNull(currentOwner);
        ArgumentExceptionHelper.ThrowIfNull(subscribe);
        return new(() => currentOwner(), (owner, observer) => subscribe((TOwner)owner, observer));
    }

    /// <summary>Creates an INPC adapter with exact-name and wildcard notification handling.</summary>
    /// <typeparam name="TOwner">The notifying source.</typeparam>
    /// <param name="currentOwner">Gets the current source.</param>
    /// <param name="propertyName">The ordinal notification name.</param>
    /// <returns>The dependency.</returns>
    public static ValidationDependency PropertyChanged<TOwner>(Func<TOwner?> currentOwner, string propertyName)
        where TOwner : class, INotifyPropertyChanged
    {
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(propertyName);
        return Create(currentOwner, (owner, observer) =>
        {
            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (string.IsNullOrEmpty(args.PropertyName) || string.Equals(propertyName, args.PropertyName, StringComparison.Ordinal))
                {
                    observer.OnNext(default);
                }
            };
            owner.PropertyChanged += handler;
            return Disposable.Create((owner, handler), static state => state.owner.PropertyChanged -= state.handler);
        });
    }

    /// <summary>Creates a collection adapter covering every collection change action.</summary>
    /// <typeparam name="TOwner">The notifying collection source.</typeparam>
    /// <param name="currentOwner">Gets the current collection.</param>
    /// <returns>The dependency.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValidationDependency CollectionChanged<TOwner>(Func<TOwner?> currentOwner)
        where TOwner : class, INotifyCollectionChanged =>
        Create(currentOwner, static (owner, observer) =>
        {
            NotifyCollectionChangedEventHandler handler = (_, _) => observer.OnNext(default);
            owner.CollectionChanged += handler;
            return Disposable.Create((owner, handler), static state => state.owner.CollectionChanged -= state.handler);
        });

    /// <summary>Defers a typed cached-owner adapter until the current value and metadata snapshot has been read.</summary>
    /// <param name="dependency">An adapter whose getter returns a typed owner cached by the read callback.</param>
    /// <returns>A snapshot-owner adapter attached before delivery of that same read snapshot.</returns>
    /// <remarks>Keep stable root dependencies before the read. The supplied getter must not reevaluate the selected expression.</remarks>
    public static ValidationDependency AfterRead(ValidationDependency dependency)
    {
        ArgumentExceptionHelper.ThrowIfNull(dependency);
        return new(dependency._owner, dependency._subscribe) { RequiresAfterRead = true };
    }

    /// <summary>Owns a writer receipt and relays queued assignment failures to its exact binding subscription.</summary>
    /// <param name="receipt">The independent receipt created for this subscription.</param>
    /// <returns>The owned failure dependency; disposal cancels pending writer work.</returns>
    public static ValidationDependency Writer(ValidationWriteReceipt receipt)
    {
        ArgumentExceptionHelper.ThrowIfNull(receipt);
        return Create(() => receipt, static (owner, observer) => owner.SubscribeFailure(observer));
    }

    /// <summary>Gets the current reference owner without discovering members.</summary>
    /// <returns>The current owner or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal object? GetOwner() => _owner();

    /// <summary>Registers with the exact current owner.</summary>
    /// <param name="owner">The current reference owner.</param>
    /// <param name="observer">The borrowed invalidation observer.</param>
    /// <returns>The owned registration token.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IDisposable Subscribe(object owner, IObserver<ValidationInvalidation> observer) => _subscribe(owner, observer);
}
