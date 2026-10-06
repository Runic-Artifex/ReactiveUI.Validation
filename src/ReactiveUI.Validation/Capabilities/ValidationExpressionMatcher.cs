// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Matches an explicitly supported expression and returns its typed argument payload.</summary>
/// <typeparam name="TSource">The source API type.</typeparam>
/// <typeparam name="TValue">The selected value type.</typeparam>
/// <param name="expression">The current call's expression.</param>
/// <param name="arguments">The immutable payload on success.</param>
/// <returns>Whether the expression belongs to this finite schema.</returns>
public delegate bool ValidationExpressionMatcher<TSource, TValue>(Expression<Func<TSource, TValue>> expression, [NotNullWhen(true)] out ValidationArguments? arguments);
