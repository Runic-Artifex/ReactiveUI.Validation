// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Executes exact index operands, borrowed selector storage and protected write boundaries.</summary>
public sealed class SemanticOperationBoundaryCompilerTests
{
    /// <summary>The shipping Primitives capability namespace.</summary>
    private const string RuntimeNamespace = "ReactiveUI.Validation";

    /// <summary>The stable root variable in generated behavior probes.</summary>
    private const string ModelVariable = "model";

    /// <summary>The finite call marker in the trusted fixture.</summary>
    private const string RegisterMember = "Register";

    /// <summary>The original API selector payload slot.</summary>
    private const string SelectorArgument = "selector";

    /// <summary>The exact behavior probe entry point.</summary>
    private const string EntryMethod = "Check";

    /// <summary>The inserted behavior probe marker in the trusted indexed fixture.</summary>
    private const string ProbeMarker = "__PROBE__";

    /// <summary>The bounded indexed model with observable key effects and nullable setter input.</summary>
    private const string IndexFixture = """
        using System;
        using System.ComponentModel;
        using System.Diagnostics.CodeAnalysis;
        using System.Linq.Expressions;
        public enum Kind { First = 4 }
        public sealed class Model : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            public void Signal() => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs("Key"));
            public string Order = "";
            public int Key { get { Order += "F"; return 2; } }
            public int Second { get { Order += "S"; return 9; } }
            public int GetterCalls;
            public int SetterCalls;
            public string? Stored;
            public string Slot = "";
            public int[]? ArrayKey;
            public Kind? NullableKey;
            public string this[int first,int second=7,Kind kind=Kind.First,string? label=null,Kind? nullableKind=Kind.First]
            {
                get { GetterCalls++; NullableKey=nullableKind; Slot=$"{first}/{second}/{kind}/{label ?? "null"}"; return Slot; }
                [param:AllowNull] set { SetterCalls++; NullableKey=nullableKind; Stored=value; Slot=$"{first}/{second}/{kind}/{label ?? "null"}"; }
            }
            public string this[int first,params int[] rest]
            {
                get { GetterCalls++; ArrayKey=rest; Slot=$"{first}/{string.Join(",",rest)}"; return Slot; }
                [param:AllowNull] set { SetterCalls++; ArrayKey=rest; Stored=value; Slot=$"{first}/{string.Join(",",rest)}"; }
            }
            public static void Register<TSource,TValue>(TSource source,Expression<Func<TSource,TValue>> selector) { }
            public void Plan() => Register(this,__SELECTOR__);
            __PROBE__
        }
        """;

    /// <summary>The original legal getter with an independently protected setter.</summary>
    private const string ProtectedFixture = """
        using System;
        using System.ComponentModel;
        using System.Linq.Expressions;
        public class Base : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            public void Signal() => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs("Message"));
            public string Message { get; protected set; }="initial";
        }
        public sealed partial class Derived : Base
        {
            public static void Register<TSource,TValue>(TSource source,Expression<Func<TSource,TValue>> selector) { }
            public void Plan() => Register(this,__SELECTOR__);
            private sealed class Values : IObservable<string>,IDisposable
            {
                public IDisposable Subscribe(IObserver<string> observer) { observer.OnNext("written"); return this; }
                public void Dispose() { }
            }
            public static bool Check()
            {
                var owner=new Derived();
                var target=new ReactiveUI.Validation.Capabilities.ValidationTarget<Derived,string>(current=>
                    new ReactiveUI.Validation.Capabilities.ValidationWritePlan<string>(
                        ()=>ReactiveUI.Validation.Capabilities.ValidationTargetAccess<string>.Present(current,value=>current.Message=value),
                        Array.Empty<ReactiveUI.Validation.Capabilities.ValidationDependency>()));
                using var binding=target.Bind(owner).Bind(new Values());
                if (owner.Message!="written") return false;
                __GENERATED__
                return owner.Message=="__EXPECTED__";
            }
        }
        """;

    /// <summary>Uses defaults, named arguments and params arrays once for read, metadata and exact setter bridges.</summary>
    /// <param name="expression">The original expression-tree selector.</param>
    /// <param name="order">The required source evaluation order.</param>
    /// <param name="slot">The actual selected indexed slot.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments("x => x[x.Key]", "F", "2/7/First/null")]
    [Arguments("x => x[second:x.Second,first:x.Key]", "SF", "2/9/First/null")]
    [Arguments("x => x[x.Key,1,2]", "F", "2/1,2")]
    public async Task IndexedOperandsRemainAtomic(string expression, string order, string slot)
    {
        var (source, original, typedAlternative) = await AdmitIndexSourceAsync(IndexFixture.Replace("__SELECTOR__", expression));
        var tree = original.Compilation.SyntaxTrees.First();
        var model = original.Compilation.GetSemanticModel(tree);
        var call = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static node => node.Expression.ToString() == RegisterMember);
        var site = new CallSite(model, call, (IMethodSymbol)model.GetSymbolInfo(call).Symbol!, RuntimeNamespace, string.Empty, 0);
        var selectorSyntax = site.GetArgument(SelectorArgument)!;
        await Assert.That(GeneratorHelpers.TrySelector(site, selectorSyntax, out var read, out _, false)).IsTrue();
        await Assert.That(GeneratorHelpers.TryAccessPlan(site, selectorSyntax, out var target, out _)).IsTrue();
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                {{(typedAlternative ? NamedFactoryProbe(expression, slot, order) : string.Empty)}}
                var value = ((Func<{{RuntimeNamespace}}.Capabilities.ValidationRead<string>>)({{read!.EmitRead(ModelVariable, RuntimeNamespace)}}))();
                if (value.Value != "{{slot}}" || model.Order != "{{order}}" || model.GetterCalls != 1) return false;
                if (model.ArrayKey is null && model.NullableKey != Kind.First) return false;
                model.Order="";
                var metadata = ((Func<{{RuntimeNamespace}}.Capabilities.ValidationRead<{{RuntimeNamespace}}.Capabilities.ValidationPath>>)
                    ({{read.EmitMetadataRead(ModelVariable, RuntimeNamespace)}}))();
                if (!metadata.HasOwner || model.Order != "{{order}}" || model.GetterCalls != 1) return false;
                model.Order="";
                Action<string?> write = output => {{target!.EmitAssignment(ModelVariable, "output")}};
                write(null);
                return model.Order == "{{order}}" && model.GetterCalls == 1 && model.SetterCalls == 1 && model.Stored is null && model.Slot == "{{slot}}";
            }
            """;
        var fragments = string.Join(Environment.NewLine, site.AdditionalSources.Select(static fragment => EmbeddedSource(fragment.Source)));
        using var probeHost = CapabilityCompilerHost.Create(false, includeValidation: false);
        var result = await probeHost.RunAsync(source.Replace(ProbeMarker, probe) + fragments);
        await Assert.That(result.CompilationDiagnostics).IsEmpty();
        await Assert.That(result.ExecuteBoolean("Model", EntryMethod)).IsTrue();
    }

    /// <summary>Sees semantic in-parameter escapes even when the call omits its keyword.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task OmittedInCannotFreezeSelectorStorage()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using System.Runtime.CompilerServices;
            public sealed class Model
            {
                public int First => 1;
                public int Second => 2;
                public static void Register<TSource,TValue>(TSource source,Expression<Func<TSource,TValue>> selector) { }
                public void Plan()
                {
                    Expression<Func<Model,int>> selector=x=>x.First;
                    Borrow(selector);
                    Register(this,selector);
                }
                private static void Borrow(in Expression<Func<Model,int>> slot) => Unsafe.AsRef(in slot)=x=>x.Second;
                public static bool Check()
                {
                    var model=new Model(); Expression<Func<Model,int>> selector=x=>x.First; Borrow(selector);
                    var path=ReactiveUI.Validation.Capabilities.ValidationPath.Legacy("Second");
                    var paths=new[]{path};
                    var typed=new ReactiveUI.Validation.Capabilities.ValidationSelector<Model,int>(owner=>
                        new ReactiveUI.Validation.Capabilities.ValidationAccessPlan<int>(
                            ()=>ReactiveUI.Validation.Capabilities.ValidationRead<int>.Present(owner.Second,paths),
                            Array.Empty<ReactiveUI.Validation.Capabilities.ValidationDependency>(),
                            ReactiveUI.Validation.Capabilities.ValidationObservationOptions<int>.Default));
                    using var registry=new ReactiveUI.Validation.Capabilities.ValidationPlanRegistry(1);
                    using var attached=registry.Attach(model);
                    using var registration=registry.RegisterSelector(ReactiveUI.Validation.Capabilities.ValidationPlanRole.RuleValue,selector,typed);
                    var actual=ReactiveUI.Validation.Capabilities.ValidationRuntime.ResolveSelector(model,selector,
                        ReactiveUI.Validation.Capabilities.ValidationPlanRole.RuleValue,string.Empty).Bind(model).Read();
                    return ((MemberExpression)selector.Body).Member.Name=="Second" && actual.Value==2 && actual.Paths[0].Equals(path);
                }
            }
            """;
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false);
        var result = await host.RunAsync(source);
        await Assert.That(result.CompilationDiagnostics).IsEmpty();
        var tree = result.Compilation.SyntaxTrees.First();
        var semantic = result.Compilation.GetSemanticModel(tree);
        var call = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static node => node.Expression.ToString() == RegisterMember);
        var site = new CallSite(semantic, call, (IMethodSymbol)semantic.GetSymbolInfo(call).Symbol!, RuntimeNamespace, string.Empty, 0);
        await Assert.That(GeneratorHelpers.TrySelector(site, site.GetArgument(SelectorArgument)!, out _, out var reason, false)).IsFalse();
        await Assert.That(reason).Contains("typed ValidationSelector");
        await Assert.That(result.ExecuteBoolean("Model", EntryMethod)).IsTrue();
    }

    /// <summary>Proves protected receiver legality and executes an authored legal derived target.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task ProtectedSetterRetainsThroughTypeRestriction()
    {
        foreach (var legal in new[] { false, true })
        {
            var actual = ProtectedFixture.Replace("__SELECTOR__", legal ? "x=>x.Message" : "x=>((Base)x).Message");
            var baseline = actual.Replace("__GENERATED__", string.Empty).Replace("__EXPECTED__", "written");
            using var baselineHost = CapabilityCompilerHost.Create(false, includeValidation: false);
            var original = await baselineHost.RunAsync(baseline);
            await Assert.That(original.CompilationDiagnostics).IsEmpty();
            var tree = original.Compilation.SyntaxTrees.First();
            var semantic = original.Compilation.GetSemanticModel(tree);
            var call = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static node => node.Expression.ToString() == RegisterMember);
            var site = new CallSite(semantic, call, (IMethodSymbol)semantic.GetSymbolInfo(call).Symbol!, RuntimeNamespace, string.Empty, 0);
            await Assert.That(GeneratorHelpers.TryAccessPlan(site, site.GetArgument(SelectorArgument)!, out var target, out var reason)).IsEqualTo(legal);
            if (legal)
            {
                actual = actual.Replace("__GENERATED__", $"Action<string> write=value=>{target!.EmitAssignment("owner", "value")}; write(\"generated\");")
                    .Replace("__EXPECTED__", "generated");
                actual += string.Join(Environment.NewLine, site.AdditionalSources.Select(static fragment => EmbeddedSource(fragment.Source)));
            }
            else
            {
                await Assert.That(reason).Contains("typed");
                actual = baseline;
                using var illegalHost = CapabilityCompilerHost.Create(false, includeValidation: false);
                var illegal = await illegalHost.RunAsync(baseline.Replace(
                    "public void Plan() => Register(this,x=>((Base)x).Message);",
                    "public void Plan() { ((Base)this).Message=\"illegal\"; }"));
                await Assert.That(illegal.CompilationDiagnostics.Any(static diagnostic => diagnostic.Id == "CS1540")).IsTrue();
            }

            using var probeHost = CapabilityCompilerHost.Create(false, includeValidation: false);
            var result = await probeHost.RunAsync(actual);
            await Assert.That(result.CompilationDiagnostics).IsEmpty();
            await Assert.That(result.ExecuteBoolean("Derived", EntryMethod)).IsTrue();
        }
    }

    /// <summary>Records the locked compiler's named-expression-tree boundary before executing a typed delegate route.</summary>
    /// <param name="source">The original expression-tree consumer.</param>
    /// <returns>The admitted source, actual semantic compilation and alternative disposition.</returns>
    private static async Task<(string Source, CapabilityCompilation Compilation, bool TypedAlternative)> AdmitIndexSourceAsync(string source)
    {
        using var originalHost = CapabilityCompilerHost.Create(false, includeValidation: false);
        var result = await originalHost.RunAsync(source.Replace(ProbeMarker, string.Empty));
        var typedAlternative = source.Contains("second:x.Second", StringComparison.Ordinal);
        if (typedAlternative)
        {
            await Assert.That(result.CompilationDiagnostics.Length).IsEqualTo(1);
            await Assert.That(result.CompilationDiagnostics[0].Id).IsEqualTo("CS9307");
            source = source.Replace("Expression<Func<TSource,TValue>> selector", "Func<TSource,TValue> selector")
                .Replace("using System.Linq.Expressions;", string.Empty);
            using var alternativeHost = CapabilityCompilerHost.Create(false, includeValidation: false);
            result = await alternativeHost.RunAsync(source.Replace(ProbeMarker, string.Empty));
        }

        await Assert.That(result.CompilationDiagnostics).IsEmpty();
        return (source, result, typedAlternative);
    }

    /// <summary>Executes an authored typed factory for the compiler-rejected reordered expression-tree shape.</summary>
    /// <param name="expression">The exact legal delegate expression.</param>
    /// <param name="slot">The expected selected slot.</param>
    /// <param name="order">The required source evaluation order.</param>
    /// <returns>The bounded behavior probe followed by a fresh mechanical planner probe.</returns>
    private static string NamedFactoryProbe(string expression, string slot, string order) => $$"""
        Func<Model,string> getter={{expression}};
        var authored=new {{RuntimeNamespace}}.Capabilities.ValidationSelector<Model,string>(owner=>
            new {{RuntimeNamespace}}.Capabilities.ValidationAccessPlan<string>(
                ()=>{{RuntimeNamespace}}.Capabilities.ValidationRead<string>.Present(getter(owner),Array.Empty<{{RuntimeNamespace}}.Capabilities.ValidationPath>()),
                Array.Empty<{{RuntimeNamespace}}.Capabilities.ValidationDependency>(),
                {{RuntimeNamespace}}.Capabilities.ValidationObservationOptions<string>.Default));
        var selected=authored.Bind(model).Read();
        if (selected.Value!="{{slot}}" || model.Order!="{{order}}" || model.GetterCalls!=1) return false;
        model.Order=""; model.GetterCalls=0;
        """;

    /// <summary>Embeds separate generated files in one trusted probe without changing their namespace.</summary>
    /// <param name="source">The standalone emitted auxiliary source.</param>
    /// <returns>The equivalent block-scoped namespace suitable for this single-file fixture.</returns>
    private static string EmbeddedSource(string source)
    {
        var unit = SyntaxFactory.ParseCompilationUnit(source);
        if (unit.Members.OfType<FileScopedNamespaceDeclarationSyntax>().SingleOrDefault() is not { } fileNamespace)
        {
            return source;
        }

        var block = SyntaxFactory.NamespaceDeclaration(fileNamespace.Name).WithExterns(fileNamespace.Externs)
            .WithUsings(fileNamespace.Usings).WithMembers(fileNamespace.Members).WithTriviaFrom(fileNamespace);
        return unit.ReplaceNode(fileNamespace, block).NormalizeWhitespace().ToFullString();
    }
}
