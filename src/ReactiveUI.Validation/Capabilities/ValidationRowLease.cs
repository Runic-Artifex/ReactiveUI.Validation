// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Transfers a row validation stream and an optional explicitly owned helper lifetime to a collection adapter.</summary>
/// <remarks>The stream is borrowed; only its subscription and the supplied lifetime are disposed.</remarks>
[System.Diagnostics.DebuggerDisplay("ValidationRowLease: {States}")]
public sealed class ValidationRowLease : IDisposable
{
    /// <summary>The explicitly transferred helper lifetime.</summary>
    private IDisposable? _lifetime;

    /// <summary>Initializes a new instance of the <see cref="ValidationRowLease"/> class.</summary>
    /// <param name="states">The row states, including an initial state when available.</param>
    /// <param name="lifetime">An optional explicitly transferred helper or other row resource.</param>
    [SuppressMessage("Design", "SST2309:Avoid optional public parameters", Justification = "Null is the fixed absence of an explicitly transferred lifetime.")]
    public ValidationRowLease(IObservable<IValidationState> states, IDisposable? lifetime = null)
    {
        ArgumentExceptionHelper.ThrowIfNull(states);
        States = states;
        _lifetime = lifetime;
    }

    /// <summary>Gets the borrowed row-state stream.</summary>
    public IObservable<IValidationState> States { get; }

    /// <summary>Disposes the explicitly transferred helper lifetime at most once.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _lifetime, null)?.Dispose();
}
