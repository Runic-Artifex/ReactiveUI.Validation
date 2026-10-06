// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Identifies an actual packaged producer and its immutable analyzer bytes.</summary>
/// <param name="Package">The restored package identity.</param>
/// <param name="Path">The selected compiler-band analyzer path.</param>
/// <param name="Sha256">The analyzer SHA-256.</param>
internal sealed record CapabilityProducerInput(string Package, string Path, string Sha256);
