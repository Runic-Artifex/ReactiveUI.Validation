// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Reads stable output synchronously from a borrowed, possibly stack-only source.</summary>
/// <typeparam name="TSource">The source, which may be a ref struct.</typeparam>
/// <typeparam name="TValue">The ordinary output value.</typeparam>
/// <param name="source">The borrowed source, available only during this invocation.</param>
/// <returns>The selected output.</returns>
[SuppressMessage("Design", "CA1045:Do not pass types by reference", Justification = "A scoped readonly borrow is the explicit stack-only snapshot contract.")]
public delegate TValue ValidationSnapshotReader<TSource, out TValue>(scoped in TSource source)
    where TSource : allows ref struct;
