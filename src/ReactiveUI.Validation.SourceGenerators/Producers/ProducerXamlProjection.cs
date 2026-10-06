// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// Adapted from ReactiveUI.Binding.SourceGenerators 9.1.0, commit
// 05c45cec845835d670960fac733bb302bc1c1071, Helpers/XamlPageReader.cs and
// Helpers/XamlMemberResolver.cs. This frontend describes declarations only;
// the platform generator remains responsible for emitting and initializing them.
// Platform contracts: Avalonia 12.1.3 at
// 8eeda4f6f546165b3f72e63c9f42247abb306905 and MAUI 10.0.110 at
// 6c379bf1dc24461a985a093be3fd8a3e9bc6fc00. Platform generators own
// implementation, initialization, UI hosting, and their validity diagnostics.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ReactiveUI.Validation.SourceGenerators.Producers;

/// <summary>Reads the declaration contract of recognized MAUI and Avalonia XAML inputs.</summary>
internal static class ProducerXamlProjection
{
    /// <summary>The XAML language namespace.</summary>
    private const string LanguageNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The MAUI-supported XAML 2009 language namespace.</summary>
    private const string Language2009Namespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    /// <summary>The MAUI control XML namespace.</summary>
    private const string MauiNamespace = "http://schemas.microsoft.com/dotnet/2021/maui";

    /// <summary>The MAUI global XML namespace.</summary>
    private const string MauiGlobalNamespace = "http://schemas.microsoft.com/dotnet/maui/global";

    /// <summary>The platform data-template type name.</summary>
    private const string DataTemplateName = "DataTemplate";

    /// <summary>The internal accessibility contract value.</summary>
    private const string InternalAccessibility = "internal";

    /// <summary>The private accessibility contract value.</summary>
    private const string PrivateAccessibility = "private";

    /// <summary>The pinned MAUI declaration producer profile.</summary>
    private const string MauiProfile = "Microsoft.Maui.Controls.SourceGen/10.0.110:XAML name projection";

    /// <summary>The pinned Avalonia declaration producer profile.</summary>
    private const string AvaloniaProfile = "Avalonia.Generators/12.1.3:XAML name projection";

    /// <summary>The size of an encoded prefix and namespace pair.</summary>
    private const int NamespacePairLength = 2;

    /// <summary>The maximum execution time for one configured Avalonia glob match.</summary>
    private const int GlobTimeoutMilliseconds = 250;

    /// <summary>The bounded matching time for the pinned regex-based input filter.</summary>
    private static readonly TimeSpan GlobTimeout = TimeSpan.FromMilliseconds(GlobTimeoutMilliseconds);

    /// <summary>MAUI's ordered framework-name to platform selection pairs.</summary>
    private static readonly KeyValuePair<string, string>[] MauiTargets =
        [new("-android", "Android"), new("-ios", "iOS"), new("-macos", "macOS"), new("-maccatalyst", "MacCatalyst")];

    /// <summary>Reports a recognized input whose field contract cannot be resolved safely.</summary>
    private static readonly DiagnosticDescriptor InvalidXaml = new(
        "RUVG010",
        "Unresolved XAML producer contract",
        "{0}. Check the XAML name/type, referenced assemblies and platform name-generator configuration; rebuild with the matching producer profile.",
        "ReactiveUI.Validation.Generation",
        DiagnosticSeverity.Error,
        true);

    /// <summary>Avalonia 12.1.3 name-generator behavior values.</summary>
    private enum AvaloniaBehavior
    {
        /// <summary>Generate get-only name-reference properties.</summary>
        OnlyProperties = 0,

        /// <summary>Generate fields initialized by InitializeComponent.</summary>
        InitializeComponent = 1,
    }

    /// <summary>Avalonia 12.1.3 default named-member modifier values.</summary>
    private enum AvaloniaFieldModifier
    {
        /// <summary>Public accessibility.</summary>
        Public = 0,

        /// <summary>Private accessibility.</summary>
        Private = 1,

        /// <summary>Internal accessibility.</summary>
        Internal = 2,

        /// <summary>Protected accessibility.</summary>
        Protected = 3,
    }

    /// <summary>Registers parsing, symbol resolution and diagnostics without publishing projected source.</summary>
    /// <param name="context">The initialization context.</param>
    /// <returns>Resolved field declarations.</returns>
    internal static IncrementalValueProvider<ImmutableArray<ProducerDeclaration>> Register(in IncrementalGeneratorInitializationContext context)
    {
        var implicitNamespaces = context.CompilationProvider.Select(ImplicitMauiNamespaces)
            .WithComparer(StringComparer.Ordinal);
        var pages = context.AdditionalTextsProvider.Combine(context.AnalyzerConfigOptionsProvider).Combine(implicitNamespaces)
            .Select(static (input, ct) => Read(input.Left.Left, input.Left.Right, input.Right, ct))
            .Where(static page => page is not null)
            .Collect();
        var resolved = context.CompilationProvider.Combine(pages)
            .Select(static (input, ct) => Resolve(input.Left, input.Right, ct));
        context.RegisterSourceOutput(resolved, static (output, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                output.ReportDiagnostic(diagnostic);
            }
        });
        return resolved.Select(static (result, _) => result.Declarations);
    }

    /// <summary>Bounds recognized Avalonia filtering and retains actionable timeout diagnostics.</summary>
    /// <param name="file">The original additional XAML input.</param>
    /// <param name="options">The applicable analyzer configuration.</param>
    /// <param name="namespaceContext">The stable encoded implicit namespace metadata.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The recognized input or its original-path filter failure.</returns>
    private static Page? Read(AdditionalText file, AnalyzerConfigOptionsProvider options, string namespaceContext, CancellationToken ct)
    {
        try
        {
            return ReadRecognized(file, options, namespaceContext, ct);
        }
        catch (RegexMatchTimeoutException)
        {
            ct.ThrowIfCancellationRequested();
            return new(
                file.Path,
                file.GetText(ct) ?? SourceText.From(string.Empty),
                new(true, InternalAccessibility),
                error: new("Avalonia path/namespace glob matching timed out; simplify the filter or use an explicit typed validation contract"));
        }
    }

    /// <summary>Reads only additional files marked as source-generator inputs by their platform.</summary>
    /// <param name="file">The additional XAML input.</param>
    /// <param name="options">The applicable analyzer options.</param>
    /// <param name="namespaceContext">The stable encoded implicit namespace metadata.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static Page? ReadRecognized(AdditionalText file, AnalyzerConfigOptionsProvider options, string namespaceContext, CancellationToken ct)
    {
        if (ProjectionDisabled(options.GlobalOptions))
        {
            return null;
        }

        var itemOptions = options.GetOptions(file);
        var avalonia = IsAvaloniaInput(file.Path, itemOptions, options.GlobalOptions, ct);
        if (!avalonia && !IsMauiInput(itemOptions))
        {
            return null;
        }

        var text = file.GetText(ct);
        var profile = ReadProfile(options.GlobalOptions, avalonia, out var failure);
        if (text is null)
        {
            return new(file.Path, SourceText.From(string.Empty), profile, error: new("The recognized XAML input cannot be read"));
        }

        return failure is not null
            ? new(file.Path, text, profile, error: new(failure))
            : ParsePage(file.Path, text, profile, itemOptions, options.GlobalOptions, namespaceContext, ct);
    }

    /// <summary>Reads the explicit projection opt-out.</summary>
    /// <param name="options">The applicable analyzer options.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ProjectionDisabled(AnalyzerConfigOptions options) =>
        options.TryGetValue("build_property.ReactiveUIValidationProducerProjectionEnabled", out var enabled)
        && string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>Recognizes Avalonia's eligible additional-file inputs.</summary>
    /// <param name="path">The original additional-file path.</param>
    /// <param name="itemOptions">The additional-file analyzer metadata.</param>
    /// <param name="globals">The global analyzer configuration.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAvaloniaInput(string path, AnalyzerConfigOptions itemOptions, AnalyzerConfigOptions globals, CancellationToken ct) =>
        itemOptions.TryGetValue("build_metadata.AdditionalFiles.SourceItemGroup", out var group)
        && group == "AvaloniaXaml"
        && HasAvaloniaExtension(path)
        && AvaloniaEnabled(globals)
        && MatchesFilter(globals, "build_property.AvaloniaNameGeneratorFilterByPath", path, ct);

    /// <summary>Recognizes Avalonia's supported filename extensions.</summary>
    /// <param name="path">The original additional-file path.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasAvaloniaExtension(string path) => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".paml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads Avalonia's default-enabled boolean option.</summary>
    /// <param name="options">The applicable analyzer options.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AvaloniaEnabled(AnalyzerConfigOptions options) =>
        !options.TryGetValue("build_property.AvaloniaNameGeneratorIsEnabled", out var enabled) || !bool.TryParse(enabled, out var parsed) || parsed;

    /// <summary>Recognizes MAUI's selected generation item kind.</summary>
    /// <param name="options">The applicable analyzer options.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMauiInput(AnalyzerConfigOptions options) =>
        options.TryGetValue("build_metadata.AdditionalFiles.GenKind", out var kind) && kind == "Xaml";

    /// <summary>Reads exact platform behavior and default accessibility options.</summary>
    /// <param name="options">The applicable analyzer options.</param>
    /// <param name="avalonia">Whether the Avalonia producer contract applies.</param>
    /// <param name="failure">The actionable resolution failure.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static PageProfile ReadProfile(AnalyzerConfigOptions options, bool avalonia, out string? failure)
    {
        failure = null;
        if (!avalonia)
        {
            return new(false, PrivateAccessibility);
        }

        var properties = false;
        if (options.TryGetValue("build_property.AvaloniaNameGeneratorBehavior", out var behavior)
            && Enum.TryParse(behavior, true, out AvaloniaBehavior parsed))
        {
            if (parsed is not AvaloniaBehavior.OnlyProperties and not AvaloniaBehavior.InitializeComponent)
            {
                failure = $"Invalid Avalonia name-generator behavior '{behavior}'";
            }

            properties = parsed == AvaloniaBehavior.OnlyProperties;
        }

        var access = InternalAccessibility;
        if (options.TryGetValue("build_property.AvaloniaNameGeneratorDefaultFieldModifier", out var modifier))
        {
            access = AvaloniaDefaultAccessibility(modifier);
            if (access is null)
            {
                failure = $"Invalid Avalonia named-field modifier '{modifier}'";
            }
        }

        return new(true, access ?? InternalAccessibility, properties);
    }

    /// <summary>Loads one recognized XML input with its lexical namespace context.</summary>
    /// <param name="path">The original additional-file path.</param>
    /// <param name="text">The immutable source text.</param>
    /// <param name="profile">The selected platform declaration options.</param>
    /// <param name="itemOptions">The additional-file analyzer metadata.</param>
    /// <param name="globals">The global analyzer configuration.</param>
    /// <param name="namespaceContext">The stable encoded implicit namespace metadata.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static Page? ParsePage(
        string path,
        SourceText text,
        PageProfile profile,
        AnalyzerConfigOptions itemOptions,
        AnalyzerConfigOptions globals,
        string namespaceContext,
        CancellationToken ct)
    {
        try
        {
            var prefixes = ReadPrefixes(profile.Avalonia ? string.Empty : namespaceContext);
            using var reader = CreateReader(text, prefixes, ct);
            var root = XDocument.Load(reader, LoadOptions.SetLineInfo).Root;
            ct.ThrowIfCancellationRequested();
            var ownerName = root is null ? null : LanguageAttribute(root, "Class", profile.Avalonia)?.Value;
            if (string.IsNullOrWhiteSpace(ownerName) || !MatchesOwnerFilter(ownerName!, profile.Avalonia, globals, ct))
            {
                return null;
            }

            if (!profile.Avalonia && root is not null && itemOptions.TryGetValue("build_property.TargetFramework", out var target))
            {
                ApplyMauiPlatformSelection(root, target, ct);
            }

            return new(path, text, profile, root, ownerName, prefixes: prefixes);
        }
        catch (XmlException exception)
        {
            return new(path, text, profile, error: new($"Malformed recognized XAML: {exception.Message}", exception.LineNumber, exception.LinePosition));
        }
        catch (ArgumentException exception)
        {
            return new(path, text, profile, error: new($"Invalid XAML namespace metadata: {exception.Message}"));
        }
    }

    /// <summary>Reads the stable encoded implicit namespace metadata.</summary>
    /// <param name="namespaceContext">The stable encoded implicit namespace metadata.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static Dictionary<string, string> ReadPrefixes(string namespaceContext)
    {
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        var values = namespaceContext.Split('\0');
        for (var index = 0; index + 1 < values.Length; index += NamespacePairLength)
        {
            prefixes[values[index]] = values[index + 1];
        }

        return prefixes;
    }

    /// <summary>Creates a secure reader with the platform's implicit namespace context.</summary>
    /// <param name="text">The immutable source text.</param>
    /// <param name="prefixes">The implicit XML prefix mappings.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static XmlReader CreateReader(SourceText text, Dictionary<string, string> prefixes, CancellationToken ct)
    {
        var namespaces = new XmlNamespaceManager(new NameTable());
        foreach (var pair in prefixes)
        {
            namespaces.AddNamespace(pair.Key, pair.Value);
        }

        return XmlReader.Create(
            new CancellationReader(text.ToString(), ct),
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = true,
                ConformanceLevel = prefixes.Count == 0 ? ConformanceLevel.Document : ConformanceLevel.Fragment,
            },
            new XmlParserContext(namespaces.NameTable, namespaces, null, XmlSpace.None));
    }

    /// <summary>Applies Avalonia's namespace filter to the declared owner.</summary>
    /// <param name="ownerName">The XAML code-behind class identity.</param>
    /// <param name="avalonia">Whether the Avalonia producer contract applies.</param>
    /// <param name="globals">The global analyzer configuration.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesOwnerFilter(string ownerName, bool avalonia, AnalyzerConfigOptions globals, CancellationToken ct) =>
        !avalonia || MatchesFilter(globals, "build_property.AvaloniaNameGeneratorFilterByNamespace", OwnerNamespace(ownerName), ct);

    /// <summary>Reads the declared owner's namespace for Avalonia glob matching.</summary>
    /// <param name="ownerName">The complete XAML class identity.</param>
    /// <returns>The declaring namespace or an empty global namespace.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string OwnerNamespace(string ownerName) => ownerName.Remove(Math.Max(ownerName.LastIndexOf('.'), 0));

    /// <summary>Resolves each named field against the original compilation.</summary>
    /// <param name="compilation">The original semantic compilation.</param>
    /// <param name="pages">The immutable parsed additional inputs.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static Result Resolve(Compilation compilation, ImmutableArray<Page?> pages, CancellationToken ct)
    {
        var result = new CollectionResult();
        var resolver = new TypeResolver(compilation, ct);
        var names = new Dictionary<INamedTypeSymbol, HashSet<string>>(SymbolEqualityComparer.Default);
        var orderedPages = new List<Page>();
        foreach (var page in pages)
        {
            if (page is not null)
            {
                orderedPages.Add(page);
            }
        }

        orderedPages.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        foreach (var page in orderedPages)
        {
            ResolvePage(page, compilation, resolver, names, result, ct);
        }

        return new(result.Declarations.ToImmutable(), result.Diagnostics.ToImmutable());
    }

    /// <summary>Resolves the owner and named members for one input.</summary>
    /// <param name="page">The recognized XAML input.</param>
    /// <param name="compilation">The original semantic compilation.</param>
    /// <param name="resolver">The original-compilation type resolver.</param>
    /// <param name="names">The names already encountered in producer order.</param>
    /// <param name="result">The accumulated declarations and diagnostics.</param>
    /// <param name="ct">The cancellation token.</param>
    private static void ResolvePage(
        Page page,
        Compilation compilation,
        TypeResolver resolver,
        Dictionary<INamedTypeSymbol, HashSet<string>> names,
        CollectionResult result,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (page.Error is { } error)
        {
            result.Diagnostics.Add(Diagnostic.Create(InvalidXaml, page.Location(null), error.Message));
            return;
        }

        var owner = FindOwner(compilation, page.OwnerName!, ct);
        if (owner is null || !IsPartialChain(owner, ct))
        {
            result.Diagnostics.Add(Diagnostic.Create(
                InvalidXaml,
                page.Location(page.Root),
                $"XAML class '{page.OwnerName}' must resolve to an unambiguous source type with partial containing declarations"));
            return;
        }

        if (page.Avalonia && !resolver.IsAvaloniaElement(owner))
        {
            return;
        }

        AddMauiBase(page, owner, resolver, result);
        if (!names.TryGetValue(owner, out var ownerNames))
        {
            ownerNames = new(StringComparer.Ordinal);
            names.Add(owner, ownerNames);
        }

        Collect(page.Root!, new(page, owner, ownerNames, resolver, result, ct));
    }

    /// <summary>Adds MAUI's generated base when the original owner has no explicit base.</summary>
    /// <param name="page">The recognized XAML input.</param>
    /// <param name="owner">The original source declaring type.</param>
    /// <param name="resolver">The original-compilation type resolver.</param>
    /// <param name="result">The accumulated declarations and diagnostics.</param>
    private static void AddMauiBase(Page page, INamedTypeSymbol owner, TypeResolver resolver, CollectionResult result)
    {
        if (page.Avalonia || !resolver.HasMauiMetadata || owner.BaseType?.SpecialType != SpecialType.System_Object)
        {
            return;
        }

        var baseType = resolver.ResolveElement(page.Root!, page, out var failure);
        if (baseType is null)
        {
            result.Diagnostics.Add(Diagnostic.Create(InvalidXaml, page.Location(page.Root), $"XAML class '{page.OwnerName}' base type: {failure}"));
            return;
        }

        result.Declarations.Add(new(
            owner,
            string.Empty,
            string.Empty,
            baseType: ProducerProjection.TypeName(baseType),
            profile: MauiProfile,
            location: page.Location(page.Root)));
    }

    /// <summary>Collects platform named members while traversing property elements.</summary>
    /// <param name="element">The original XML element.</param>
    /// <param name="context">The stable named-member traversal state.</param>
    private static void Collect(XElement element, CollectionContext context)
    {
        context.Cancellation.ThrowIfCancellationRequested();

        // The pinned Avalonia transformer clears template names and all descendants before name resolution.
        if (context.Page.Avalonia && (element.Name.LocalName is DataTemplateName or "ControlTemplate"))
        {
            return;
        }

        var name = ReadName(element, context.Page);
        if (name is not null)
        {
            CollectName(element, name, context);
        }

        if (IsMauiNameScope(element, context.Page.Avalonia))
        {
            return;
        }

        foreach (var child in element.Elements())
        {
            Collect(child, context);
        }
    }

    /// <summary>Reads the platform's supported name attribute or property element.</summary>
    /// <param name="element">The original XML element.</param>
    /// <param name="page">The recognized XAML input.</param>
    /// <returns>The resolved producer contract value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? ReadName(XElement element, Page page) => LanguageAttribute(element, "Name", page.Avalonia)?.Value
        ?? (page.Avalonia ? AvaloniaName(element) : null);

    /// <summary>Reads Avalonia's ordinary Name property as an attribute or property element.</summary>
    /// <param name="element">The original named XML object element.</param>
    /// <returns>The first lexical ordinary Name property value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? AvaloniaName(XElement element) => element.Attribute("Name")?.Value
        ?? element.Element(XName.Get($"{element.Name.LocalName}.Name", element.Name.NamespaceName))?.Value;

    /// <summary>Checks MAUI's exact descendant-excluding framework scopes.</summary>
    /// <param name="element">The original XML element.</param>
    /// <param name="avalonia">Whether the Avalonia producer contract applies.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMauiNameScope(XElement element, bool avalonia) => !avalonia
        && (element.Name.NamespaceName is MauiNamespace or MauiGlobalNamespace)
        && (element.Name.LocalName is DataTemplateName or "ControlTemplate" or "Style" or "VisualStateManager.VisualStateGroups");

    /// <summary>Validates one named node and resolves its declaration contract.</summary>
    /// <param name="element">The original XML element.</param>
    /// <param name="name">The requested member or type name.</param>
    /// <param name="context">The generator initialization context.</param>
    private static void CollectName(XElement element, string name, CollectionContext context)
    {
        var page = context.Page;
        var location = page.Location(element);
        var token = SyntaxFactory.ParseToken(name);
        var validName = token.IsKind(SyntaxKind.IdentifierToken) || (!page.Avalonia && SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None);
        if (!validName || token.ContainsDiagnostics || token.Span.Length != name.Length)
        {
            context.Result.Diagnostics.Add(Diagnostic.Create(
                InvalidXaml,
                location,
                $"XAML field name '{name}' is invalid or appears more than once in '{page.OwnerName}'"));
            return;
        }

        name = token.ValueText;
        if (!context.Names.Add(name))
        {
            if (!page.Avalonia)
            {
                context.Result.Diagnostics.Add(Diagnostic.Create(InvalidXaml, location, $"XAML field name '{name}' appears more than once in '{page.OwnerName}'"));
            }

            return;
        }

        // Before-Csc members, including WPF and WinUI fields, retain their original symbols.
        if (context.Owner.GetMembers(name).IsEmpty)
        {
            AddNamedDeclaration(element, name, context);
        }
    }

    /// <summary>Adds a safely resolved field or get-only property contract.</summary>
    /// <param name="element">The original XML element.</param>
    /// <param name="name">The requested member or type name.</param>
    /// <param name="context">The generator initialization context.</param>
    private static void AddNamedDeclaration(XElement element, string name, CollectionContext context)
    {
        var page = context.Page;
        var type = context.Resolver.ResolveElement(element, page, out var failure);
        var location = page.Location(element);
        if (type is null)
        {
            context.Result.Diagnostics.Add(Diagnostic.Create(InvalidXaml, location, $"XAML field '{name}': {failure}"));
            return;
        }

        if (page.Avalonia && !context.Resolver.IsAvaloniaElement(type))
        {
            return;
        }

        var access = Accessibility(LanguageAttribute(element, "FieldModifier", page.Avalonia)?.Value, page.Accessibility);
        var typeName = ProducerProjection.TypeName(type);
        var body = page.Properties ? " { get => throw new global::System.InvalidOperationException(); }" : ";";
        var declaration = $"#nullable disable\n{access} {typeName} @{name}{body}\n#nullable enable";
        context.Result.Declarations.Add(new(context.Owner, name, declaration, profile: page.Avalonia ? AvaloniaProfile : MauiProfile, location: location));
    }

    /// <summary>Maps platform field-modifier spellings to C# accessibility.</summary>
    /// <param name="value">The configured value to compare.</param>
    /// <param name="fallback">The platform default accessibility.</param>
    /// <returns>The resolved producer contract value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Accessibility(string? value, string fallback) => value?.ToLowerInvariant() switch
    {
        "public" => "public",
        InternalAccessibility or "notpublic" => InternalAccessibility,
        "protected" => "protected",
        PrivateAccessibility => PrivateAccessibility,
        _ => fallback,
    };

    /// <summary>Reads Avalonia's enum-based default modifier, including invalid-value fallback.</summary>
    /// <param name="value">The configured value to compare.</param>
    /// <returns>The resolved producer contract value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? AvaloniaDefaultAccessibility(string value) => !Enum.TryParse(value, true, out AvaloniaFieldModifier modifier)
        ? InternalAccessibility
        : modifier switch
        {
            AvaloniaFieldModifier.Public => "public",
            AvaloniaFieldModifier.Private => PrivateAccessibility,
            AvaloniaFieldModifier.Internal => InternalAccessibility,
            AvaloniaFieldModifier.Protected => "protected",
            _ => null,
        };

    /// <summary>Reads XAML language attributes with the platform's supported language namespaces.</summary>
    /// <param name="element">The original XML element.</param>
    /// <param name="name">The requested member or type name.</param>
    /// <param name="avalonia">Whether the Avalonia producer contract applies.</param>
    /// <returns>The resolved producer contract value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static XAttribute? LanguageAttribute(XElement element, string name, bool avalonia) =>
        element.Attribute(XName.Get(name, LanguageNamespace)) ?? (avalonia ? null : element.Attribute(XName.Get(name, Language2009Namespace)));

    /// <summary>Extracts only namespace metadata, so ordinary C# edits do not repeat XML parsing.</summary>
    /// <param name="compilation">The original semantic compilation.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static string ImplicitMauiNamespaces(Compilation compilation, CancellationToken ct)
    {
        if (!ImplicitNamespacesEnabled(compilation.Assembly))
        {
            return string.Empty;
        }

        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal) { [string.Empty] = MauiGlobalNamespace };
        ReadAssemblyPrefixes(compilation.Assembly, prefixes, ct);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ReadAssemblyPrefixes(assembly, prefixes, ct);
        }

        var sorted = new List<string>(prefixes.Keys);
        sorted.Sort(StringComparer.Ordinal);
        var output = new StringBuilder();
        foreach (var prefix in sorted)
        {
            if (output.Length > 0)
            {
                _ = output.Append('\0');
            }

            _ = output.Append(prefix).Append('\0').Append(prefixes[prefix]);
        }

        return output.ToString();
    }

    /// <summary>Reads MAUI's assembly-level implicit namespace opt-in.</summary>
    /// <param name="assembly">The original or referenced assembly symbol.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    private static bool ImplicitNamespacesEnabled(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "Microsoft.Maui.Controls.Xaml.Internals.AllowImplicitXmlnsDeclarationAttribute"
                && (attribute.ConstructorArguments.IsEmpty || attribute.ConstructorArguments[0].Value is true))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Accumulates the declared implicit prefixes in producer enumeration order.</summary>
    /// <param name="assembly">The original or referenced assembly symbol.</param>
    /// <param name="prefixes">The implicit XML prefix mappings.</param>
    /// <param name="ct">The cancellation token.</param>
    private static void ReadAssemblyPrefixes(IAssemblySymbol assembly, Dictionary<string, string> prefixes, CancellationToken ct)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            ct.ThrowIfCancellationRequested();
            if (attribute.AttributeClass?.ToDisplayString() == "Microsoft.Maui.Controls.XmlnsPrefixAttribute"
                && attribute.ConstructorArguments.Length >= NamespacePairLength
                && attribute.ConstructorArguments[0].Value is string uri && attribute.ConstructorArguments[1].Value is string prefix)
            {
                prefixes[prefix] = uri;
            }
        }
    }

    /// <summary>Matches Avalonia's semicolon-separated case-insensitive wildcard filters.</summary>
    /// <param name="options">The applicable analyzer options.</param>
    /// <param name="key">The analyzer configuration key.</param>
    /// <param name="value">The configured value to compare.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    private static bool MatchesFilter(AnalyzerConfigOptions options, string key, string value, CancellationToken ct)
    {
        var patterns = options.TryGetValue(key, out var configured) ? configured.Split(';') : new[] { "*" };
        foreach (var pattern in patterns)
        {
            if (MatchesWildcard(pattern, value, ct))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Matches the exact Avalonia 12.1.3 regex-based glob contract.</summary>
    /// <param name="pattern">The untrimmed configured wildcard pattern.</param>
    /// <param name="value">The original path or declared namespace.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the pinned producer selects the input.</returns>
    [SuppressMessage(
        "Injection",
        "SES1303:Regular-expression pattern must not be built from non-constant data",
        Justification = "Avalonia 12.1.3 intentionally accepts build-time glob filters; all literal text is Regex.Escape'd before fixed wildcard substitutions and anchors, with bounded execution.")]
    private static bool MatchesWildcard(string pattern, string value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Regex.IsMatch(value, $"^{Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".")}$", RegexOptions.IgnoreCase | RegexOptions.Singleline, GlobTimeout);
    }

    /// <summary>Applies MAUI's declared OnPlatform subtree selection before collecting names.</summary>
    /// <param name="root">The parsed XML document root.</param>
    /// <param name="targetFramework">The selected target framework.</param>
    /// <param name="ct">The cancellation token.</param>
    private static void ApplyMauiPlatformSelection(XElement root, string targetFramework, CancellationToken ct)
    {
        var target = MauiTarget(targetFramework);
        if (target is null)
        {
            return;
        }

        foreach (var platform in root.DescendantsAndSelf(XName.Get("OnPlatform", MauiNamespace)))
        {
            RemoveOtherPlatforms(platform, target, ct);
        }
    }

    /// <summary>Determines the target platform using MAUI's framework-name matching order.</summary>
    /// <param name="framework">The selected target framework name.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static string? MauiTarget(string framework)
    {
        string? target = null;
        foreach (var pair in MauiTargets)
        {
            if (framework.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target = pair.Value;
            }
        }

        return target;
    }

    /// <summary>Removes only named On branches that target other platforms.</summary>
    /// <param name="platform">The OnPlatform XML element.</param>
    /// <param name="target">The exact target platform name.</param>
    /// <param name="ct">The cancellation token.</param>
    private static void RemoveOtherPlatforms(XElement platform, string target, CancellationToken ct)
    {
        var child = platform.FirstNode;
        while (child is not null)
        {
            ct.ThrowIfCancellationRequested();
            var next = child.NextNode;
            if (child is XElement item && item.Name == XName.Get("On", MauiNamespace)
                && item.Attribute(nameof(Platform)) is { } platforms && !TargetsPlatform(platforms.Value, target))
            {
                item.Remove();
            }

            child = next;
        }
    }

    /// <summary>Checks exact trimmed platform names from a comma-separated selector.</summary>
    /// <param name="platforms">The comma-separated platform names.</param>
    /// <param name="target">The exact target platform name.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    private static bool TargetsPlatform(string platforms, string target)
    {
        foreach (var value in platforms.Split(','))
        {
            if (value.Trim() == target)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds source owners, including nested and generic partial types.</summary>
    /// <param name="compilation">The original semantic compilation.</param>
    /// <param name="name">The requested member or type name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static INamedTypeSymbol? FindOwner(Compilation compilation, string name, CancellationToken ct)
    {
        if (compilation.GetTypeByMetadataName(name) is { } exact)
        {
            return exact;
        }

        INamedTypeSymbol? candidate = null;
        foreach (var symbol in compilation.GetSymbolsWithName(static _ => true, SymbolFilter.Type, ct))
        {
            if (symbol is not INamedTypeSymbol type || !HasSourceLocation(type) || OwnerName(type) != name)
            {
                continue;
            }

            if (candidate is not null)
            {
                return null;
            }

            candidate = type;
        }

        return candidate;
    }

    /// <summary>Identifies types declared in the original compilation.</summary>
    /// <param name="type">The semantic named type.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    private static bool HasSourceLocation(INamedTypeSymbol type)
    {
        foreach (var location in type.Locations)
        {
            if (location.IsInSource)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Formats a XAML class identity without generic arguments.</summary>
    /// <param name="type">The semantic named type.</param>
    /// <returns>The resolved producer contract value.</returns>
    private static string OwnerName(INamedTypeSymbol type)
    {
        var prefix = type.ContainingType is { } parent ? OwnerName(parent) : type.ContainingNamespace.ToDisplayString();
        return type.ContainingNamespace.IsGlobalNamespace && type.ContainingType is null ? type.Name : $"{prefix}.{type.Name}";
    }

    /// <summary>Checks that every source part can participate in declaration projection.</summary>
    /// <param name="owner">The original source declaring type.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    private static bool IsPartialChain(INamedTypeSymbol owner, CancellationToken ct)
    {
        for (var current = owner; current is not null; current = current.ContainingType)
        {
            if (!HasPartialDeclarations(current, ct))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks each original partial declaration of one containing type.</summary>
    /// <param name="type">The semantic named type.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the requested producer contract matches.</returns>
    private static bool HasPartialDeclarations(INamedTypeSymbol type, CancellationToken ct)
    {
        if (type.DeclaringSyntaxReferences.IsEmpty)
        {
            return false;
        }

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(ct) is not TypeDeclarationSyntax declaration || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Resolves XML names using assembly identities and unambiguous namespace mappings.</summary>
    internal sealed class TypeResolver
    {
        /// <summary>The using namespace prefix contract value.</summary>
        private const string UsingNamespacePrefix = "using:";

        /// <summary>The clr namespace prefix contract value.</summary>
        private const string ClrNamespacePrefix = "clr-namespace:";

        /// <summary>The assembly qualifier contract value.</summary>
        private const string AssemblyQualifier = "assembly=";

        /// <summary>The namespace definition attribute contract value.</summary>
        private const string NamespaceDefinitionAttribute = "XmlnsDefinitionAttribute";

        /// <summary>The original semantic compilation.</summary>
        private readonly Compilation _compilation;

        /// <summary>The resolution cancellation token.</summary>
        private readonly CancellationToken _ct;

        /// <summary>The original assembly and its direct references.</summary>
        private readonly ImmutableArray<IAssemblySymbol> _assemblies;

        /// <summary>CLR namespace mappings declared by the original assembly and its references.</summary>
        private readonly List<NamespaceMapping> _mappings;

        /// <summary>Initializes a new instance of the <see cref="TypeResolver"/> class.</summary>
        /// <param name="compilation">The original semantic compilation.</param>
        /// <param name="ct">The cancellation token.</param>
        internal TypeResolver(Compilation compilation, CancellationToken ct)
        {
            _compilation = compilation;
            _ct = ct;
            _assemblies = ImmutableArray.Create(compilation.Assembly).AddRange(compilation.SourceModule.ReferencedAssemblySymbols);
            _mappings = ReadMappings(_assemblies, ct);
        }

        /// <summary>Gets whether actual MAUI metadata supplies the generated root's base contract.</summary>
        internal bool HasMauiMetadata => _compilation.GetTypeByMetadataName("Microsoft.Maui.Controls.BindableObject") is not null;

        /// <summary>Resolves explicit lexical namespaces before inherited implicit MAUI prefixes.</summary>
        /// <param name="element">The original XML element.</param>
        /// <param name="prefix">The lexical XML namespace prefix.</param>
        /// <param name="page">The recognized XAML input.</param>
        /// <returns>The resolved producer contract value.</returns>
        internal static string? ResolvePrefix(XElement element, string prefix, Page page)
        {
            foreach (var ancestor in element.AncestorsAndSelf())
            {
                var attribute = ancestor.Attribute(prefix.Length == 0 ? XName.Get("xmlns") : XNamespace.Xmlns + prefix);
                if (attribute is not null)
                {
                    return attribute.Value;
                }
            }

            var fallback = prefix.Length == 0 ? string.Empty : null;
            return page.Prefixes.TryGetValue(prefix, out var uri) ? uri : fallback;
        }

        /// <summary>Copies an explicit lexical segment without requiring System.Range in netstandard2.0.</summary>
        /// <param name="value">The original lexical value.</param>
        /// <param name="start">The segment's starting character offset.</param>
        /// <param name="length">The segment's explicit character length.</param>
        /// <returns>The requested lexical segment.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string Slice(string value, int start, int length) => value.Remove(start + length).Remove(0, start);

        /// <summary>Joins CLR names without adding a separator to the global namespace.</summary>
        /// <param name="namespaceName">The CLR namespace name.</param>
        /// <param name="typeName">The CLR metadata type name.</param>
        /// <returns>The resolved producer contract value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string MetadataName(string namespaceName, string typeName) => namespaceName.Length == 0 ? typeName : $"{namespaceName}.{typeName}";

        /// <summary>Matches either a simple or complete assembly identity.</summary>
        /// <param name="assembly">The original or referenced assembly symbol.</param>
        /// <param name="name">The requested member or type name.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool MatchesAssembly(IAssemblySymbol assembly, string name) =>
            string.Equals(assembly.Identity.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(assembly.Identity.ToString(), name, StringComparison.OrdinalIgnoreCase);

        /// <summary>Adds one distinct resolved type.</summary>
        /// <param name="symbol">The resolved candidate type.</param>
        /// <param name="candidates">The distinct candidate type symbols.</param>
        internal static void Add(INamedTypeSymbol? symbol, List<INamedTypeSymbol> candidates)
        {
            if (symbol is null)
            {
                return;
            }

            foreach (var candidate in candidates)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, symbol))
                {
                    return;
                }
            }

            candidates.Add(symbol);
        }

        /// <summary>Recognizes the explicit CLR namespace URI forms.</summary>
        /// <param name="uri">The XML namespace URI.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsClrNamespace(string uri) => uri.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal)
            || uri.StartsWith(UsingNamespacePrefix, StringComparison.Ordinal);

        /// <summary>Reads exact supported assembly qualification without silently ignoring clauses.</summary>
        /// <param name="uri">The XML namespace URI.</param>
        /// <param name="namespaceName">The CLR namespace name.</param>
        /// <param name="assemblyName">The explicitly selected assembly identity.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        internal static bool ParseClrNamespace(string uri, out string namespaceName, out string? assemblyName)
        {
            var prefix = uri.StartsWith(UsingNamespacePrefix, StringComparison.Ordinal) ? UsingNamespacePrefix : ClrNamespacePrefix;
            var pieces = uri.Remove(0, prefix.Length).Split(';');
            namespaceName = pieces[0];
            assemblyName = null;
            for (var index = 1; index < pieces.Length; index++)
            {
                var clause = pieces[index];
                if (!clause.StartsWith(AssemblyQualifier, StringComparison.Ordinal) || assemblyName is not null)
                {
                    return false;
                }

                assemblyName = clause.Remove(0, AssemblyQualifier.Length);
            }

            return true;
        }

        /// <summary>Reads namespace definitions once per semantic compilation revision.</summary>
        /// <param name="assemblies">The original assembly and its direct references.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The resolved producer contract value.</returns>
        internal static List<NamespaceMapping> ReadMappings(ImmutableArray<IAssemblySymbol> assemblies, CancellationToken ct)
        {
            var mappings = new List<NamespaceMapping>();
            foreach (var assembly in assemblies)
            {
                foreach (var attribute in assembly.GetAttributes())
                {
                    ct.ThrowIfCancellationRequested();
                    if (ReadMapping(attribute, assembly) is { } mapping)
                    {
                        mappings.Add(mapping);
                    }
                }
            }

            return mappings;
        }

        /// <summary>Reads a supported assembly-level namespace definition.</summary>
        /// <param name="attribute">The namespace-definition attribute.</param>
        /// <param name="assembly">The original or referenced assembly symbol.</param>
        /// <returns>The resolved producer contract value.</returns>
        internal static NamespaceMapping? ReadMapping(AttributeData attribute, IAssemblySymbol assembly)
        {
            if (attribute.AttributeClass?.Name != NamespaceDefinitionAttribute || attribute.ConstructorArguments.Length < NamespacePairLength
                || attribute.ConstructorArguments[0].Value is not string uri || attribute.ConstructorArguments[1].Value is not string clrNamespace)
            {
                return null;
            }

            string? target = null;
            foreach (var pair in attribute.NamedArguments)
            {
                if (pair.Key == "AssemblyName")
                {
                    target = pair.Value.Value as string;
                }
            }

            return new(uri, clrNamespace, target, assembly);
        }

        /// <summary>Splits generic argument tokens while checking balanced parentheses.</summary>
        /// <param name="text">The immutable source text.</param>
        /// <param name="tokens">The balanced lexical argument tokens.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        internal static bool ArgumentTokens(string text, out List<string> tokens, CancellationToken ct)
        {
            tokens = [];
            var depth = 0;
            var start = 0;
            for (var index = 0; index <= text.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                var character = index == text.Length ? ',' : text[index];
                depth = character switch
                {
                    '(' => depth + 1,
                    ')' => depth - 1,
                    _ => depth,
                };
                if (depth < 0 || (index == text.Length && depth != 0))
                {
                    return false;
                }

                if (character != ',' || depth != 0)
                {
                    continue;
                }

                tokens.Add(Slice(text, start, index - start).Trim());
                start = index + 1;
            }

            return true;
        }

        /// <summary>Converts recursive named generic arguments to Roslyn's construction input.</summary>
        /// <param name="arguments">The recursive generic argument symbols.</param>
        /// <returns>The resolved producer contract value.</returns>
        internal static ITypeSymbol[] ConstructionArguments(ImmutableArray<INamedTypeSymbol> arguments)
        {
            var result = new ITypeSymbol[arguments.Length];
            ImmutableArray<ITypeSymbol>.CastUp(arguments).CopyTo(result);

            return result;
        }

        /// <summary>Resolves the type and recursive generic arguments of an XML element.</summary>
        /// <param name="element">The original XML element.</param>
        /// <param name="page">The recognized XAML input.</param>
        /// <param name="failure">The actionable resolution failure.</param>
        /// <returns>The resolved producer contract value.</returns>
        internal INamedTypeSymbol? ResolveElement(XElement element, Page page, out string failure)
        {
            var arguments = LanguageAttribute(element, "TypeArguments", page.Avalonia)?.Value;
            var types = ImmutableArray<INamedTypeSymbol>.Empty;
            return arguments is not null && !ParseArguments(arguments, element, page, out types, out failure)
                ? null
                : ResolveName(element.Name.NamespaceName, element.Name.LocalName, types, page.Avalonia, out failure);
        }

        /// <summary>Applies Avalonia's named-type eligibility when its framework metadata is present.</summary>
        /// <param name="type">The semantic named type.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        internal bool IsAvaloniaElement(INamedTypeSymbol type)
        {
            var element = _compilation.GetTypeByMetadataName("Avalonia.StyledElement");
            if (element is null)
            {
                // Explicit typed fixtures have no UI-framework metadata; final actual symbols still must match.
                return true;
            }

            for (var current = type; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, element))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parses a comma-separated XAML type list with recursive generic arguments.</summary>
        /// <param name="text">The immutable source text.</param>
        /// <param name="element">The original XML element.</param>
        /// <param name="page">The recognized XAML input.</param>
        /// <param name="types">The types.</param>
        /// <param name="failure">The actionable resolution failure.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        private bool ParseArguments(string text, XElement element, Page page, out ImmutableArray<INamedTypeSymbol> types, out string failure)
        {
            types = default;
            if (!ArgumentTokens(text, out var tokens, _ct))
            {
                failure = $"malformed x:TypeArguments '{text}'";
                return false;
            }

            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            foreach (var token in tokens)
            {
                _ct.ThrowIfCancellationRequested();
                var type = ParseArgument(token, element, page, out failure);
                if (type is null)
                {
                    return false;
                }

                builder.Add(type);
            }

            types = builder.ToImmutable();
            failure = string.Empty;
            return true;
        }

        /// <summary>Resolves one lexical generic argument and its nested argument list.</summary>
        /// <param name="token">The current lexical generic type argument.</param>
        /// <param name="element">The original XML element.</param>
        /// <param name="page">The recognized XAML input.</param>
        /// <param name="failure">The actionable resolution failure.</param>
        /// <returns>The resolved producer contract value.</returns>
        private INamedTypeSymbol? ParseArgument(string token, XElement element, Page page, out string failure)
        {
            var open = token.IndexOf('(');
            var arguments = ImmutableArray<INamedTypeSymbol>.Empty;
            if (open >= 0 && !NestedArguments(token, open, element, page, out arguments, out failure))
            {
                return null;
            }

            var qualified = open < 0 ? token : token.Remove(open).Trim();
            var colon = qualified.IndexOf(':');
            var prefix = colon < 0 ? string.Empty : qualified.Remove(colon);
            var local = colon < 0 ? qualified : qualified.Remove(0, colon + 1);
            var xmlNamespace = ResolvePrefix(element, prefix, page);
            if (local.Length == 0 || xmlNamespace is null)
            {
                failure = $"unresolved XAML type argument '{qualified}'";
                return null;
            }

            return ResolveName(xmlNamespace, local, arguments, page.Avalonia, out failure);
        }

        /// <summary>Parses a nested generic argument list and reports malformed delimiters.</summary>
        /// <param name="token">The current lexical generic type argument.</param>
        /// <param name="open">The opening generic-list delimiter position.</param>
        /// <param name="element">The original XML element.</param>
        /// <param name="page">The recognized XAML input.</param>
        /// <param name="arguments">The recursive generic argument symbols.</param>
        /// <param name="failure">The actionable resolution failure.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        private bool NestedArguments(string token, int open, XElement element, Page page, out ImmutableArray<INamedTypeSymbol> arguments, out string failure)
        {
            arguments = default;
            failure = string.Empty;
            if (!token.EndsWith(")", StringComparison.Ordinal) || !ParseArguments(Slice(token, open + 1, token.Length - open - NamespacePairLength), element, page, out arguments, out failure))
            {
                failure = $"malformed generic XAML type argument '{token}'";
                return false;
            }

            return true;
        }

        /// <summary>Resolves one XML name with MAUI's markup-extension precedence.</summary>
        /// <param name="uri">The XML namespace URI.</param>
        /// <param name="name">The requested member or type name.</param>
        /// <param name="arguments">The recursive generic argument symbols.</param>
        /// <param name="avalonia">Whether the Avalonia producer contract applies.</param>
        /// <param name="failure">The actionable resolution failure.</param>
        /// <returns>The resolved producer contract value.</returns>
        private INamedTypeSymbol? ResolveName(string uri, string name, ImmutableArray<INamedTypeSymbol> arguments, bool avalonia, out string failure)
        {
            if (!avalonia && name != DataTemplateName && !name.EndsWith("Extension", StringComparison.Ordinal))
            {
                var extension = ResolveExactName(uri, $"{name}Extension", arguments, out failure);
                if (extension is { IsStatic: false })
                {
                    return extension;
                }

                if (extension is null && failure.StartsWith("ambiguous", StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return ResolveExactName(uri, name, arguments, out failure);
        }

        /// <summary>Resolves the exact CLR type name for one XML namespace mapping.</summary>
        /// <param name="uri">The XML namespace URI.</param>
        /// <param name="name">The requested member or type name.</param>
        /// <param name="arguments">The recursive generic argument symbols.</param>
        /// <param name="failure">The actionable resolution failure.</param>
        /// <returns>The resolved producer contract value.</returns>
        private INamedTypeSymbol? ResolveExactName(string uri, string name, ImmutableArray<INamedTypeSymbol> arguments, out string failure)
        {
            _ct.ThrowIfCancellationRequested();
            var suffix = arguments.IsEmpty ? name : $"{name}`{arguments.Length}";
            var candidates = new List<INamedTypeSymbol>();
            if (!FindCandidates(uri, suffix, candidates))
            {
                failure = $"unsupported CLR namespace qualification '{uri}'";
                return null;
            }

            if (candidates.Count != 1)
            {
                var kind = candidates.Count == 0 ? "unresolved" : "ambiguous";
                failure = $"{kind} XAML type '{{{uri}}}{name}' (generic arity {arguments.Length})";
                return null;
            }

            failure = string.Empty;
            return arguments.IsEmpty ? candidates[0] : candidates[0].Construct(ConstructionArguments(arguments));
        }

        /// <summary>Collects candidates according to the exact XML namespace form.</summary>
        /// <param name="uri">The XML namespace URI.</param>
        /// <param name="suffix">The metadata type name including generic arity.</param>
        /// <param name="candidates">The distinct candidate type symbols.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        private bool FindCandidates(string uri, string suffix, List<INamedTypeSymbol> candidates)
        {
            if (uri is LanguageNamespace or Language2009Namespace)
            {
                Add(_compilation.GetTypeByMetadataName($"System.{suffix}"), candidates);
                return true;
            }

            if (IsClrNamespace(uri))
            {
                return AddClrCandidates(uri, suffix, candidates);
            }

            if (uri == MauiGlobalNamespace)
            {
                AddGlobalCandidates(suffix, candidates);
            }

            AddMappedCandidates(uri, suffix, candidates);
            return true;
        }

        /// <summary>Collects explicit CLR namespace candidates from the selected assembly identity.</summary>
        /// <param name="uri">The XML namespace URI.</param>
        /// <param name="suffix">The metadata type name including generic arity.</param>
        /// <param name="candidates">The distinct candidate type symbols.</param>
        /// <returns>Whether the requested producer contract matches.</returns>
        private bool AddClrCandidates(string uri, string suffix, List<INamedTypeSymbol> candidates)
        {
            if (!ParseClrNamespace(uri, out var namespaceName, out var assemblyName))
            {
                return false;
            }

            foreach (var assembly in _assemblies)
            {
                _ct.ThrowIfCancellationRequested();
                if (assemblyName is null || MatchesAssembly(assembly, assemblyName))
                {
                    Add(assembly.GetTypeByMetadataName(MetadataName(namespaceName, suffix)), candidates);
                }
            }

            return true;
        }

        /// <summary>Expands current-assembly MAUI global namespace declarations by one mapped URI.</summary>
        /// <param name="suffix">The metadata type name including generic arity.</param>
        /// <param name="candidates">The distinct candidate type symbols.</param>
        private void AddGlobalCandidates(string suffix, List<INamedTypeSymbol> candidates)
        {
            foreach (var mapping in _mappings)
            {
                _ct.ThrowIfCancellationRequested();
                if (mapping.Uri == MauiGlobalNamespace && SymbolEqualityComparer.Default.Equals(mapping.Source, _compilation.Assembly))
                {
                    AddMappedCandidates(mapping.ClrNamespace, suffix, candidates);
                }
            }
        }

        /// <summary>Collects mapped CLR candidates while preserving MAUI's current-assembly global scope.</summary>
        /// <param name="uri">The XML namespace URI.</param>
        /// <param name="suffix">The metadata type name including generic arity.</param>
        /// <param name="candidates">The distinct candidate type symbols.</param>
        private void AddMappedCandidates(string uri, string suffix, List<INamedTypeSymbol> candidates)
        {
            foreach (var mapping in _mappings)
            {
                _ct.ThrowIfCancellationRequested();
                if (mapping.Uri != uri || (uri == MauiGlobalNamespace && !SymbolEqualityComparer.Default.Equals(mapping.Source, _compilation.Assembly)))
                {
                    continue;
                }

                AddMappingCandidates(mapping, suffix, candidates);
            }
        }

        /// <summary>Resolves a namespace definition's explicit or declaring assembly.</summary>
        /// <param name="mapping">The qualified XML to CLR namespace mapping.</param>
        /// <param name="suffix">The metadata type name including generic arity.</param>
        /// <param name="candidates">The distinct candidate type symbols.</param>
        private void AddMappingCandidates(NamespaceMapping mapping, string suffix, List<INamedTypeSymbol> candidates)
        {
            foreach (var assembly in _assemblies)
            {
                var matches = mapping.AssemblyName is null
                    ? SymbolEqualityComparer.Default.Equals(assembly, mapping.Source)
                    : MatchesAssembly(assembly, mapping.AssemblyName);
                if (matches)
                {
                    Add(assembly.GetTypeByMetadataName(MetadataName(mapping.ClrNamespace, suffix)), candidates);
                }
            }
        }
    }

    /// <summary>One assembly-qualified XML to CLR namespace declaration.</summary>
    /// <param name="uri">The XML namespace URI.</param>
    /// <param name="clrNamespace">The CLR namespace or implicit URI.</param>
    /// <param name="assemblyName">The explicitly selected assembly identity.</param>
    /// <param name="source">The attribute declaring assembly.</param>
    internal sealed class NamespaceMapping(string uri, string clrNamespace, string? assemblyName, IAssemblySymbol source)
    {
        /// <summary>Gets the declared XML namespace URI.</summary>
        internal string Uri { get; } = uri;

        /// <summary>Gets the mapped CLR namespace or implicit global URI.</summary>
        internal string ClrNamespace { get; } = clrNamespace;

        /// <summary>Gets the explicitly selected target assembly, if supplied.</summary>
        internal string? AssemblyName { get; } = assemblyName;

        /// <summary>Gets the attribute's declaring assembly.</summary>
        internal IAssemblySymbol Source { get; } = source;
    }

    /// <summary>Platform options for one recognized additional input.</summary>
    /// <param name="avalonia">Whether the Avalonia producer contract applies.</param>
    /// <param name="accessibility">The default generated member accessibility.</param>
    /// <param name="properties">Whether the producer emits get-only properties.</param>
    internal sealed class PageProfile(bool avalonia, string accessibility, bool properties = false)
    {
        /// <summary>Gets whether Avalonia's declaration rules apply.</summary>
        internal bool Avalonia { get; } = avalonia;

        /// <summary>Gets the default named-member accessibility.</summary>
        internal string Accessibility { get; } = accessibility;

        /// <summary>Gets whether Avalonia emits get-only properties.</summary>
        internal bool Properties { get; } = properties;
    }

    /// <summary>An actionable additional-input failure with its original line position.</summary>
    /// <param name="message">The actionable input failure description.</param>
    /// <param name="line">The original XML line number.</param>
    /// <param name="column">The original XML column number.</param>
    internal sealed class PageError(string message, int line = 0, int column = 0)
    {
        /// <summary>Gets the actionable failure description.</summary>
        internal string Message { get; } = message;

        /// <summary>Gets the original XML line number.</summary>
        internal int Line { get; } = line;

        /// <summary>Gets the original XML column number.</summary>
        internal int Column { get; } = column;
    }

    /// <summary>A parsed immutable additional input.</summary>
    /// <param name="path">The original additional-file path.</param>
    /// <param name="text">The immutable source text.</param>
    /// <param name="profile">The selected platform declaration options.</param>
    /// <param name="root">The parsed XML document root.</param>
    /// <param name="ownerName">The XAML code-behind class identity.</param>
    /// <param name="error">The original-input reading or parsing failure.</param>
    /// <param name="prefixes">The implicit XML prefix mappings.</param>
    internal sealed class Page(
        string path,
        SourceText text,
        PageProfile profile,
        XElement? root = null,
        string? ownerName = null,
        PageError? error = null,
        IReadOnlyDictionary<string, string>? prefixes = null)
    {
        /// <summary>Gets the additional input path.</summary>
        internal string Path { get; } = path;

        /// <summary>Gets the immutable source revision used for diagnostic locations.</summary>
        internal SourceText Text { get; } = text;

        /// <summary>Gets the XAML code-behind class identity.</summary>
        internal string? OwnerName { get; } = ownerName;

        /// <summary>Gets whether Avalonia plain-name and accessibility conventions apply.</summary>
        internal bool Avalonia { get; } = profile.Avalonia;

        /// <summary>Gets whether Avalonia emits get-only name-reference properties.</summary>
        internal bool Properties { get; } = profile.Properties;

        /// <summary>Gets implicit MAUI namespaces available to lexical type arguments.</summary>
        internal IReadOnlyDictionary<string, string> Prefixes { get; } = prefixes ?? new Dictionary<string, string>();

        /// <summary>Gets the configured default field accessibility.</summary>
        internal string Accessibility { get; } = profile.Accessibility;

        /// <summary>Gets the parsed XML document root.</summary>
        internal XElement? Root { get; } = root;

        /// <summary>Gets an input-reading or XML-parsing failure.</summary>
        internal PageError? Error { get; } = error;

        /// <summary>Maps an XML line position back to the original additional text.</summary>
        /// <param name="element">The original XML element.</param>
        /// <returns>The resolved producer contract value.</returns>
        internal Location Location(XElement? element)
        {
            var lineNumber = Error?.Line ?? 0;
            var columnNumber = Error?.Column ?? 0;
            if (element is IXmlLineInfo info && info.HasLineInfo())
            {
                lineNumber = info.LineNumber;
                columnNumber = info.LinePosition;
            }

            var line = Math.Min(Math.Max(lineNumber - 1, 0), Text.Lines.Count - 1);
            var column = Math.Max(columnNumber - 1, 0);
            var start = Math.Min(Text.Lines[line].Start + column, Text.Lines[line].End);
            TextSpan span = new(start, 0);
            return Microsoft.CodeAnalysis.Location.Create(Path, span, Text.Lines.GetLinePositionSpan(span));
        }
    }

    /// <summary>Accumulates resolved declarations and original-input diagnostics.</summary>
    private sealed class CollectionResult
    {
        /// <summary>Gets the resolved semantic declarations.</summary>
        internal ImmutableArray<ProducerDeclaration>.Builder Declarations { get; } = ImmutableArray.CreateBuilder<ProducerDeclaration>();

        /// <summary>Gets actionable original-input diagnostics.</summary>
        internal ImmutableArray<Diagnostic>.Builder Diagnostics { get; } = ImmutableArray.CreateBuilder<Diagnostic>();
    }

    /// <summary>Owns the stable state used while traversing one XAML document.</summary>
    /// <param name="page">The recognized XAML input.</param>
    /// <param name="owner">The original source declaring type.</param>
    /// <param name="names">The names already encountered in producer order.</param>
    /// <param name="resolver">The original-compilation type resolver.</param>
    /// <param name="result">The accumulated declarations and diagnostics.</param>
    /// <param name="cancellation">The traversal cancellation token.</param>
    private sealed class CollectionContext(Page page, INamedTypeSymbol owner, HashSet<string> names, TypeResolver resolver, CollectionResult result, CancellationToken cancellation)
    {
        /// <summary>Gets the recognized input.</summary>
        internal Page Page { get; } = page;

        /// <summary>Gets its original declaring type.</summary>
        internal INamedTypeSymbol Owner { get; } = owner;

        /// <summary>Gets previously encountered names in producer order.</summary>
        internal HashSet<string> Names { get; } = names;

        /// <summary>Gets the original-compilation type resolver.</summary>
        internal TypeResolver Resolver { get; } = resolver;

        /// <summary>Gets the accumulated declarations and diagnostics.</summary>
        internal CollectionResult Result { get; } = result;

        /// <summary>Gets the traversal cancellation token.</summary>
        internal CancellationToken Cancellation { get; } = cancellation;
    }

    /// <summary>Declarations and errors from one immutable resolution step.</summary>
    /// <param name="declarations">The resolved private declaration contracts.</param>
    /// <param name="diagnostics">The original-input diagnostics.</param>
    private sealed class Result(ImmutableArray<ProducerDeclaration> declarations, ImmutableArray<Diagnostic> diagnostics)
    {
        /// <summary>Gets the successfully resolved private declarations.</summary>
        internal ImmutableArray<ProducerDeclaration> Declarations { get; } = declarations;

        /// <summary>Gets actionable additional-input failures.</summary>
        internal ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
    }

    /// <summary>Checks cancellation whenever the XML parser requests another input buffer.</summary>
    /// <param name="text">The immutable source text.</param>
    /// <param name="ct">The cancellation token.</param>
    private sealed class CancellationReader(string text, CancellationToken ct) : System.IO.StringReader(text)
    {
        /// <inheritdoc />
        public override int Read(char[] buffer, int index, int count)
        {
            ct.ThrowIfCancellationRequested();
            return base.Read(buffer, index, count);
        }
    }
}
