// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Declares typed normalization equivalence for enclosing storage setters without reading a selected leaf.</summary>
/// <typeparam name="TStorage">The complete enclosing storage type.</typeparam>
[System.Diagnostics.DebuggerDisplay("HasReceipt = {HasReceipt}")]
public sealed class ValidationStorageReceipt<TStorage>
{
    /// <summary>The typed current storage reader.</summary>
    private readonly Func<TStorage> _current;

    /// <summary>The caller-declared normalization equivalence.</summary>
    private readonly IEqualityComparer<TStorage> _equivalence;

    /// <summary>The expected complete normalized storage.</summary>
    private TStorage _expected = default!;

    /// <summary>Initializes a new instance of the <see cref="ValidationStorageReceipt{TStorage}"/> class.</summary>
    /// <param name="current">Reads complete enclosing storage through legal typed access.</param>
    /// <param name="equivalence">Compares actual storage with declared expected normalized storage.</param>
    public ValidationStorageReceipt(Func<TStorage> current, IEqualityComparer<TStorage> equivalence)
    {
        ArgumentExceptionHelper.ThrowIfNull(current);
        ArgumentExceptionHelper.ThrowIfNull(equivalence);
        _current = current;
        _equivalence = equivalence;
    }

    /// <summary>Gets whether a write has declared its expected normalized storage.</summary>
    public bool HasReceipt { get; private set; }

    /// <summary>Captures expected storage before calling its final setter.</summary>
    /// <param name="expected">The complete expected result, including declared setter normalization.</param>
    public void Capture(TStorage expected)
    {
        _expected = expected;
        HasReceipt = true;
    }

    /// <summary>Checks current enclosing storage using the explicitly supplied equivalence.</summary>
    /// <returns>Whether current storage belongs to the declared write result.</returns>
    public bool IsCurrent() => HasReceipt && _equivalence.Equals(_current(), _expected);
}
