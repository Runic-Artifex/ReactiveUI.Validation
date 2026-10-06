// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ReactiveUI.Validation.SourceGenerators;

if (args.Length == 3 && args[0] == "expression-il")
{
    var evidence = ExpressionIlMetadata.Read(args[1]);
    await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Original caller expression construction IL inspected.");
    return 0;
}

if (args.Length == 3 && args[0] == "legacy-pe")
{
    var evidence = LegacyPeerMetadata.Read(args[1]);
    await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Historical peer original normal API IL verified.");
    return 0;
}

if (args.Length is not (3 or 4) || args.Length == 4 && args[3] != "registered")
{
    await Console.Error.WriteLineAsync("Expected exact compiler manifest, emitted-source root, and report path.");
    return 2;
}
var registered = args.Length == 4;
var replay = CompilerReplay.Read(args[0], args[1], registered);
var manifest = replay.Manifest;
var generatedRoot = replay.GeneratedRoot;
var originalPaths = replay.OriginalPaths;
var referencePaths = replay.ReferencePaths;
var analyzerPaths = replay.AnalyzerPaths;
var generatedPaths = replay.GeneratedPaths;
var ownPaths = replay.OwnPaths;
var compilation = replay.Compilation;
string[] Values(string kind) => replay.Values(kind);
using var suppressorLoader = new CompilerSuppressorLoadContext(analyzerPaths);
var suppressors = suppressorLoader.LoadSuppressors(analyzerPaths);
var rawDiagnostics = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>())).GetDiagnostics();
var finalDiagnostics = suppressors.IsEmpty ? compilation.GetDiagnostics()
    : await compilation.WithAnalyzers(suppressors).GetAllDiagnosticsAsync();
var diagnostics = finalDiagnostics.Where(static diagnostic => !diagnostic.IsSuppressed && diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
if (diagnostics.Length != 0)
{
    throw new InvalidDataException("Exact consumer semantic replay is not clean:\n" + string.Join("\n", diagnostics.Select(static diagnostic => diagnostic.ToString())));
}

var shippingMethods = SemanticInventory.Catalogue(compilation);
var dispatch = SemanticInventory.Dispatch(replay);
var declarations = SemanticInventory.Declarations(replay);
object FileEvidence(string path) => new { path, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) };
var report = new
{
    completed = true,
    mode = registered ? "registered" : "generated",
    manifest = FileEvidence(manifest),
    assembly = compilation.AssemblyName,
    output = Values("output").Single(),
    language = Values("language").Single(),
    nullable = Values("nullable").Single(),
    allowlist = Values("allowlist"),
    noWarn = Values("nowarn"),
    redundantSuppressions = Values("redundantSuppressions").Single(),
    publishTrimmed = Values("publishTrimmed").Single(),
    ilLinkTarget = Values("ilLinkTarget").Single(),
    aotProperties = new
    {
        publishAot = Values("publishAot").Single(),
        isAotCompatible = Values("isAotCompatible").Single(),
        enableAotAnalyzer = Values("enableAotAnalyzer").Single(),
    },
    analyzerConfigs = Values("analyzerConfig").Select(FileEvidence),
    producerProfiles = Values("profile"),
    defines = Values("define"),
    originalSources = originalPaths.Select(FileEvidence),
    references = referencePaths.Select(FileEvidence),
    validationAssemblies = ShippingAssemblyMetadata.Read(referencePaths),
    analyzers = analyzerPaths.Select(FileEvidence),
    producerSuppressors = suppressors.Select(static suppressor => suppressor.GetType().FullName),
    suppressedDiagnostics = rawDiagnostics.Where(raw => !finalDiagnostics.Any(final => !final.IsSuppressed && final.Id == raw.Id && final.Location.Equals(raw.Location)))
        .Select(static diagnostic => new { id = diagnostic.Id, source = diagnostic.Location.SourceTree?.FilePath, message = diagnostic.GetMessage() }),
    generatedSources = generatedPaths.Select(path => new
    {
        path = Path.GetRelativePath(generatedRoot, path).Replace('\\', '/'),
        sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
        validationOwned = ownPaths.Contains(path, StringComparer.Ordinal),
    }),
    shippingMethods = shippingMethods.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(pair => new
    {
        methodId = pair.Key,
        api = pair.Value.ContainingType.Name + "." + pair.Value.Name,
        typeParameters = pair.Value.TypeParameters.Select(static parameter => parameter.Name),
        genericParameterContracts = SemanticInventory.GenericParameters(pair.Value.TypeParameters),
        signature = SemanticInventory.Signature(pair.Value),
        selectorForm = ValidationCallClassifier.SelectorForm(pair.Value),
        returnType = SemanticInventory.TypeName(pair.Value.ReturnType),
        arity = pair.Value.Arity,
        parameters = pair.Value.Parameters.Select(static parameter => new { name = parameter.Name, type = SemanticInventory.TypeName(parameter.Type), refKind = parameter.RefKind.ToString() }),
    }),
    dispatch,
    declarations,
};
await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Semantic inspection passed: {ownPaths.Length} Validation files, {dispatch.Count} consumer calls.");
return 0;
