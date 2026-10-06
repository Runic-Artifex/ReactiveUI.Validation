using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Reactive.Builder;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Collections;
using ReactiveUI.Validation.Reactive.Components;
using ReactiveUI.Validation.Reactive.Components.Abstractions;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
using ReactiveUI.Validation.Reactive.States;
#else
using ReactiveUI;
using ReactiveUI.Builder;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Collections;
using ReactiveUI.Validation.Components;
using ReactiveUI.Validation.Components.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using ReactiveUI.Validation.States;
#endif

internal static class Program
{
    private static int Main(string[] args)
    {
        bool requireAot = args.Contains("--require-aot");
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
        if (requireAot && RuntimeFeature.IsDynamicCodeSupported)
        {
            Console.WriteLine("{\"scenario\":\"runtime\",\"passed\":false,\"error\":\"dynamic code enabled\"}");
            return 1;
        }
        Console.WriteLine($"{{\"scenario\":\"runtime\",\"passed\":true,\"dynamicCodeSupported\":{RuntimeFeature.IsDynamicCodeSupported.ToString().ToLowerInvariant()}}}");
        int failures = 0;
        foreach (var scenario in new (string Name, Action Run)[]
        {
            ("custom-context", CustomContext),
            ("observable-component", ObservableComponent),
            ("observable-rule-helper", ObservableRule),
            ("expression-rule-errors", ExpressionRule),
            ("legacy-binding", LegacyBinding),
            ("typed-helper-binding", TypedBinding),
            ("selected-context-binding", ContextBinding),
            ("direct-observables-static-setter", DirectBinding),
            ("legacy-unrooted-target-setter", LegacyUnrootedTarget),
            ("typed-unrooted-target-setter", TypedUnrootedTarget),
        })
        {
            try
            {
                scenario.Run();
                Console.WriteLine($"{{\"scenario\":\"{scenario.Name}\",\"passed\":true}}");
            }
            catch (Exception ex)
            {
                failures++;
                // Avoid reflection-based JSON serialization in a NativeAOT probe.
                Console.WriteLine($"{{\"scenario\":\"{scenario.Name}\",\"passed\":false,\"exception\":\"{ex.GetType().Name}\"}}");
                Console.Error.WriteLine($"{scenario.Name}: {ex}");
            }
        }
        return failures == 0 ? 0 : 1;
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CustomContext()
    {
        using var context = new ValidationContext();
        var rule = new Component(new ValidationState(false, "first"));
        var states = new List<IValidationState>();
        using var subscription = context.ValidationStatusChange.Subscribe(new Observer<IValidationState>(states.Add));
        Check(states[^1].IsValid, "empty initial context");
        context.Add(rule);
        Check(!context.GetIsValid() && !context.IsValid && context.Text.ToSingleLine().Contains("first"), "invalid membership");
        rule.Set(new ValidationState(false, "second"));
        Check(states[^1].Text.ToSingleLine().Contains("second"), "message-only change");
        rule.Set(ValidationState.Valid);
        Check(context.GetIsValid() && context.IsValid, "valid transition");
        context.Remove(rule);
        int count = states.Count;
        rule.Set(new ValidationState(false, "removed"));
        Check(states.Count == count && context.GetIsValid(), "removed source detached");
    }

    private static void ObservableComponent()
    {
        using var context = new ValidationContext();
        var source = new Replay<IValidationState>(new ValidationState(false, "observable"));
        using var component = new ObservableValidation<Model, bool>(source);
        context.Add(component);
        Check(!context.GetIsValid(), "observable initial invalid");
        source.Set(ValidationState.Valid);
        Check(context.GetIsValid(), "observable valid");
        context.Remove(component);
    }

    private static void ObservableRule()
    {
        using var model = new Model();
        var validity = new Replay<bool>(false);
        using var helper = model.ValidationRule(validity, "rule");
        Check(!helper.IsValid && !model.ValidationContext.GetIsValid(), "rule initial invalid");
        validity.Set(true);
        Check(helper.IsValid && model.ValidationContext.GetIsValid(), "rule valid");
        helper.Dispose();
        Check(model.ValidationContext.Validations.Count == 0, "rule removed on helper disposal");
    }

    private static void ExpressionRule()
    {
        using var model = new Model();
        using var helper = model.ValidationRule(vm => vm.Name, static value => value == "ok", "name");
        Check(!helper.IsValid && model.HasErrors, "initial errors");
        Check(model.GetErrors(nameof(Model.Name)).Cast<string>().Single() == "name", "property errors");
        model.Name = "ok";
        Check(helper.IsValid && !model.HasErrors, "error cleared");
        model.Name = "bad";
        Check(!helper.IsValid && model.HasErrors, "error restored");
    }

    private static void LegacyBinding() => ExerciseBinding((view, model) => view.BindValidation(model, vm => vm!.Rule, v => v.Message));
    private static void TypedBinding() => ExerciseBinding((view, model) => view.BindValidationState(model, vm => vm.Rule, v => v.Valid, static state => state.IsValid));

    private static void ExerciseBinding(Func<View, Model, IDisposable> bind)
    {
        using var first = new Model();
        using var second = new Model();
        var oldSource = new Replay<IValidationState>(new ValidationState(false, "old"));
        var newSource = new Replay<IValidationState>(ValidationState.Valid);
        using var oldComponent = new ObservableValidation<Model, bool>(oldSource);
        using var newComponent = new ObservableValidation<Model, bool>(newSource);
        using var oldHelper = new ValidationHelper(oldComponent);
        using var newHelper = new ValidationHelper(newComponent);
        first.Rule = oldHelper;
        second.Rule = newHelper;
        var view = new View { ViewModel = first };
        using var binding = bind(view, first);
        Check(view.Message == "old" || !view.Valid, "binding initial");
        view.ViewModel = second;
        Check(view.Message.Length == 0 && view.Valid, "replacement valid");
        int count = view.Assignments;
        oldSource.Set(new ValidationState(false, "stale"));
        Check(view.Assignments == count, "old model detached");
        newSource.Set(new ValidationState(false, "new"));
        Check(view.Message == "new" || !view.Valid, "new model update");
        second.Rule = null;
        Check(view.Message.Length == 0 && view.Valid, "null helper clears");
        count = view.Assignments;
        newSource.Set(ValidationState.Valid);
        Check(view.Assignments == count, "null helper detaches source");
        var replacementSource = new Replay<IValidationState>(new ValidationState(false, "replacement"));
        using var replacementComponent = new ObservableValidation<Model, bool>(replacementSource);
        using var replacementHelper = new ValidationHelper(replacementComponent);
        second.Rule = replacementHelper;
        Check(view.Message == "replacement" || !view.Valid, "same model helper replacement");
        count = view.Assignments;
        newSource.Set(new ValidationState(false, "stale helper"));
        Check(view.Assignments == count, "old helper detached after replacement");
        replacementSource.Set(ValidationState.Valid);
        Check(view.Message.Length == 0 && view.Valid, "replacement helper update");
        view.ViewModel = null;
        Check(view.Message.Length == 0 && view.Valid, "null model clears");
        view.ViewModel = second;
        binding.Dispose();
        binding.Dispose();
        count = view.Assignments;
        replacementSource.Set(new ValidationState(false, "disposed"));
        view.ViewModel = first;
        Check(view.Assignments == count, "disposed binding detached");
    }

    private static void ContextBinding()
    {
        using var first = new Model();
        using var second = new Model();
        using var extra = new ValidationContext();
        var component = new Component(new ValidationState(false, "context"));
        first.Selected = extra;
        extra.Add(component);
        var view = new View { ViewModel = first };
        var states = new List<IValidationState>();
        using var binding = view.BindValidationContext(first, vm => vm.Selected, states.Add);
        Check(!states[^1].IsValid, "selected initial");
        first.Selected = second.ValidationContext;
        Check(states[^1].IsValid, "selected replacement");
        int count = states.Count;
        component.Set(new ValidationState(false, "stale"));
        Check(count == states.Count, "old context detached");
        first.Selected = null;
        Check(states[^1].IsValid, "null selected context");
        second.Selected = extra;
        view.ViewModel = second;
        Check(!states[^1].IsValid, "selected context model replacement");
        count = states.Count;
        first.Selected = extra;
        Check(states.Count == count, "old model selection detached");
        view.ViewModel = null;
        Check(states[^1].IsValid, "null model selected context clears");
        count = states.Count;
        component.Set(ValidationState.Valid);
        Check(states.Count == count, "null model selected source detached");
        binding.Dispose();
        count = states.Count;
        first.Selected = extra;
        view.ViewModel = first;
        Check(count == states.Count, "disposed selected binding");
    }

    private static void DirectBinding()
    {
        // Explicit observable boundary and static setter: equivalent shape to generator output,
        // deliberately bypasses expression observation and reflection assignment.
        using var context = new ValidationContext();
        var component = new Component(new ValidationState(false, "direct"));
        context.Add(component);
        var view = new View();
        using var binding = context.ValidationStatusChange.Subscribe(new Observer<IValidationState>(state => SetValid(view, state)));
        Check(!view.Valid, "direct initial");
        component.Set(ValidationState.Valid);
        Check(view.Valid, "direct valid");
        binding.Dispose();
        int count = view.Assignments;
        component.Set(new ValidationState(false, "after"));
        Check(view.Assignments == count, "direct disposal");
    }

    private static void LegacyUnrootedTarget()
    {
        using var model = new Model();
        var source = new Replay<IValidationState>(new ValidationState(false, "unrooted"));
        using var component = new ObservableValidation<Model, bool>(source);
        using var helper = new ValidationHelper(component);
        model.Rule = helper;
        var view = new UnrootedView(model);
        using var binding = view.BindValidation(model, vm => vm!.Rule, v => v.Error);
        Check(view.Error == "unrooted" && view.ErrorWrites > 0, "legacy reflection-only setter initial");
        source.Set(ValidationState.Valid);
        Check(view.Error.Length == 0 && view.ErrorWrites > 1, "legacy reflection-only setter update");
    }

    private static void TypedUnrootedTarget()
    {
        using var model = new Model();
        var source = new Replay<IValidationState>(new ValidationState(false, "unrooted"));
        using var component = new ObservableValidation<Model, bool>(source);
        using var helper = new ValidationHelper(component);
        model.Rule = helper;
        var view = new UnrootedView(model);
        using var binding = view.BindValidationState(model, vm => vm.Rule, v => v.Status, static state => state.IsValid);
        Check(!view.Status && view.StatusWrites > 0, "typed reflection-only setter initial");
        source.Set(ValidationState.Valid);
        Check(view.Status && view.StatusWrites > 1, "typed reflection-only setter update");
    }

    private static void SetValid(View view, IValidationState state) => view.Valid = state.IsValid;
}

internal sealed class Model : ReactiveValidationObject
{
    public string Name { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "bad";
    public ValidationHelper? Rule { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public IValidationContext? Selected { get; set => this.RaiseAndSetIfChanged(ref field, value); }
}

internal sealed class View : ReactiveObject, IViewFor<Model>
{
    public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
    public int Assignments { get; private set; }
    public string Message { get; set { field = value; Assignments++; } } = "";
    public bool Valid { get; set { field = value; Assignments++; } } = true;
}

internal sealed class Component(IValidationState initial) : IValidationComponent
{
    private readonly Replay<IValidationState> _source = new(initial);
    public IValidationText Text => _source.Current.Text;
    public bool IsValid => _source.Current.IsValid;
    public IObservable<IValidationState> ValidationStatusChange => _source;
    public void Set(IValidationState state) => _source.Set(state);
}

internal sealed class Replay<T>(T initial) : IObservable<T>
{
    private readonly List<IObserver<T>> _observers = [];
    public T Current { get; private set; } = initial;
    public IDisposable Subscribe(IObserver<T> observer)
    {
        _observers.Add(observer);
        observer.OnNext(Current);
        return new Cleanup(() => _observers.Remove(observer));
    }
    public void Set(T value)
    {
        Current = value;
        foreach (var observer in _observers.ToArray()) observer.OnNext(value);
    }
}
internal sealed class Observer<T>(Action<T> next) : IObserver<T>
{
    public void OnNext(T value) => next(value);
    public void OnError(Exception error) => throw error;
    public void OnCompleted() { }
}
internal sealed class Cleanup(Action cleanup) : IDisposable
{
    private Action? _cleanup = cleanup;
    public void Dispose() => Interlocked.Exchange(ref _cleanup, null)?.Invoke();
}

// Error/Status setters are referenced only by library reflection. Constructors
// initialize backing fields and all assertions read getters; no static writes,
// linker roots, DynamicDependency, or DynamicallyAccessedMembers annotations.
internal sealed class UnrootedView(Model model) : ReactiveObject, IViewFor<Model>
{
    private string _error = "sentinel";
    private bool _status = true;
    public Model? ViewModel { get; set => this.RaiseAndSetIfChanged(ref field, value); } = model;
    object? IViewFor.ViewModel { get => ViewModel; set => ViewModel = (Model?)value; }
    public int ErrorWrites { get; private set; }
    public int StatusWrites { get; private set; }
    public string Error { get => _error; set { _error = value; ErrorWrites++; } }
    public bool Status { get => _status; set { _status = value; StatusWrites++; } }
}
