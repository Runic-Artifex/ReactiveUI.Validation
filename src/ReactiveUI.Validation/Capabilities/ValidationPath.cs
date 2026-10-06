// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>An ordinal presentation path with an optional typed structural identity.</summary>
[System.Diagnostics.DebuggerDisplay("ValidationPath")]
public sealed class ValidationPath : IEquatable<ValidationPath>
{
    /// <summary>The bound _key contract.</summary>
    private readonly IKey _key;

    /// <summary>The bound _legacy contract.</summary>
    private readonly bool _legacy;

    /// <summary>Initializes a new instance of the <see cref="ValidationPath"/> class.</summary>
    /// <param name="displayPath">The presentation metadata.</param>
    /// <param name="key">The typed identity.</param>
    /// <param name="legacy">Whether legacy matching applies.</param>
    private ValidationPath(string displayPath, IKey key, bool legacy)
    {
        ArgumentExceptionHelper.ThrowIfNullOrEmpty(displayPath);
        DisplayPath = displayPath;
        _key = key;
        _legacy = legacy;
    }

    /// <summary>A typed structural key domain.</summary>
    private interface IKey
    {
        /// <summary>Compares keys within the same type and comparer domain.</summary>
        /// <param name="other">The other identity.</param>
        /// <returns>Whether identities match.</returns>
        bool Same(IKey other);

        /// <summary>Returns the typed key hash.</summary>
        /// <returns>The key hash.</returns>
        int Hash();
    }

    /// <summary>Gets the full presentation path used by legacy error channels.</summary>
    public string DisplayPath { get; }

    /// <summary>Gets whether matching intentionally uses the legacy full name.</summary>
    public bool IsLegacy => _legacy;

    /// <summary>Creates an exact ordinal legacy path.</summary>
    /// <param name="fullPath">The complete path.</param>
    /// <returns>The path identity.</returns>
    /// <remarks>
    /// Legacy metadata explicitly opts into ordinal full-name membership, including a structural query with that display name.
    /// Use structural metadata to distinguish physical members, conversions or index arguments sharing a name.
    /// </remarks>
    public static ValidationPath Legacy(string fullPath)
    {
        ObservablePropertyValidation.ValidatePath(fullPath);
        return new(fullPath, new Key<string>(fullPath, StringComparer.Ordinal), true);
    }

    /// <summary>Creates a structural identity with explicit typed equality.</summary>
    /// <typeparam name="TKey">The immutable key type.</typeparam>
    /// <param name="displayPath">The legacy presentation name.</param>
    /// <param name="identity">Member, conversion and argument identities.</param>
    /// <param name="comparer">The key equality contract.</param>
    /// <returns>The structural path.</returns>
    /// <remarks>
    /// Keys compare only within the same key type and comparer instance. The display path does not define structural equality.
    /// Rule membership remains structural when both paths are structural; an explicitly legacy counterpart instead selects its complete ordinal name.
    /// </remarks>
    public static ValidationPath Structural<TKey>(string displayPath, TKey identity, IEqualityComparer<TKey> comparer)
    {
        ArgumentExceptionHelper.ThrowIfNull(comparer);
        return new(displayPath, new Key<TKey>(identity, comparer), false);
    }

    /// <inheritdoc/>
    public bool Equals(ValidationPath? other) => other is not null && _legacy == other._legacy && _key.Same(other._key);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ValidationPath path && Equals(path);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_legacy, _key.Hash());

    /// <summary>Matches membership using exact identity unless either side explicitly supplies a legacy full name.</summary>
    /// <param name="other">The nonnull selected path.</param>
    /// <returns>Whether the paths select the same rule membership.</returns>
    internal bool Matches(ValidationPath other) => Equals(other)
        || ((_legacy || other._legacy) && StringComparer.Ordinal.Equals(DisplayPath, other.DisplayPath));

    /// <summary>Preserves a typed key without reflective equality.</summary>
    /// <typeparam name="TKey">The immutable identity type.</typeparam>
    /// <param name="value">The typed identity.</param>
    /// <param name="comparer">The stable identity domain comparer.</param>
    private sealed class Key<TKey>(TKey value, IEqualityComparer<TKey> comparer) : IKey
    {
        /// <summary>Gets the typed identity.</summary>
        private TKey Value => value;

        /// <summary>Gets the identity domain.</summary>
        private IEqualityComparer<TKey> Comparer => comparer;

        /// <inheritdoc/>
        public bool Same(IKey other) => other is Key<TKey> key && ReferenceEquals(comparer, key.Comparer) && comparer.Equals(value, key.Value);

        /// <inheritdoc/>
        public int Hash() => value is null ? 0 : comparer.GetHashCode(value);
    }
}
