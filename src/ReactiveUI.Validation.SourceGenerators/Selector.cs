// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Stores a validated property path and generates direct getters.</summary>
internal sealed class Selector
{
    /// <summary>Initializes a new instance of the <see cref="Selector"/> class.</summary>
    /// <param name="properties">The ordered readable path properties.</param>
    internal Selector(ImmutableArray<IPropertySymbol> properties) => Properties = properties;

    /// <summary>Gets the ordered properties of the path.</summary>
    internal ImmutableArray<IPropertySymbol> Properties { get; }

    /// <summary>Gets the full ordinal property path.</summary>
    internal string Path => string.Join(".", Properties.Select(static property => property.Name));

    /// <summary>Formats the complete null-aware getter.</summary>
    /// <param name="root">The root variable.</param>
    /// <returns>Direct property access syntax.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string Getter(string root) => Access(root, Properties.Length);

    /// <summary>Formats a null-aware notification owner getter.</summary>
    /// <param name="root">The root variable.</param>
    /// <param name="segment">The observed property index.</param>
    /// <returns>Owner property access syntax.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string OwnerGetter(string root, int segment) => Access(root, segment);

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="root">The root variable.</param>
    /// <param name="count">The number of segments.</param>
    /// <returns>The null-aware path expression.</returns>
    private string Access(string root, int count)
    {
        var expression = new StringBuilder(root);
        var guards = ImmutableArray.CreateBuilder<string>();
        for (var index = 0; index < count; index++)
        {
            if (index > 0 && Properties[index - 1].Type.IsReferenceType)
            {
                guards.Add($"{expression} is null");
            }

            _ = expression.Append(".@").Append(Properties[index].Name);
        }

        return guards.Count == 0 ? expression.ToString() : $"({string.Join(" || ", guards)} ? default : {expression})";
    }
}
