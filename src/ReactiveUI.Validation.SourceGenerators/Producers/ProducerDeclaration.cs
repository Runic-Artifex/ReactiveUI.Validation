// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Producers;

/// <summary>A declaration used only in the private semantic compilation.</summary>
internal sealed class ProducerDeclaration
{
    /// <summary>Initializes a new instance of the <see cref="ProducerDeclaration"/> class.</summary>
    /// <param name="owner">The original declaring type.</param>
    /// <param name="memberName">The generated member name.</param>
    /// <param name="declaration">The private declaration text.</param>
    /// <param name="interfaceName">An added interface, if any.</param>
    /// <param name="baseType">An added base type, if any.</param>
    /// <param name="profile">The pinned producer contract.</param>
    /// <param name="location">The original input location.</param>
    internal ProducerDeclaration(
        INamedTypeSymbol owner,
        string memberName,
        string declaration,
        string? interfaceName = null,
        string? baseType = null,
        string profile = "ReactiveUI.SourceGenerators/4.2.0",
        Location? location = null)
    {
        Owner = owner;
        Name = memberName;
        Declaration = declaration;
        InterfaceName = interfaceName;
        BaseType = baseType;
        Profile = profile;
        Location = location ?? owner.Locations[0];
    }

    /// <summary>Gets the original declaring type.</summary>
    internal INamedTypeSymbol Owner { get; }

    /// <summary>Gets the emitted member name, or an empty name for a type contract.</summary>
    internal string Name { get; }

    /// <summary>Gets the private declaration text.</summary>
    internal string Declaration { get; }

    /// <summary>Gets an added interface.</summary>
    internal string? InterfaceName { get; }

    /// <summary>Gets an added base type.</summary>
    internal string? BaseType { get; }

    /// <summary>Gets the pinned producer profile.</summary>
    internal string Profile { get; }

    /// <summary>Gets the original input location.</summary>
    internal Location Location { get; }
}
