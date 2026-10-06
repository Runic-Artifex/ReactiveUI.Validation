// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Extracts an argument using explicitly registered, legally typed access.</summary>
/// <typeparam name="T">The extracted argument type.</typeparam>
/// <param name="expression">The current expression subtree.</param>
/// <param name="value">The extracted value on success.</param>
/// <returns>Whether the exact supported subtree was recognized.</returns>
public delegate bool ValidationArgumentExtractor<T>(Expression expression, out T value);
