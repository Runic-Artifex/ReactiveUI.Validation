// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>An immutable, invocation-local collection of typed schema arguments.</summary>
[DebuggerDisplay("Arguments = {_entries.Length}")]
public sealed class ValidationArguments
{
    /// <summary>The finite entries retained by this owner.</summary>
    private readonly Entry[] _entries;

    /// <summary>Initializes a new instance of the <see cref="ValidationArguments"/> class with no arguments.</summary>
    public ValidationArguments() => _entries = [];

    /// <summary>Initializes a new instance of the ValidationArguments class.</summary>
    /// <param name="entries">The entries contract.</param>
    private ValidationArguments(Entry[] entries) => _entries = entries;

    /// <summary>Adds a typed argument to a new payload.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="argument">The schema's identity token.</param>
    /// <param name="value">The current invocation's value.</param>
    /// <returns>The new immutable payload.</returns>
    /// <exception cref="InvalidOperationException">The payload already contains this token.</exception>
    public ValidationArguments Add<T>(ValidationArgument<T> argument, T value)
    {
        ArgumentExceptionHelper.ThrowIfNull(argument);
        foreach (var entry in _entries)
        {
            if (ReferenceEquals(entry.Token, argument))
            {
                throw new InvalidOperationException("An argument token may occur only once in a match payload.");
            }
        }

        var entries = new Entry[_entries.Length + 1];
        _entries.CopyTo(entries, 0);
        entries[^1] = new TypedEntry<T>(argument, value);
        return new(entries);
    }

    /// <summary>Gets a value using the exact typed token registered by its schema.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="argument">The schema's identity token.</param>
    /// <returns>The current invocation's value.</returns>
    /// <exception cref="KeyNotFoundException">The token is absent from this payload.</exception>
    public T Get<T>(ValidationArgument<T> argument)
    {
        ArgumentExceptionHelper.ThrowIfNull(argument);
        foreach (var entry in _entries)
        {
            if (entry is TypedEntry<T> typed && ReferenceEquals(typed.Token, argument))
            {
                return typed.Value;
            }
        }

        throw new KeyNotFoundException("The typed argument was not produced by this expression match.");
    }

    /// <summary>Stores a finite registration identity.</summary>
    /// <param name="token">The token contract.</param>
    private class Entry(object token)
    {
        /// <summary>Gets the typed argument identity token.</summary>
        internal object Token { get; } = token;
    }

    /// <summary>Stores a value with its exact typed token.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="token">The token contract.</param>
    /// <param name="value">The value contract.</param>
    private sealed class TypedEntry<T>(ValidationArgument<T> token, T value) : Entry(token)
    {
        /// <summary>Gets the typed argument value.</summary>
        internal T Value { get; } = value;
    }
}
