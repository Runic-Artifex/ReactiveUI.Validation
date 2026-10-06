// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Exercises exact access bridges through the normal generated binding pipeline.</summary>
public sealed class AccessBridgeIntegrationTests
{
    /// <summary>The fixed actual consumer entry type.</summary>
    private const string EntryType = "View";

    /// <summary>The fixed actual consumer entry method.</summary>
    private const string EntryMethod = "Check";

    /// <summary>The original lexical private generic owner and its normal selected target calls.</summary>
    private const string PrivateOwnerFixture = """
            using System;
            using {{ui}};
            using {{ui}}.Builder;
            using {{binding}};
            using {{root}}.Capabilities;
            using {{root}}.Extensions;
            using {{root}}.Helpers;
            public sealed class Model : ReactiveValidationObject
            {
                public string Name { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "invalid";
            }
            public sealed partial class View : ReactiveObject, IViewFor<Model>
            {
                private Panel<string>? Owner { get; set => this.RaiseAndSetIfChanged(ref field, value); } = new();
                public Model? ViewModel { get; set; }
                object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
                private sealed class Panel<T> where T : class
                {
                    [GeneratedValidationAccess] public T? Initial { get; init; }
                    [GeneratedValidationAccess] public readonly T? Storage = default;
                }
                private IDisposable BindInitial(Model model) => this.BindValidation(model, x => x.Name, x => x.Owner!.Initial);
                private IDisposable BindStorage(Model model) => this.BindValidation(model, x => x.Name, x => x.Owner!.Storage);
                private void Detach() => Owner = null;
                private void Attach() => Owner = new();
                public static bool Check()
                {
                    RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
                    using var model = new Model();
                    using var rule = model.ValidationRule(x => x.Name, static value => value?.Contains('@') == true, "required");
                    var view = new View { ViewModel = model };
                    using var initial = view.BindInitial(model);
                    using var storage = view.BindStorage(model);
                    var first = view.Owner ?? throw new InvalidOperationException("The initial owner is missing.");
                    if (first.Initial != "required" || first.Storage != "required") return false;
                    model.Name = "owner@example.test";
                    if (first.Initial is not { Length: 0 } || first.Storage is not { Length: 0 }) return false;
                    view.Detach();
                    model.Name = "invalid";
                    view.Attach();
                    var second = view.Owner ?? throw new InvalidOperationException("The replacement owner is missing.");
                    if (second.Initial != "required" || second.Storage != "required"
                        || first.Initial is not { Length: 0 } || first.Storage is not { Length: 0 }) return false;
                    initial.Dispose(); storage.Dispose();
                    model.Name = "disposed@example.test";
                    view.Detach(); view.Attach();
                    return view.Owner?.Initial is null && view.Owner?.Storage is null
                        && second.Initial == "required" && second.Storage == "required";
                }
            }
            """;

    /// <summary>The explicit immutable replacement route when a nonpartial owner cannot host an intrinsic.</summary>
    private const string NonpartialFactoryFixture = """
        using System;
        using {{root}}.Capabilities;
        public sealed class FactoryHost
        {
            private sealed class Storage<T> where T : class
            {
                public T? Initial { get; init; }
                public readonly T? Value;
                public readonly int Marker;
                public Storage(T? initial, T? value, int marker) { Initial = initial; Value = value; Marker = marker; }
            }
            private sealed class Values : IObservable<string>
            {
                private IObserver<string>? _observer;
                public IDisposable Subscribe(IObserver<string> observer)
                {
                    _observer = observer;
                    observer.OnNext("first");
                    return new Lease(() => _observer = null);
                }
                public void Publish(string value) => _observer?.OnNext(value);
                private sealed class Lease(Action dispose) : IDisposable { public void Dispose() => dispose(); }
            }
            public static bool Check()
            {
                var cell = new ValidationCell<Storage<string>?>(new(null, null, 7));
                var lens = new ValidationLens<Storage<string>?, string>(
                    static storage => storage is null ? ValidationRead<string>.Missing([]) : ValidationRead<string>.Present(storage.Initial ?? "", []),
                    static (storage, value) => storage is null ? null : new Storage<string>(value, value, storage.Marker));
                var values = new Values();
                using var binding = lens.Target().Bind(cell).Bind(values);
                var first = cell.Value;
                if (first?.Initial != "first" || first.Value != "first" || first.Marker != 7) return false;
                cell.Value = null;
                values.Publish("cached");
                cell.Value = new(null, null, 11);
                var replayed = cell.Value;
                if (replayed?.Initial != "cached" || replayed.Value != "cached" || replayed.Marker != 11 || first.Initial != "first") return false;
                values.Publish("updated");
                if (cell.Value?.Initial != "updated" || cell.Value.Value != "updated" || cell.Value.Marker != 11 || replayed.Initial != "cached") return false;
                binding.Dispose();
                values.Publish("disposed");
                cell.Value = new(null, null, 13);
                return cell.Value.Initial is null && cell.Value.Value is null && cell.Value.Marker == 13;
            }
        }
        """;

    /// <summary>Transports inaccessible generic owners without leaking their types into global generated bodies.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and actual runtime assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PrivateGenericOwnersRetainExactInitAndReadonlyReplay(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var ui = reactive ? "ReactiveUI.Reactive" : "ReactiveUI";
        var binding = reactive ? "ReactiveUI.Binding.Reactive" : "ReactiveUI.Binding";
        var source = PrivateOwnerFixture.Replace("{{ui}}", ui, StringComparison.Ordinal).Replace("{{root}}", root, StringComparison.Ordinal)
            .Replace("{{binding}}", binding, StringComparison.Ordinal);
        var result = await host.RunAsync(source);
        var original = result.Compilation.RemoveSyntaxTrees(result.Compilation.SyntaxTrees.Where(static tree => tree.FilePath != "CapabilityCaller.cs"));
        await Assert.That(string.Join("\n", original.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))).IsEmpty();
        await Assert.That(string.Join("\n", result.CompilationDiagnostics
            .Where(static diagnostic => diagnostic.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))).IsEmpty();
        await Assert.That(string.Join("\n", result.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ValidationSources.Any(static file => file.HintName == "ValidationInterceptors.g.cs"
            && file.Text.Contains("View.Panel<", StringComparison.Ordinal))).IsFalse();
        await Assert.That(result.ValidationSources.Any(static file => file.Text.Contains("UnsafeAccessorType", StringComparison.Ordinal)
            && file.Text.Contains("View+Panel`1", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Executes a legal typed replacement alternative for a private generic type in a nonpartial owner.</summary>
    /// <param name="reactive">Whether to select the System.Reactive flavor.</param>
    /// <returns>The asynchronous original compiler and runtime lifecycle assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NonpartialPrivateGenericStorageUsesTypedReplacementFactory(bool reactive)
    {
        using var host = CapabilityCompilerHost.Create(reactive);
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var result = await host.RunAsync(NonpartialFactoryFixture.Replace("{{root}}", root, StringComparison.Ordinal));
        await Assert.That(string.Join("\n", result.CompilationDiagnostics
            .Where(static diagnostic => diagnostic.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning))).IsEmpty();
        await Assert.That(string.Join("\n", result.GeneratorDiagnostics)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ValidationSources.Any(static file => file.Text.Contains("UnsafeAccessor", StringComparison.Ordinal))).IsFalse();
        await Assert.That(result.ExecuteBoolean("FactoryHost", EntryMethod)).IsTrue();
    }
}
