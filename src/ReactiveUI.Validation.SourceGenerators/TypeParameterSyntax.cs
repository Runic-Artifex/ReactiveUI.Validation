// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Rebinds type-parameter syntax while preserving unrelated members and literals.</summary>
internal static class TypeParameterSyntax
{
    /// <summary>Rebinds generated code only at unqualified type-parameter positions.</summary>
    /// <param name="code">The generated source fragment.</param>
    /// <param name="substitutions">The proven caller-to-host type-parameter map.</param>
    /// <returns>The type-rebound code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReplaceCode(string code, IReadOnlyDictionary<string, string> substitutions) =>
        new Rewriter(substitutions).Visit(SyntaxFactory.ParseCompilationUnit(code))!.ToFullString();

    /// <summary>Rebinds a generated type name using its original API slot positions.</summary>
    /// <param name="type">The generated type syntax.</param>
    /// <param name="substitutions">The proven type-parameter map.</param>
    /// <returns>The rebound type name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReplaceType(string type, IReadOnlyDictionary<string, string> substitutions) =>
        new Rewriter(substitutions).Visit(SyntaxFactory.ParseTypeName(type))!.ToFullString();

    /// <summary>Changes type references while excluding member names and qualified named types.</summary>
    /// <param name="substitutions">The type-parameter identity map.</param>
    private sealed class Rewriter(IReadOnlyDictionary<string, string> substitutions) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) => substitutions.TryGetValue(node.Identifier.ValueText, out var replacement) && IsType(node)
            ? SyntaxFactory.IdentifierName($"@{replacement}").WithTriviaFrom(node)
            : base.VisitIdentifierName(node);

        /// <summary>Checks syntactic type-parameter positions in generated typed code.</summary>
        /// <param name="node">The unqualified identifier.</param>
        /// <returns>Whether rebinding changes only a type reference.</returns>
        private static bool IsType(IdentifierNameSyntax node)
        {
            if (node.Parent is null)
            {
                return true;
            }

            if (node.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax or MemberAccessExpressionSyntax)
            {
                return false;
            }

            SyntaxNode current = node;
            while (current.Parent is NullableTypeSyntax or ArrayTypeSyntax or PointerTypeSyntax)
            {
                current = current.Parent;
            }

            return current.Parent switch
            {
                TypeArgumentListSyntax => true,
                VariableDeclarationSyntax declaration => declaration.Type == current,
                ParameterSyntax parameter => parameter.Type == current,
                TypeConstraintSyntax constraint => constraint.Type == current,
                CastExpressionSyntax cast => cast.Type == current,
                ObjectCreationExpressionSyntax creation => creation.Type == current,
                TypeOfExpressionSyntax typeOf => typeOf.Type == current,
                DefaultExpressionSyntax defaultValue => defaultValue.Type == current,
                TupleElementSyntax element => element.Type == current,
                SimpleBaseTypeSyntax baseType => baseType.Type == current,
                MethodDeclarationSyntax method => method.ReturnType == current,
                LocalFunctionStatementSyntax method => method.ReturnType == current,
                BinaryExpressionSyntax binary when binary.Kind() is SyntaxKind.AsExpression or SyntaxKind.IsExpression => binary.Right == current,
                _ => false,
            };
        }
    }
}
