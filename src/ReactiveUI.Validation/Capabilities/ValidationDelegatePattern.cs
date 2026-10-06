// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Creates finite delegate identity matchers without invoking or inspecting delegates.</summary>
public static class ValidationDelegatePattern
{
    /// <summary>Matches the exact invocation-list identity of a known typed delegate.</summary>
    /// <typeparam name="TSource">The source API type.</typeparam>
    /// <typeparam name="TValue">The selected value type.</typeparam>
    /// <param name="exemplar">The explicitly supported delegate, retained by the matcher.</param>
    /// <returns>A matcher using CLR delegate equality and an empty immutable payload.</returns>
    /// <remarks>Fresh method-group delegates may match; different capture owners fail. The matcher never accesses delegate Method or Target properties or invokes the supplied selector.</remarks>
    public static ValidationDelegateMatcher<TSource, TValue> Create<TSource, TValue>(Func<TSource, TValue> exemplar)
    {
        ArgumentExceptionHelper.ThrowIfNull(exemplar);
        return Match;

        bool Match(Func<TSource, TValue> selection, [NotNullWhen(true)] out ValidationArguments? arguments)
        {
            ArgumentExceptionHelper.ThrowIfNull(selection);
            if (!exemplar.Equals(selection))
            {
                arguments = null;
                return false;
            }

            arguments = new();
            return true;
        }
    }
}
