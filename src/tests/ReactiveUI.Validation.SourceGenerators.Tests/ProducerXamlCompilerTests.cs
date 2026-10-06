// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Tests recognized XAML inputs with the actual pinned peer producers and an explicit typed field fixture.</summary>
/// <remarks>The field fixture proves compiler integration, not execution of a MAUI or Avalonia platform host.</remarks>
public sealed class ProducerXamlCompilerTests
{
    /// <summary>The stable additional-file identity across incremental edits.</summary>
    private const string XamlPath = "FixturePage.xaml";

    /// <summary>The page type name used by the compiler fixtures.</summary>
    private const string PageTypeName = "Fixture.Page";

    /// <summary>The check method name used by the compiler fixtures.</summary>
    private const string CheckMethodName = "Check";

    /// <summary>The public accessibility used by the compiler fixtures.</summary>
    private const string PublicAccessibility = "public";

    /// <summary>The private accessibility used by the compiler fixtures.</summary>
    private const string PrivateAccessibility = "private";

    /// <summary>The maui template markup used by the compiler fixtures.</summary>
    private const string MauiTemplateMarkup = "<maui:DataTemplate><local:Missing x:Name=\"TemplateControl\" /></maui:DataTemplate>";

    /// <summary>The named control attribute used by the compiler fixtures.</summary>
    private const string NamedControlAttribute = "x:Name=\"NameControl\"";

    /// <summary>The stable named-control member identity.</summary>
    private const string ControlName = "NameControl";

    /// <summary>A reserved C# keyword that the MAUI producer escapes.</summary>
    private const string KeywordName = "class";

    /// <summary>The verbatim C# spelling of the keyword member.</summary>
    private const string VerbatimKeywordName = "@class";

    /// <summary>Malformed XML used only for ignored or explicitly disabled input tests.</summary>
    private const string UnselectedMalformedMarkup = "<Malformed";

    /// <summary>A named generic control, including a nested generic XAML argument.</summary>
    private const string PageMarkup = """
        <Page xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              xmlns:maui="http://schemas.microsoft.com/dotnet/2021/maui"
              xmlns:local="clr-namespace:Fixture" x:Class="Fixture.Page">
          <Page.Children>
            <local:Control x:Name="NameControl" x:TypeArguments="local:Control(x:String)" />
            <maui:DataTemplate><local:Missing x:Name="TemplateControl" /></maui:DataTemplate>
          </Page.Children>
        </Page>
        """;

    /// <summary>The missing control markup frontend rejection input.</summary>
    private const string MissingControlMarkup = """
        <Page
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:c="clr-namespace:Fixture"
          x:Class="Fixture.Page">
          <c:Missing x:Name="Field" />
        </Page>
        """;

    /// <summary>The malformed arguments markup frontend rejection input.</summary>
    private const string MalformedArgumentsMarkup = """
        <Page
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:c="clr-namespace:Fixture"
          x:Class="Fixture.Page">
          <c:Control x:Name="Field" x:TypeArguments="c:Control(x:String" />
        </Page>
        """;

    /// <summary>The unknown argument namespace markup frontend rejection input.</summary>
    private const string UnknownArgumentNamespaceMarkup = """
        <Page
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:c="clr-namespace:Fixture"
          x:Class="Fixture.Page">
          <c:Control x:Name="Field" x:TypeArguments="missing:String" />
        </Page>
        """;

    /// <summary>The missing assembly markup frontend rejection input.</summary>
    private const string MissingAssemblyMarkup = """
        <Page
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:c="clr-namespace:Fixture;assembly=Absent"
          x:Class="Fixture.Page">
          <c:Control x:Name="Field" x:TypeArguments="x:String" />
        </Page>
        """;

    /// <summary>The invalid control name markup frontend rejection input.</summary>
    private const string InvalidControlNameMarkup = """
        <Page
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:c="clr-namespace:Fixture"
          x:Class="Fixture.Page">
          <c:Control x:Name="Invalid-Name" x:TypeArguments="x:String" />
        </Page>
        """;

    /// <summary>The duplicate control name markup frontend rejection input.</summary>
    private const string DuplicateControlNameMarkup = """
        <Page
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:c="clr-namespace:Fixture"
          x:Class="Fixture.Page">
          <c:Control x:Name="Field" x:TypeArguments="x:String" />
          <c:Control x:Name="Field" x:TypeArguments="x:String" />
        </Page>
        """;

    /// <summary>A source-only owner and generic control for frontend failures.</summary>
    private const string PlainSource = """
        namespace Fixture
        {
            public partial class Page { }
            public class Control<T> { }
        }
        """;

    /// <summary>The same real peer input must work in both orders and retain identical output after an edit and revert.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <param name="validationFirst">Whether Validation precedes its actual peer producers.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments(false, true)]
    [Arguments(false, false)]
    [Arguments(true, true)]
    [Arguments(true, false)]
    public async Task RecognizedXamlContractsExecuteAndRevertWithActualPeers(bool reactive, bool validationFirst)
    {
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst, additionalGenerators: [new NamedFieldFixture().AsSourceGenerator()]);
        var source = Caller(reactive);
        var original = await host.RunAsync(source, additionalTexts: [new XamlInput(PageMarkup)], globalOptions: Globals(), additionalFileOptions: Options(avalonia: false));
        await AssertClean(original);
        await Assert.That(original.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
        await Assert.That(original.ValidationSources).IsNotEmpty();
        await Assert.That(original.Compilation.SyntaxTrees.Any(static tree => tree.FilePath.Contains("PrivateProducerProjection", StringComparison.Ordinal))).IsFalse();
        await Assert.That(original.GeneratedSources.Any(static file => file.HintName == "Fixture.XamlFields.g.cs")).IsTrue();

        var editedMarkup = PageMarkup.Replace("local:Control(x:String)", "x:Int32", StringComparison.Ordinal);
        var drift = await host.RunAsync(source, additionalTexts: [new XamlInput(editedMarkup)], globalOptions: Globals(), additionalFileOptions: Options(avalonia: false));
        await Assert.That((await drift.GetDispatchDiagnosticsAsync()).Any(static diagnostic => diagnostic.Id == "RUVG008")).IsTrue();

        var reverted = await host.RunAsync(source, additionalTexts: [new XamlInput(PageMarkup)], globalOptions: Globals(), additionalFileOptions: Options(avalonia: false));
        await AssertClean(reverted);
        await Assert.That(reverted.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
        await Assert.That(Hashes(reverted)).IsEqualTo(Hashes(original));
    }

    /// <summary>Ordinary, keyword and verbatim names retain the platform's actual C# member identity.</summary>
    /// <param name="name">The original XAML name.</param>
    /// <param name="memberName">The corresponding C# identifier spelling.</param>
    /// <param name="avalonia">Whether the Avalonia producer contract applies.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments(ControlName, ControlName, false)]
    [Arguments(KeywordName, VerbatimKeywordName, false)]
    [Arguments(VerbatimKeywordName, VerbatimKeywordName, false)]
    [Arguments(VerbatimKeywordName, VerbatimKeywordName, true)]
    public async Task LexicalNamesPreserveActualMemberIdentity(string name, string memberName, bool avalonia)
    {
        using var host = CapabilityCompilerHost.Create(
            reactive: false,
            additionalGenerators: [new NamedFieldFixture(memberName: memberName).AsSourceGenerator()]);
        var markup = PageMarkup.Replace(NamedControlAttribute, $"x:Name=\"{name}\"", StringComparison.Ordinal)
            .Replace(MauiTemplateMarkup, string.Empty, StringComparison.Ordinal);
        var source = Caller(reactive: false).Replace(ControlName, memberName, StringComparison.Ordinal);
        var result = await host.RunAsync(source, additionalTexts: [new XamlInput(markup)], globalOptions: Globals(PrivateAccessibility), additionalFileOptions: Options(avalonia));
        await AssertClean(result);
        await Assert.That(result.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
    }

    /// <summary>Avalonia clears both the template's own name and its descendants before resolving named members.</summary>
    /// <param name="templateName">The template type's local name, independent of its XML namespace.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments("DataTemplate")]
    [Arguments("ControlTemplate")]
    public async Task AvaloniaTemplatesExcludeOwnNamesAndDescendants(string templateName)
    {
        using var host = CapabilityCompilerHost.Create(reactive: false, additionalGenerators: [new NamedFieldFixture().AsSourceGenerator()]);
        var template = $"<local:{templateName} x:Name=\"Invalid-Name\"><local:Missing x:Name=\"MissingControl\" /></local:{templateName}>";
        var markup = PageMarkup.Replace(MauiTemplateMarkup, template, StringComparison.Ordinal);
        var result = await host.RunAsync(
            Caller(reactive: false),
            additionalTexts: [new XamlInput(markup)],
            globalOptions: Globals(PrivateAccessibility),
            additionalFileOptions: Options(avalonia: true));
        await AssertClean(result);
        await Assert.That(result.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
    }

    /// <summary>Avalonia plain names and configured accessibility follow the recognized profile.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <param name="validationFirst">Whether Validation precedes its peer producers.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments(false, true)]
    [Arguments(false, false)]
    [Arguments(true, true)]
    [Arguments(true, false)]
    public async Task AvaloniaPlainNamesAndDefaultModifierExecute(bool reactive, bool validationFirst)
    {
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst, additionalGenerators: [new NamedFieldFixture(PublicAccessibility).AsSourceGenerator()]);
        var markup = PageMarkup.Replace(NamedControlAttribute, "Name=\"NameControl\"", StringComparison.Ordinal)
            .Replace("clr-namespace:Fixture", "using:Fixture", StringComparison.Ordinal)
            .Replace(MauiTemplateMarkup, string.Empty, StringComparison.Ordinal);
        var result = await host.RunAsync(
            Caller(reactive),
            additionalTexts: [new XamlInput(markup)],
            globalOptions: Globals("Public"),
            additionalFileOptions: Options(avalonia: true));
        await AssertClean(result);
        await Assert.That(result.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
    }

    /// <summary>Avalonia Name property-element syntax contributes the same named-control contract.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AvaloniaNamePropertyElementsExecute(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive, additionalGenerators: [new NamedFieldFixture(PublicAccessibility).AsSourceGenerator()]);
        var markup = PageMarkup.Replace(
            "<local:Control x:Name=\"NameControl\" x:TypeArguments=\"local:Control(x:String)\" />",
            "<local:Control x:TypeArguments=\"local:Control(x:String)\"><local:Control.Name>NameControl</local:Control.Name></local:Control>",
            StringComparison.Ordinal)
            .Replace(MauiTemplateMarkup, string.Empty, StringComparison.Ordinal);
        var result = await host.RunAsync(
            Caller(reactive),
            additionalTexts: [new XamlInput(markup)],
            globalOptions: Globals("Public"),
            additionalFileOptions: Options(avalonia: true));
        await AssertClean(result);
        await Assert.That(result.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
    }

    /// <summary>Avalonia's OnlyProperties behavior predicts its real get-only member kind.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <param name="behavior">The platform's enum configuration spelling.</param>
    /// <param name="modifier">The configured default modifier spelling.</param>
    /// <param name="accessibility">The resulting member accessibility.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments(false, "OnlyProperties", "Public", PublicAccessibility)]
    [Arguments(true, "OnlyProperties", "Public", PublicAccessibility)]
    [Arguments(false, " 0 ", " 1 ", PrivateAccessibility)]
    [Arguments(true, " 0 ", " 1 ", PrivateAccessibility)]
    public async Task AvaloniaOnlyPropertiesPreservesGetOnlyContract(bool reactive, string behavior, string modifier, string accessibility)
    {
        using var host = CapabilityCompilerHost.Create(reactive, additionalGenerators: [new NamedFieldFixture(accessibility, property: true).AsSourceGenerator()]);
        var options = new Dictionary<string, string>(Globals(modifier)) { ["build_property.AvaloniaNameGeneratorBehavior"] = behavior };
        var markup = PageMarkup.Replace(MauiTemplateMarkup, string.Empty, StringComparison.Ordinal);
        var result = await host.RunAsync(Caller(reactive), additionalTexts: [new XamlInput(markup)], globalOptions: options, additionalFileOptions: Options(avalonia: true));
        await AssertClean(result);
        await Assert.That(result.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
        var member = result.Compilation.GetTypeByMetadataName(PageTypeName)!.GetMembers(ControlName).Single();
        await Assert.That(member is IPropertySymbol { GetMethod: not null, SetMethod: null, NullableAnnotation: NullableAnnotation.None }).IsTrue();
        await Assert.That(member.DeclaredAccessibility == (accessibility == PrivateAccessibility ? Accessibility.Private : Accessibility.Public)).IsTrue();
    }

    /// <summary>MAUI field modifier aliases and invalid spellings preserve its exact fallback policy.</summary>
    /// <param name="modifier">The XAML modifier spelling.</param>
    /// <param name="accessibility">The platform's resulting C# accessibility.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments("Assembly", PrivateAccessibility)]
    [Arguments("NotPublic", "internal")]
    [Arguments("Family", PrivateAccessibility)]
    [Arguments("FamOrAssem", PrivateAccessibility)]
    [Arguments("FamAndAssem", PrivateAccessibility)]
    [Arguments("Unknown", PrivateAccessibility)]
    public async Task FieldModifierAliasesPreserveActualContracts(string modifier, string accessibility)
    {
        using var host = CapabilityCompilerHost.Create(reactive: false, additionalGenerators: [new NamedFieldFixture(accessibility).AsSourceGenerator()]);
        var markup = PageMarkup.Replace(NamedControlAttribute, $"{NamedControlAttribute} x:FieldModifier=\"{modifier}\"", StringComparison.Ordinal);
        var result = await host.RunAsync(Caller(reactive: false), additionalTexts: [new XamlInput(markup)], globalOptions: Globals(), additionalFileOptions: Options(avalonia: false));
        await AssertClean(result);
    }

    /// <summary>Malformed or unresolved selected inputs identify their original XAML file.</summary>
    /// <param name="markup">The malformed or unresolved marked input.</param>
    /// <param name="message">The actionable diagnostic detail.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments("<Page", "Malformed recognized XAML")]
    [Arguments("<Page xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" x:Class=\"Fixture.Missing\" />", "XAML class 'Fixture.Missing'")]
    [Arguments(MissingControlMarkup, "unresolved XAML type")]
    [Arguments(MalformedArgumentsMarkup, "malformed x:TypeArguments")]
    [Arguments(UnknownArgumentNamespaceMarkup, "unresolved XAML type argument")]
    [Arguments(MissingAssemblyMarkup, "unresolved XAML type")]
    [Arguments(InvalidControlNameMarkup, "is invalid or appears more than once")]
    [Arguments(DuplicateControlNameMarkup, "appears more than once")]
    public async Task UnresolvableInputsHaveActionableOriginalLocations(string markup, string message)
    {
        using var host = CapabilityCompilerHost.Create(reactive: false);
        var result = await host.RunAsync(PlainSource, additionalTexts: [new XamlInput(markup)], additionalFileOptions: Options(avalonia: false));
        var diagnostics = result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Id == "RUVG010").ToArray();
        await Assert.That(diagnostics).IsNotEmpty();
        await Assert.That(Array.Exists(diagnostics, diagnostic => diagnostic.GetMessage().Contains(message, StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.TrueForAll(diagnostics, static diagnostic => diagnostic.Location.GetLineSpan().Path == XamlPath)).IsTrue();
    }

    /// <summary>A URI mapping to two matching CLR types must never choose a type by assembly enumeration order.</summary>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    public async Task AmbiguousXmlnsDefinitionsAreRejected()
    {
        const string source = """
            [assembly: Maui.XmlnsDefinition("urn:controls", "First")]
            [assembly: Maui.XmlnsDefinition("urn:controls", "Second")]
            namespace Maui
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
                public class XmlnsDefinitionAttribute(string uri, string clrNamespace) : System.Attribute { }
            }
            namespace First { public class Control { } }
            namespace Second { public class Control { } }
            namespace Fixture { public partial class Page { } }
            """;
        const string markup = """
            <Page xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:c="urn:controls" x:Class="Fixture.Page">
              <c:Control x:Name="Field" />
            </Page>
            """;
        using var host = CapabilityCompilerHost.Create(reactive: false);
        var result = await host.RunAsync(source, additionalTexts: [new XamlInput(markup)], additionalFileOptions: Options(avalonia: false));
        var ambiguous = result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == "RUVG010"
            && diagnostic.GetMessage().Contains("ambiguous XAML type", StringComparison.Ordinal));
        await Assert.That(ambiguous).IsTrue();
    }

    /// <summary>CLR qualification and explicit URI mapping assembly names select the declared assembly.</summary>
    /// <param name="useMapping">Whether to resolve an assembly-qualified XmlnsDefinition mapping.</param>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AssemblyQualifiedTypesAndMappingsResolveUnambiguously(bool useMapping)
    {
        using var host = CapabilityCompilerHost.Create(reactive: false, additionalGenerators: [new NamedFieldFixture(PublicAccessibility, "global::Foreign.Controls.Control").AsSourceGenerator()]);
        var selected = ControlReference(host, "ForeignControls");
        var other = ControlReference(host, "OtherControls").WithAliases(["other"]);
        const string mapping = """
            [assembly: Maui.XmlnsDefinition("urn:controls", "Foreign.Controls", AssemblyName = "ForeignControls")]
            namespace Maui
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
                public class XmlnsDefinitionAttribute : System.Attribute
                {
                    public XmlnsDefinitionAttribute(string uri, string clrNamespace) { _ = uri; _ = clrNamespace; }
                    public string? AssemblyName { get; set; }
                }
            }
            """;
        var uri = useMapping ? "urn:controls" : "clr-namespace:Foreign.Controls;assembly=ForeignControls";
        var markup = $$"""
            <Page xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:c="{{uri}}" x:Class="Fixture.Page">
              <c:Control x:Name="NameControl" x:FieldModifier="Public" />
            </Page>
            """;
        var caller = Caller(reactive: false);
        var source = useMapping ? caller.Replace("namespace Fixture", $"{mapping}\nnamespace Fixture", StringComparison.Ordinal) : caller;
        var result = await host.RunAsync(
            source,
            additionalTexts: [new XamlInput(markup)],
            globalOptions: Globals(),
            additionalReferences: [selected, other],
            additionalFileOptions: Options(avalonia: false));
        await AssertClean(result);
        var field = (IFieldSymbol)result.Compilation.GetTypeByMetadataName(PageTypeName)!.GetMembers(ControlName).Single();
        await Assert.That(field.Type.ContainingAssembly.Name).IsEqualTo("ForeignControls");
    }

    /// <summary>Explicit CLR namespace syntax can refer to controls in the global C# namespace.</summary>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    public async Task GlobalClrNamespaceControlsExecute()
    {
        using var host = CapabilityCompilerHost.Create(reactive: false, additionalGenerators: [new NamedFieldFixture(typeName: "global::GlobalControl").AsSourceGenerator()]);
        var source = $"{Caller(reactive: false)}\npublic class GlobalControl : Fixture.Control<Fixture.Control<string>> {{ }}";
        const string markup = """
            <Page xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:c="clr-namespace:" x:Class="Fixture.Page">
              <c:GlobalControl x:Name="NameControl" />
            </Page>
            """;
        var result = await host.RunAsync(source, additionalTexts: [new XamlInput(markup)], globalOptions: Globals(), additionalFileOptions: Options(avalonia: false));
        await AssertClean(result);
        await Assert.That(result.ExecuteBoolean(PageTypeName, CheckMethodName)).IsTrue();
    }

    /// <summary>Unmarked files and disabled Avalonia inputs cannot create private declarations.</summary>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    public async Task UnmarkedAndDisabledInputsAreIgnored()
    {
        using var host = CapabilityCompilerHost.Create(reactive: false);
        var unmarked = await host.RunAsync(PlainSource, additionalTexts: [new XamlInput(UnselectedMalformedMarkup)]);
        await Assert.That(unmarked.GeneratorDiagnostics).IsEmpty();
        var disabled = await host.RunAsync(
            PlainSource,
            additionalTexts: [new XamlInput(UnselectedMalformedMarkup)],
            globalOptions: new Dictionary<string, string> { ["build_property.AvaloniaNameGeneratorIsEnabled"] = "false" },
            additionalFileOptions: Options(avalonia: true));
        await Assert.That(disabled.GeneratorDiagnostics).IsEmpty();
        var optedOut = await host.RunAsync(
            PlainSource,
            additionalTexts: [new XamlInput(UnselectedMalformedMarkup)],
            globalOptions: new Dictionary<string, string> { ["build_property.ReactiveUIValidationProducerProjectionEnabled"] = "false" },
            additionalFileOptions: Options(avalonia: false));
        await Assert.That(optedOut.GeneratorDiagnostics).IsEmpty();
    }

    /// <summary>A before-Csc field keeps its original type even if the XAML type cannot be predicted.</summary>
    /// <returns>The asynchronous compiler verification.</returns>
    [Test]
    public async Task OriginallyDeclaredFieldsRemainVisibleWithoutProjection()
    {
        const string source = """
            namespace Fixture
            {
                public partial class Page { public string NameControl = "original"; }
            }
            """;
        var markup = PageMarkup.Replace("local:Control", "local:Missing", StringComparison.Ordinal);
        using var host = CapabilityCompilerHost.Create(reactive: false);
        var result = await host.RunAsync(source, additionalTexts: [new XamlInput(markup)], additionalFileOptions: Options(avalonia: false));
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        var owner = result.Compilation.GetTypeByMetadataName(PageTypeName)!;
        await Assert.That(((IFieldSymbol)owner.GetMembers(ControlName).Single()).Type.SpecialType).IsEqualTo(SpecialType.System_String);
    }

    /// <summary>Checks all final diagnostics, including the actual producer contract checker.</summary>
    /// <param name="result">The complete peer run.</param>
    /// <returns>The asynchronous assertions.</returns>
    private static async Task AssertClean(CapabilityCompilation result)
    {
        await Assert.That(string.Join("\n", result.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(string.Join("\n", result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))).IsEmpty();
        await Assert.That(string.Join("\n", await result.GetDispatchDiagnosticsAsync())).IsEmpty();
    }

    /// <summary>Formats every producer output hash deterministically.</summary>
    /// <param name="result">The complete peer run.</param>
    /// <returns>The comparable output fingerprint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Hashes(CapabilityCompilation result) => string.Join("\n", result.GeneratedSources
        .OrderBy(static file => file.Generator, StringComparer.Ordinal).ThenBy(static file => file.HintName, StringComparer.Ordinal)
        .Select(static file => $"{file.Generator}:{file.HintName}:{file.Sha256}"));

    /// <summary>Creates a precise precompiled control identity for assembly-qualification tests.</summary>
    /// <param name="host">The flavor-isolated compile-reference owner.</param>
    /// <param name="assemblyName">The explicit reference assembly name.</param>
    /// <returns>The in-memory compile reference.</returns>
    /// <exception cref="InvalidOperationException">The trusted control fixture cannot be compiled.</exception>
    private static PortableExecutableReference ControlReference(CapabilityCompilerHost host, string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(
                """
                namespace Foreign.Controls
                {
                    public class Control : System.ComponentModel.INotifyPropertyChanged
                    {
                        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
                        public string? Text
                        {
                            get;
                            set
                            {
                                field = value;
                                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Text)));
                            }
                        }
                    }
                }
                """,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14))],
            host.ReferencePaths.Select(static path => MetadataReference.CreateFromFile(path)),
            new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        if (!emitted.Success)
        {
            throw new InvalidOperationException(string.Join("\n", emitted.Diagnostics));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    /// <summary>Marks the additional file using the platform's actual item metadata convention.</summary>
    /// <param name="avalonia">Whether to select Avalonia instead of MAUI.</param>
    /// <returns>The explicit per-file options.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, IReadOnlyDictionary<string, string>> Options(bool avalonia) => new()
    {
        [XamlPath] = new Dictionary<string, string>
        {
            [avalonia ? "build_metadata.AdditionalFiles.SourceItemGroup" : "build_metadata.AdditionalFiles.GenKind"] = avalonia ? "AvaloniaXaml" : "Xaml",
            ["build_metadata.AdditionalFiles.RelativePath"] = XamlPath,
        },
    };

    /// <summary>Supplies exact pinned package-version metadata and an optional Avalonia field modifier.</summary>
    /// <param name="avaloniaModifier">The configured Avalonia field accessibility.</param>
    /// <returns>The producer profile metadata.</returns>
    private static Dictionary<string, string> Globals(string? avaloniaModifier = null)
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.ReactiveUIValidationBindingProducerVersion"] = "9.1.0",
            ["build_property.ReactiveUIValidationReactiveProducerVersion"] = "4.2.0",
            ["build_property.ReactiveUIValidationAvaloniaProducerVersion"] = "12.1.3",
            ["build_property.ReactiveUIValidationMauiProducerVersion"] = "10.0.110",
        };
        if (avaloniaModifier is not null)
        {
            options["build_property.AvaloniaNameGeneratorDefaultFieldModifier"] = avaloniaModifier;
        }

        return options;
    }

    /// <summary>Builds a flavor-specific normal rule that reads the private XAML field.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The trusted executable caller fixture.</returns>
    private static string Caller(bool reactive)
    {
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        return $$"""
            using {{ui}};
            using {{ui}}.Builder;
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            namespace Fixture
            {
                public class Control<T> : ReactiveObject
                {
                    public string? Text { get; set => this.RaiseAndSetIfChanged(ref field, value); }
                }
                public partial class Page : ReactiveObject, IValidatableViewModel
                {
                    public IValidationContext ValidationContext { get; } = new ValidationContext();
                    public static bool Check()
                    {
                        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                        var page = new Page();
                        using var rule = page.ValidationRule(x => x.NameControl.Text, x => x == "ok", "required");
                        if (rule.IsValid) return false;
                        page.NameControl.Text = "ok";
                        if (!rule.IsValid) return false;
                        page.NameControl.Text = null;
                        if (rule.IsValid) return false;
                        rule.Dispose();
                        page.NameControl.Text = "ok";
                        return !rule.IsValid;
                    }
                }
            }
            """;
    }

    /// <summary>A fixture that supplies a real typed field after the generator round begins.</summary>
    /// <param name="accessibility">The exact platform-profile field accessibility.</param>
    /// <param name="typeName">The exact resolved platform field type.</param>
    /// <param name="property">Whether the platform profile produces a get-only name-reference property.</param>
    /// <param name="memberName">The exact C# identifier spelling emitted by the platform.</param>
    private sealed class NamedFieldFixture(
        string accessibility = PrivateAccessibility,
        string typeName = "global::Fixture.Control<global::Fixture.Control<string>>",
        bool property = false,
        string memberName = ControlName) : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var memberBody = property ? "{ get; } = new();" : "= new();";
            var declaration = $"#nullable disable\nnamespace Fixture {{ public partial class Page {{ {accessibility} {typeName} {memberName} {memberBody} }} }}";
            context.RegisterSourceOutput(
                context.ParseOptionsProvider,
                (output, _) => output.AddSource("Fixture.XamlFields.g.cs", SourceText.From(declaration, System.Text.Encoding.UTF8)));
        }
    }

    /// <summary>An immutable additional-file revision supplied to the retained compiler host.</summary>
    /// <param name="markup">The complete XAML source.</param>
    private sealed class XamlInput(string markup) : AdditionalText
    {
        /// <inheritdoc />
        public override string Path => XamlPath;

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SourceText.From(markup);
        }
    }
}
