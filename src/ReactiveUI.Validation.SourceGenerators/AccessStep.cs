// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>One typed property, field or indexer read and ordinary write.</summary>
internal sealed class AccessStep
{
    /// <summary>The owner marker in a writable expression.</summary>
    internal const string OwnerMarker = "__runic_lens_owner";

    /// <summary>The member access expression, parameterized by its owner.</summary>
    private readonly string _access;

    /// <summary>Initializes a new instance of the <see cref="AccessStep"/> class.</summary>
    /// <param name="member">The exact selected member.</param>
    /// <param name="ownerType">The declared member owner.</param>
    /// <param name="valueType">The member storage type.</param>
    /// <param name="access">The direct access template.</param>
    /// <param name="indices">The ordered argument templates.</param>
    internal AccessStep(ISymbol member, ITypeSymbol ownerType, ITypeSymbol valueType, string access, ImmutableArray<string> indices)
    {
        Member = member;
        OwnerType = ownerType;
        ValueType = valueType;
        _access = access;
        Indices = indices;
    }

    /// <summary>Gets the exact writable storage member.</summary>
    internal ISymbol Member { get; }

    /// <summary>Gets the receiver type for null guarding.</summary>
    internal ITypeSymbol OwnerType { get; }

    /// <summary>Gets the member storage type.</summary>
    internal ITypeSymbol ValueType { get; }

    /// <summary>Gets the exact current index argument expressions.</summary>
    internal ImmutableArray<string> Indices { get; }

    /// <summary>Gets or sets the original receiver operation for observation snapshots.</summary>
    internal IOperation? OwnerOperation { get; set; }

    /// <summary>Gets or sets the exact typed index operations.</summary>
    internal ImmutableArray<IOperation> IndexOperations { get; set; } = [];

    /// <summary>Gets or sets the exact statically proven getter bridge invocation.</summary>
    internal string? ReadBridge { get; set; }

    /// <summary>Gets or sets the exact statically proven setter bridge statement invocation.</summary>
    internal string? WriteBridge { get; set; }

    /// <summary>Formats a stable cached key local for one storage segment.</summary>
    /// <param name="segment">The storage segment.</param>
    /// <param name="argument">The index argument.</param>
    /// <returns>The synchronous key local identifier.</returns>
    internal static string IndexMarker(int segment, int argument) => $"__runic_lens_key{segment}_{argument}";

    /// <summary>Formats the member access with current root arguments.</summary>
    /// <param name="owner">The synchronous owner local.</param>
    /// <param name="root">The current root for dynamic index arguments.</param>
    /// <returns>The assignable expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string Access(string owner, string root) => ReadBridge is null
        ? Selector.ReplaceRoot(Selector.ReplaceIdentifier(_access, OwnerMarker, owner), root)
        : Selector.ReplaceIdentifier(ReadBridge, AccessBridgeEmitter.OwnerPlaceholder, owner);

    /// <summary>Formats an ordinary assignment or a selected static setter invocation.</summary>
    /// <param name="owner">The synchronous owned receiver local.</param>
    /// <param name="root">The current source for dynamic key operands.</param>
    /// <param name="value">The assigned value, converted once by ordinary C# assignment.</param>
    /// <returns>The complete assignment statement.</returns>
    internal string Assignment(string owner, string root, string value) => WriteBridge is null
        ? $"{Selector.ReplaceRoot(Selector.ReplaceIdentifier(_access, OwnerMarker, owner), root)} = {value};"
        : $"{Selector.ReplaceIdentifier(Selector.ReplaceIdentifier(WriteBridge, AccessBridgeEmitter.OwnerPlaceholder, owner), AccessBridgeEmitter.ValuePlaceholder, value)};";
}
