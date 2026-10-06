// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Reference-owned storage for value types, immutable roots and explicit invalidation.</summary>
/// <typeparam name="T">The stored value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationCell")]
public sealed class ValidationCell<T> : INotifyPropertyChanged
{
    /// <summary>The current owned storage value.</summary>
    private T _value;

    /// <summary>Initializes a new instance of the <see cref="ValidationCell{T}"/> class.</summary>
    /// <param name="initial">The initial owned value.</param>
    public ValidationCell(T initial) => _value = initial;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the current value; writes always invalidate, even when values compare equal.</summary>
    public T Value
    {
        get => _value;
        set
        {
            Set(value, null);
        }
    }

    /// <summary>Gets the increasing storage revision on its serialized owner.</summary>
    public long Revision => StoragePolicy.Revision;

    /// <summary>Gets the latest write origin, or null for ordinary external writes.</summary>
    public object? LastWriteOrigin => StoragePolicy.LastOrigin;

    /// <summary>Gets the explicit origin policy shared by writers of this storage.</summary>
    public ValidationStoragePolicy StoragePolicy { get; } = new();

    /// <summary>Replaces storage with an explicit write origin before notifying observers.</summary>
    /// <param name="value">The complete replacement storage.</param>
    /// <param name="origin">A stable writer identity, or null for external writes.</param>
    public void Set(T value, object? origin)
    {
        _value = value;
        StoragePolicy.RecordChange(origin);
        PropertyChanged?.Invoke(this, new(nameof(Value)));
    }

    /// <summary>Writes back a typed mutation of the current value on its serialized owner.</summary>
    /// <param name="update">Returns the replacement value; borrowed references must not escape.</param>
    public void Update(Func<T, T> update)
    {
        ArgumentExceptionHelper.ThrowIfNull(update);
        Value = update(_value);
    }

    /// <summary>Signals externally performed changes without discovering any member.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invalidate()
    {
        StoragePolicy.RecordChange(null);
        PropertyChanged?.Invoke(this, new(nameof(Value)));
    }
}
