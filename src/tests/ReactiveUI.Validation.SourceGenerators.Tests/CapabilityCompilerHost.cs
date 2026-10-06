// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ReactiveUI.Validation.SourceGenerators;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Runs the shipped producer generators with a flavor-isolated consumer graph.</summary>
internal sealed class CapabilityCompilerHost : IDisposable
{
    /// <summary>The normal-call interception namespaces admitted by the consumer.</summary>
    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14)
        .WithFeatures(new Dictionary<string, string> { ["InterceptorsNamespaces"] = "ReactiveUI.Validation.Generated;ReactiveUI.Binding.Generated.Interceptors" });

    /// <summary>Mirrors locked SDK .NETCoreApp defaults for reference unification and ignored strong naming.</summary>
    private static readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
        {
            ["CS1701"] = ReportDiagnostic.Suppress,
            ["CS1702"] = ReportDiagnostic.Suppress,
            ["CS8002"] = ReportDiagnostic.Suppress,
        });

    /// <summary>The stable original compilation identity across edits.</summary>
    private readonly string _assemblyName = $"CapabilityCaller{Guid.NewGuid():N}";

    /// <summary>The actual producer names behind incremental adapters.</summary>
    private readonly ImmutableArray<string> _generatorNames;

    /// <summary>The selected compile references.</summary>
    private ImmutableArray<MetadataReference> _references;

    /// <summary>The actual packaged diagnostic suppressors.</summary>
    private ImmutableArray<DiagnosticAnalyzer> _suppressors;

    /// <summary>The fresh producer and suppressor instances owned by this host.</summary>
    private ImmutableArray<object> _producerInstances;

    /// <summary>The retained incremental driver.</summary>
    private GeneratorDriver _driver;

    /// <summary>The last original source tree, reused for identical input.</summary>
    private SyntaxTree? _tree;

    /// <summary>Whether the host compilation graphs have been released.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="CapabilityCompilerHost"/> class.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <param name="validationFirst">Whether Validation precedes the peer producers.</param>
    /// <param name="includeValidation">Whether to include Validation generation.</param>
    /// <param name="additionalGenerators">Explicit fixture producers, when a contract test needs them.</param>
    /// <param name="additionalProducerInputs">Verified actual platform producer inputs.</param>
    /// <exception cref="InvalidOperationException">The pinned packages expose no generators.</exception>
    private CapabilityCompilerHost(
        bool reactive,
        bool validationFirst,
        bool includeValidation,
        IEnumerable<ISourceGenerator>? additionalGenerators,
        IEnumerable<CapabilityProducerInput>? additionalProducerInputs)
    {
        var inputs = ReadInputs(reactive);
        ReferencePaths = inputs.References;
        ProducerInputs = additionalProducerInputs is null ? inputs.Producers : inputs.Producers.AddRange(additionalProducerInputs);
        ProducerAssemblies = CapabilityProducerAssemblies.Get(ProducerInputs);
        _references = [.. ReferencePaths.Select(static path => MetadataReference.CreateFromFile(path))];
        var producers = new List<ISourceGenerator>();
        var generatorNames = new List<string>();
        var suppressors = new List<DiagnosticAnalyzer>();
        var instances = new List<object>();
        foreach (var assembly in ProducerAssemblies)
        {
            AddProducerGenerators(assembly, producers, generatorNames, suppressors, instances);
        }

        if (producers.Count == 0)
        {
            throw new InvalidOperationException("Pinned packages did not expose any producer generators.");
        }

        if (additionalGenerators is not null)
        {
            foreach (var generator in additionalGenerators)
            {
                producers.Add(generator);
                generatorNames.Add(generator.GetType().FullName!);
            }
        }

        if (includeValidation)
        {
            var index = validationFirst ? 0 : producers.Count;
            producers.Insert(index, new ValidationGenerator().AsSourceGenerator());
            generatorNames.Insert(index, typeof(ValidationGenerator).FullName!);
        }

        _suppressors = [.. suppressors];
        _producerInstances = [.. instances];
        _generatorNames = [.. generatorNames];
        _driver = CSharpGeneratorDriver.Create(producers, parseOptions: ParseOptions);
    }

    /// <summary>Gets the exact flavor-isolated reference files.</summary>
    internal ImmutableArray<string> ReferencePaths { get; }

    /// <summary>Gets the selected pinned generator files and their byte hashes.</summary>
    internal ImmutableArray<CapabilityProducerInput> ProducerInputs { get; }

    /// <summary>Gets the verified process-lifetime assembly identities used by this host.</summary>
    internal ImmutableArray<Assembly> ProducerAssemblies { get; }

    /// <summary>Gets this host's independent packaged generator and suppressor instances.</summary>
    internal ImmutableArray<object> ProducerInstances => _producerInstances;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _tree = null;
        _driver = CSharpGeneratorDriver.Create([], parseOptions: ParseOptions);
        _references = [];
        _suppressors = [];
        _producerInstances = [];
    }

    /// <summary>Creates a driver that retains incremental state between calls.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <param name="validationFirst">Whether Validation precedes the peer producers.</param>
    /// <param name="includeValidation">Whether to include Validation generation.</param>
    /// <param name="additionalGenerators">Optional explicit contract fixture producers.</param>
    /// <param name="additionalProducerInputs">Optional verified actual platform producers.</param>
    /// <returns>The owned compiler host.</returns>
    internal static CapabilityCompilerHost Create(
        bool reactive,
        bool validationFirst = true,
        bool includeValidation = true,
        IEnumerable<ISourceGenerator>? additionalGenerators = null,
        IEnumerable<CapabilityProducerInput>? additionalProducerInputs = null) =>
        new(reactive, validationFirst, includeValidation, additionalGenerators, additionalProducerInputs);

    /// <summary>Compiles a caller with actual peer generation and preserves driver state for edits.</summary>
    /// <param name="source">The original caller source.</param>
    /// <param name="path">The stable original caller file path.</param>
    /// <param name="additionalTexts">Additional inputs such as XAML documents.</param>
    /// <param name="globalOptions">Analyzer configuration for producer profiles.</param>
    /// <param name="additionalReferences">Explicit framework or precompiled peer references.</param>
    /// <param name="additionalFileOptions">Producer options keyed by additional file path.</param>
    /// <returns>The complete compilation and generated source evidence.</returns>
    internal async Task<CapabilityCompilation> RunAsync(
        string source,
        string path = "CapabilityCaller.cs",
        IEnumerable<AdditionalText>? additionalTexts = null,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        IEnumerable<MetadataReference>? additionalReferences = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? additionalFileOptions = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_tree is null || _tree.FilePath != path || (await _tree.GetTextAsync()).ToString() != source)
        {
            _tree = CSharpSyntaxTree.ParseText(source, ParseOptions, path);
        }

        ImmutableArray<AdditionalText> texts = additionalTexts is null ? [] : [.. additionalTexts];
        var optionsProvider = new HostOptionsProvider(WithPinnedProducerOptions(globalOptions), additionalFileOptions);
        var analyzerOptions = new AnalyzerOptions(texts, optionsProvider);
        _driver = _driver.ReplaceAdditionalTexts(texts).WithUpdatedAnalyzerConfigOptions(optionsProvider);
        var references = additionalReferences is null ? _references : _references.AddRange(additionalReferences);
        var original = CSharpCompilation.Create(_assemblyName, [_tree], references, CompilationOptions);
        _driver = _driver.RunGeneratorsAndUpdateCompilation(original, out var compilation, out var diagnostics);
        var generated = _driver.GetRunResult().Results.SelectMany((result, index) => result.GeneratedSources.Select(source => new CapabilityGeneratedSource(
            _generatorNames[index],
            source.HintName,
            source.SourceText.ToString(),
            Hash(source.SourceText.ToString())))).ToImmutableArray();
        var compilerDiagnostics = _suppressors.IsEmpty
            ? compilation.GetDiagnostics()
            : await compilation.WithAnalyzers(_suppressors, analyzerOptions).GetAllDiagnosticsAsync();
        return new(
            (CSharpCompilation)compilation,
            diagnostics,
            generated,
            [.. compilerDiagnostics.Where(static diagnostic => !diagnostic.IsSuppressed)],
            analyzerOptions);
    }

    /// <summary>Supplies the producer versions verified by the selected runtime assets.</summary>
    /// <param name="overrides">Explicit options, including intentional profile-drift fixtures.</param>
    /// <returns>The effective producer options.</returns>
    private static Dictionary<string, string> WithPinnedProducerOptions(IReadOnlyDictionary<string, string>? overrides)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["build_property.ReactiveUIValidationReactiveProducerVersion"] = "4.2.0",
            ["build_property.ReactiveUIValidationBindingProducerVersion"] = "9.1.0",
        };
        if (overrides is not null)
        {
            foreach (var option in overrides)
            {
                options[option.Key] = option.Value;
            }
        }

        return options;
    }

    /// <summary>Reads the selected runtime project's restored graph instead of the mixed host graph.</summary>
    /// <param name="reactive">Whether to select the System.Reactive graph.</param>
    /// <returns>The precise compile references and analyzer inputs.</returns>
    /// <exception cref="InvalidOperationException">The pinned analyzer pair is absent.</exception>
    private static HostInputs ReadInputs(bool reactive)
    {
        var flavor = reactive ? "Reactive" : "Primitives";
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CompilerHostInputs", $"{flavor}.assets.json")));
        var root = document.RootElement;
        var folders = root.GetProperty("packageFolders").EnumerateObject().Select(static folder => folder.Name).ToArray();
        var libraries = root.GetProperty("libraries");
        var target = root.GetProperty("targets").GetProperty("net10.0");
        var references = CompilerTestReferences.RuntimePaths().ToList();
        var producers = new List<CapabilityProducerInput>();
        foreach (var package in target.EnumerateObject())
        {
            var library = libraries.GetProperty(package.Name);
            if (library.GetProperty("type").GetString() != "package")
            {
                continue;
            }

            var packagePath = library.GetProperty("path").GetString()!;
            var directory = folders.Select(folder => Path.Combine(folder, packagePath)).First(Directory.Exists);
            if (package.Value.TryGetProperty("compile", out var compile))
            {
                references.AddRange(compile.EnumerateObject().Where(static asset => asset.Name.EndsWith(".dll", StringComparison.Ordinal)).Select(asset => Path.Combine(directory, asset.Name)));
            }

            var analyzerAsset = GetAnalyzerAsset(package.Name, reactive);
            if (analyzerAsset is null)
            {
                continue;
            }

            var analyzer = Path.GetFullPath(Path.Combine(directory, analyzerAsset));
            producers.Add(new(package.Name, analyzer, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(analyzer)))));
        }

        if (producers.Count != 2)
        {
            throw new InvalidOperationException($"Expected the pinned SourceGenerators 4.2.0 and Binding 9.1.0 analyzer pair for {flavor}.");
        }

        references.Add(Path.Combine(AppContext.BaseDirectory, reactive ? "ReactiveUI.Validation.Reactive.dll" : "ReactiveUI.Validation.dll"));
        return new([.. references.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)], [.. producers.OrderBy(static input => input.Package, StringComparer.Ordinal)]);
    }

    /// <summary>Selects the compiler bands tested with the locked Roslyn host.</summary>
    /// <param name="package">The restored package identity.</param>
    /// <param name="reactive">The selected runtime flavor.</param>
    /// <returns>The pinned analyzer asset, or null for a runtime package.</returns>
    private static string? GetAnalyzerAsset(string package, bool reactive)
    {
        if (package == "ReactiveUI.SourceGenerators/4.2.0")
        {
            return "analyzers/dotnet/roslyn5.0/cs/ReactiveUI.SourceGenerators.Roslyn.dll";
        }

        return package == (reactive ? "ReactiveUI.Binding.Reactive/9.1.0" : "ReactiveUI.Binding/9.1.0")
            ? "analyzers/dotnet/roslyn4.13/cs/ReactiveUI.Binding.SourceGenerators.dll"
            : null;
    }

    /// <summary>Hashes the full generated text rather than a selected interceptor subset.</summary>
    /// <param name="text">The emitted source text.</param>
    /// <returns>The SHA-256 hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Creates independent generators from one verified process-lifetime analyzer assembly.</summary>
    /// <param name="assembly">The exact verified producer assembly.</param>
    /// <param name="producers">The combined producer list.</param>
    /// <param name="generatorNames">The original names behind incremental adapters.</param>
    /// <param name="suppressors">The actual producer diagnostic suppressors.</param>
    /// <param name="instances">This host's fresh packaged producer instances.</param>
    private static void AddProducerGenerators(Assembly assembly, List<ISourceGenerator> producers, List<string> generatorNames, List<DiagnosticAnalyzer> suppressors, List<object> instances)
    {
        foreach (var type in assembly.GetTypes()
            .Where(static type => !type.IsAbstract && (type.GetCustomAttribute<GeneratorAttribute>() is not null || typeof(DiagnosticSuppressor).IsAssignableFrom(type)))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            var instance = Activator.CreateInstance(type, nonPublic: true);
            instances.Add(instance!);
            if (instance is DiagnosticSuppressor suppressor)
            {
                suppressors.Add(suppressor);
            }
            else if (instance is IIncrementalGenerator incremental)
            {
                producers.Add(incremental.AsSourceGenerator());
                generatorNames.Add(type.FullName!);
            }
            else if (instance is ISourceGenerator generator)
            {
                producers.Add(generator);
                generatorNames.Add(type.FullName!);
            }
        }
    }

    /// <summary>Supplies producer configuration without inheriting the test host's build settings.</summary>
    /// <param name="options">The explicitly supplied global producer options.</param>
    /// <param name="fileOptions">The options keyed by additional file path.</param>
    private sealed class HostOptionsProvider(
        IReadOnlyDictionary<string, string>? options,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? fileOptions) : AnalyzerConfigOptionsProvider
    {
        /// <summary>The empty per-file options.</summary>
        private readonly HostOptions _empty = new(null);

        /// <summary>The frozen additional-file configuration for this compilation.</summary>
        private readonly Dictionary<string, HostOptions> _fileOptions = fileOptions is null
            ? new(StringComparer.Ordinal)
            : fileOptions.ToDictionary(static pair => pair.Key, static pair => new HostOptions(pair.Value), StringComparer.Ordinal);

        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions { get; } = new HostOptions(options);

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _empty;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            _fileOptions.TryGetValue(textFile.Path, out var values) ? values : _empty;
    }

    /// <summary>Provides case-insensitive analyzer configuration lookup.</summary>
    /// <param name="options">The explicit option values.</param>
    private sealed class HostOptions(IReadOnlyDictionary<string, string>? options) : AnalyzerConfigOptions
    {
        /// <summary>The selected option values.</summary>
        private readonly Dictionary<string, string> _options = options is null ? new(StringComparer.OrdinalIgnoreCase) : new(options, StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => _options.TryGetValue(key, out value);
    }

    /// <summary>The compiler input manifest.</summary>
    /// <param name="References">Compile reference paths.</param>
    /// <param name="Producers">Actual packaged generator paths.</param>
    private sealed record HostInputs(ImmutableArray<string> References, ImmutableArray<CapabilityProducerInput> Producers);
}
