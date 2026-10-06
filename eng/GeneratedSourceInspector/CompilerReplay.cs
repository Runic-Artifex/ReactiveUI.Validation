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

internal sealed record CompilerReplay(string Manifest, string GeneratedRoot, string[] OriginalPaths, string[] ReferencePaths,
    string[] AnalyzerPaths, string[] GeneratedPaths, string[] OwnPaths, SyntaxTree[] Trees, CSharpCompilation Compilation, string[][] Records)
{
    internal string[] Values(string kind) => Records.Where(record => record[0] == kind).Select(static record => record[1]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    internal static CompilerReplay Read(string manifestPath, string generatedPath, bool registered)
    {
var manifest = Path.GetFullPath(manifestPath);
var generatedRoot = Path.GetFullPath(generatedPath);
var records = File.ReadAllLines(manifest).Where(static line => !string.IsNullOrWhiteSpace(line))
    .Select(static line => line.Split('|', 2)).ToArray();
if (records.Any(static record => record.Length != 2 || record[0] is not ("source" or "reference" or "analyzer" or "define" or "assembly" or "output" or "language" or "nullable" or "allowlist" or "profile" or "nowarn" or "redundantSuppressions" or "publishTrimmed" or "ilLinkTarget" or "publishAot" or "isAotCompatible" or "enableAotAnalyzer" or "analyzerConfig")))
{
    throw new InvalidDataException("Unknown or malformed exact compiler input record.");
}

string[] Values(string kind) => records.Where(record => record[0] == kind).Select(static record => record[1]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
var originalPaths = Values("source");
var referencePaths = Values("reference");
var analyzerPaths = Values("analyzer");
var generatedPaths = Directory.Exists(generatedRoot)
    ? Directory.GetFiles(generatedRoot, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray() : [];
var ownPaths = generatedPaths.Where(path => Path.GetRelativePath(generatedRoot, path).Split(Path.DirectorySeparatorChar).Contains("ReactiveUI.Validation.SourceGenerators", StringComparer.Ordinal)).ToArray();
if (originalPaths.Length == 0 || referencePaths.Length == 0 || analyzerPaths.Length == 0 || (!registered && ownPaths.Length == 0))
{
    throw new InvalidDataException("Consumer compiler manifest or Validation-owned emitted sources are empty.");
}

if (!LanguageVersionFacts.TryParse(Values("language").Single(), out var language))
{
    throw new InvalidDataException("Unknown exact consumer compiler language version.");
}
var nullable = Values("nullable").Single() switch
{
    "enable" => NullableContextOptions.Enable,
    "warnings" => NullableContextOptions.Warnings,
    "annotations" => NullableContextOptions.Annotations,
    "disable" => NullableContextOptions.Disable,
    _ => throw new InvalidDataException("Unknown exact consumer nullable configuration."),
};
var parse = CSharpParseOptions.Default.WithLanguageVersion(language).WithPreprocessorSymbols(Values("define"))
    .WithFeatures(new Dictionary<string, string> { ["InterceptorsNamespaces"] = string.Join(";", Values("allowlist")) });
var allPaths = originalPaths.Concat(generatedPaths).Distinct(StringComparer.Ordinal).ToArray();
var trees = allPaths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path)).ToArray();
var outputKind = Values("output").Single() switch
{
    "Exe" => OutputKind.ConsoleApplication,
    "WinExe" => OutputKind.WindowsApplication,
    "Library" => OutputKind.DynamicallyLinkedLibrary,
    _ => throw new InvalidDataException("Unknown exact consumer compiler output kind."),
};
var sdkTrimDefault = Values("publishTrimmed").Single() == "true" && Values("redundantSuppressions").Single().Length == 0;
if (Values("nowarn").Any(value => value is not ("1701" or "1702" or "8002") && !(sdkTrimDefault && value == "IL2121")))
{
    throw new InvalidDataException("Consumer compiler suppression exceeds the locked SDK .NETCoreApp defaults.");
}
var compilation = CSharpCompilation.Create(Values("assembly").Single(), trees, referencePaths.Select(static path => MetadataReference.CreateFromFile(path)),
    new CSharpCompilationOptions(outputKind, nullableContextOptions: nullable)
        .WithSpecificDiagnosticOptions(Values("nowarn").ToDictionary(static value => value.StartsWith("IL", StringComparison.Ordinal) ? value : "CS" + value, static _ => ReportDiagnostic.Suppress)));
return new(manifest, generatedRoot, originalPaths, referencePaths, analyzerPaths, generatedPaths, ownPaths, trees, compilation, records);
    }
}
