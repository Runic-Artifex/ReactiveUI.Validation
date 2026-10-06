// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks actual overload selection after private declaration enrichment and real producer execution.</summary>
public sealed class FuncProducerCompilerTests
{
    /// <summary>The finite delegate's original input slot and widened actual API result contract.</summary>
    private const string VarianceTemplate = """
            using System;
            using System.Linq;
            using __UI_ROOT__;
            using __UI_ROOT__.Builder;
            using __VALIDATION_ROOT__.Abstractions;
            using __VALIDATION_ROOT__.Capabilities;
            using __VALIDATION_ROOT__.Contexts;
            using __VALIDATION_ROOT__.Extensions;
            public class BaseModel : ReactiveObject, IValidatableViewModel
            {
                private string? _name;
                public int Reads;
                public string? Name { get { Reads++; return _name; } set => this.RaiseAndSetIfChanged(ref _name,value); }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
            }
            public sealed class DerivedModel : BaseModel
            {
                public new string? Name { get => throw new Exception("Wrong shadow getter"); set => throw new Exception("Wrong shadow setter"); }
                public static bool Check()
                {
                    RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                    var model = new DerivedModel();
                    Func<BaseModel,string?> selector = value => value.Name;
                    using var rule = ValidatableViewModelExtensions.ValidationRule<DerivedModel,object>(
                        model,selector,static value => value is string text && text == "ok",
                        static value => value is null ? "missing" : "invalid:" + value);
                    if (rule.IsValid || rule.Message.ToSingleLine() != "missing") return false;
                    var component = (IValidationPathComponent)model.ValidationContext.Validations.Items.Single();
                    if (!component.ContainsPropertyName(nameof(BaseModel.Name),true)
                        || component.ValidationPaths.Count != 1 || component.ValidationPaths[0].IsLegacy
                        || component.ValidationPaths[0].DisplayPath != nameof(BaseModel.Name)) return false;
                    ((BaseModel)model).Name = "ok";
                    if (!rule.IsValid) return false;
                    ((BaseModel)model).Name = null;
                    if (rule.IsValid || rule.Message.ToSingleLine() != "missing") return false;
                    rule.Dispose();
                    var reads = model.Reads;
                    ((BaseModel)model).Name = "ok";
                    return model.Reads == reads && !model.ValidationContext.Validations.Items.Any();
                }
            }
            """;

    /// <summary>Preserves priority Func selection and explicit Expression controls for initially unavailable generated members.</summary>
    /// <param name="reactive">Whether to use the System.Reactive runtime graph.</param>
    /// <param name="validationFirst">Whether Validation precedes the actual peer generators.</param>
    /// <returns>The asynchronous compiler, final dispatch and execution assertions.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task GeneratedMemberUsesActualSelectedSelectorForm(bool reactive, bool validationFirst)
    {
        using var host = CapabilityCompilerHost.Create(reactive, validationFirst);
        var result = await host.RunAsync(Source(reactive), path: "FuncProducerCaller.cs");
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        var tree = result.Compilation.SyntaxTrees.Single(static item => item.FilePath == "FuncProducerCaller.cs");
        var model = result.Compilation.GetSemanticModel(tree);
        var forms = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(invocation => model.GetSymbolInfo(invocation).Symbol)
            .OfType<IMethodSymbol>()
            .Where(ValidationCallClassifier.IsNormalMethod)
            .Select(ValidationCallClassifier.SelectorForm)
            .ToArray();
        await Assert.That(forms).IsEquivalentTo(["Func", "Func", "Expression"]);
        await Assert.That(result.ExecuteBoolean("Model", "Check")).IsTrue();
        await Assert.That(result.Compilation.SyntaxTrees.Any(static item => item.FilePath == "Validation.PrivateProducerProjection.cs")).IsFalse();
    }

    /// <summary>Preserves the original base member and covariant output of a recovered finite delegate.</summary>
    /// <param name="reactive">Whether to use the System.Reactive runtime graph.</param>
    /// <returns>The asynchronous real compiler, dispatch and execution assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ContravariantFuncReadsOriginalMemberSlot(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(VarianceSource(reactive));
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean("DerivedModel", "Check")).IsTrue();
    }

    /// <summary>Uses an explicit generic caller type and a throwing shadow to expose unintended member rebinding.</summary>
    /// <param name="reactive">Whether to use the System.Reactive namespaces.</param>
    /// <returns>The finite stored Func caller with its original base input contract.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string VarianceSource(bool reactive) =>
        VarianceTemplate.Replace("__UI_ROOT__", reactive ? "ReactiveUI.Reactive" : "ReactiveUI", StringComparison.Ordinal)
            .Replace("__VALIDATION_ROOT__", reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation", StringComparison.Ordinal);

    /// <summary>Supplies identical original source to real peers regardless of their driver ordering.</summary>
    /// <param name="reactive">Whether to use the System.Reactive namespaces.</param>
    /// <returns>The original producer-backed caller.</returns>
    private static string Source(bool reactive)
    {
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        return $$"""
            using System;
            using System.Linq.Expressions;
            using ReactiveUI.SourceGenerators;
            using {{(reactive ? "ReactiveUI.Reactive.Builder" : "ReactiveUI.Builder")}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            [IReactiveObject]
            public partial class Model : IValidatableViewModel
            {
                [Reactive] private string? _name;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static bool Check()
                {
                    RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                    var model = new Model();
                    using var inline = model.ValidationRule(x => x.Name, static x => x == "ok", "inline");
                    using var shim = ValidatableViewModelExtensions.ValidationRule<Model,string>(
                        model, x => x.Name, static x => x == "ok", "shim");
                    Expression<Func<Model,string?>> expression = x => x.Name;
                    using var retained = model.ValidationRule(expression, static x => x == "ok", "retained");
                    if (inline.IsValid || shim.IsValid || retained.IsValid) return false;
                    model.Name = "ok";
                    if (!inline.IsValid || !shim.IsValid || !retained.IsValid) return false;
                    model.Name = null;
                    return !inline.IsValid && !shim.IsValid && !retained.IsValid;
                }
            }
            """;
    }
}
