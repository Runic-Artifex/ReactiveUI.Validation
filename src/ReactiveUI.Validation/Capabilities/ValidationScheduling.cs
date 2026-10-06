// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using ReactiveUI.Primitives.Extensions.Reactive;
#else
using ReactiveUI.Primitives.Extensions;
#endif

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Adapts input and presentation notifications to explicitly selected serialized owners.</summary>
public static class ValidationScheduling
{
    /// <summary>Queues source notifications in order on the explicit model owner before validation consumes them.</summary>
    /// <typeparam name="TValue">The source value.</typeparam>
    /// <param name="values">The input notifications.</param>
    /// <param name="ownerScheduler">The model-owner scheduler.</param>
    /// <returns>A cold stream whose subscription owns its scheduled delivery and upstream subscription.</returns>
    /// <remarks>
    /// Select a scheduler appropriate to the model owner. This adapts notifications, not arbitrary writes to a model or context.
    /// Reentrant notifications queue behind the current callback; disposal rejects queued delivery.
    /// </remarks>
    public static IObservable<TValue> SerializeInputs<TValue>(IObservable<TValue> values, IScheduler ownerScheduler)
    {
        ArgumentExceptionHelper.ThrowIfNull(values);
        ArgumentExceptionHelper.ThrowIfNull(ownerScheduler);
        return new ValidationOwnedObservable<TValue>(values.ObserveOnSafe(ownerScheduler));
    }

    /// <summary>Defers presentation on an explicit UI or provider scheduler while domain validity remains on its owner.</summary>
    /// <typeparam name="TValue">The presentation value.</typeparam>
    /// <param name="values">The synchronous domain or formatted-state stream.</param>
    /// <param name="presentationScheduler">The presentation scheduler.</param>
    /// <returns>A cold stream owning only scheduled delivery and its source subscription.</returns>
    public static IObservable<TValue> PresentOn<TValue>(IObservable<TValue> values, IScheduler presentationScheduler)
    {
        ArgumentExceptionHelper.ThrowIfNull(values);
        ArgumentExceptionHelper.ThrowIfNull(presentationScheduler);
        return new ValidationOwnedObservable<TValue>(values.ObserveOnSafe(presentationScheduler));
    }
}
