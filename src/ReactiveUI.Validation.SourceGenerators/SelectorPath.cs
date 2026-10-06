// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Separates legacy dotted membership from exact converted/indexed operation identity.</summary>
/// <param name="displayPath">The displayPath contract.</param>
/// <param name="identity">The identity contract.</param>
/// <param name="indices">The indices contract.</param>
/// <param name="structural">The structural contract.</param>
[SuppressMessage("Performance", "PSH1100", Justification = "This is compiler-time planning and source formatting over bounded immutable operations, not emitted subscription execution.")]
internal sealed class SelectorPath(string displayPath, string identity, ImmutableArray<SelectorIndex> indices, bool structural)
{
    /// <summary>Gets the full readable root-relative path.</summary>
    internal string DisplayPath { get; } = displayPath;

    /// <summary>Gets the exact member and receiver-conversion identity.</summary>
    internal string Identity { get; } = identity;

    /// <summary>Gets the ordered current index argument operations.</summary>
    internal ImmutableArray<SelectorIndex> Indices { get; } = indices;

    /// <summary>Gets the exact leaf reads that activate this validated path.</summary>
    internal List<IOperation> SelectedOperations { get; } = [];

    /// <summary>Gets whether exact structural matching is required.</summary>
    internal bool Structural { get; } = structural;

    /// <summary>Emits one typed path using current snapshotted index values.</summary>
    /// <param name="namespaceRoot">The runtime flavor namespace.</param>
    /// <param name="values">The current index values in path order.</param>
    /// <returns>The complete ValidationPath expression.</returns>
    internal string Emit(string namespaceRoot, ImmutableArray<string> values)
    {
        var path = $"global::{namespaceRoot}.Capabilities.ValidationPath";
        if (!Structural)
        {
            return $"{path}.Legacy({GeneratorHelpers.Quote(DisplayPath)})";
        }

        var types = Indices.Select(static index => GeneratorHelpers.TypeName(index.Type)).ToArray();
        var identity = GeneratorHelpers.Quote(Identity);
        var key = values.IsEmpty ? identity : $"({identity}, {string.Join(", ", values)})";
        var type = values.IsEmpty ? "string" : $"(string, {string.Join(", ", types)})";
        return $"{path}.Structural({GeneratorHelpers.Quote(DisplayPath)}, {key}, global::System.Collections.Generic.EqualityComparer<{type}>.Default)";
    }
}
