// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using ReactiveUI.Validation.SourceGenerators.Producers;

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
        "The validation call cannot be represented by the original API's interceptor generic signature. Use a typed selector/target factory "
        + "in the legal lexical scope, scoped finite runtime registration, or explicit observables and callbacks.",
        Category,
        DiagnosticSeverity.Error,
        true);

    /// <summary>Reports unavailable compiler interception support.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedLocation = new(
        "RUVG003",
        "Validation call cannot be intercepted",
        "The compiler must provide encoded interception locations and allow ReactiveUI.Validation.Generated. Retain the packaged buildTransitive props "
        + "and use the supported .NET 10.0.401 SDK and C# 14 compiler.",
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

    /// <summary>The FNV-1a namespace identity multiplier.</summary>
    private const ulong HashPrime = 1_099_511_628_211;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var projection = ProducerProjection.Register(in context);
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax invocation
                && GetName(invocation) is RuleName or "BindValidation" or "BindValidationState" or "BindValidationContext"
                    or "ForProperty" or "ForViewModel" or "ForValidationHelperProperty",
            static (syntax, _) => (InvocationExpressionSyntax)syntax.Node).Collect()
            .Combine(projection)
            .Select(static (data, cancellation) => RebindCalls(data.Left, data.Right, cancellation));
        context.RegisterSourceOutput(projection.Combine(calls).Combine(context.AnalyzerConfigOptionsProvider), static (output, data) =>
        {
            if (!data.Left.Right.IsEmpty)
            {
                EmitProducerRecords(in output, data.Left.Left, data.Right);
            }
        });
        context.RegisterSourceOutput(calls, EmitCalls);
    }

    /// <summary>Recognizes only shipping normal generator-only overloads.</summary>
    /// <param name="method">The resolved method.</param>
    /// <returns>Whether the final call must have generated dispatch.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNormalMethod(IMethodSymbol method) => ValidationCallClassifier.IsNormalMethod(method);

    /// <summary>Returns a deterministic namespace component for generated application code.</summary>
    /// <param name="assemblyName">The consumer assembly name.</param>
    /// <returns>The collision-resistant namespace component.</returns>
    internal static string AssemblyIdentity(string assemblyName)
    {
        var identity = new StringBuilder("Assembly_");
        var hash = 14_695_981_039_346_656_037UL;
        foreach (var character in assemblyName)
        {
            _ = identity.Append(char.IsLetterOrDigit(character) ? character : '_');
            hash = unchecked((hash ^ character) * HashPrime);
        }

        _ = identity.Append('_').Append(hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture));
        return identity.ToString();
    }

    /// <summary>Rebinds original invocations against finite private declaration contracts.</summary>
    /// <param name="invocations">Original invocation nodes.</param>
    /// <param name="projection">The private semantic compilation.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The normal calls requiring generated dispatch.</returns>
    private static ImmutableArray<CallSite> RebindCalls(
        ImmutableArray<InvocationExpressionSyntax> invocations,
        ProducerProjection projection,
        CancellationToken cancellation)
    {
        var calls = ImmutableArray.CreateBuilder<CallSite>();
        foreach (var invocation in invocations)
        {
            var site = FindCall(
                projection.Compilation.GetSemanticModel(invocation.SyntaxTree),
                invocation,
                projection.OriginalCompilation,
                projection.MemberKeys,
                cancellation);
            if (site is not null)
            {
                calls.Add(site);
            }
        }

        return calls.ToImmutable();
    }

    /// <summary>Emits inert final-contract records and verifies resolved producer versions.</summary>
    /// <param name="output">The source output context.</param>
    /// <param name="projection">The private declaration profiles.</param>
    /// <param name="options">Restored producer version metadata.</param>
    private static void EmitProducerRecords(in SourceProductionContext output, ProducerProjection projection, AnalyzerConfigOptionsProvider options)
    {
        var records = ProducerContractChecker.Records(projection);
        if (records.Length > 0)
        {
            output.AddSource("ValidationProducerContracts.g.cs", SourceText.From(records, Encoding.UTF8));
        }

        var profiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in projection.Declarations)
        {
            if (!profiles.Add(declaration.Profile))
            {
                continue;
            }

            var (key, expected) = ProfileVersion(declaration.Profile);
            if (!options.GlobalOptions.TryGetValue(key, out var actual) || actual != expected)
            {
                output.ReportDiagnostic(Diagnostic.Create(ProducerContractChecker.UnsupportedProfile, declaration.Location, declaration.Profile, actual ?? "unavailable"));
            }
        }
    }

    /// <summary>Selects the restored version property for a finite producer profile.</summary>
    /// <param name="profile">The pinned producer profile identity.</param>
    /// <returns>The compiler-visible property and expected version.</returns>
    private static (string Key, string Version) ProfileVersion(string profile) => profile switch
    {
        var name when name.StartsWith("ReactiveUI.Binding", StringComparison.Ordinal) => ("build_property.ReactiveUIValidationBindingProducerVersion", "9.1.0"),
        var name when name.StartsWith("Avalonia.Generators", StringComparison.Ordinal) => ("build_property.ReactiveUIValidationAvaloniaProducerVersion", "12.1.3"),
        var name when name.StartsWith("Microsoft.Maui.Controls.SourceGen", StringComparison.Ordinal) => ("build_property.ReactiveUIValidationMauiProducerVersion", "10.0.110"),
        _ => ("build_property.ReactiveUIValidationReactiveProducerVersion", "4.2.0"),
    };

    /// <summary>Emits typed interceptors and separately hosted access plans.</summary>
    /// <param name="output">The source output context.</param>
    /// <param name="sites">Rebound original normal calls.</param>
    private static void EmitCalls(SourceProductionContext output, ImmutableArray<CallSite> sites)
    {
        var emitted = new StringBuilder();
        var additional = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var id = 0;
        foreach (var candidate in sites)
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            var site = new CallSite(
                candidate.Model,
                candidate.Invocation,
                candidate.Method,
                candidate.NamespaceRoot,
                candidate.Attribute,
                id,
                candidate.FailureReason,
                candidate.OriginalCompilation) { ProducerMemberKeys = candidate.ProducerMemberKeys };
            id++;
            EmitCall(in output, site, emitted, additional);
        }

        foreach (var fragment in additional)
        {
            output.AddSource(fragment.Key, SourceText.From(fragment.Value, Encoding.UTF8));
        }

        if (emitted.Length > 0)
        {
            output.AddSource("ValidationInterceptors.g.cs", SourceText.From(BuildSource(emitted, sites[0].Model.Compilation.AssemblyName ?? "Application"), Encoding.UTF8));
        }
    }

    /// <summary>Validates and lowers one original normal call.</summary>
    /// <param name="output">The source output context.</param>
    /// <param name="site">The typed original invocation.</param>
    /// <param name="emitted">The interceptor method buffer.</param>
    /// <param name="additional">Unique separately hosted source fragments.</param>
    private static void EmitCall(in SourceProductionContext output, CallSite site, StringBuilder emitted, IDictionary<string, string> additional)
    {
        var failure = ValidateCall(site);
        if (failure is not null)
        {
            output.ReportDiagnostic(failure);
            return;
        }

        var handled = TryEmit(site, out var source, out var diagnostic);
        if (diagnostic is not null)
        {
            output.ReportDiagnostic(diagnostic);
        }
        else if (handled && source is not null)
        {
            _ = emitted.AppendLine(source);
            AddFragments(in output, site, additional);
        }
        else
        {
            output.ReportDiagnostic(Diagnostic.Create(
                UnsupportedSelector,
                site.Invocation.GetLocation(),
                "This normal overload cannot be lowered. Use a typed selector/target factory, finite runtime registration, explicit observables, or Unsafe explicitly."));
        }
    }

    /// <summary>Dispatches an exact normal call to its semantic backend.</summary>
    /// <param name="site">The original call.</param>
    /// <param name="source">The emitted interceptor method.</param>
    /// <param name="diagnostic">The actionable lowering error, if any.</param>
    /// <returns>Whether a backend owns the call.</returns>
    private static bool TryEmit(CallSite site, out string? source, out Diagnostic? diagnostic)
    {
        if (site.Method.Name == RuleName)
        {
            return RuleEmitter.TryEmit(site, out source, out diagnostic);
        }

        return site.Method.ContainingType.Name == "ValidationBinding"
            ? StaticBindingEmitter.TryEmit(site, out source, out diagnostic)
            : BindingEmitter.TryEmit(site, out source, out diagnostic);
    }

    /// <summary>Deduplicates separately hosted typed declarations.</summary>
    /// <param name="output">The source output context.</param>
    /// <param name="site">The lowered call and its fragments.</param>
    /// <param name="additional">The unique fragment buffer.</param>
    private static void AddFragments(in SourceProductionContext output, CallSite site, IDictionary<string, string> additional)
    {
        foreach (var fragment in site.AdditionalSources)
        {
            if (additional.TryGetValue(fragment.HintName, out var existing) && existing != fragment.Source)
            {
                output.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedSelector,
                    site.Invocation.GetLocation(),
                    "Generated access plans disagree on a shared declaration. Use an explicit typed selector/target factory in the original lexical scope."));
                continue;
            }

            additional[fragment.HintName] = fragment.Source;
        }
    }

    /// <summary>Wraps emitted methods in their dedicated interception namespace.</summary>
    /// <param name="emitted">The generated interceptor methods.</param>
    /// <param name="assemblyName">The caller assembly name.</param>
    /// <returns>The complete generated compilation unit.</returns>
    private static string BuildSource(StringBuilder emitted, string assemblyName)
    {
        var identity = AssemblyIdentity(assemblyName);
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

        if (site.Model.Compilation.GetTypeByMetadataName($"{site.NamespaceRoot}.Extensions.GeneratedValidationObservation") is null
            || site.Model.Compilation.GetTypeByMetadataName($"{site.NamespaceRoot}.Capabilities.ValidationRuntime") is null)
        {
            return Diagnostic.Create(UnsupportedRuntime, site.Invocation.GetLocation());
        }

        if (!GenericInterceptorEmitter.CanGenerate(site, out _))
        {
            return Diagnostic.Create(UnsupportedType, site.Invocation.GetLocation());
        }

        var generatedNamespace = $"ReactiveUI.Validation.Generated.{AssemblyIdentity(site.Model.Compilation.AssemblyName ?? "Application")}";
        var admitted = AdmitsInterception(site.Invocation.SyntaxTree.Options, generatedNamespace);
        return site.Attribute.Length == 0 || !admitted ? Diagnostic.Create(UnsupportedLocation, site.Invocation.GetLocation()) : null;
    }

    /// <summary>Checks the packaged encoded-interceptor namespace allowlist.</summary>
    /// <param name="parseOptions">The original tree compiler options.</param>
    /// <param name="generatedNamespace">The assembly-specific interceptor namespace.</param>
    /// <returns>Whether the original call can be intercepted.</returns>
    private static bool AdmitsInterception(ParseOptions parseOptions, string generatedNamespace)
    {
        if (parseOptions is not CSharpParseOptions { LanguageVersion: LanguageVersion.CSharp14 } options)
        {
            return false;
        }

        foreach (var feature in options.Features)
        {
            if (feature.Key != "InterceptorsNamespaces")
            {
                continue;
            }

            foreach (var name in feature.Value.Split(';'))
            {
                var allowed = name.Trim();
                if (generatedNamespace == allowed || generatedNamespace.StartsWith($"{allowed}.", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="invocation">The invocation contract to inspect.</param>
    /// <returns>The checked semantic result.</returns>
    private static string? GetName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Validates and formats the original compilation contract.</summary>
    /// <param name="model">The original tree semantic model in the private compilation.</param>
    /// <param name="invocation">The original invocation retaining its interception location.</param>
    /// <param name="originalCompilation">The compilation before private producer enrichment.</param>
    /// <param name="producerMemberKeys">Finite producer members covered by final contract validation.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The normal call requiring generated dispatch, or null.</returns>
    [SuppressMessage("Usage", "RSEXPERIMENTAL002", Justification = "The supported Roslyn 5.9 compiler provides encoded interception locations through this experimental API.")]
    private static CallSite? FindCall(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        Compilation originalCompilation,
        IReadOnlyCollection<string> producerMemberKeys,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (ValidationRuntimeDispatchPolicy.IsOptedIn(model, invocation.SpanStart))
        {
            return null;
        }

        var info = model.GetSymbolInfo(invocation, cancellation);
        if (info.Symbol is not IMethodSymbol resolved)
        {
            return FindCandidateCall(info.CandidateSymbols, model, invocation, originalCompilation);
        }

        var root = ValidationCallClassifier.RuntimeRoot(resolved);
        if (root is null || !ValidationCallClassifier.IsGeneratorOnly(resolved))
        {
            return null;
        }

        var method = ValidationCallClassifier.NormalizeMethod(resolved);
        if (method is null)
        {
            return new(
                model,
                invocation,
                resolved,
                root,
                string.Empty,
                0,
                "The validation extension signature cannot be generated. Use an explicit observable or choose Unsafe explicitly.",
                originalCompilation);
        }

        var location = model.GetInterceptableLocation(invocation, cancellation);
        return new(model, invocation, method, root, location?.GetInterceptsLocationAttributeSyntax() ?? string.Empty, 0, originalCompilation: originalCompilation)
        {
            ProducerMemberKeys = producerMemberKeys,
        };
    }

    /// <summary>Leaves safe metadata candidates to final binding and rejects unresolved normal calls.</summary>
    /// <param name="candidates">The original binding candidates.</param>
    /// <param name="model">The private semantic model for the original tree.</param>
    /// <param name="invocation">The original invocation.</param>
    /// <param name="originalCompilation">The compiler input before enrichment.</param>
    /// <returns>An actionable unresolved normal call, or null for safe metadata.</returns>
    private static CallSite? FindCandidateCall(
        ImmutableArray<ISymbol> candidates,
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        Compilation originalCompilation)
    {
        foreach (var symbol in candidates)
        {
            if (symbol is IMethodSymbol candidate && ValidationCallClassifier.RuntimeRoot(candidate) is not null
                && !ValidationCallClassifier.IsGeneratorOnly(candidate))
            {
                return null;
            }
        }

        foreach (var symbol in candidates)
        {
            if (symbol is IMethodSymbol candidate && ValidationCallClassifier.RuntimeRoot(candidate) is { } root
                && ValidationCallClassifier.IsGeneratorOnly(candidate))
            {
                return new(
                    model,
                    invocation,
                    candidate,
                    root,
                    string.Empty,
                    0,
                    "The validation selector cannot be resolved in the original compilation. Declare the property contract in source "
                    + "(including a partial property), use an explicit typed descriptor or observable, or choose Unsafe explicitly.",
                    originalCompilation);
            }
        }

        return null;
    }
}
