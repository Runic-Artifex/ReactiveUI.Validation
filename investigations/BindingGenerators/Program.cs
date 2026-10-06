using ReactiveUI;
using ReactiveUI.SourceGenerators;
#if REACTIVE_SHIM
using ReactiveUI.Binding.Reactive;
using ReactiveUI.Reactive;
using ReactiveUI.Reactive.Builder;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
using System.Reactive.Linq;
#else
using ReactiveUI.Binding;
using ReactiveUI.Builder;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using ReactiveUI.Primitives;
#endif

namespace BindingGeneratorInvestigation;

internal static partial class Program
{
    private static readonly string[] ExpectedNames = ["", "Ada", "", "Detached"];
    private static void Main(string[] args)
    {
        var dynamicCode = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
        Console.WriteLine($"RUNTIME dynamicCodeSupported={dynamicCode}");
        Require(!args.Contains("--require-aot", StringComparer.Ordinal) || !dynamicCode, "NativeAOT runtime required");
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        using var first = new GeneratedModel();
        using var second = new GeneratedModel();
        var target = new GeneratedView { ViewModel = first };
        var observed = new List<string>();
        using var observation = first.WhenAnyValue(x => x.Name).Subscribe(observed.Add);
        var projected = new List<bool>();
        using var callback = target.BindValidationState(first, x => x.Rule, state => state.IsValid, projected.Add);
        using var generatedTarget = first.Rule.ValidationChanged.Select(x => x.IsValid).BindTo(target, x => x.IsValid);
        Require(!target.IsValid && !projected[^1], "invalid initial rule state");
        first.Name = "Ada";
        Require(target.IsValid && projected[^1], "generated Name update and target assignment");
        target.ViewModel = second;
        Require(!projected[^1], "model replacement invalid state");
        first.Name = "";
        first.Name = "Detached";
        Require(!projected[^1], "old model detached from typed callback");
        second.Name = "Grace";
        Require(projected[^1], "replacement model update");
        target.ViewModel = null;
        Require(projected[^1], "null model typed callback valid fallback");
        second.Name = "";
        Require(projected[^1], "null model detaches old source");
        Require(observed.SequenceEqual(ExpectedNames), "source-generated property observation");
#if NEGATIVE_SELECTOR
        System.Linq.Expressions.Expression<Func<GeneratedModel, string>> selector = x => x.Name;
        using var hiddenSelector = first.WhenAnyValue(selector).Subscribe(_ => { });
#endif
        RunShapes();
        Console.WriteLine("PASS generated properties; observed rule; generated BindTo; typed callback replacement/null; nested/private/generic/property selectors");
    }

    private static void RunShapes()
    {
        var owner = new PrivateNode();
        var values = new List<string?>();
        using var path = owner.WhenAnyValue(x => x.Child!.Value).Subscribe(values.Add);
        owner.Child = new PrivateNode { Value = "one" };
        var old = owner.Child;
        owner.Child = new PrivateNode { Value = "two" };
        old.Value = "detached";
        Require(values[^1] == "two", "private nested property chain replacement");
        owner.Child = null;
        var generic = new GenericNode<int>();
        var seen = new List<int>();
        using var closed = generic.WhenAnyValue(x => x.Value).Subscribe(seen.Add);
        generic.Value = 42;
        Require(seen[^1] == 42, "closed generic source");
        Require(ObserveGeneric(generic) == 42, "open generic source");
#if NEGATIVE_ANONYMOUS
        var anonymous = new { Value = "anonymous" };
        string? anonymousValue = null;
        using var anonymousObservation = anonymous.WhenAnyValue(x => x.Value).Subscribe(x => anonymousValue = x);
        Require(anonymousValue == "anonymous", "anonymous source");
#else
        const string anonymousValue = "excluded: RXUIBIND015";
#endif
        Console.WriteLine($"SHAPES private-chain=[{string.Join(',', values.Select(x => x ?? "<null>"))}] generic={seen[^1]} anonymous={anonymousValue}");
    }

    private static T? ObserveGeneric<T>(GenericNode<T> source)
    {
        T? value = default;
        using var subscription = source.WhenAnyValue(x => x.Value).Subscribe(x => value = x);
        return value;
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition) throw new InvalidOperationException(contract);
    }

    private sealed partial class PrivateNode : ReactiveObject
    {
        [Reactive] private string? _value;
        [Reactive] private PrivateNode? _child;
    }

    private sealed partial class GenericNode<T> : ReactiveObject
    {
        [Reactive] private T? _value;
    }
}

internal sealed partial class GeneratedModel : ReactiveObject, IValidatableViewModel, IDisposable
{
    [Reactive] private string _name = "";
    public IValidationContext ValidationContext { get; } = new ValidationContext();
    public ValidationHelper Rule { get; }

    public GeneratedModel()
    {
        Rule = this.ValidationRule(x => x.Name, this.WhenAnyValue(x => x.Name).Select(x => !string.IsNullOrWhiteSpace(x)), "Name required");
    }

    public void Dispose()
    {
        Rule.Dispose();
        ValidationContext.Dispose();
    }
}

internal sealed partial class GeneratedView : ReactiveObject, IViewFor<GeneratedModel>
{
    [Reactive] private GeneratedModel? _viewModel;
    [Reactive] private bool _isValid;
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (GeneratedModel?)value; }
}
