using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

// Bounded investigation host, deliberately separate from the production generator.
var flavor = args[0];
var root = Path.GetFullPath(args[1]);
var cache = args[2];
// New reproduction output is separate from the immutable historical evidence.
var output = Path.GetFullPath(args[3]);
var historicalEvidence = Path.Combine(root, "evidence");
if (output == root || output == historicalEvidence || output.StartsWith(historicalEvidence + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    throw new InvalidOperationException("Reproduction cannot overwrite historical evidence");
var evidence = Path.Combine(output, "evidence", flavor);
if (Directory.Exists(evidence))
    throw new InvalidOperationException("Each flavor requires a fresh reproduction output");
Directory.CreateDirectory(evidence);
var validationPackage = flavor == "Reactive" ? "runic.reactiveui.validation.reactive" : "runic.reactiveui.validation";
var bindingPackage = flavor == "Reactive" ? "reactiveui.binding.reactive" : "reactiveui.binding";
var ruiPath = Path.Combine(cache, "reactiveui.sourcegenerators/4.2.0/analyzers/dotnet/roslyn5.0/cs/ReactiveUI.SourceGenerators.Roslyn.dll");
var bindingPath = Path.Combine(cache, bindingPackage, "9.1.0/analyzers/dotnet/roslyn4.13/cs/ReactiveUI.Binding.SourceGenerators.dll");
var validationPath = Path.Combine(cache, validationPackage, "8.1.0-runic.0.790.17.15.10/analyzers/dotnet/roslyn5.9/cs/ReactiveUI.Validation.SourceGenerators.dll");
var ruiAssembly = Assembly.LoadFrom(ruiPath);
var bindingAssembly = Assembly.LoadFrom(bindingPath);
var validationAssembly = Assembly.LoadFrom(validationPath);
var producerGenerators = Discover(ruiAssembly).Concat(Discover(bindingAssembly)).ToArray();
var references = File.ReadAllLines(Path.Combine(root, "obj", flavor, "references.txt"))
    .Where(path => !path.Contains("microsoft.codeanalysis", StringComparison.OrdinalIgnoreCase))
    .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
var parse = new CSharpParseOptions(LanguageVersion.CSharp14).WithFeatures(new[]
{
    new KeyValuePair<string, string>("InterceptorsNamespaces", "ReactiveUI.Validation.Generated;ReactiveUI.Binding.Generated.Interceptors"),
});
var ui = flavor == "Reactive" ? "ReactiveUI.Reactive" : "ReactiveUI";
var validation = flavor == "Reactive" ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
var imports = flavor == "Reactive" ? "using ReactiveUI.Binding.Reactive;\nusing System.Reactive.Linq;" : "using ReactiveUI.Binding;\nusing ReactiveUI.Primitives;";
var source = $$"""
    #nullable enable
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ReactiveUI;
    using {{ui}}.Builder;
    using ReactiveUI.SourceGenerators;
    using {{validation}}.Extensions;
    using {{validation}}.Helpers;
    {{imports}}
    namespace ProjectionInput;
    public sealed partial class Model : ReactiveValidationObject
    {
        [Reactive] private string? _name = null;
    }
    public static class Runner
    {
        public static string Run()
        {
            RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
            using var model = new Model();
            var seen = new List<string?>();
            using var observation = model.WhenAnyValue(x => x.Name).Subscribe(seen.Add);
            using var rule = model.ValidationRule(x => x.Name, value => !string.IsNullOrWhiteSpace(value), "required");
            Require(!rule.IsValid && model.Name is null, "initial nullable invalid");
            model.Name = "Ada";
            Require(rule.IsValid, "generated setter delivers valid update");
            model.Name = null;
            Require(!rule.IsValid, "nullable reset invalid");
            rule.Dispose();
            Require(model.ValidationContext.Validations.Count == 0, "rule disposal unregisters");
            model.Name = "Detached";
            Require(seen.SequenceEqual(new string?[] { null, "Ada", null, "Detached" }), "Binding privately projects same field and observes updates");
            return "initial-invalid -> Ada-valid -> null-invalid -> disposed; Binding=[null,Ada,null,Detached]";
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
    """;
var originalPath = Path.Combine(evidence, "OriginalInput.cs");
File.WriteAllText(originalPath, source);
var originalTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parse, originalPath);
// Match the SDK's observed NoWarn=1701;1702 defaults. System.Reactive's net8
// framework reference is unified with the net10 reference set by the real SDK too.
var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
    .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> { ["CS1701"] = ReportDiagnostic.Suppress, ["CS1702"] = ReportDiagnostic.Suppress });
var results = new List<object>();
foreach (var order in new[] { "producer-first", "adapter-first" })
{
    var directory = Path.Combine(evidence, order);
    Directory.CreateDirectory(directory);
    var compilation = CSharpCompilation.Create("ProjectionConsumer_" + flavor + "_" + order.Replace('-', '_'), new[] { originalTree }, references,
        compilationOptions);
    var adapter = new ProjectionAdapter(validationAssembly, parse);
    var generators = order == "producer-first" ? producerGenerators.Append(adapter).ToArray() : new[] { (ISourceGenerator)adapter }.Concat(producerGenerators).ToArray();
    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parse);
    driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var final, out var driverDiagnostics);
    var run = driver.GetRunResult();
    var diagnostics = driverDiagnostics.Concat(final.GetDiagnostics()).Where(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ToArray();
    File.WriteAllLines(Path.Combine(directory, "diagnostics.txt"), diagnostics.Select(d => d.ToString()));
    Require(diagnostics.Length == 0, string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));
    var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(validationAssembly.GetType("ReactiveUI.Validation.SourceGenerators.ValidationDispatchAnalyzer", true)!)!;
    var dispatch = await final.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAnalyzerDiagnosticsAsync();
    File.WriteAllLines(Path.Combine(directory, "dispatch-diagnostics.txt"), dispatch.Select(d => d.ToString()));
    Require(dispatch.Length == 0, "final dispatch analysis: " + string.Join(Environment.NewLine, dispatch));
    var generated = run.Results.SelectMany(result => result.GeneratedSources.Select(item => new { generator = result.Generator.GetType().FullName, item.HintName, text = item.SourceText.ToString() })).ToArray();
    for (var index = 0; index < generated.Length; index++)
    {
        File.WriteAllText(Path.Combine(directory, index + "." + Path.GetFileName(generated[index].HintName)), generated[index].text);
    }
    Require(final.SyntaxTrees.Contains(originalTree), "original tree identity retained");
    Require(!final.SyntaxTrees.Any(tree => tree.GetText().ToString().Contains(ProjectionAdapter.Marker, StringComparison.Ordinal)), "private declaration never emitted");
    Require(generated.Any(item => item.text.Contains("RaiseAndSetIfChanged", StringComparison.Ordinal)), "actual ReactiveUI property implementation emitted");
    Require(generated.Any(item => item.HintName == "ValidationInterceptors.g.cs"), "Validation interceptor forwarded");
    var producedProperty = final.GetTypeByMetadataName("ProjectionInput.Model")!.GetMembers("Name").OfType<IPropertySymbol>().Single();
    Require(producedProperty.Type.SpecialType == SpecialType.System_String && producedProperty.NullableAnnotation == NullableAnnotation.Annotated
        && producedProperty.DeclaredAccessibility == Accessibility.Public, "actual producer property matches projected public string? contract");
    var ruleCall = originalTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single(node => node.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "ValidationRule");
    var interceptor = final.GetSemanticModel(originalTree).GetInterceptorMethod(ruleCall);
    Require(interceptor is not null, "original Validation call intercepted in final compilation");
    var bindingCall = originalTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single(node => node.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "WhenAnyValue");
    var bindingInterceptor = final.GetSemanticModel(originalTree).GetInterceptorMethod(bindingCall);
    Require(bindingInterceptor is not null, "original Binding call intercepted in final compilation");
    using var image = new MemoryStream();
    var emit = final.Emit(image);
    Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
    File.WriteAllBytes(Path.Combine(directory, "Consumer.dll"), image.ToArray());
    image.Position = 0;
    var assembly = AssemblyLoadContext.Default.LoadFromStream(image);
    var trace = (string)assembly.GetType("ProjectionInput.Runner", true)!.GetMethod("Run")!.Invoke(null, null)!;
    results.Add(new { order, trace, generatedCount = generated.Length, emittedProjection = false, finalDispatchDiagnostics = dispatch.Length,
        originalPath, originalSourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(),
        originalCallSpan = ruleCall.Span.ToString(), originalCallLineSpan = ruleCall.GetLocation().GetLineSpan().ToString(),
        interceptor = interceptor!.ToDisplayString(), bindingInterceptor = bindingInterceptor!.ToDisplayString(), producedProperty = producedProperty.ToDisplayString(),
        innerDriver = "prototype-only private Validation analysis", sources = generated.Select(item => new { item.generator, item.HintName }) });
    File.WriteAllText(Path.Combine(directory, "PRIVATE-PROJECTION-NOT-EMITTED.txt"), adapter.ProjectedText);
    Console.WriteLine(flavor + " " + order + " PASS: " + trace);
}

// Control: normal driver ordering alone cannot supply another generator's property.
var controlCompilation = CSharpCompilation.Create("ProjectionControl_" + flavor, new[] { originalTree }, references,
    compilationOptions);
GeneratorDriver control = CSharpGeneratorDriver.Create(producerGenerators.Append(CreateValidation(validationAssembly)), parseOptions: parse);
control = control.RunGeneratorsAndUpdateCompilation(controlCompilation, out var controlFinal, out var controlDiagnostics);
var controlAnalyzer = (DiagnosticAnalyzer)Activator.CreateInstance(validationAssembly.GetType("ReactiveUI.Validation.SourceGenerators.ValidationDispatchAnalyzer", true)!)!;
var controlDispatch = await controlFinal.WithAnalyzers(ImmutableArray.Create(controlAnalyzer)).GetAnalyzerDiagnosticsAsync();
File.WriteAllLines(Path.Combine(evidence, "unprojected-control-diagnostics.txt"), controlDiagnostics.Concat(controlDispatch).Select(d => d.ToString()));
Require(controlDiagnostics.Any(d => d.Id == "RUVG001") && controlDispatch.Any(d => d.Id == "RUVG005"), "unprojected control must reject unsupported and undispatched normal call");
Require(controlFinal.GetTypeByMetadataName("ProjectionInput.Model")!.GetMembers("Name").OfType<IPropertySymbol>().Count() == 1, "control real producer adds Name after Validation's original semantic pass");
Require(!controlFinal.GetDiagnostics().Any(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error), "control final source binds successfully; only deliberate generator/dispatch errors reject it");
File.WriteAllText(Path.Combine(evidence, "result.json"), JsonSerializer.Serialize(new
{
    flavor, roslyn = typeof(CSharpCompilation).Assembly.FullName, sdkDefaultNoWarn = "1701;1702",
    inputs = new[] { ruiPath, bindingPath, validationPath }.Select(path => new { path, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() }),
    generators = new[] { ruiAssembly, bindingAssembly }.SelectMany(assembly => assembly.GetTypes().Where(type => type.GetCustomAttributes<GeneratorAttribute>().Any()).Select(type => type.FullName)), results,
    control = new { generatorDiagnostics = controlDiagnostics.Select(d => d.Id), dispatchDiagnostics = controlDispatch.Select(d => d.Id) },
}, new JsonSerializerOptions { WriteIndented = true }));

static IEnumerable<ISourceGenerator> Discover(Assembly assembly) => assembly.GetTypes()
    .Where(type => !type.IsAbstract && type.GetCustomAttributes<GeneratorAttribute>().Any())
    .Select(type => Activator.CreateInstance(type) switch
    {
        IIncrementalGenerator incremental => incremental.AsSourceGenerator(),
        ISourceGenerator classic => classic,
        _ => throw new InvalidOperationException("Unknown generator " + type),
    });
static ISourceGenerator CreateValidation(Assembly assembly) => ((IIncrementalGenerator)Activator.CreateInstance(assembly.GetType("ReactiveUI.Validation.SourceGenerators.ValidationGenerator", true)!)!).AsSourceGenerator();
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

// This adapter is not a proposed production implementation. Its private inner driver
// runs Validation against a compiler-only declaration; the outer driver remains a
// single driver over the unchanged original source for all real producer generators.
sealed class ProjectionAdapter(Assembly validationAssembly, CSharpParseOptions parse) : ISourceGenerator
{
    public const string Marker = "PRIVATE_SEMANTIC_PROJECTION_NEVER_EMIT";
    public string? ProjectedText { get; private set; }
    public void Initialize(GeneratorInitializationContext context) { }
    public void Execute(GeneratorExecutionContext context)
    {
        var model = context.Compilation.GetTypeByMetadataName("ProjectionInput.Model") ?? throw new InvalidOperationException("Missing model");
        var field = model.GetMembers("_name").OfType<IFieldSymbol>().Single();
        if (field.Type.SpecialType != SpecialType.System_String || field.NullableAnnotation != NullableAnnotation.Annotated
            || !field.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "ReactiveUI.SourceGenerators.ReactiveAttribute"))
            throw new InvalidOperationException("Prototype only accepts one known [Reactive] string? _name field");
        ProjectedText = "// " + Marker + "\n#nullable enable\nnamespace ProjectionInput { public sealed partial class Model { public string? Name { get { throw null!; } set { } } } }";
        var projection = CSharpSyntaxTree.ParseText(SourceText.From(ProjectedText, Encoding.UTF8), parse, "PRIVATE-PROJECTION.cs");
        var privateCompilation = context.Compilation.AddSyntaxTrees(projection);
        var generator = ((IIncrementalGenerator)Activator.CreateInstance(validationAssembly.GetType("ReactiveUI.Validation.SourceGenerators.ValidationGenerator", true)!)!).AsSourceGenerator();
        GeneratorDriver inner = CSharpGeneratorDriver.Create(new[] { generator }, parseOptions: parse);
        inner = inner.RunGeneratorsAndUpdateCompilation(privateCompilation, out _, out var diagnostics, context.CancellationToken);
        foreach (var diagnostic in diagnostics) context.ReportDiagnostic(diagnostic);
        foreach (var source in inner.GetRunResult().Results.Single().GeneratedSources)
            context.AddSource(source.HintName, source.SourceText);
        // Never forward the private compilation or its projection syntax tree.
    }
}
