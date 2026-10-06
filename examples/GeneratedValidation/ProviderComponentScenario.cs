// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Validation.Reactive.Capabilities;
#else
using ReactiveUI.Validation.Capabilities;
#endif

internal static class ProviderComponentScenario
{
    [ValidationRuntimeDispatch]
    internal static void Run()
    {
        var manual = new ManualSource();
        var selector = new ValidationSelector<ManualSource, string?>(source => new(
            () => ValidationRead<string?>.Present(source.Value, [ValidationPath.Legacy(nameof(ManualSource.Value))]),
            [ValidationDependency.Create(() => source, static (owner, observer) => owner.Subscribe(observer))],
            ValidationObservationOptions<string?>.Default));
        using var plain = new BasePropertyValidation<ManualSource, string>(manual, selector, static value => value == "ok", "manual-required");
        using var dynamic = new BasePropertyValidation<ManualSource, string>(manual, selector, static value => value == "ok", static value => "manual:" + value);
        using var complete = new BasePropertyValidation<ManualSource, string>(manual, selector, static value => value == "ok", static (value, valid) => valid ? "accepted" : "seen:" + value);
        using var context = new ValidationContext();
        using var registered = ValidationRuntime.RegisterRule(manual, context, selector, static value => new ValidationState(value == "ok", "typed-rule"));
        Require(!plain.IsValid && !dynamic.IsValid && !complete.IsValid && !registered.IsValid && manual.Listeners == 4,
            "All typed component constructors and the rule facade must read the real initial null leaf.");
        Require(plain.Text!.ToSingleLine() == "manual-required" && dynamic.Text!.ToSingleLine() == "manual:" && complete.Text!.ToSingleLine() == "seen:",
            "Static, dynamic and full-message constructors must preserve their original message contracts.");
        manual.Value = "ok";
        manual.Invalidate();
        Require(plain.IsValid && dynamic.IsValid && complete.IsValid && registered.IsValid,
            "A non-INPC source must update through its declared notification provider.");
        Require(complete.Text!.ToSingleLine() == "accepted", "A complete message factory retains valid-state text.");
        plain.Dispose();
        dynamic.Dispose();
        complete.Dispose();
        registered.Dispose();
        Require(manual.Listeners == 0 && context.Validations.Count == 0, "Component disposal releases registrations and the original context membership.");
        manual.Value = "borrowed";
        manual.Invalidate();

        using var provider = new ProviderModel();
        using var fallback = new ValidationPlanRegistry(2);
        using var attachment = fallback.Attach(provider);
        using var fallbackEntry = fallback.RegisterSelectorPattern(ValidationPlanRole.RuleValue, "unused-fallback",
            ValidationExpressionPattern.Create<ProviderModel, string?>(source => source.Value),
            static _ => throw new InvalidOperationException("Source provider precedence was bypassed."));
        Expression<Func<ProviderModel, string?>> expression = source => source.Value;
        using var providerRule = provider.ValidationRule(expression, static value => value == "ok", "provider-required");
        Require(provider.SelectorRequests == 1 && !providerRule.IsValid, "The exact typed source provider must precede an attached fallback catalog.");
        provider.Value = "ok";
        Require(providerRule.IsValid, "The provider's typed descriptor must observe its current source.");
        providerRule.Dispose();
        provider.Value = null;
        Func<ProviderModel, string?> selection = ProviderModel.SelectValue;
        using var fallbackDelegateEntry = fallback.RegisterDelegateSelectorPattern(ValidationPlanRole.RuleValue, "unused-delegate-fallback",
            ValidationDelegatePattern.Create(selection),
            static _ => throw new InvalidOperationException("Delegate source provider precedence was bypassed."));
        using var delegateRule = provider.ValidationRule(selection, static value => value == "ok", "delegate-provider-required");
        Require(provider.DelegateSelectorRequests == 1 && provider.MetadataReads == 0 && !delegateRule.IsValid,
            "The exact typed delegate provider precedes the attached fallback without invoking metadata selectors.");
        provider.Value = "ok";
        Require(delegateRule.IsValid && provider.MetadataReads == 0,
            "The delegate provider descriptor observes current source values without executing its metadata delegate.");
        delegateRule.Dispose();
        Require(provider.ValidationContext.Validations.Count == 0,
            "Expression and delegate provider helper disposal release their original context membership.");
        provider.Value = "after-dispose";
        Require(delegateRule.IsValid && provider.MetadataReads == 0,
            "Disposed delegate provider helpers remain frozen while the borrowed source stays usable.");

        var first = new Customer();
        var second = new Customer();
        var host = new Host { Model = first };
        using var adapter = new ValidationViewAdapter<Host, Customer>(host, static owner => owner.Model,
            static (owner, model) => owner.Set(model), static (owner, changed) => owner.Register(changed));
        Require(ReferenceEquals(adapter.ViewModel, first) && host.Registrations == 1, "A typed host adapter owns only its provider registration.");
        host.Set(second);
        Require(ReferenceEquals(adapter.ViewModel, second), "A provider replacement must follow the current host storage.");
        adapter.ViewModel = null;
        Require(host.Model is null, "Typed adapter writes must reach the real host.");
        adapter.Dispose();
        Require(host.Registrations == 0, "Adapter disposal releases the explicit host registration.");
        first.Dispose();
        second.Dispose();
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }

    private sealed class ManualSource
    {
        private readonly List<IObserver<ValidationInvalidation>> _observers = [];
        internal string? Value { get; set; }
        internal int Listeners => _observers.Count;
        internal Cleanup Subscribe(IObserver<ValidationInvalidation> observer)
        {
            _observers.Add(observer);
            return new Cleanup(() => _observers.Remove(observer));
        }
        internal void Invalidate()
        {
            foreach (var observer in _observers.ToArray()) observer.OnNext(default);
        }
    }

    private sealed class Host
    {
        private Action? _changed;
        internal Customer? Model { get; set; }
        internal int Registrations { get; private set; }
        internal void Set(Customer? model) { Model = model; _changed?.Invoke(); }
        internal Cleanup Register(Action changed)
        {
            _changed += changed;
            Registrations++;
            changed();
            return new Cleanup(() => { _changed -= changed; Registrations--; });
        }
    }

    private sealed class ProviderModel : ReactiveObject, IValidatableViewModel, IValidationPlanProvider, IValidationDelegatePlanProvider, IDisposable
    {
        private readonly ValidationPlanRegistry _catalog = new(2);
        private readonly IDisposable _delegateRegistration;
        private readonly IDisposable _registration;
        internal ProviderModel()
        {
            _registration = _catalog.RegisterSelectorPattern(ValidationPlanRole.RuleValue, "provider.value",
                ValidationExpressionPattern.Create<ProviderModel, string?>(source => source.Value),
                static _ => new ValidationSelector<ProviderModel, string?>(source => new(
                    () => ValidationRead<string?>.Present(source.Value, [ValidationPath.Legacy(nameof(Value))]),
                    [ValidationDependency.PropertyChanged(() => source, nameof(Value))], ValidationObservationOptions<string?>.Default)));
            _delegateRegistration = _catalog.RegisterDelegateSelector<ProviderModel, string?>(ValidationPlanRole.RuleValue, SelectValue,
                new ValidationSelector<ProviderModel, string?>(source => new(
                    () => ValidationRead<string?>.Present(source.Value, [ValidationPath.Legacy(nameof(Value))]),
                    [ValidationDependency.PropertyChanged(() => source, nameof(Value))], ValidationObservationOptions<string?>.Default)));
        }
        public string? Value { get; set => this.RaiseAndSetIfChanged(ref field, value); }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        internal int SelectorRequests { get; private set; }
        internal int DelegateSelectorRequests { get; private set; }
        internal int MetadataReads { get; private set; }
        internal static string? SelectValue(ProviderModel source)
        {
            source.MetadataReads++;
            return source.Value;
        }
        public bool TryGetSelector<TSource, TValue>(ValidationPlanRequest request, Expression<Func<TSource, TValue>>? expression,
            [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector)
        {
            SelectorRequests++;
            return _catalog.TryGetSelector(request, expression, out selector);
        }
        public bool TryGetTarget<TSource, TValue, TOut>(ValidationPlanRequest request, Expression<Func<TSource, TValue>>? expression,
            [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target)
        {
            target = null;
            return false;
        }
        public bool TryGetDelegateSelector<TSource, TValue>(ValidationPlanRequest request, Func<TSource, TValue>? selection,
            [NotNullWhen(true)] out ValidationSelector<TSource, TValue>? selector)
        {
            DelegateSelectorRequests++;
            return _catalog.TryGetDelegateSelector(request, selection, out selector);
        }
        public bool TryGetDelegateTarget<TSource, TValue, TOut>(ValidationPlanRequest request, Func<TSource, TValue>? selection,
            [NotNullWhen(true)] out ValidationTarget<TSource, TOut>? target)
        {
            target = null;
            return false;
        }
        public void Dispose() { _registration.Dispose(); _delegateRegistration.Dispose(); _catalog.Dispose(); ValidationContext.Dispose(); }
    }
}
