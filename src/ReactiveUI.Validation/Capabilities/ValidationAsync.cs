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

/// <summary>Creates owned latest-request validation streams with explicit pending, failure and completion policies.</summary>
public static class ValidationAsync
{
    /// <summary>Validates each value, cancelling and rejecting results from superseded requests.</summary>
    /// <typeparam name="TValue">The supplied value type.</typeparam>
    /// <param name="values">Values serialized on the model owner, including the initial value.</param>
    /// <param name="validate">The validator, whose cancellation token ends on replacement or disposal.</param>
    /// <param name="pendingState">The synchronous state while a request is pending.</param>
    /// <param name="ownerScheduler">The explicit scheduler delivering completion back to the model owner.</param>
    /// <param name="failureState">Optional error-to-state policy invoked on the owner scheduler. Without one, a current failure terminates the stream.</param>
    /// <returns>A cold observable owning only request cancellation and subscriptions.</returns>
    /// <remarks>
    /// Pending validity is chosen by the caller. Register the stream in a blocking or advisory context as appropriate.
    /// No model, source or context is disposed. Source completion waits for the latest request;
    /// disposal cancels it. A validator ignoring cancellation still cannot deliver an obsolete result.
    /// Completion scheduling does not serialize arbitrary concurrent model mutation; use <see cref="ValidationScheduling.SerializeInputs{TValue}"/> before registration when needed.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "SST2309:Avoid optional public parameters",
        Justification = "Null is the stable absence of a failure projection; no configurable default is embedded.")]
    public static IObservable<IValidationState> ForLatest<TValue>(
        IObservable<TValue> values,
        Func<TValue, CancellationToken, Task<IValidationState>> validate,
        IValidationState pendingState,
        IScheduler ownerScheduler,
        Func<Exception, IValidationState>? failureState = null)
    {
        ArgumentExceptionHelper.ThrowIfNull(values);
        ArgumentExceptionHelper.ThrowIfNull(validate);
        ArgumentExceptionHelper.ThrowIfNull(pendingState);
        ArgumentExceptionHelper.ThrowIfNull(ownerScheduler);
        return new ValidationOwnedObservable<IValidationState>(values.Select(value => Observable.FromAsync(token => Validate(value, validate, token))
                .ObserveOnSafe(ownerScheduler).Select(result => Project(result, failureState)).StartWith(pendingState))
            .SwitchTo());
    }

    /// <summary>Converts only current request failures according to the explicit caller policy.</summary>
    /// <typeparam name="TValue">The supplied value.</typeparam>
    /// <param name="value">The current input.</param>
    /// <param name="validate">The caller validator.</param>
    /// <param name="token">The owned cancellation token.</param>
    /// <returns>The complete state or failure to dispatch on the owner.</returns>
    /// <exception cref="InvalidOperationException">The validator returns null; the failure is returned for owner dispatch.</exception>
    private static async Task<(IValidationState? State, Exception? Error)> Validate<TValue>(TValue value, Func<TValue, CancellationToken, Task<IValidationState>> validate, CancellationToken token)
    {
        try
        {
            return (await validate(value, token).ConfigureAwait(false) ?? throw new InvalidOperationException("The asynchronous validator returned null."), null);
        }
        catch (Exception error)
        {
            return (null, error);
        }
    }

    /// <summary>Applies failure policy only after owner scheduling and obsolete-request rejection.</summary>
    /// <param name="result">The completed request.</param>
    /// <param name="failureState">The explicit owner-side error policy.</param>
    /// <returns>The complete current state.</returns>
    /// <exception cref="InvalidOperationException">The failure policy returns null.</exception>
    private static IValidationState Project((IValidationState? State, Exception? Error) result, Func<Exception, IValidationState>? failureState) =>
        result.State ?? (failureState is null
            ? throw result.Error!
            : failureState(result.Error!) ?? throw new InvalidOperationException("The asynchronous failure policy returned null."));
}
