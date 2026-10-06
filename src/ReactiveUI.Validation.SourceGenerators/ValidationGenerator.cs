// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ReactiveUI.Validation.SourceGenerators;

/// <summary>Generates typed observation and assignment for inline validation calls.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValidationGenerator : IIncrementalGenerator
{
    /// <summary>Reports selectors whose contracts cannot be generated.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedSelector = new(
        "RUVG001",
        "Unsupported validation selector",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>Reports call-site types inaccessible to generated code.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedType = new(
        "RUVG002",
        "Unsupported validation call type",
        "The generated validation call references inaccessible or open generic types. Move the call to accessible concrete types, "
        + "supply explicit observables and callbacks, or choose Unsafe explicitly.",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>Reports unavailable compiler interception support.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedLocation = new(
        "RUVG003",
        "Validation call cannot be intercepted",
        "The compiler cannot provide an encoded interceptable location. Use the supported .NET 10.0.401 SDK and C# 14 compiler.",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>Reports incompatible runtime generation contracts.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedRuntime = new(
        "RUVG004",
        "Unsupported validation runtime contract",
        "Use the matching validation runtime package containing GeneratedValidationObservation and the safe observable APIs with its bundled analyzer",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>Validates and formats the original compilation contract.</summary>
    private const string RuleName = "ValidationRule";

    /// <summary>The analyzer diagnostic category.</summary>
    private const string Category = "ReactiveUI.Validation.Generation";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax invocation
                && GetName(invocation) is RuleName or "BindValidation" or "BindValidationState" or "BindValidationContext",
            static (syntax, _) => FindCall(syntax)).Where(static call => call is not null);
        context.RegisterSourceOutput(calls.Collect(), static (output, sites) =>
        {
            var emitted = new StringBuilder();
            var id = 0;
            foreach (var candidate in sites)
            {
                var site = new CallSite(
                    candidate!.Model,
                    candidate.Invocation,
                    candidate.Method,
                    candidate.NamespaceRoot,
                    candidate.Attribute,
                    id,
                    candidate.FailureReason);
                id++;
                var failure = ValidateCall(site);
                if (failure is not null)
                {
                    output.ReportDiagnostic(failure);
                    continue;
                }

                string? source;
                Diagnostic? diagnostic;
                var handled = site.Method.Name == RuleName
                    ? RuleEmitter.TryEmit(site, out source, out diagnostic)
                    : BindingEmitter.TryEmit(site, out source, out diagnostic);
                if (diagnostic is not null)
                {
                    output.ReportDiagnostic(diagnostic);
                }
                else if (handled && source is not null)
                {
                    _ = emitted.AppendLine(source);
                }
                else
                {
                    output.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedSelector,
                        site.Invocation.GetLocation(),
                        "This generator-only overload is unsupported. Use an explicit observable and callback or choose Unsafe explicitly."));
                }
            }

            if (emitted.Length == 0)
            {
                return;
            }

            output.AddSource("ValidationInterceptors.g.cs", SourceText.From(BuildSource(emitted, sites[0]!.Model.Compilation.AssemblyName ?? "Application"), Encoding.UTF8));
        });
    }

    /// <summary>Recognizes only shipping normal generator-only overloads.</summary>
    /// <param name="method">The resolved method.</param>
    /// <returns>Whether the final call must have generated dispatch.</returns>
    internal static bool IsNormalMethod(IMethodSymbol method) => RuntimeRoot(method) is not null && IsStub(method);

    /// <summary>Wraps emitted methods in their dedicated interception namespace.</summary>
    /// <param name="emitted">The generated interceptor methods.</param>
    /// <param name="assemblyName">The caller assembly name.</param>
    /// <returns>The complete generated compilation unit.</returns>
    private static string BuildSource(StringBuilder emitted, string assemblyName)
    {
        var identity = new StringBuilder("Assembly_");
        foreach (var character in assemblyName)
        {
            _ = identity.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        var prefix = $$"""
                // <auto-generated/>
                #nullable enable
                namespace System.Runtime.CompilerServices
                {
                    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                    file sealed class InterceptsLocationAttribute : global::System.Attribute
                {
                    public InterceptsLocationAttribute(int version, string data) { }
                }
                }
                namespace ReactiveUI.Validation.Generated.{{identity}}
                {
                    internal static class ValidationInterceptors
                    {
                """;
        return $"{prefix}\n{emitted}\n}} }}";
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="site">The site contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static Diagnostic? ValidateCall(CallSite site)
    {
        if (site.FailureReason is not null)
        {
            return Diagnostic.Create(UnsupportedSelector, site.Invocation.GetLocation(), site.FailureReason);
        }

        if (site.Model.Compilation.GetTypeByMetadataName($"{site.NamespaceRoot}.Extensions.GeneratedValidationObservation") is null)
        {
            return Diagnostic.Create(UnsupportedRuntime, site.Invocation.GetLocation());
        }

        foreach (var parameter in site.Method.Parameters)
        {
            if (!GeneratorHelpers.IsAccessibleType(site.Model.Compilation, parameter.Type))
            {
                return Diagnostic.Create(UnsupportedType, site.Invocation.GetLocation());
            }
        }

        return site.Attribute.Length == 0 ? Diagnostic.Create(UnsupportedLocation, site.Invocation.GetLocation()) : null;
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="invocation">The invocation contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static string? GetName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="resolved">The resolved contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static IMethodSymbol? NormalizeMethod(IMethodSymbol resolved)
    {
        if (!resolved.ContainingType.IsExtension)
        {
            return resolved.ReducedFrom is null ? resolved : resolved.GetConstructedReducedFrom();
        }

        var arguments = resolved.ContainingType.TypeArguments.AddRange(resolved.TypeArguments);
        foreach (var member in resolved.ContainingType.ContainingType.GetMembers(resolved.Name))
        {
            if (member is not IMethodSymbol candidate || candidate.TypeParameters.Length != arguments.Length
                || candidate.Parameters.Length != resolved.Parameters.Length + 1)
            {
                continue;
            }

            var constructed = candidate.Construct(arguments.ToArray());
            var matches = true;
            for (var index = 0; index < resolved.Parameters.Length; index++)
            {
                matches &= SymbolEqualityComparer.Default.Equals(constructed.Parameters[index + 1].Type, resolved.Parameters[index].Type);
            }

            if (matches)
            {
                return constructed;
            }
        }

        return null;
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="context">The context contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    [SuppressMessage("Usage", "RSEXPERIMENTAL002", Justification = "The supported Roslyn 5.9 compiler provides encoded interception locations through this experimental API.")]
    private static CallSite? FindCall(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var info = context.SemanticModel.GetSymbolInfo(invocation);
        if (info.Symbol is not IMethodSymbol resolved)
        {
            foreach (var candidate in info.CandidateSymbols.OfType<IMethodSymbol>())
            {
                var candidateRoot = RuntimeRoot(candidate);
                if (candidateRoot is not null && IsStub(candidate))
                {
                    return new(
                        context.SemanticModel,
                        invocation,
                        candidate,
                        candidateRoot,
                        string.Empty,
                        0,
                        "The validation selector cannot be resolved in the original compilation. Declare the property contract in source (including a "
                        + "partial property), use an explicit observable, or choose Unsafe explicitly.");
                }
            }

            return null;
        }

        var root = RuntimeRoot(resolved);
        if (root is null || !IsStub(resolved))
        {
            return null;
        }

        var method = NormalizeMethod(resolved);
        if (method is null)
        {
            return new(
                context.SemanticModel,
                invocation,
                resolved,
                root,
                string.Empty,
                0,
                "The validation extension signature cannot be generated. Use an explicit observable or choose Unsafe explicitly.");
        }

        var location = context.SemanticModel.GetInterceptableLocation(invocation);
        return new(context.SemanticModel, invocation, method, root, location?.GetInterceptsLocationAttributeSyntax() ?? string.Empty, 0);
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="method">The method contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static string? RuntimeRoot(IMethodSymbol method)
    {
        var root = method.ContainingAssembly.Name switch
        {
            "ReactiveUI.Validation" => "ReactiveUI.Validation",
            "ReactiveUI.Validation.Reactive" => "ReactiveUI.Validation.Reactive",
            _ => null,
        };
        return root is not null && method.ContainingNamespace.ToDisplayString() == $"{root}.Extensions" ? root : null;
    }

    /// <summary>Checks the original semantic contract.</summary>
    /// <param name="method">The method contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static bool IsStub(IMethodSymbol method)
    {
        var owner = method.ContainingType.IsExtension ? method.ContainingType.ContainingType.Name : method.ContainingType.Name;
        if (method.Name != RuleName)
        {
            return method.Name switch
            {
                "BindValidation" => owner == "ViewForExtensions",
                "BindValidationContext" => owner == "ValidationContextBindingExtensions",
                "BindValidationState" => owner == "ValidationStateBindingExtensions",
                _ => false,
            };
        }

        if (owner is not ("ValidatableViewModelExtensions" or "ValidationRuleContextExtensions"))
        {
            return false;
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Name == "isPropertyValid")
            {
                return true;
            }
        }

        return false;
    }
}
