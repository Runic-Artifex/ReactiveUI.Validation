// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

#if REACTIVE_SHIM
namespace ReactiveUI.Validation.Reactive.Capabilities;
#else
namespace ReactiveUI.Validation.Capabilities;
#endif

/// <summary>Validates a borrowed, possibly stack-only source without retaining it.</summary>
/// <typeparam name="TSource">The source, which may be a ref struct.</typeparam>
/// <param name="source">The borrowed source, available only during this invocation.</param>
/// <returns>A complete ordinary validation state.</returns>
[SuppressMessage("Design", "CA1045:Do not pass types by reference", Justification = "A scoped readonly borrow is the explicit stack-only validation contract.")]
public delegate IValidationState ValidationSnapshotValidator<TSource>(scoped in TSource source)
    where TSource : allows ref struct;
