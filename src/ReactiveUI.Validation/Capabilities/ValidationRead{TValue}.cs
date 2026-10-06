// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>A single typed value and metadata snapshot, distinguishing a missing parent from a null leaf.</summary>
/// <typeparam name="TValue">The value type.</typeparam>
[System.Diagnostics.DebuggerDisplay("ValidationRead")]
public readonly struct ValidationRead<TValue> : IEquatable<ValidationRead<TValue>>
{
    /// <summary>The bound _paths contract.</summary>
    private readonly IReadOnlyList<ValidationPath>? _paths;

    /// <summary>Initializes a new instance of the <see cref="ValidationRead{TValue}"/> struct.</summary>
    /// <param name="hasOwner">Whether the selected owner exists.</param>
    /// <param name="value">The selected value.</param>
    /// <param name="paths">The current metadata identities.</param>
    private ValidationRead(bool hasOwner, TValue value, IReadOnlyList<ValidationPath> paths)
    {
        ArgumentExceptionHelper.ThrowIfNull(paths);
        HasOwner = hasOwner;
        Value = value;
        var copy = new ValidationPath[paths.Count];
        for (var index = 0; index < copy.Length; index++)
        {
            ArgumentExceptionHelper.ThrowIfNull(paths[index]);
            copy[index] = paths[index];
        }

        _paths = Array.AsReadOnly(copy);
    }

    /// <summary>Gets whether the selected owner exists; a present null leaf still returns true.</summary>
    public bool HasOwner { get; }

    /// <summary>Gets the typed value.</summary>
    public TValue Value { get; }

    /// <summary>Gets the complete metadata snapshot.</summary>
    public IReadOnlyList<ValidationPath> Paths => _paths ?? Array.Empty<ValidationPath>();

    /// <summary>Creates an available value, including an available null leaf.</summary>
    /// <param name="value">The current value.</param>
    /// <param name="paths">Its current identities.</param>
    /// <returns>The immutable snapshot.</returns>
    public static ValidationRead<TValue> Present(TValue value, IReadOnlyList<ValidationPath> paths) => new(true, value, paths);

    /// <summary>Creates an unavailable parent snapshot.</summary>
    /// <param name="paths">Its current identities.</param>
    /// <returns>The missing-owner snapshot.</returns>
    public static ValidationRead<TValue> Missing(IReadOnlyList<ValidationPath> paths) => new(false, default!, paths);

    /// <summary>Compares complete snapshots using the default typed value comparer.</summary>
    /// <param name="left">The first snapshot.</param>
    /// <param name="right">The second snapshot.</param>
    /// <returns>Whether the snapshots are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ValidationRead<TValue> left, ValidationRead<TValue> right) => left.Equals(right);

    /// <summary>Compares complete snapshots for inequality.</summary>
    /// <param name="left">The first snapshot.</param>
    /// <param name="right">The second snapshot.</param>
    /// <returns>Whether the snapshots differ.</returns>
    public static bool operator !=(ValidationRead<TValue> left, ValidationRead<TValue> right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(ValidationRead<TValue> other)
    {
        if (HasOwner != other.HasOwner || !EqualityComparer<TValue>.Default.Equals(Value, other.Value) || Paths.Count != other.Paths.Count)
        {
            return false;
        }

        for (var index = 0; index < Paths.Count; index++)
        {
            if (!Paths[index].Equals(other.Paths[index]) || !StringComparer.Ordinal.Equals(Paths[index].DisplayPath, other.Paths[index].DisplayPath))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ValidationRead<TValue> read && Equals(read);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(HasOwner);
        hash.Add(Value, EqualityComparer<TValue>.Default);
        foreach (var path in Paths)
        {
            hash.Add(path);
            hash.Add(path.DisplayPath, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>Replaces the delivered value while preserving missing-owner information and metadata.</summary>
    /// <param name="value">The explicit fallback or default value.</param>
    /// <returns>The normalized snapshot.</returns>
    internal ValidationRead<TValue> WithValue(TValue value) => new(HasOwner, value, Paths);
}
