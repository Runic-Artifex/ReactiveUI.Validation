// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Preserves bare static index operands without synthesizing expression-tree array creation.</summary>
public sealed class StaticSelectorOperandCompilerTests
{
    /// <summary>Runs actual static, qualified, instance and constant operands through the normal rule API.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous original admission, generated behavior and actual PE assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StaticArrayOperandsRetainOriginalSymbolsAndNormalDispatch(bool reactive)
    {
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var source = $$"""
            using System;
            using {{ui}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public static readonly int[] Tail = [1,2];
                public readonly int[] Values = [1,2];
                public const string Suffix = "Tail";
                public int Value = 2;
                public int Reads;
                public int Gets;
                public int Key { get { Reads++; return Value; } }
                public string Text { get => "literal:"; }
                public string this[int first,params int[] rest] { get { Gets++; return first + "/" + string.Join(",",rest); } }
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                public static bool Check()
                {
                    var model = new Model();
                    using var bare = model.ValidationRule(x=>x[x.Key,Tail],value=>value=="2/1,2","bare");
                    using var qualified = model.ValidationRule(x=>x[x.Key,Model.Tail],value=>value=="2/1,2","qualified");
                    using var instance = model.ValidationRule(x=>x[x.Key,x.Values],value=>value=="2/1,2","instance");
                    using var expression = model.ValidationRule((System.Linq.Expressions.Expression<Func<Model,string?>>)(x=>x[x.Key,Tail]),value=>value=="2/1,2","expression");
                    using var constant = model.ValidationRule(x=>x.Text+Suffix,value=>value=="literal:Tail","constant");
                    if (!bare.IsValid || !qualified.IsValid || !instance.IsValid || !expression.IsValid || !constant.IsValid || model.Reads!=4 || model.Gets!=4) return false;
                    model.Value=3; model.RaisePropertyChanged(nameof(Key));
                    if (bare.IsValid || qualified.IsValid || instance.IsValid || expression.IsValid || !constant.IsValid || model.Reads!=8 || model.Gets!=8) return false;
                    bare.Dispose(); qualified.Dispose(); instance.Dispose(); expression.Dispose(); constant.Dispose();
                    model.Value=4; model.RaisePropertyChanged(nameof(Key));
                    model.ValidationContext.Dispose();
                    return model.Reads==8 && model.Gets==8;
                }
            }
            """;
        using var admission = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await admission.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(string.Join("\n", result.ValidationSources.Select(static item => item.Text))).Contains("global::Model.@Tail");
        await Assert.That(result.ExecuteBoolean("Model", "Check")).IsTrue();
        await using var binary = new MemoryStream();
        await Assert.That(result.Compilation.Emit(binary).Success).IsTrue();
        binary.Position = 0;
        using var reader = new PEReader(binary);
        var metadata = reader.GetMetadataReader();
        await Assert.That(metadata.MemberReferences.Any(handle => metadata.GetString(metadata.GetMemberReference(handle).Name) == "NewArrayInit")).IsFalse();
    }
}
