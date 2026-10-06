// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>One independently scoped generated compilation unit.</summary>
internal sealed class GeneratedSourceFragment
{
    /// <summary>Initializes a new instance of the <see cref="GeneratedSourceFragment"/> class.</summary>
    /// <param name="hintName">The unique source hint.</param>
    /// <param name="source">The complete compilation unit.</param>
    internal GeneratedSourceFragment(string hintName, string source)
    {
        HintName = hintName;
        Source = source;
    }

    /// <summary>Gets the deterministic source hint.</summary>
    internal string HintName { get; }

    /// <summary>Gets the complete compilation unit.</summary>
    internal string Source { get; }
}
