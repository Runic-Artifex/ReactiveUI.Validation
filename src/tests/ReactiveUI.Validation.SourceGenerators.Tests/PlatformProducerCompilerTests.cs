// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Runs real platform name generators against actual framework metadata.</summary>
public sealed class PlatformProducerCompilerTests
{
    /// <summary>The two library producers and two actual platform producers.</summary>
    private const int ExpectedProducerCount = 4;

    /// <summary>The immutable manifest package collection property.</summary>
    private const string PackagesProperty = "packages";

    /// <summary>The original named error control.</summary>
    private const string ErrorName = "NameError";

    /// <summary>The original view metadata identity.</summary>
    private const string ViewMetadataName = "PlatformFixture.View";

    /// <summary>The actual Avalonia control metadata identity.</summary>
    private const string AvaloniaTextBlockType = "Avalonia.Controls.TextBlock";

    /// <summary>The emitted original named-control access.</summary>
    private const string ErrorAccess = ".NameError";

    /// <summary>The explicit public XAML modifier attribute.</summary>
    private const string PublicFieldModifier = " x:FieldModifier=\"public\"";

    /// <summary>The actual Avalonia behavior configuration key.</summary>
    private const string AvaloniaBehaviorOption = "build_property.AvaloniaNameGeneratorBehavior";

    /// <summary>The actual Avalonia default-accessibility configuration key.</summary>
    private const string AvaloniaFieldModifierOption = "build_property.AvaloniaNameGeneratorDefaultFieldModifier";

    /// <summary>The disabled producer option value.</summary>
    private const string FalseOption = "false";

    /// <summary>The actual MAUI root element type.</summary>
    private const string MauiRootType = "ContentPage";

    /// <summary>The actual Avalonia root element type.</summary>
    private const string AvaloniaRootType = "UserControl";

    /// <summary>Rejects mutated platform archives before loading an actual generator.</summary>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task ActualPlatformInputPinsRejectChangedArchiveBytes()
    {
        var inputDirectory = Path.Combine(AppContext.BaseDirectory, "CompilerHostInputs");
        var restored = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(inputDirectory, "Platforms.assets.json")))!;
        var pinned = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(inputDirectory, "Platforms.immutable-inputs.json")))!;
        var package = pinned[PackagesProperty]![0]!.DeepClone();
        var path = package["path"]!.GetValue<string>();
        var originalFolders = restored["packageFolders"]!.AsObject().Select(static pair => pair.Key).ToArray();
        var archiveName = $"{path.Replace('/', '.')}.nupkg";
        var archivePath = originalFolders.Select(folder => Path.Combine(folder, path, archiveName)).First(File.Exists);
        var temporary = Path.Combine(Path.GetTempPath(), $"validation-platform-inputs-{Guid.NewGuid():N}");
        try
        {
            var packageDirectory = Path.Combine(temporary, PackagesProperty, path);
            _ = Directory.CreateDirectory(packageDirectory);
            var bytes = await File.ReadAllBytesAsync(archivePath);
            bytes[0] ^= byte.MaxValue;
            await File.WriteAllBytesAsync(Path.Combine(packageDirectory, archiveName), bytes);
            restored["packageFolders"] = new JsonObject { [Path.Combine(temporary, PackagesProperty)] = new JsonObject() };
            pinned[PackagesProperty] = new JsonArray(package);
            await File.WriteAllTextAsync(Path.Combine(temporary, "Platforms.assets.json"), restored.ToJsonString());
            await File.WriteAllTextAsync(Path.Combine(temporary, "Platforms.immutable-inputs.json"), pinned.ToJsonString());
            InvalidOperationException? rejected = null;
            try
            {
                _ = PlatformProducerInputs.Read(temporary);
            }
            catch (InvalidOperationException exception)
            {
                rejected = exception;
            }

            await Assert.That(rejected?.Message.Contains("bytes differ", StringComparison.Ordinal) == true).IsTrue();
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>Proves the reviewed platform producers emit their real control contracts.</summary>
    /// <param name="maui">Whether to select MAUI rather than Avalonia markup.</param>
    /// <param name="onlyProperties">Whether Avalonia emits name-scope getters.</param>
    /// <returns>The asynchronous assertions.</returns>
    /// <exception cref="InvalidOperationException">The actual producer emits an unexpected member kind.</exception>
    [Test]
    [Arguments(true, false)]
    [Arguments(false, false)]
    [Arguments(false, true)]
    public async Task ActualPlatformGeneratorsEmitExactMembers(bool maui, bool onlyProperties)
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false, additionalProducerInputs: inputs.Producers);
        var markup = Markup(maui, ErrorName);
        var result = await RunAsync(host, inputs, Source(maui, validationCalls: false), markup, maui, onlyProperties);
        await AssertCleanAsync(result, dispatch: false);
        var view = result.Compilation.GetTypeByMetadataName(ViewMetadataName)!;
        var error = view.GetMembers(ErrorName).Single();
        var actualType = error switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => throw new InvalidOperationException("Platform generator emitted an unexpected member kind."),
        };
        await Assert.That(actualType.ToDisplayString()).IsEqualTo(maui ? "Microsoft.Maui.Controls.Label" : AvaloniaTextBlockType);
        await Assert.That(actualType.NullableAnnotation).IsEqualTo(NullableAnnotation.None);
        await Assert.That(error.DeclaredAccessibility).IsEqualTo(Accessibility.Public);
        await Assert.That(error is IPropertySymbol).IsEqualTo(onlyProperties);
        await Assert.That(view.GetMembers("RootView").Single() is IFieldSymbol or IPropertySymbol).IsTrue();
        await Assert.That(view.GetMembers("GenericError").Single() is IFieldSymbol or IPropertySymbol).IsTrue();
        var platformGenerator = maui ? "Microsoft.Maui.Controls.SourceGen.XamlGenerator" : "AvaloniaNameIncrementalGenerator";
        await Assert.That(result.GeneratedSources.Any(source => source.Generator.Contains(platformGenerator, StringComparison.Ordinal))).IsTrue();
        await Assert.That(host.ProducerInputs.Length).IsEqualTo(ExpectedProducerCount);
        PlatformProducerEvidence.Write($"{(maui ? "Maui" : "Avalonia")}-{(onlyProperties ? "Properties" : "Fields")}-ProducerContract", Source(maui, validationCalls: false), markup, host, result);
    }

    /// <summary>Co-runs Validation, ReactiveUI, Binding and the actual platform generators.</summary>
    /// <param name="maui">Whether to select MAUI markup.</param>
    /// <param name="reactive">Whether to select System.Reactive.</param>
    /// <param name="validationFirst">Whether Validation precedes its peer producers.</param>
    /// <param name="onlyProperties">Whether Avalonia emits read-only name-scope getters.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(true, false, true, false)]
    [Arguments(true, false, false, false)]
    [Arguments(true, true, true, false)]
    [Arguments(true, true, false, false)]
    [Arguments(false, false, true, false)]
    [Arguments(false, false, false, false)]
    [Arguments(false, true, true, false)]
    [Arguments(false, true, false, false)]
    [Arguments(false, false, true, true)]
    [Arguments(false, false, false, true)]
    [Arguments(false, true, true, true)]
    [Arguments(false, true, false, true)]
    public async Task ActualPlatformControlsParticipateInNormalValidation(bool maui, bool reactive, bool validationFirst, bool onlyProperties)
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst, additionalProducerInputs: validationFirst ? inputs.Producers : inputs.Producers.Reverse());
        var source = Source(maui, validationCalls: true, reactive);
        var markup = Markup(maui, ErrorName);
        var result = await RunAsync(host, inputs, source, markup, maui, onlyProperties);
        await AssertCleanAsync(result);
        await Assert.That(result.ValidationSources.Any(static file => file.Text.Contains(ErrorAccess, StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.ValidationSources.Any(static file => file.Text.Contains(".GenericError", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.ValidationSources.Any(static file => file.Text.Contains(".Name", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers("CreateAdapter").Length).IsEqualTo(1);
        await Assert.That(result.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers("CreateTarget").Length).IsEqualTo(1);
        await Assert.That(result.Compilation.SyntaxTrees.Any(static tree => tree.FilePath.Contains("Projection", StringComparison.Ordinal))).IsFalse();
        await Assert.That(host.ReferencePaths.Any(static path => Path.GetFileName(path) == "System.Reactive.dll")).IsEqualTo(reactive);

        var changed = await RunAsync(host, inputs, source.Replace(ErrorName, "TitleError", StringComparison.Ordinal), Markup(maui, "TitleError"), maui, onlyProperties);
        await AssertCleanAsync(changed);
        await Assert.That(changed.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Length).IsEqualTo(0);
        await Assert.That(changed.ValidationSources.Any(static file => file.Text.Contains(".TitleError", StringComparison.Ordinal))).IsTrue();

        var reverted = await RunAsync(host, inputs, source, markup, maui, onlyProperties);
        await AssertCleanAsync(reverted);
        await Assert.That(string.Join("\n", reverted.GeneratedSources.Select(static file => file.Sha256)))
            .IsEqualTo(string.Join("\n", result.GeneratedSources.Select(static file => file.Sha256)));

        var removed = await host.RunAsync(
            source,
            additionalReferences: inputs.References.Where(reference => !host.ReferencePaths.Contains(reference.Display!, StringComparer.Ordinal)),
            globalOptions: Options(maui, onlyProperties));
        await Assert.That(removed.CompilationDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(removed.ValidationSources.Any(static file => file.Text.Contains(ErrorAccess, StringComparison.Ordinal))).IsFalse();
        PlatformProducerEvidence.Write(Profile(maui, reactive, validationFirst, onlyProperties), source, markup, host, result);
    }

    /// <summary>Retains real producer errors and rejects missing or malformed named-control inputs.</summary>
    /// <param name="maui">Whether to select MAUI markup.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ActualPlatformMalformedAndMissingControlsRemainErrors(bool maui)
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, additionalProducerInputs: inputs.Producers);
        var source = Source(maui, validationCalls: true);
        var malformed = await RunAsync(host, inputs, source, "<malformed", maui, false);
        await Assert.That(malformed.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(malformed.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == "RUVG010")).IsTrue();

        var missing = await RunAsync(host, inputs, source, Markup(maui, "AnotherName"), maui, false);
        await Assert.That(missing.CompilationDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(missing.ValidationSources.Any(static file => file.Text.Contains(ErrorAccess, StringComparison.Ordinal))).IsFalse();
    }

    /// <summary>Invalidates real Avalonia members when behavior, enablement or filters change.</summary>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task ActualAvaloniaOptionsInvalidateMembers()
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false, additionalProducerInputs: inputs.Producers);
        var source = Source(false, validationCalls: false);
        var markup = Markup(false, ErrorName);
        var fields = await RunAsync(host, inputs, source, markup, false, false);
        await AssertCleanAsync(fields, dispatch: false);
        await Assert.That(fields.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single() is IFieldSymbol).IsTrue();

        var properties = await RunAsync(host, inputs, source, markup, false, true);
        await AssertCleanAsync(properties, dispatch: false);
        await Assert.That(properties.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single() is IPropertySymbol { SetMethod: null }).IsTrue();

        var numeric = await RunAsync(
            host,
            inputs,
            source,
            markup.Replace(PublicFieldModifier, string.Empty, StringComparison.Ordinal),
            false,
            false,
            new Dictionary<string, string> { [AvaloniaBehaviorOption] = " 0 ", [AvaloniaFieldModifierOption] = " 1 " });
        await AssertCleanAsync(numeric, dispatch: false);
        var numericMember = numeric.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single();
        await Assert.That(numericMember is IPropertySymbol { SetMethod: null }).IsTrue();
        await Assert.That(numericMember.DeclaredAccessibility).IsEqualTo(Accessibility.Private);

        foreach (var option in new[]
        {
            new KeyValuePair<string, string>("build_property.AvaloniaNameGeneratorIsEnabled", FalseOption),
            new KeyValuePair<string, string>("build_property.AvaloniaNameGeneratorFilterByPath", "Excluded/*"),
            new KeyValuePair<string, string>("build_property.AvaloniaNameGeneratorFilterByNamespace", "Excluded.*"),
        })
        {
            var filtered = await RunAsync(host, inputs, source, markup, false, false, new Dictionary<string, string> { [option.Key] = option.Value });
            await AssertCleanAsync(filtered, dispatch: false);
            await Assert.That(filtered.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Length).IsEqualTo(0);
        }

        var restored = await RunAsync(host, inputs, source, markup, false, false);
        await AssertCleanAsync(restored, dispatch: false);
        await Assert.That(string.Join("\n", restored.GeneratedSources.Select(static file => file.Sha256))).IsEqualTo(string.Join("\n", fields.GeneratedSources.Select(static file => file.Sha256)));
    }

    /// <summary>Preserves each real producer's root and template-name traversal contract.</summary>
    /// <param name="maui">Whether to select MAUI markup.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ActualPlatformNameScopesMatchPinnedProducers(bool maui)
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, additionalProducerInputs: inputs.Producers);
        var root = maui ? MauiRootType : AvaloniaRootType;
        var control = maui ? "Label" : "TextBlock";
        var markup = Markup(maui, ErrorName).Replace(
            $"</{root}>",
            $$"""
            <{{root}}.Resources>
                <DataTemplate x:Key="NamedTemplate">
                    <{{control}} x:Name="TemplateError" x:FieldModifier="public" />
                </DataTemplate>
            </{{root}}.Resources>
            </{{root}}>
            """,
            StringComparison.Ordinal);
        var result = await RunAsync(host, inputs, Source(maui, validationCalls: true), markup, maui, false);
        await AssertCleanAsync(result);
        var view = result.Compilation.GetTypeByMetadataName(ViewMetadataName)!;
        await Assert.That(view.GetMembers("RootView").Length).IsEqualTo(1);
        await Assert.That(view.GetMembers("TemplateError").Length).IsEqualTo(0);
    }

    /// <summary>Preserves actual default, explicit and invalid-modifier fallback accessibility.</summary>
    /// <param name="maui">Whether to select MAUI.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ActualPlatformFieldModifiersMatchPinnedProfiles(bool maui)
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false, additionalProducerInputs: inputs.Producers);
        var source = Source(maui, validationCalls: false);
        var markup = Markup(maui, ErrorName);
        var defaults = await RunAsync(host, inputs, source, markup.Replace(PublicFieldModifier, string.Empty, StringComparison.Ordinal), maui, false);
        await AssertCleanAsync(defaults, dispatch: false);
        await Assert.That(defaults.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single().DeclaredAccessibility)
            .IsEqualTo(maui ? Accessibility.Private : Accessibility.Internal);

        var notPublic = await RunAsync(host, inputs, source, markup.Replace("FieldModifier=\"public\"", "FieldModifier=\"NotPublic\"", StringComparison.Ordinal), maui, false);
        await AssertCleanAsync(notPublic, dispatch: false);
        await Assert.That(notPublic.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single().DeclaredAccessibility).IsEqualTo(Accessibility.Internal);

        var invalid = await RunAsync(
            host,
            inputs,
            source,
            markup.Replace("FieldModifier=\"public\"", "FieldModifier=\"invalid\"", StringComparison.Ordinal),
            maui,
            false,
            new Dictionary<string, string> { [AvaloniaFieldModifierOption] = "public" });
        await AssertCleanAsync(invalid, dispatch: false);
        await Assert.That(invalid.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single().DeclaredAccessibility)
            .IsEqualTo(maui ? Accessibility.Private : Accessibility.Public);
    }

    /// <summary>Resolves recursive MAUI type arguments with the real producer and framework metadata.</summary>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task ActualMauiNestedTypeArgumentsRemainExact()
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, additionalProducerInputs: inputs.Producers);
        var markup = Markup(true, ErrorName).Replace("x:TypeArguments=\"x:String\"", "x:TypeArguments=\"local:GenericControl(x:String)\"", StringComparison.Ordinal);
        var result = await RunAsync(host, inputs, Source(true, validationCalls: true), markup, true, false);
        await AssertCleanAsync(result);
        var field = (IFieldSymbol)result.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers("GenericError").Single();
        await Assert.That(field.Type.ToDisplayString()).IsEqualTo("PlatformFixture.GenericControl<PlatformFixture.GenericControl<string>>");
    }

    /// <summary>Validates legal original-scope access to actual private fields and read-only getters.</summary>
    /// <param name="maui">Whether to select default-private MAUI fields.</param>
    /// <param name="reactive">Whether to select System.Reactive.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(true, false)]
    [Arguments(true, true)]
    [Arguments(false, false)]
    [Arguments(false, true)]
    public async Task ActualPlatformPrivateControlAccessIsValidated(bool maui, bool reactive)
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(reactive, additionalProducerInputs: inputs.Producers);
        var markup = Markup(maui, ErrorName).Replace(PublicFieldModifier, string.Empty, StringComparison.Ordinal);
        var options = maui ? null : new Dictionary<string, string> { [AvaloniaBehaviorOption] = " 0 ", [AvaloniaFieldModifierOption] = " 1 " };
        var result = await RunAsync(host, inputs, Source(maui, validationCalls: true, reactive), markup, maui, false, options);
        await AssertCleanAsync(result);
        var member = result.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single();
        await Assert.That(member.DeclaredAccessibility).IsEqualTo(Accessibility.Private);
        await Assert.That(member is IPropertySymbol).IsEqualTo(!maui);
    }

    /// <summary>Predicts Avalonia Name property-element syntax consumed by the actual pinned parser.</summary>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task ActualAvaloniaNamePropertyElementsRemainUsable()
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, additionalProducerInputs: inputs.Producers);
        var markup = Markup(false, ErrorName).Replace(
            "<controls:TextBlock x:Name=\"NameError\" x:FieldModifier=\"public\" />",
            """
            <controls:TextBlock x:FieldModifier="public">
                <controls:TextBlock.Name>NameError</controls:TextBlock.Name>
            </controls:TextBlock>
            """,
            StringComparison.Ordinal);
        var result = await RunAsync(host, inputs, Source(false, validationCalls: true), markup, false, false);
        await AssertCleanAsync(result);
        var field = (IFieldSymbol)result.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single();
        await Assert.That(field.Type.ToDisplayString()).IsEqualTo(AvaloniaTextBlockType);
        await Assert.That(result.ValidationSources.Any(static file => file.Text.Contains(ErrorAccess, StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Coherently changes the actual named control type and rejects original member collisions.</summary>
    /// <param name="maui">Whether to select MAUI controls.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ActualPlatformTypeEditsAndCollisionsRemainVisible(bool maui)
    {
        var inputs = PlatformProducerInputs.Read();
        using var host = CapabilityCompilerHost.Create(false, additionalProducerInputs: inputs.Producers);
        var source = Source(maui, validationCalls: true);
        var markup = Markup(maui, ErrorName);
        var changedMarkup = markup.Replace(maui ? "controls:Label" : "controls:TextBlock", maui ? "controls:Entry" : "controls:TextBox", StringComparison.Ordinal);
        var changed = await RunAsync(host, inputs, source, changedMarkup, maui, false);
        await AssertCleanAsync(changed);
        var field = (IFieldSymbol)changed.Compilation.GetTypeByMetadataName(ViewMetadataName)!.GetMembers(ErrorName).Single();
        await Assert.That(field.Type.ToDisplayString()).IsEqualTo(maui ? "Microsoft.Maui.Controls.Entry" : "Avalonia.Controls.TextBox");

        var collidedSource = source.Replace("public Model? ViewModel", "public string NameError = string.Empty;\n public Model? ViewModel", StringComparison.Ordinal);
        var collision = await RunAsync(host, inputs, collidedSource, markup, maui, false);
        await Assert.That(collision.CompilationDiagnostics.Any(static diagnostic => diagnostic.Id == "CS0102")).IsTrue();
    }

    /// <summary>Rejects a missing real platform producer and a configuration outside the audited profile.</summary>
    /// <param name="maui">Whether to select MAUI controls.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ActualPlatformFinalOutputAndVersionDriftAreRejected(bool maui)
    {
        var inputs = PlatformProducerInputs.Read();
        var source = Source(maui, validationCalls: true);
        var markup = Markup(maui, ErrorName);
        using var missingHost = CapabilityCompilerHost.Create(false);
        var missing = await RunAsync(missingHost, inputs, source, markup, maui, false);
        var finalDiagnostics = await missing.GetDispatchDiagnosticsAsync();
        await Assert.That(finalDiagnostics.Any(static diagnostic => diagnostic.Id == "RUVG008")).IsTrue();

        using var actualHost = CapabilityCompilerHost.Create(false, additionalProducerInputs: inputs.Producers);
        var drift = await RunAsync(actualHost, inputs, source, markup, maui, false, new Dictionary<string, string>
        {
            [maui ? "build_property.ReactiveUIValidationMauiProducerVersion" : "build_property.ReactiveUIValidationAvaloniaProducerVersion"] = "999.0.0",
        });
        await Assert.That(drift.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == "RUVG009")).IsTrue();
    }

    /// <summary>Creates one stable additional input with the exact producer metadata.</summary>
    /// <param name="host">The retained incremental driver.</param>
    /// <param name="inputs">The verified framework graph.</param>
    /// <param name="source">The original C# caller.</param>
    /// <param name="markup">The actual XAML document.</param>
    /// <param name="maui">Whether to select MAUI metadata.</param>
    /// <param name="onlyProperties">Whether Avalonia emits getters.</param>
    /// <param name="optionOverrides">Explicit edited producer options.</param>
    /// <returns>The asynchronous final combined compilation.</returns>
    private static Task<CapabilityCompilation> RunAsync(
        CapabilityCompilerHost host,
        PlatformProducerInputs inputs,
        string source,
        string markup,
        bool maui,
        bool onlyProperties,
        IReadOnlyDictionary<string, string>? optionOverrides = null)
    {
        var path = maui ? "Views/View.xaml" : "Views/View.axaml";
        IReadOnlyDictionary<string, string> fileOptions = maui
            ? new Dictionary<string, string>
            {
                ["build_metadata.AdditionalFiles.GenKind"] = "Xaml",
                ["build_metadata.AdditionalFiles.RelativePath"] = path,
                ["build_metadata.AdditionalFiles.TargetPath"] = path,
                ["build_metadata.AdditionalFiles.Inflator"] = "SourceGen",
                ["build_metadata.AdditionalFiles.EnableDiagnostics"] = FalseOption,
                ["build_property.Configuration"] = "Release",
            }
            : new Dictionary<string, string> { ["build_metadata.AdditionalFiles.SourceItemGroup"] = "AvaloniaXaml" };
        var globalOptions = new Dictionary<string, string>(Options(maui, onlyProperties));
        if (optionOverrides is not null)
        {
            foreach (var option in optionOverrides)
            {
                globalOptions[option.Key] = option.Value;
            }
        }

        return host.RunAsync(
            source,
            additionalTexts: [new XamlInput(path, markup)],
            globalOptions: globalOptions,
            additionalReferences: inputs.References.Where(reference => !host.ReferencePaths.Contains(reference.Display!, StringComparer.Ordinal)),
            additionalFileOptions: new Dictionary<string, IReadOnlyDictionary<string, string>> { [path] = fileOptions });
    }

    /// <summary>Formats one evidence identity for the complete participating driver profile.</summary>
    /// <param name="maui">Whether to select MAUI.</param>
    /// <param name="reactive">Whether to select System.Reactive.</param>
    /// <param name="validationFirst">Whether Validation precedes its peers.</param>
    /// <param name="onlyProperties">Whether Avalonia emits getters.</param>
    /// <returns>The invocation profile identity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Profile(bool maui, bool reactive, bool validationFirst, bool onlyProperties) =>
        $"{(maui ? "Maui" : "Avalonia")}-{(reactive ? "Reactive" : "Primitives")}-{(validationFirst ? "ValidationFirst" : "ValidationLast")}-{(onlyProperties ? "Properties" : "Fields")}";

    /// <summary>Supplies the explicitly audited profiles rather than an implicit latest producer.</summary>
    /// <param name="maui">Whether to select MAUI.</param>
    /// <param name="onlyProperties">Whether Avalonia emits getters.</param>
    /// <returns>The exact compiler profile options.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, string> Options(bool maui, bool onlyProperties) => new Dictionary<string, string>
    {
        ["build_property.ReactiveUIValidationMauiProducerVersion"] = "10.0.110",
        ["build_property.ReactiveUIValidationAvaloniaProducerVersion"] = "12.1.3",
        ["build_property.TargetFramework"] = "net10.0",
        ["build_property.Configuration"] = "Release",
        ["build_property.EnableMauiXamlDiagnostics"] = FalseOption,
        ["build_property.MauiXamlInflator"] = "SourceGen",
        ["build_property.AvaloniaNameGeneratorIsEnabled"] = maui ? FalseOption : "true",
        [AvaloniaBehaviorOption] = onlyProperties ? "OnlyProperties" : "InitializeComponent",
        ["build_property.AvaloniaNameGeneratorAttachDevTools"] = FalseOption,
    };

    /// <summary>Creates real framework control markup, including root and generic names.</summary>
    /// <param name="maui">Whether to select MAUI.</param>
    /// <param name="name">The named error control.</param>
    /// <returns>The platform document.</returns>
    private static string Markup(bool maui, string name) => $$"""
        <{{(maui ? MauiRootType : AvaloniaRootType)}}
            xmlns="{{(maui ? "http://schemas.microsoft.com/dotnet/2021/maui" : "https://github.com/avaloniaui")}}"
            xmlns:x="{{(maui ? "http://schemas.microsoft.com/winfx/2009/xaml" : "http://schemas.microsoft.com/winfx/2006/xaml")}}"
            xmlns:local="clr-namespace:PlatformFixture"
            xmlns:controls="clr-namespace:{{(maui ? "Microsoft.Maui.Controls;assembly=Microsoft.Maui.Controls" : "Avalonia.Controls;assembly=Avalonia.Controls")}}"
            x:Class="{{ViewMetadataName}}" x:Name="RootView" x:FieldModifier="public">
            <{{(maui ? "VerticalStackLayout" : "StackPanel")}}>
                <controls:{{(maui ? "Label" : "TextBlock")}} x:Name="{{name}}" x:FieldModifier="public" />
                <local:GenericControl x:TypeArguments="x:String" x:Name="GenericError" x:FieldModifier="public" />
            </{{(maui ? "VerticalStackLayout" : "StackPanel")}}>
        </{{(maui ? MauiRootType : AvaloniaRootType)}}>
        """;

    /// <summary>Creates a consumer with actual Reactive field generation and typed platform access.</summary>
    /// <param name="maui">Whether to select MAUI controls.</param>
    /// <param name="validationCalls">Whether to include original normal validation calls.</param>
    /// <param name="reactive">Whether to select System.Reactive.</param>
    /// <returns>The original caller source.</returns>
    private static string Source(bool maui, bool validationCalls, bool reactive = false)
    {
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var binding = reactive ? "ReactiveUI.Binding.Reactive" : "ReactiveUI.Binding";
        var validation = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var integration = validationCalls ? Integration(maui) : string.Empty;
        return $$"""
            using System;
            using {{ui}};
            using {{binding}};
            using ReactiveUI.SourceGenerators;
            using {{validation}}.Abstractions;
            using {{validation}}.Contexts;
            using {{validation}}.Extensions;
            {{(validationCalls ? $"using {validation}.Capabilities;" : string.Empty)}}
            namespace PlatformFixture;
            public sealed class GenericControl<T> : {{(maui ? "Microsoft.Maui.Controls.Label" : AvaloniaTextBlockType)}} { }
            public sealed partial class Model : ReactiveObject, IValidatableViewModel
            {
                [Reactive] private string? _name;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
            }
            public sealed partial class View : {{(maui ? "Microsoft.Maui.Controls.ContentPage" : "Avalonia.Controls.UserControl")}}, IViewFor<Model>
            {
                public Model? ViewModel { get; set; }
                object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
                {{integration}}
            }
            """;
    }

    /// <summary>Creates typed runtime provider and actual host-adapter member declarations.</summary>
    /// <param name="maui">Whether the real host uses BindingContext rather than DataContext.</param>
    /// <returns>The original strongly typed integration members.</returns>
    private static string Integration(bool maui)
    {
        var modelProperty = maui ? "BindingContext" : "DataContext";
        return $$"""
            public IDisposable Bind(Model model) => this.BindValidation(model, x => x.Name, v => v.NameError.Text);
            public IDisposable BindGeneric(Model model) => this.BindValidation(model, x => x.Name, v => v.GenericError.Text);
            public ValidationTarget<View, string?> CreateTarget() => new(static view => new ValidationWritePlan<string?>(
                () => view.NameError is { } control
                    ? ValidationTargetAccess<string?>.Present(control, value => control.Text = value)
                    : ValidationTargetAccess<string?>.Missing(),
                [ValidationDependency.PropertyChanged(() => view.NameError, "Text")]));
            public IDisposable RegisterTarget(ValidationPlanRegistry registry) => registry.RegisterTarget<View, string?, string?>(
                ValidationPlanRole.Target, view => view.NameError.Text, CreateTarget());
            public ValidationViewAdapter<View, Model> CreateAdapter() => new(this,
                static view => view.{{modelProperty}} as Model,
                static (view, model) => view.{{modelProperty}} = model,
                static (view, changed) =>
                {
                    System.ComponentModel.INotifyPropertyChanged owner = view;
                    System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
                    {
                        if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == "{{modelProperty}}")
                        {
                            changed();
                        }
                    };
                    owner.PropertyChanged += handler;
                    return new Registration(() => owner.PropertyChanged -= handler);
                });
            private sealed class Registration(Action release) : IDisposable
            {
                public void Dispose() => release();
            }
            """;
    }

    /// <summary>Asserts real generation and final compiler errors with readable full evidence.</summary>
    /// <param name="result">The complete producer round.</param>
    /// <param name="dispatch">Whether final Validation dispatch must succeed.</param>
    /// <returns>The asynchronous assertions.</returns>
    private static async Task AssertCleanAsync(CapabilityCompilation result, bool dispatch = true)
    {
        await Assert.That(string.Join("\n", result.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(string.Join("\n", result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))).IsEmpty();
        var emitted = result.Compilation.Emit(Stream.Null);
        await Assert.That(emitted.Success).IsTrue();
        await Assert.That(string.Join("\n", emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))).IsEmpty();
        if (dispatch)
        {
            await Assert.That(string.Join("\n", await result.GetDispatchDiagnosticsAsync())).IsEmpty();
        }
    }

    /// <summary>Supplies stable in-memory XAML to the actual producer driver.</summary>
    /// <param name="path">The original additional file identity.</param>
    /// <param name="contents">The document contents.</param>
    private sealed class XamlInput(string path, string contents) : AdditionalText
    {
        /// <inheritdoc />
        public override string Path => path;

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(contents);
    }
}
