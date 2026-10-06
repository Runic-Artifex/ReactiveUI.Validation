// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Preserves exact base dispatch when a constructed root contains file-local types.</summary>
public sealed class FuncRootTransportCompilerTests
{
    /// <summary>The actual original and authored-descriptor caller, differing only at the registration site.</summary>
    private const string Fixture = """
        using System;
        using System.Linq;
        using __UI__;
        using __UI__.Builder;
        using __ROOT__.Abstractions;
        using __ROOT__.Capabilities;
        using __ROOT__.Contexts;
        using __ROOT__.Extensions;
        using __ROOT__.States;
        public class Base<T> : ReactiveObject, IValidatableViewModel
        {
            private string? _name;
            public int Gets;
            public string? Name { get { Gets++; return _name; } set { _name=value; this.RaisePropertyChanged(nameof(Name)); } }
            public IValidationContext ValidationContext { get; } = new ValidationContext();
        }
        public sealed partial class Derived<T> : Base<T>
        {
            public new string? Name { get => throw new Exception("Wrong shadow getter"); set => throw new Exception("Wrong shadow setter"); }
        }
        file sealed class Token { }
        public static class Fixture
        {
            public static bool Check()
            {
                RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                var model = new Derived<Token[]>();
                Func<Base<Token[]>,string?> selector = value => value.Name;
                __REGISTER__
                if (rule.IsValid || model.Gets!=1) return false;
                ((Base<Token[]>)model).Name="ok";
                if (!rule.IsValid || model.Gets!=2) return false;
                ((Base<Token[]>)model).Name=null;
                if (rule.IsValid || model.Gets!=3) return false;
                rule.Dispose();
                ((Base<Token[]>)model).Name="after";
                var clean=model.Gets==3 && !model.ValidationContext.Validations.Items.Any();
                model.ValidationContext.Dispose();
                return clean;
            }
        }
        """;

    /// <summary>An open generic caller transports the original base constraint through the normal API slots.</summary>
    private const string OpenFixture = """
        using System;
        using System.Linq;
        using __UI__;
        using __UI__.Builder;
        using __ROOT__.Abstractions;
        using __ROOT__.Capabilities;
        using __ROOT__.Contexts;
        using __ROOT__.Extensions;
        using __ROOT__.Helpers;
        public class BaseModel : ReactiveObject, IValidatableViewModel
        {
            private string? _name;
            public int Gets;
            public int Keys;
            public int IndexGets;
            public int Index { get { Keys++; return 0; } }
            public string? this[int first,int second=7] { get { IndexGets++; if (first!=0 || second!=7) throw new Exception("Wrong key"); return _name; } }
            public string? Name { get { Gets++; return _name; } set { _name=value; this.RaisePropertyChanged(nameof(Name)); this.RaisePropertyChanged("Item[]"); } }
            public IValidationContext ValidationContext { get; } = new ValidationContext();
        }
        public sealed class DerivedModel : BaseModel
        {
            public new string? Name { get => throw new Exception("Wrong shadow getter"); set => throw new Exception("Wrong shadow setter"); }
            public new int Index => throw new Exception("Wrong shadow key");
            public new string? this[int first,int second=7] => throw new Exception("Wrong shadow indexer");
            public static bool Check()
            {
                RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                var model = new DerivedModel();
                using var rule = Factory.Attach<BaseModel,DerivedModel>(model);
                using var indexed = Factory.AttachIndexed<BaseModel,DerivedModel>(model);
                var components = model.ValidationContext.Validations.Items.Cast<IValidationPathComponent>().ToArray();
                var component = components.Single(item => item.ContainsPropertyName(nameof(BaseModel.Name),true));
                var index = components.Single(item => item.ValidationPaths[0].DisplayPath=="[Index,7]");
                if (rule.IsValid || rule.Message.ToSingleLine()!="missing" || model.Gets!=1
                    || component.ValidationPaths.Count!=1 || component.ValidationPaths[0].IsLegacy
                    || component.ValidationPaths[0].DisplayPath!=nameof(BaseModel.Name)
                    || indexed.IsValid || model.Keys!=1 || model.IndexGets!=1
                    || index.ValidationPaths.Count!=1 || index.ValidationPaths[0].IsLegacy) return false;
                ((BaseModel)model).Name="ok";
                if (!rule.IsValid || !indexed.IsValid || model.Gets!=2 || model.Keys!=2 || model.IndexGets!=2) return false;
                ((BaseModel)model).Name=null;
                if (rule.IsValid || indexed.IsValid || rule.Message.ToSingleLine()!="missing"
                    || model.Gets!=3 || model.Keys!=3 || model.IndexGets!=3) return false;
                rule.Dispose(); indexed.Dispose();
                ((BaseModel)model).Name="after";
                var clean=model.Gets==3 && model.Keys==3 && model.IndexGets==3 && !model.ValidationContext.Validations.Items.Any();
                model.ValidationContext.Dispose();
                return clean;
            }
        }
        public static class Factory
        {
            public static ValidationHelper Attach<TBase,TDerived>(TDerived model)
                where TBase:BaseModel where TDerived:TBase
            {
                Func<TBase,string?> selector = owner => owner.Name;
                return ValidatableViewModelExtensions.ValidationRule<TDerived,object>(model,selector,
                    value => value is string text && text=="ok", value => value is null ? "missing" : "invalid:"+value);
            }
            public static ValidationHelper AttachIndexed<TBase,TDerived>(TDerived model)
                where TBase:BaseModel where TDerived:TBase
            {
                Func<TBase,string?> selector = owner => owner[owner.Index];
                return ValidatableViewModelExtensions.ValidationRule<TDerived,object>(model,selector,
                    value => value is string text && text=="ok", value => value is null ? "missing" : "invalid:"+value);
            }
        }
        """;

    /// <summary>Rejects unnameable automatic variance and executes its legal typed descriptor counterpart.</summary>
    /// <param name="reactive">Whether to use the System.Reactive runtime graph.</param>
    /// <returns>The asynchronous original admission, diagnostic and exact execution assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FileLocalGenericRootsRequireExactAuthoredTransport(bool reactive)
    {
        var source = Fixture.Replace("__UI__", reactive ? "ReactiveUI.Reactive" : "ReactiveUI")
            .Replace("__ROOT__", reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation");
        var automatic = source.Replace("__REGISTER__", "using var rule = ValidatableViewModelExtensions.ValidationRule<Derived<Token[]>,string>"
            + "(model,selector,value=>value==\"ok\",\"base\");");
        using var admission = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await admission.RunAsync(automatic);
        await Assert.That(original.CompilationDiagnostics.Where(static item => item.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var rejected = await host.RunAsync(automatic);
        var failures = rejected.GeneratorDiagnostics.Where(static item => item.Id == "RUVG001").ToArray();
        await Assert.That(failures).Count().IsEqualTo(1);
        await Assert.That(failures[0].GetMessage()).Contains("typed ValidationSelector");
        await Assert.That((await rejected.GetDispatchDiagnosticsAsync()).Where(static item => item.Id == "RUVG005")).Count().IsEqualTo(1);
        var authored = source.Replace("__REGISTER__", """
            var paths = new[] { ValidationPath.Legacy(nameof(Base<Token[]>.Name)) };
            var plan = ValidationSelector.Create(model, owner => ValidationAccessPlan.Create(
                () => ValidationRead.Present(selector(owner), paths),
                new[] { ValidationDependency.PropertyChanged(() => (Base<Token[]>)owner, nameof(Base<Token[]>.Name)) },
                () => paths));
            using var rule = ValidationRuntime.RegisterRule(model, model.ValidationContext, plan, value => new ValidationState(value=="ok","base"));
            """);
        var result = await host.RunAsync(authored);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static item => item.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(nameof(Fixture), "Check")).IsTrue();
    }

    /// <summary>Preserves a formal type-parameter base member while the selected API accepts its derived type.</summary>
    /// <param name="reactive">Whether to use the System.Reactive runtime graph.</param>
    /// <returns>The asynchronous original admission, generated dispatch and exact behavior assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OpenGenericVarianceUsesOriginalConstraintMember(bool reactive)
    {
        var source = OpenFixture.Replace("__UI__", reactive ? "ReactiveUI.Reactive" : "ReactiveUI")
            .Replace("__ROOT__", reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation");
        using var admission = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await admission.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static item => item.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static item => item.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean("DerivedModel", "Check")).IsTrue();
    }
}
