// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Retains every emitted file, including helpers and hosted bridges.</summary>
/// <param name="Generator">The owning generator type.</param>
/// <param name="HintName">The emitted source hint.</param>
/// <param name="Text">The complete generated source.</param>
/// <param name="Sha256">The complete generated text SHA-256.</param>
internal sealed record CapabilityGeneratedSource(string Generator, string HintName, string Text, string Sha256);
