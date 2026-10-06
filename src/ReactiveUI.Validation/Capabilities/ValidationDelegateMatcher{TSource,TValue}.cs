// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Matches an explicitly supported typed delegate without invoking it.</summary>
/// <typeparam name="TSource">The source API type.</typeparam>
/// <typeparam name="TValue">The selected value type.</typeparam>
/// <param name="selection">The current call's supplied delegate.</param>
/// <param name="arguments">The immutable typed payload on success.</param>
/// <returns>Whether the delegate belongs to this finite contract.</returns>
public delegate bool ValidationDelegateMatcher<TSource, TValue>(Func<TSource, TValue> selection, [NotNullWhen(true)] out ValidationArguments? arguments);
