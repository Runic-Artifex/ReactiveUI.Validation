// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Preserves final dispatch errors for lookalike runtime policy attributes.</summary>
public sealed class RuntimeDispatchPolicyCompilerTests
{
    /// <summary>Rejects a caller-owned short-name lookalike in every lexical scope.</summary>
    /// <param name="reactive">Whether to use System.Reactive.</param>
    /// <param name="scope">The scope where a lookalike attribute is placed.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false, "assembly")]
    [Arguments(false, "type")]
    [Arguments(false, "method")]
    [Arguments(true, "assembly")]
    [Arguments(true, "type")]
    [Arguments(true, "method")]
    public async Task LookalikePolicyCannotWaiveUnregisteredCalls(bool reactive, string scope)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var assemblyAttribute = scope == "assembly" ? "[assembly: Fake.ValidationRuntimeDispatch]" : string.Empty;
        var typeAttribute = scope == "type" ? "[Fake.ValidationRuntimeDispatch]" : string.Empty;
        var methodAttribute = scope == "method" ? "[Fake.ValidationRuntimeDispatch]" : string.Empty;
        var source = $$"""
            using System;
            using System.Linq.Expressions;
            using {{ui}};
            using {{root}}.Abstractions;
            using {{root}}.Contexts;
            using {{root}}.Extensions;
            using {{root}}.Helpers;
            {{assemblyAttribute}}
            namespace Fake
            {
                [AttributeUsage(AttributeTargets.All)]
                public sealed class ValidationRuntimeDispatchAttribute : Attribute { }
            }
            {{typeAttribute}}
            public sealed class Model : ReactiveObject, IValidatableViewModel
            {
                public string Name { get; set; } = string.Empty;
                public IValidationContext ValidationContext { get; } = new ValidationContext();
                {{methodAttribute}}
                public void Probe(Expression<Func<Model, string>> selector)
                {
                    using var rule = this.ValidationRule(selector, x => x.Length > 0, "bad");
                    Func<Model, Expression<Func<Model, string>>, Func<string, bool>, string, ValidationHelper> indirect =
                        ValidatableViewModelExtensions.ValidationRule<Model, string>;
                    GC.KeepAlive(indirect);
                }
            }
            """;
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == "RUVG001" && diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        var dispatch = await result.GetDispatchDiagnosticsAsync();
        await Assert.That(dispatch.Any(static diagnostic => diagnostic.Id == "RUVG005" && diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(dispatch.Any(static diagnostic => diagnostic.Id == "RUVG007" && diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
    }
}
