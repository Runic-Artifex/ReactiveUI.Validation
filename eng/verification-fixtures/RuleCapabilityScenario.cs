// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
#if REACTIVE_SHIM
using ReactiveUI.Reactive;
using ReactiveUI.Validation.Reactive.Abstractions;
using ReactiveUI.Validation.Reactive.Capabilities;
using ReactiveUI.Validation.Reactive.Collections;
using ReactiveUI.Validation.Reactive.Components.Abstractions;
using ReactiveUI.Validation.Reactive.Contexts;
using ReactiveUI.Validation.Reactive.Extensions;
using ReactiveUI.Validation.Reactive.Helpers;
using ReactiveUI.Validation.Reactive.States;
#else
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Capabilities;
using ReactiveUI.Validation.Collections;
using ReactiveUI.Validation.Components.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using ReactiveUI.Validation.States;
#endif

/// <summary>Shared bounded rule capability corpus for actual-package acceptance.</summary>
internal static class RuleCapabilityScenario
{
    /// <summary>Runs the same positive and controlled-failure rule contracts in either flavor.</summary>
    internal static void Run()
    {
        if (!RuleCapabilityCases.DeclaredNotifier.Model.Check()) throw new InvalidOperationException("Rule capability DeclaredNotifier failed.");
        if (!RuleCapabilityCases.GenericInterface.Fixture.Check()) throw new InvalidOperationException("Rule capability GenericInterface failed.");
        if (!RuleCapabilityCases.GenericBase.Fixture.Check()) throw new InvalidOperationException("Rule capability GenericBase failed.");
        if (!RuleCapabilityCases.CheckedFailure.Model.Check()) throw new InvalidOperationException("Rule capability CheckedFailure failed.");
        if (!RuleCapabilityCases.GenericCallable.Model.Check()) throw new InvalidOperationException("Rule capability GenericCallable failed.");
        if (!RuleCapabilityCases.TypedContext.Model.Check()) throw new InvalidOperationException("Rule capability TypedContext failed.");
        if (!RuleCapabilityCases.DynamicIndex.Model.Check()) throw new InvalidOperationException("Rule capability DynamicIndex failed.");
        if (!RuleCapabilityCases.LegacySeed.Model.Check()) throw new InvalidOperationException("Rule capability LegacySeed failed.");
        if (!RuleCapabilityCases.AnonymousRichState.Fixture.Check()) throw new InvalidOperationException("Rule capability AnonymousRichState failed.");
        if (!RuleCapabilityCases.InterfaceParents.Model.Check()) throw new InvalidOperationException("Rule capability InterfaceParents failed.");
        if (!RuleCapabilityCases.MissingDefaultValue.Model.Check()) throw new InvalidOperationException("Rule capability MissingDefaultValue failed.");
        if (!RuleCapabilityCases.MissingSuppress.Model.Check()) throw new InvalidOperationException("Rule capability MissingSuppress failed.");
        if (!RuleCapabilityCases.MissingFallback.Model.Check()) throw new InvalidOperationException("Rule capability MissingFallback failed.");
        if (!RuleCapabilityCases.PrivateCapture.Fixture.Check()) throw new InvalidOperationException("Rule capability PrivateCapture failed.");
        if (!RuleCapabilityCases.PrivateGenericClass.Fixture.Check()) throw new InvalidOperationException("Rule capability PrivateGenericClass failed.");
        if (!RuleCapabilityCases.PrivateGenericStruct.Fixture.Check()) throw new InvalidOperationException("Rule capability PrivateGenericStruct failed.");
        if (!RuleCapabilityCases.ComputedInline.Model.Check()) throw new InvalidOperationException("Rule capability ComputedInline failed.");
        if (!RuleCapabilityCases.ComputedStored.Model.Check()) throw new InvalidOperationException("Rule capability ComputedStored failed.");
        if (!RuleCapabilityCases.PredicateContexts.Model.Check()) throw new InvalidOperationException("Rule capability PredicateContexts failed.");
        if (!RuleCapabilityCases.IndexedBoundary.Model.Check()) throw new InvalidOperationException("Rule capability IndexedBoundary failed.");
        if (!RuleCapabilityCases.ProtectedTarget.Model.Check()) throw new InvalidOperationException("Rule capability ProtectedTarget failed.");
        if (!RuleCapabilityCases.GenericProvenance.Fixture.Check()) throw new InvalidOperationException("Rule capability GenericProvenance failed.");
        if (!RuleCapabilityCases.RuntimeProvenance.Model.Check()) throw new InvalidOperationException("Rule capability RuntimeProvenance failed.");
        if (!RuleCapabilityCases.StableProvenance.Model.Check()) throw new InvalidOperationException("Rule capability StableProvenance failed.");
        if (!RuleCapabilityCases.StaticObject.Model.Check()) throw new InvalidOperationException("Rule capability StaticObject failed.");
        if (!RuleCapabilityCases.CustomOr.Model.Check()) throw new InvalidOperationException("Rule capability CustomOr failed.");
        if (!RuleCapabilityCases.CustomAnd.Model.Check()) throw new InvalidOperationException("Rule capability CustomAnd failed.");
    }
}

/// <summary>Reads callback-mutated observations at assertion time.</summary>
internal sealed class RuleCapabilityObserved<T>(T value)
{
    private readonly object _gate = new();
    private T _value = value;
    internal T Value { get { lock (_gate) { return _value; } } set { lock (_gate) { _value = value; } } }
}

/// <summary>Compares current notification snapshots rather than an earlier SDK flow-analysis value.</summary>
internal static class RuleCapabilityAssertions
{
    internal static bool Equal<T>(T actual,T expected) => EqualityComparer<T>.Default.Equals(actual,expected);
}

namespace RuleCapabilityCases.DeclaredNotifier
{
    public sealed class Model
    {
        private int _value;
        private event Action? Changed;
        public int Value => _value;
        public int Listeners { get; private set; }
        public void Set(int value) { _value = value; Changed?.Invoke(); }
        public IDisposable Subscribe(IObserver<ValidationInvalidation> observer)
        {
            Action handler = () => observer.OnNext(default);
            Changed += handler; Listeners++;
            return new Token(() => { Changed -= handler; Listeners--; });
        }
        public static bool Check()
        {
            var model = new Model();
            using var context = new ValidationContext();
            var selector = new ValidationSelector<Model, int>(current =>
                new ValidationAccessPlan<int>(
                    () => ValidationRead<int>.Present(current.Value, new[] { ValidationPath.Legacy(nameof(Value)) }),
                    new[] { ValidationDependency.Create(() => current, static (owner, observer) => owner.Subscribe(observer)) },
                    ValidationObservationOptions<int>.Default));
            var rule = ValidationRuntime.RegisterRule(model, context, selector,
                value => value > 5 ? ValidationState.Valid : new ValidationState(false, "plain:" + value));
            if (rule.IsValid || model.Listeners != 1) { rule.Dispose(); return false; }
            model.Set(9);
            var valid = rule.IsValid && context.GetIsValid();
            rule.Dispose(); model.Set(0);
            return valid && model.Listeners == 0 && !context.Validations.Items.Any();
        }
    }
    public sealed class Token(Action cleanup) : IDisposable
    {
        private Action? _cleanup = cleanup;
        public void Dispose() { var callback = _cleanup; _cleanup = null; callback?.Invoke(); }
    }
}

namespace RuleCapabilityCases.GenericInterface
{
    public interface IName : INotifyPropertyChanged { string? Name { get; } }
    public class NameBase : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private string? _name;
        public virtual string? Name => _name;
        public new event PropertyChangedEventHandler? PropertyChanged;
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Set(string value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); }
    }
    public sealed class Model : NameBase, IName
    {
        public new string? Name { get { _ = base.Name; throw new InvalidOperationException("new shadow getter"); } }
        string? IName.Name => base.Name;
    }
    public static class Factory
    {
        public static ValidationHelper Attach<T>(T model) where T : class, IName, global::ReactiveUI.IReactiveObject, IValidatableViewModel
            => model.ValidationRule(x => x.Name, value => value == "ok", "required");
    }
    public static class Fixture
    {
        public static bool Check()
        {
            var model = new Model();
            using var rule = Factory.Attach(model);
            if (rule.IsValid) return false;
            model.Set("ok");
            return rule.IsValid && model.ValidationContext.GetIsValid();
        }
    }
}

namespace RuleCapabilityCases.GenericBase
{
    public interface IName : INotifyPropertyChanged { string? Name { get; } }
    public class NameBase : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private string? _name;
        public virtual string? Name => _name;
        public new event PropertyChangedEventHandler? PropertyChanged;
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Set(string value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); }
    }
    public sealed class Model : NameBase, IName
    {
        public new string? Name { get { _ = base.Name; throw new InvalidOperationException("new shadow getter"); } }
        string? IName.Name => base.Name;
    }
    public static class Factory
    {
        public static ValidationHelper Attach<T>(T model) where T : NameBase
            => model.ValidationRule(x => x.Name, value => value == "ok", "required");
    }
    public static class Fixture
    {
        public static bool Check()
        {
            var model = new Model();
            using var rule = Factory.Attach(model);
            if (rule.IsValid) return false;
            model.Set("ok");
            return rule.IsValid && model.ValidationContext.GetIsValid();
        }
    }
}

namespace RuleCapabilityCases.CheckedFailure
{
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        public int Count { get; set; } = 300;
        public int ListenerCount { get; private set; }
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ListenerCount++; }
            remove { _changed -= value; ListenerCount--; }
        }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Invalidate() => _changed?.Invoke(this, new(nameof(Count)));
        public static bool Check()
        {
            var model = new Model();
            try
            {
                using var failed = model.ValidationRule(x => checked((byte)x.Count), value => value > 5, "byte");
                return false;
            }
            catch (OverflowException) { }
            if (model.ListenerCount != 0 || model.ValidationContext.Validations.Items.Any()) return false;
            model.Count = 9;
            using var rule = model.ValidationRule(x => checked((byte)x.Count), value => value > 5, "byte");
            return rule.IsValid && model.ListenerCount == 1 && model.ValidationContext.GetIsValid();
        }
    }
}

namespace RuleCapabilityCases.GenericCallable
{
    public interface IName : INotifyPropertyChanged { string? Name { get; } }
    public sealed class Model : IName
    {
        private string? _name;
        private string? Name { get { _ = _name; throw new InvalidOperationException("private shadow"); } }
        string? IName.Name => _name;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Set(string value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); }
        public static bool Check()
        {
            using var context = new ValidationContext();
            var model = new Model();
            Func<Model,IValidationContext,ValidationHelper> attach = Factory.Register<Model>;
            using var rule = attach(model, context);
            if (rule.IsValid) return false;
            model.Set("ok");
            return rule.IsValid && context.GetIsValid();
        }
    }
    public static class Factory
    {
        public static ValidationHelper Register<TSource>(TSource source, IValidationContext context) where TSource : class, IName
        {
            var selector = new ValidationSelector<TSource,string?>(owner => new ValidationAccessPlan<string?>(
                () => ValidationRead<string?>.Present(((IName)owner).Name, new[] { ValidationPath.Legacy("Name") }),
                new[] { ValidationDependency.PropertyChanged(() => owner, "Name") }, ValidationObservationOptions<string?>.Default));
            return ValidationRuntime.RegisterRule(source, context, selector, value => new ValidationState(value == "ok", "required"));
        }
    }
}

namespace RuleCapabilityCases.TypedContext
{
    public sealed class Model : INotifyPropertyChanged
    {
        public int Count { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Set(int value) { Count = value; PropertyChanged?.Invoke(this, new(nameof(Count))); }
        public static bool Check()
        {
            using var context = new ValidationContext();
            var model = new Model();
            var selector = new ValidationSelector<Model,int>(source => new ValidationAccessPlan<int>(
                () => ValidationRead<int>.Present(source.Count, new[] { ValidationPath.Legacy(nameof(Count)) }),
                new[] { ValidationDependency.PropertyChanged(() => source, nameof(Count)) }, ValidationObservationOptions<int>.Default));
            using var one = ValidationRuntime.RegisterRule(model, context, selector, value => new ValidationState(value > 0, "positive"));
            using var two = ValidationRuntime.RegisterRule(model, context, selector, value => new ValidationState(value > 0, "count:" + value));
            if (one.IsValid || two.IsValid || context.GetIsValid()) return false;
            model.Set(1);
            if (!one.IsValid || !two.IsValid || !context.GetIsValid()) return false;
            one.Dispose(); two.Dispose();
            return !context.Validations.Items.Any();
        }
    }
}

namespace RuleCapabilityCases.DynamicIndex
{
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        public TrackingCollection? Values { get; set; } = new() { 0, 7, 7 };
        public int Index { get; set; }
        public int Listeners { get; private set; }
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Invalidate(string name) => _changed?.Invoke(this, new(name));
        public static bool Check()
        {
            var model = new Model();
            using var rule = model.ValidationRule(x => x.Values![x.Index], value => value > 5, value => "item:" + value);
            if (rule.IsValid || rule.Message.ToSingleLine() != "item:0") return false;
            model.Index = 1; model.Invalidate(nameof(Index));
            if (!rule.IsValid) return false;
            var component = model.ValidationContext.Validations.Items.OfType<IValidationPathComponent>().Single();
            var previousPaths = component.ValidationPaths.ToArray();
            model.Index = 2; model.Invalidate(nameof(Index));
            if (!rule.IsValid || previousPaths.SequenceEqual(component.ValidationPaths)) return false;
            model.Values![2] = 3;
            if (rule.IsValid || rule.Message.ToSingleLine() != "item:3") return false;
            var previous = model.Values!;
            model.Values = null; model.Invalidate(nameof(Values));
            if (rule.IsValid || rule.Message.ToSingleLine() != "item:0") return false;
            model.Index = 0; model.Invalidate(nameof(Index));
            model.Values = new TrackingCollection { 8 }; model.Invalidate(nameof(Values));
            if (!rule.IsValid) return false;
            previous[0] = 100;
            var bounds = new Model { Index = 99 };
            try
            {
                using var failed = bounds.ValidationRule(x => x.Values![x.Index], value => value > 5, "bounds");
                return false;
            }
            catch (ArgumentOutOfRangeException) { }
            return rule.IsValid && model.ValidationContext.GetIsValid() && previous.Listeners == 0
                && bounds.Listeners == 0 && bounds.Values!.Listeners == 0 && !bounds.ValidationContext.Validations.Items.Any();
        }
    }
    public sealed class TrackingCollection : ObservableCollection<int>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public int Listeners { get; private set; }
        public new event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            base.OnCollectionChanged(args);
            _changed?.Invoke(this, args);
        }
    }
}

namespace RuleCapabilityCases.LegacySeed
{
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        public string? Value { get; set; }
        public new event PropertyChangedEventHandler? PropertyChanged;
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Value)));
        public static bool Check()
        {
            var model = new Model();
            using var rule = model.ValidationRule(x => x.Value, value => value == "ok", "required");
            var selector = new ValidationSelector<Model,IObservable<IValidationState>?>(source =>
                new ValidationAccessPlan<IObservable<IValidationState>?>(
                    () => ValidationRead<IObservable<IValidationState>?>.Present(rule.ValidationChanged, Array.Empty<ValidationPath>()),
                    Array.Empty<ValidationDependency>(), ValidationObservationOptions<IObservable<IValidationState>?>.Default));
            var capture = new Capture(model.ValidationContext);
            using var presentation = ValidationRuntime.ObserveState(model, selector, ValidationInitialSequence.LegacyValid).Subscribe(capture);
            return string.Join(",", capture.Values) == "True,False" && !capture.DomainWasValid && !rule.IsValid && !model.ValidationContext.GetIsValid();
        }
    }
    public sealed class Capture(IValidationContext context) : IObserver<IValidationState>
    {
        public List<bool> Values { get; } = new();
        public bool DomainWasValid { get; private set; }
        public void OnNext(IValidationState state) { Values.Add(state.IsValid); DomainWasValid |= context.GetIsValid(); }
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
    }
}

namespace RuleCapabilityCases.AnonymousRichState
{
    file sealed class LocalModel : INotifyPropertyChanged
    {
        public string Text { get; set; } = "same";
        public int Revision { get; set; } = 1;
        public IValidationState? Latest { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Invalidate() => PropertyChanged?.Invoke(this, new(nameof(Revision)));
        public ValidationHelper Register(IValidationContext context)
            => RegisterCore(context, source => new { source.Text, source.Revision },
                value => new RichState(false, value.Text, value.Revision));
        private ValidationHelper RegisterCore<TValue>(IValidationContext context, Func<LocalModel,TValue> read, Func<TValue,IValidationState> validate)
        {
            var paths = new[] { ValidationPath.Legacy(nameof(Text)), ValidationPath.Legacy(nameof(Revision)) };
            var selector = new ValidationSelector<LocalModel,TValue>(source => new ValidationAccessPlan<TValue>(
                () => ValidationRead<TValue>.Present(read(source), paths),
                new[] { ValidationDependency.PropertyChanged(() => source, nameof(Text)), ValidationDependency.PropertyChanged(() => source, nameof(Revision)) },
                ValidationObservationOptions<TValue>.Default));
            return ValidationRuntime.RegisterRule(this, context, selector, value => { var state = validate(value); Latest = state; return state; });
        }
    }
    public readonly record struct RichState(bool IsValid, string Code, int Revision) : IValidationState
    {
        public IValidationText Text => ValidationText.Create(Code);
    }
    public sealed class Capture : IObserver<IValidationState>
    {
        public IValidationState? Latest { get; private set; }
        public void OnNext(IValidationState value) => Latest = value;
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
    }
    public static class Fixture
    {
        public static bool Check()
        {
            using var context = new ValidationContext();
            var model = new LocalModel();
            using var rule = model.Register(context);
            var capture = new Capture();
            using var subscription = rule.ValidationChanged.Subscribe(capture);
            if (!ReferenceEquals(capture.Latest, model.Latest) || ((RichState)capture.Latest!).Revision != 1) return false;
            var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (!component.ContainsPropertyName("Text") || !component.ContainsPropertyName("Revision") || component.ContainsPropertyName("Text", true)) return false;
            model.Revision = 2; model.Invalidate();
            return ReferenceEquals(capture.Latest, model.Latest) && ((RichState)capture.Latest!).Revision == 2
                && !rule.IsValid && !context.GetIsValid();
        }
    }
}

namespace RuleCapabilityCases.InterfaceParents
{
    public interface IAmount : INotifyPropertyChanged { int Amount { get; } }
    public sealed class Model : ReactiveObject, IAmount, IValidatableViewModel
    {
        private int _amount;
        private int Amount { get { _ = _amount; throw new InvalidOperationException("private shadow"); } }
        int IAmount.Amount => _amount;
        public IAmount? Child { get; set; }
        public new event PropertyChangedEventHandler? PropertyChanged;
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Set(int amount) { _amount = amount; PropertyChanged?.Invoke(this, new(nameof(Amount))); }
        public void Replace(IAmount? child) { Child = child; PropertyChanged?.Invoke(this, new(nameof(Child))); }
        public static bool Check()
        {
            var model = new Model();
            var observed = new RuleCapabilityObserved<long?>(-1);
            using var direct = model.ValidationRule(x => (long?)((IAmount)x).Amount, value => value == 9L, "amount");
            using var nested = model.ValidationRule(x => (long?)x.Child!.Amount,
                value => { observed.Value = value; return value == 9L; }, "child");
            if (direct.IsValid || nested.IsValid || observed.Value is not null) return false;
            model.Set(9);
            var child = new Model(); child.Set(9); model.Replace(child);
            if (!direct.IsValid || !nested.IsValid || observed.Value != 9L) return false;
            child.Set(3);
            if (nested.IsValid || observed.Value != 3L) return false;
            model.Replace(null);
            return !nested.IsValid && observed.Value is null;
        }
    }
}

namespace RuleCapabilityCases.MissingDefaultValue
{
    public sealed class Model : INotifyPropertyChanged
    {
        public Model? Child { get; set; }
        public string? Name { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
        public static bool Check()
        {
            using var context = new ValidationContext();
            var model = new Model();
            var paths = new[] { ValidationPath.Legacy("Child.Name") };
            var inputs = new List<string>();
            var missingOwnerPolicy = ValidationMissingOwnerPolicy.DefaultValue;
            var selector = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                () => source.Child is null ? ValidationRead<string?>.Missing(paths) : ValidationRead<string?>.Present(source.Child.Name, paths),
                new[] { ValidationDependency.PropertyChanged(() => source, nameof(Child)),
                    ValidationDependency.PropertyChanged(() => source.Child, nameof(Name)) },
                new ValidationObservationOptions<string?>(missingOwnerPolicy, () => "fallback", EqualityComparer<string?>.Default, false)));
            using var rule = ValidationRuntime.RegisterRule(model, context, selector, value =>
            {
                inputs.Add(value ?? "<null>");
                return new ValidationState(value == "ok", value ?? "<null>");
            });
            var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (!component.ContainsPropertyName("Child.Name", true)) return false;
            model.Child = new Model(); model.Invalidate(nameof(Child));
            if (rule.IsValid || context.GetIsValid()) return false;
            model.Child.Name = "ok"; model.Child.Invalidate(nameof(Name));
            if (!rule.IsValid || !context.GetIsValid()) return false;
            model.Child = null; model.Invalidate(nameof(Child));
            return string.Join(",", inputs) == "<null>,<null>,ok,<null>"
                && rule.IsValid == (missingOwnerPolicy == ValidationMissingOwnerPolicy.Suppress);
        }
    }
}

namespace RuleCapabilityCases.MissingSuppress
{
    public sealed class Model : INotifyPropertyChanged
    {
        public Model? Child { get; set; }
        public string? Name { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
        public static bool Check()
        {
            using var context = new ValidationContext();
            var model = new Model();
            var paths = new[] { ValidationPath.Legacy("Child.Name") };
            var inputs = new List<string>();
            var missingOwnerPolicy = ValidationMissingOwnerPolicy.Suppress;
            var selector = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                () => source.Child is null ? ValidationRead<string?>.Missing(paths) : ValidationRead<string?>.Present(source.Child.Name, paths),
                new[] { ValidationDependency.PropertyChanged(() => source, nameof(Child)),
                    ValidationDependency.PropertyChanged(() => source.Child, nameof(Name)) },
                new ValidationObservationOptions<string?>(missingOwnerPolicy, () => "fallback", EqualityComparer<string?>.Default, false)));
            using var rule = ValidationRuntime.RegisterRule(model, context, selector, value =>
            {
                inputs.Add(value ?? "<null>");
                return new ValidationState(value == "ok", value ?? "<null>");
            });
            var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (!component.ContainsPropertyName("Child.Name", true)) return false;
            model.Child = new Model(); model.Invalidate(nameof(Child));
            if (rule.IsValid || context.GetIsValid()) return false;
            model.Child.Name = "ok"; model.Child.Invalidate(nameof(Name));
            if (!rule.IsValid || !context.GetIsValid()) return false;
            model.Child = null; model.Invalidate(nameof(Child));
            return string.Join(",", inputs) == "<null>,ok"
                && rule.IsValid == (missingOwnerPolicy == ValidationMissingOwnerPolicy.Suppress);
        }
    }
}

namespace RuleCapabilityCases.MissingFallback
{
    public sealed class Model : INotifyPropertyChanged
    {
        public Model? Child { get; set; }
        public string? Name { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
        public static bool Check()
        {
            using var context = new ValidationContext();
            var model = new Model();
            var paths = new[] { ValidationPath.Legacy("Child.Name") };
            var inputs = new List<string>();
            var missingOwnerPolicy = ValidationMissingOwnerPolicy.Fallback;
            var selector = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                () => source.Child is null ? ValidationRead<string?>.Missing(paths) : ValidationRead<string?>.Present(source.Child.Name, paths),
                new[] { ValidationDependency.PropertyChanged(() => source, nameof(Child)),
                    ValidationDependency.PropertyChanged(() => source.Child, nameof(Name)) },
                new ValidationObservationOptions<string?>(missingOwnerPolicy, () => "fallback", EqualityComparer<string?>.Default, false)));
            using var rule = ValidationRuntime.RegisterRule(model, context, selector, value =>
            {
                inputs.Add(value ?? "<null>");
                return new ValidationState(value == "ok", value ?? "<null>");
            });
            var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (!component.ContainsPropertyName("Child.Name", true)) return false;
            model.Child = new Model(); model.Invalidate(nameof(Child));
            if (rule.IsValid || context.GetIsValid()) return false;
            model.Child.Name = "ok"; model.Child.Invalidate(nameof(Name));
            if (!rule.IsValid || !context.GetIsValid()) return false;
            model.Child = null; model.Invalidate(nameof(Child));
            return string.Join(",", inputs) == "fallback,<null>,ok,fallback"
                && rule.IsValid == (missingOwnerPolicy == ValidationMissingOwnerPolicy.Suppress);
        }
    }
}

namespace RuleCapabilityCases.PrivateCapture
{
    public static class Fixture
    {
        private sealed class Model : INotifyPropertyChanged
        {
            private string? Secret { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
            public void Set(string value) { Secret = value; Invalidate(nameof(Secret)); }
            public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
            private string Compute(Model current) => Secret + ":" + current.Secret;
            public ValidationHelper Register(IValidationContext context, Func<Model> captured)
            {
                var paths = new[] { ValidationPath.Legacy("Secret"), ValidationPath.Legacy("Captured.Secret") };
                var selector = new ValidationSelector<Model, string?>(source => new ValidationAccessPlan<string?>(
                    () => ValidationRead<string?>.Present(source.Compute(captured()), paths),
                    new[] {
                        ValidationDependency.PropertyChanged(() => source, nameof(Secret)),
                        ValidationDependency.PropertyChanged(() => source, "Captured"),
                        ValidationDependency.PropertyChanged(() => captured(), nameof(Secret)) },
                    ValidationObservationOptions<string?>.Default));
                Func<Model, IValidationContext, ValidationSelector<Model,string?>, Func<string?,IValidationState>, ValidationHelper> callable = ValidationRuntime.RegisterRule;
                return callable(this, context, selector,
                    value => new ValidationState(value == "source:ok", value ?? "null"));
            }
        }
        public static bool Check()
        {
            using var context = new ValidationContext();
            var source = new Model(); source.Set("source");
            var previous = new Model(); previous.Set("bad");
            var current = previous;
            var capturedReads = 0;
            using var rule = source.Register(context, () => { capturedReads++; return current; });
            if (rule.IsValid || rule.Message.ToSingleLine() != "source:bad") return false;
            current = new Model(); current.Set("ok");
            source.Invalidate("Captured");
            if (!rule.IsValid) return false;
            var afterHandoff = capturedReads;
            previous.Set("obsolete");
            if (!rule.IsValid || capturedReads != afterHandoff) return false;
            current.Set("new");
            return !rule.IsValid && rule.Message.ToSingleLine() == "source:new";
        }
    }
}

namespace RuleCapabilityCases.PrivateGenericClass
{
    public static partial class Outer<T> where T : class
    {
        private sealed partial class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            private T? Secret { get; set; }
            public new event PropertyChangedEventHandler? PropertyChanged;
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public ValidationHelper Attach() => this.ValidationRule(x => x.Secret, value => value is not null, "private required");
            public void Set(T value) { Secret = value; PropertyChanged?.Invoke(this, new(nameof(Secret))); }
        }
        public static bool Check(T current)
        {
            var model = new Model();
            using var rule = model.Attach();
            if (rule.IsValid || rule.Message.ToSingleLine() != "private required") return false;
            model.Set(current);
            return rule.IsValid && model.ValidationContext.GetIsValid();
        }
    }
    public static class Fixture { public static bool Check() => Outer<string>.Check("current"); }
}

namespace RuleCapabilityCases.PrivateGenericStruct
{
    public static partial class Outer<T> where T : struct
    {
        private sealed partial class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            private T? Secret { get; set; }
            public new event PropertyChangedEventHandler? PropertyChanged;
            public IValidationContext ValidationContext { get; } = new ValidationContext();
            public ValidationHelper Attach() => this.ValidationRule(x => x.Secret, value => value is not null, "private required");
            public void Set(T value) { Secret = value; PropertyChanged?.Invoke(this, new(nameof(Secret))); }
        }
        public static bool Check(T current)
        {
            var model = new Model();
            using var rule = model.Attach();
            if (rule.IsValid || rule.Message.ToSingleLine() != "private required") return false;
            model.Set(current);
            return rule.IsValid && model.ValidationContext.GetIsValid();
        }
    }
    public static class Fixture { public static bool Check() => Outer<int>.Check(9); }
}

namespace RuleCapabilityCases.ComputedInline
{
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        internal CountValue Count;
        public int Bonus { get; set; }
        public new event PropertyChangedEventHandler? PropertyChanged;
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
        public static bool Check()
        {
            CountValue.Conversions = 0;
            var model = new Model { Count = 2, Bonus = 3 };
            
            using var rule = model.ValidationRule(x => (long)x.Count + x.Bonus, value => value == 9L, value => "sum:" + value);
            if (rule.IsValid || rule.Message.ToSingleLine() != "sum:5" || CountValue.Conversions != 1) return false;
            var component = model.ValidationContext.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (!component.ContainsPropertyName("Count") || !component.ContainsPropertyName(nameof(Bonus))) return false;
            if (component.ContainsPropertyName("Count", true)) return false;
            model.Count = 6;
            model.Invalidate(nameof(Count));
            if (!rule.IsValid || !model.ValidationContext.GetIsValid() || CountValue.Conversions != 2) return false;
            model.Bonus = 4;
            model.Invalidate(nameof(Bonus));
            if (rule.IsValid || rule.Message.ToSingleLine() != "sum:10" || CountValue.Conversions != 3) return false;
            rule.Dispose();
            model.Count = 100;
            model.Invalidate(nameof(Count));
            return !model.ValidationContext.Validations.Items.Any() && CountValue.Conversions == 3;
        }
    }
    public readonly struct CountValue(int value) : IEquatable<CountValue>
    {
        public int Value { get; } = value;
        private static int _conversions;
        public static int Conversions { get => System.Threading.Volatile.Read(ref _conversions); set => System.Threading.Volatile.Write(ref _conversions,value); }
        public static implicit operator CountValue(int value) => new(value);
        public static implicit operator long(CountValue value) { Conversions++; return value.Value; }
        public static CountValue FromInt32(int value) => new(value);
        public long ToInt64() => (long)this;
        public bool Equals(CountValue other) => Value == other.Value;
        public override bool Equals(object? other) => other is CountValue value && Equals(value);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(CountValue left,CountValue right) => left.Equals(right);
        public static bool operator !=(CountValue left,CountValue right) => !left.Equals(right);
    }
}

namespace RuleCapabilityCases.ComputedStored
{
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        internal CountValue Count;
        public int Bonus { get; set; }
        public new event PropertyChangedEventHandler? PropertyChanged;
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public void Invalidate(string name) => PropertyChanged?.Invoke(this, new(name));
        public static bool Check()
        {
            CountValue.Conversions = 0;
            var model = new Model { Count = 2, Bonus = 3 };
            Expression<Func<Model,long>> selected = x => (long)x.Count + x.Bonus;
            using var rule = model.ValidationRule(selected, value => value == 9L, value => "sum:" + value);
            if (rule.IsValid || rule.Message.ToSingleLine() != "sum:5" || CountValue.Conversions != 1) return false;
            var component = model.ValidationContext.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (!component.ContainsPropertyName("Count") || !component.ContainsPropertyName(nameof(Bonus))) return false;
            if (component.ContainsPropertyName("Count", true)) return false;
            model.Count = 6;
            model.Invalidate(nameof(Count));
            if (!rule.IsValid || !model.ValidationContext.GetIsValid() || CountValue.Conversions != 2) return false;
            model.Bonus = 4;
            model.Invalidate(nameof(Bonus));
            if (rule.IsValid || rule.Message.ToSingleLine() != "sum:10" || CountValue.Conversions != 3) return false;
            rule.Dispose();
            model.Count = 100;
            model.Invalidate(nameof(Count));
            return !model.ValidationContext.Validations.Items.Any() && CountValue.Conversions == 3;
        }
    }
    public readonly struct CountValue(int value) : IEquatable<CountValue>
    {
        public int Value { get; } = value;
        private static int _conversions;
        public static int Conversions { get => System.Threading.Volatile.Read(ref _conversions); set => System.Threading.Volatile.Write(ref _conversions,value); }
        public static implicit operator CountValue(int value) => new(value);
        public static implicit operator long(CountValue value) { Conversions++; return value.Value; }
        public static CountValue FromInt32(int value) => new(value);
        public long ToInt64() => (long)this;
        public bool Equals(CountValue other) => Value == other.Value;
        public override bool Equals(object? other) => other is CountValue value && Equals(value);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(CountValue left,CountValue right) => left.Equals(right);
        public static bool operator !=(CountValue left,CountValue right) => !left.Equals(right);
    }
}

namespace RuleCapabilityCases.PredicateContexts
{
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private string? _name;
        private Model? _child;
        public new event PropertyChangedEventHandler? PropertyChanged;
        public string? Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public Model? Child { get => _child; set { _child = value; PropertyChanged?.Invoke(this, new(nameof(Child))); } }
        public IValidationContext CurrentContext { get; set; }
        public int ContextReads { get; private set; }
        IValidationContext IValidatableViewModel.ValidationContext { get { ContextReads++; return CurrentContext; } }
        private IValidationContext ValidationContext { get { _ = CurrentContext; throw new InvalidOperationException("shadow getter must not run"); } }
        public Model(IValidationContext context) => CurrentContext = context;
        public static bool Check()
        {
            using var original = new ValidationContext();
            using var selected = new ValidationContext();
            using var replacement = new ValidationContext();
            var model = new Model(original);
            var predicateCalls = 0;
            var messageCalls = 0;
            var seen = "unset";
            using var one = model.ValidationRule(x => x.Name, value => value == "ok", "required");
            using var two = model.ValidationRule(x => x.Child!.Name,
                value => { predicateCalls++; seen = value; model.CurrentContext = replacement; return value == "ok"; },
                value => { messageCalls++; return value is null ? "missing" : "bad:" + value; });
            using var three = model.ValidationRule(selected, x => x.Name, value => value == "ok", "selected");
            using var four = model.ValidationRule(selected, x => x.Child!.Name, value => value == "ok", value => "selected:" + value);
            Require(!one.IsValid && !two.IsValid && !three.IsValid && !four.IsValid, "actual invalid initial state");
            Require(predicateCalls == 1 && messageCalls == 1 && seen is null, "missing parent delivers default once");
            Require(model.ContextReads == 2, "selected context does not query model context");
            Require(original.Validations.Items.Count == 2 && selected.Validations.Items.Count == 2, "registration context ownership");
            var nested = original.Validations.Items.OfType<IPropertyValidationComponent>().Single(rule => rule.ContainsPropertyName("Child.Name"));
            Require(!nested.ContainsPropertyName(nameof(Child.Name)), "full root metadata path");
            Require(nested.ContainsPropertyName("Child.Name", true), "one selected leaf remains an exclusive property rule");
            model.CurrentContext = replacement;
            model.Name = "ok";
            model.Child = new Model(replacement) { Name = "ok" };
            Require(one.IsValid && two.IsValid && three.IsValid && four.IsValid, "current synchronous validity");
            Require(predicateCalls == 2 && messageCalls == 1, "dynamic message executes only while invalid");
            model.Child.Name = "bad";
            Require(!two.IsValid && two.Message.ToSingleLine() == "bad:bad", "child value and dynamic message");
            model.Child = null;
            Require(!two.IsValid && seen is null && two.Message.ToSingleLine() == "missing", "disappearing parent delivers default");
            Require(model.ContextReads == 2 && replacement.Validations.Items.Count == 0, "context selection stays captured");
            one.Dispose(); two.Dispose(); three.Dispose(); four.Dispose();
            Require(!original.Validations.Items.Any() && !selected.Validations.Items.Any(), "helpers remove from original contexts");
            return CheckExpressionPredicates();
        }
        private static bool CheckExpressionPredicates()
        {
            using var original = new ValidationContext();
            using var selected = new ValidationContext();
            using var replacement = new ValidationContext();
            var model = new Model(original);
            Expression<Func<Model,string?>> direct = source => source.Name;
            Expression<Func<Model,string?>> nested = source => source.Child!.Name;
            using var one = model.ValidationRule(direct,value => value == "ok","expression required");
            using var two = model.ValidationRule(nested,value => value == "ok",value => "expression:" + value);
            using var three = model.ValidationRule(selected,direct,value => value == "ok","expression selected");
            using var four = model.ValidationRule(selected,nested,value => value == "ok",value => "expression selected:" + value);
            Require(!one.IsValid && !two.IsValid && !three.IsValid && !four.IsValid, "expression initial actual states");
            Require(two.Message.ToSingleLine() == "expression:" && four.Message.ToSingleLine() == "expression selected:", "expression dynamic null messages");
            Require(model.ContextReads == 2 && original.Validations.Items.Count == 2 && selected.Validations.Items.Count == 2, "expression context ownership");
            model.CurrentContext = replacement;
            model.Name = "ok"; model.Child = new Model(replacement) { Name = "ok" };
            Require(one.IsValid && two.IsValid && three.IsValid && four.IsValid, "expression current values");
            Require(original.GetIsValid() && selected.GetIsValid() && !replacement.Validations.Items.Any(), "expression captured contexts");
            model.Child = null;
            Require(!two.IsValid && !four.IsValid && two.Message.ToSingleLine() == "expression:", "expression missing parent invalidation");
            one.Dispose(); two.Dispose(); three.Dispose(); four.Dispose();
            Require(model.ContextReads == 2 && !original.Validations.Items.Any() && !selected.Validations.Items.Any(), "expression disposal removes original registrations");
            return true;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

namespace RuleCapabilityCases.CustomAnd
{
    public struct Flag : IEquatable<Flag>
    {
        internal bool Value;
        private static int _combines;
        public static int Combines { get => System.Threading.Volatile.Read(ref _combines); set => System.Threading.Volatile.Write(ref _combines,value); }
        private static int _truthChecks;
        public static int TruthChecks { get => System.Threading.Volatile.Read(ref _truthChecks); set => System.Threading.Volatile.Write(ref _truthChecks,value); }
        public static bool operator true(Flag value) { TruthChecks++; return value.Value; }
        public static bool operator false(Flag value) { TruthChecks++; return !value.Value; }
        public static Flag operator &(Flag left, Flag right) { Combines++; return new Flag { Value = left.Value && right.Value }; }
        public static Flag operator |(Flag left, Flag right) { Combines++; return new Flag { Value = left.Value || right.Value }; }
        public bool Equals(Flag other) => Value == other.Value;
        public override bool Equals(object? other) => other is Flag value && Equals(value);
        public override int GetHashCode() => Value.GetHashCode();
        public bool IsTrue => Value;
        public static Flag BitwiseAnd(Flag left,Flag right) => left & right;
        public static Flag BitwiseOr(Flag left,Flag right) => left | right;
        public static bool operator ==(Flag left,Flag right) => left.Equals(right);
        public static bool operator !=(Flag left,Flag right) => !left.Equals(right);
    }
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        private Model? _other;
        private Flag _flag;
        public int Listeners { get; private set; }
        public int OtherReads { get; private set; }
        public int FlagReads { get; private set; }
        public Action? BeforeFlagRead { get; set; }
        public Flag Flag { get { FlagReads++; BeforeFlagRead?.Invoke(); return _flag; } set { _flag = value; } }
        public Model? Primary { get; set; }
        public Model? Other { get { OtherReads++; return _other; } set { _other = value; } }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void Signal(string name) => _changed?.Invoke(this, new(name));
        public static bool Check()
        {
            var model = new Model();
            using var context = model.ValidationContext;
            var left = new Model { Flag = new Flag { Value = false } };
            var right = new Model { Flag = new Flag { Value = true } };
            model.Primary = left; model.Other = right; Flag.Combines = 0; Flag.TruthChecks = 0;
            using var rule = model.ValidationRule(x => x.Primary!.Flag && x.Other!.Flag, value => value.Value, "truth required");
            var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (rule.IsValid != false || model.OtherReads != 0 || right.FlagReads != 0 || right.Listeners != 0
                || Flag.Combines != 0 || Flag.TruthChecks != 1 || !RuleCapabilityAssertions.Equal(component.PropertyCount,1)) return false;
            left.Flag = new Flag { Value = true }; left.Signal(nameof(Flag));
            if (!rule.IsValid || model.OtherReads != 1 || right.FlagReads != 1 || right.Listeners != 1
                || Flag.Combines != 1 || Flag.TruthChecks != 2 || !RuleCapabilityAssertions.Equal(component.PropertyCount,2)) return false;
            left.Flag = new Flag { Value = false }; left.Signal(nameof(Flag));
            if (right.Listeners != 0 || !RuleCapabilityAssertions.Equal(component.PropertyCount,1) || Flag.Combines != 1 || Flag.TruthChecks != 3) return false;
            rule.Dispose();
            if (model.Listeners != 0 || left.Listeners != 0 || right.Listeners != 0) return false;
            left.Flag = new Flag { Value = true }; model.Other = null; Flag.Combines = 0;
            using var missing = model.ValidationRule(x => x.Primary!.Flag && x.Other!.Flag, value => value.Value, "missing");
            if (missing.IsValid || Flag.Combines != 0) return false;
            missing.Dispose();
            model.Other = right; var error = new InvalidOperationException("user error"); right.BeforeFlagRead = () => throw error;
            try
            {
                using var failed = model.ValidationRule(x => x.Primary!.Flag && x.Other!.Flag, value => value.Value, "error");
                return false;
            }
            catch (InvalidOperationException actual)
            {
                return ReferenceEquals(actual, error) && Flag.Combines == 0 && model.Listeners == 0
                    && left.Listeners == 0 && right.Listeners == 0 && !context.Validations.Items.Any();
            }
        }
    }
}

namespace RuleCapabilityCases.CustomOr
{
    public struct Flag : IEquatable<Flag>
    {
        internal bool Value;
        private static int _combines;
        public static int Combines { get => System.Threading.Volatile.Read(ref _combines); set => System.Threading.Volatile.Write(ref _combines,value); }
        private static int _truthChecks;
        public static int TruthChecks { get => System.Threading.Volatile.Read(ref _truthChecks); set => System.Threading.Volatile.Write(ref _truthChecks,value); }
        public static bool operator true(Flag value) { TruthChecks++; return value.Value; }
        public static bool operator false(Flag value) { TruthChecks++; return !value.Value; }
        public static Flag operator &(Flag left, Flag right) { Combines++; return new Flag { Value = left.Value && right.Value }; }
        public static Flag operator |(Flag left, Flag right) { Combines++; return new Flag { Value = left.Value || right.Value }; }
        public bool Equals(Flag other) => Value == other.Value;
        public override bool Equals(object? other) => other is Flag value && Equals(value);
        public override int GetHashCode() => Value.GetHashCode();
        public bool IsTrue => Value;
        public static Flag BitwiseAnd(Flag left,Flag right) => left & right;
        public static Flag BitwiseOr(Flag left,Flag right) => left | right;
        public static bool operator ==(Flag left,Flag right) => left.Equals(right);
        public static bool operator !=(Flag left,Flag right) => !left.Equals(right);
    }
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        private Model? _other;
        private Flag _flag;
        public int Listeners { get; private set; }
        public int OtherReads { get; private set; }
        public int FlagReads { get; private set; }
        public Action? BeforeFlagRead { get; set; }
        public Flag Flag { get { FlagReads++; BeforeFlagRead?.Invoke(); return _flag; } set { _flag = value; } }
        public Model? Primary { get; set; }
        public Model? Other { get { OtherReads++; return _other; } set { _other = value; } }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void Signal(string name) => _changed?.Invoke(this, new(name));
        public static bool Check()
        {
            var model = new Model();
            using var context = model.ValidationContext;
            var left = new Model { Flag = new Flag { Value = true } };
            var right = new Model { Flag = new Flag { Value = true } };
            model.Primary = left; model.Other = right; Flag.Combines = 0; Flag.TruthChecks = 0;
            using var rule = model.ValidationRule(x => x.Primary!.Flag || x.Other!.Flag, value => value.Value, "truth required");
            var component = context.Validations.Items.OfType<IPropertyValidationComponent>().Single();
            if (rule.IsValid != true || model.OtherReads != 0 || right.FlagReads != 0 || right.Listeners != 0
                || Flag.Combines != 0 || Flag.TruthChecks != 1 || !RuleCapabilityAssertions.Equal(component.PropertyCount,1)) return false;
            left.Flag = new Flag { Value = false }; left.Signal(nameof(Flag));
            if (!rule.IsValid || model.OtherReads != 1 || right.FlagReads != 1 || right.Listeners != 1
                || Flag.Combines != 1 || Flag.TruthChecks != 2 || !RuleCapabilityAssertions.Equal(component.PropertyCount,2)) return false;
            left.Flag = new Flag { Value = true }; left.Signal(nameof(Flag));
            if (right.Listeners != 0 || !RuleCapabilityAssertions.Equal(component.PropertyCount,1) || Flag.Combines != 1 || Flag.TruthChecks != 3) return false;
            rule.Dispose();
            if (model.Listeners != 0 || left.Listeners != 0 || right.Listeners != 0) return false;
            left.Flag = new Flag { Value = false }; model.Other = null; Flag.Combines = 0;
            using var missing = model.ValidationRule(x => x.Primary!.Flag || x.Other!.Flag, value => value.Value, "missing");
            if (missing.IsValid || Flag.Combines != 0) return false;
            missing.Dispose();
            model.Other = right; var error = new InvalidOperationException("user error"); right.BeforeFlagRead = () => throw error;
            try
            {
                using var failed = model.ValidationRule(x => x.Primary!.Flag || x.Other!.Flag, value => value.Value, "error");
                return false;
            }
            catch (InvalidOperationException actual)
            {
                return ReferenceEquals(actual, error) && Flag.Combines == 0 && model.Listeners == 0
                    && left.Listeners == 0 && right.Listeners == 0 && !context.Validations.Items.Any();
            }
        }
    }
}

namespace RuleCapabilityCases.StaticObject
{
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        public dynamic Payload { get; set; } = "payload";
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void Signal() => _changed?.Invoke(this, new(nameof(Payload)));
        public static bool Check()
        {
            var model = new Model();
            using var context = model.ValidationContext;
            using var rule = model.ValidationRule(x => (string)(object)x.Payload, value => value == "ok", "object required");
            if (rule.IsValid || model.Listeners != 1) return false;
            model.Payload = "ok"; model.Signal();
            if (!rule.IsValid || !context.GetIsValid()) return false;
            rule.Dispose(); model.Payload = 7;
            try
            {
                using var failed = model.ValidationRule(x => (string)(object)x.Payload, value => value == "ok", "cast");
                return false;
            }
            catch (InvalidCastException)
            {
                return model.Listeners == 0 && !context.Validations.Items.Any();
            }
        }
    }
}

namespace RuleCapabilityCases.StableProvenance
{
    public interface ISelectors
    {
        static sealed Expression<Func<Model,string?>> Value => x => x.Name;
        static sealed Expression<Func<Model,string?>> Create() => x => x.Name;
    }
    public static class ConcreteSelectors
    {
        public static Expression<Func<Model,string?>> Value => x => x.Other;
        public static Expression<Func<Model,string?>> Create() => x => x.Other;
    }
    public static class StoredSelectors
    {
        public static readonly Expression<Func<Model,string?>> Value = x => x.Name;
    }
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        public string Name { get; private set; } = "name";
        public string Other { get; private set; } = "other";
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void SetName(string value) { Name = value; _changed?.Invoke(this,new(nameof(Name))); }
        public void SetOther(string value) { Other = value; _changed?.Invoke(this,new(nameof(Other))); }
        public static bool Check()
        {
            var model = new Model(); using var context = model.ValidationContext;
            Expression<Func<Model,string?>> local = x => x.Name;
            using var otherProperty = model.ValidationRule(ConcreteSelectors.Value,value => value == "other","other");
            using var otherFactory = model.ValidationRule(ConcreteSelectors.Create(),value => value == "other","other");
            using var nameProperty = model.ValidationRule(ISelectors.Value,value => value == "name","name");
            using var nameFactory = model.ValidationRule(ISelectors.Create(),value => value == "name","name");
            using var nameStored = model.ValidationRule(StoredSelectors.Value,value => value == "name","name");
            using var nameLocal = model.ValidationRule(local,value => value == "name","name");
            if (!context.GetIsValid() || model.Listeners != 6) return false;
            model.SetName("changed");
            if (!otherProperty.IsValid || !otherFactory.IsValid || nameProperty.IsValid || nameFactory.IsValid
                || nameStored.IsValid || nameLocal.IsValid) return false;
            model.SetOther("changed");
            if (otherProperty.IsValid || otherFactory.IsValid) return false;
            otherProperty.Dispose(); otherFactory.Dispose(); nameProperty.Dispose(); nameFactory.Dispose();
            nameStored.Dispose(); nameLocal.Dispose();
            return model.Listeners == 0 && !context.Validations.Items.Any();
        }
    }
}

namespace RuleCapabilityCases.RuntimeProvenance
{
    public interface ISelectors
    {
        static virtual Expression<Func<Model,string?>> Value => x => x.Name;
        static virtual Expression<Func<Model,string?>> Create() => x => x.Name;
    }
    public sealed class OtherSelectors : ISelectors
    {
        public static Expression<Func<Model,string?>> Value => x => x.Other;
        public static Expression<Func<Model,string?>> Create() => x => x.Other;
    }
    public static partial class AssignedSelector
    {
        public static readonly Expression<Func<Model,string?>> Value = x => x.Name;
    }
    public static partial class AssignedSelector
    {
        static AssignedSelector() { Value = x => x.Other; }
    }
    public static partial class TupleSelector
    {
        public static readonly Expression<Func<Model,string?>> Value = x => x.Name;
    }
    public static partial class TupleSelector
    {
        static TupleSelector()
        {
            Expression<Func<Model,string?>> other = x => x.Other;
            (Value,other) = (other,Value);
        }
    }
    public static partial class RefSelector
    {
        public static readonly Expression<Func<Model,string?>> Value = x => x.Name;
    }
    public static partial class RefSelector
    {
        static RefSelector() { Change(ref Value); }
        private static void Change(ref Expression<Func<Model,string?>> value) { value = x => x.Other; }
    }
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        public string Name { get; private set; } = "name";
        public string Other { get; private set; } = "other";
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        private void SetName(string value) { Name = value; _changed?.Invoke(this,new(nameof(Name))); }
        private void SetOther(string value) { Other = value; _changed?.Invoke(this,new(nameof(Other))); }
        public static bool Check() => CheckDispatch<OtherSelectors>() && CheckExpression(AssignedSelector.Value)
            && CheckExpression(TupleSelector.Value) && CheckExpression(RefSelector.Value) && CheckBorrow();
        private static bool CheckDispatch<TSelectors>() where TSelectors : ISelectors =>
            CheckExpression(TSelectors.Value) && CheckExpression(TSelectors.Create());
        private static bool CheckBorrow()
        {
            Expression<Func<Model,string?>> selected = x => x.Name;
            Borrow(selected);
            return CheckExpression(selected);
        }
        private static void Borrow(in Expression<Func<Model,string?>> selected)
        {
            System.Runtime.CompilerServices.Unsafe.AsRef(in selected) = x => x.Other;
        }
        private static bool CheckExpression(Expression<Func<Model,string?>> selected)
        {
            if (((MemberExpression)selected.Body).Member.Name != nameof(Other)) return false;
            var model = new Model(); using var context = model.ValidationContext;
            using var registry = new ValidationPlanRegistry(1); using var attachment = registry.Attach(model);
            var descriptor = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                () => ValidationRead<string?>.Present(source.Other,new[] { ValidationPath.Legacy(nameof(Other)) }),
                new[] { ValidationDependency.PropertyChanged(() => source,nameof(Other)) },
                ValidationObservationOptions<string?>.Default));
            using var registration = registry.RegisterSelector(ValidationPlanRole.RuleValue,selected,descriptor);
            using var rule = Attach(model,selected);
            if (!rule.IsValid || model.Listeners != 1) return false;
            model.SetName("changed"); if (!rule.IsValid) return false;
            model.SetOther("changed"); if (rule.IsValid || context.GetIsValid()) return false;
            rule.Dispose(); registration.Dispose();
            if (model.Listeners != 0 || context.Validations.Items.Any()) return false;
            try { using var missing = Attach(model,selected); return false; }
            catch (InvalidOperationException error)
            {
                return error.Message.StartsWith("No typed validation capability is registered",StringComparison.Ordinal)
                    && model.Listeners == 0 && !context.Validations.Items.Any();
            }
        }
        [ValidationRuntimeDispatch]
        private static ValidationHelper Attach(Model model,Expression<Func<Model,string?>> selected) =>
            model.ValidationRule(selected,value => value == "other","other required");
    }
}

namespace RuleCapabilityCases.GenericProvenance
{
    public static class Selectors<T>
    {
        public static readonly Expression<Func<Model<T>,T>> Value = x => x.Value;
        public static readonly Expression<Func<Model<T>,T>> Indexed = x => x.Values[x.Index];
        public static readonly Expression<Func<Model<T>,object?>> Boxed = x => (object)x.Value!;
        public static class Nested<TUnused>
        {
            public static Expression<Func<Model<T>,T>> Value { get { return x => x.Value; } }
        }
    }
    // Lifted user-defined operators cannot be constructed as expression trees without dynamic code.
    public static class LiftedSelectors<T>
    {
        public static readonly Expression<Func<Model<T>,int?>> Lifted = x => x.Number + x.Number;
    }
    public static class SelectorFactories
    {
        public static Expression<Func<Model<T>,T>> Value<T>() { return x => x.Value; }
        public static Expression<Func<Model<T>,T?>> Nullable<T>() where T : struct => x => (T?)x.Value;
    }
    public readonly struct Count(int value) : IEquatable<Count>
    {
        public int Value { get; } = value;
        private static int _additions;
        public static int Additions { get => System.Threading.Volatile.Read(ref _additions); set => System.Threading.Volatile.Write(ref _additions,value); }
        private static int _conversions;
        public static int Conversions { get => System.Threading.Volatile.Read(ref _conversions); set => System.Threading.Volatile.Write(ref _conversions,value); }
        public static Count operator +(Count left, Count right) { Additions++; return new(left.Value + right.Value + 1); }
        public static implicit operator int(Count value) { Conversions++; return value.Value; }
        public static Count Add(Count left,Count right) => left + right;
        public int ToInt32() => (int)this;
        public bool Equals(Count other) => Value == other.Value;
        public override bool Equals(object? other) => other is Count value && Equals(value);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(Count left,Count right) => left.Equals(right);
        public static bool operator !=(Count left,Count right) => !left.Equals(right);
    }
    public sealed class Model<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        private T _value = default!;
        private T[] _values = new T[2];
        private int _index;
        public T Value { get { ValueReads++; return _value; } }
        public T[] Values { get { ArrayReads++; return _values; } }
        public int Index { get { IndexReads++; return _index; } }
        public Count? Number { get; private set; }
        public int ValueReads { get; private set; }
        public int ArrayReads { get; private set; }
        public int IndexReads { get; private set; }
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void SetValue(T value) { _value = value; _changed?.Invoke(this,new(nameof(Value))); }
        public void SetIndexed(T value) { _values[_index] = value; _changed?.Invoke(this,new(nameof(Values))); }
        public void SetIndex(int value) { _index = value; _changed?.Invoke(this,new(nameof(Index))); }
        public void SetNumber(Count? value) { Number = value; _changed?.Invoke(this,new(nameof(Number))); }
    }
    public sealed class NullableModel<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel where T : class?
    {
        private PropertyChangedEventHandler? _changed;
        public T? Value { get; private set; }
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void SetValue(T? value) { Value = value; _changed?.Invoke(this,new(nameof(Value))); }
    }
    public static class NullableSelectors<T> where T : class?
    {
        public static readonly Expression<Func<NullableModel<T>,T?>> Value = x => x.Value;
    }
    public class Base : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        public int Value { get; private set; }
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void SetValue(int value) { Value = value; _changed?.Invoke(this,new(nameof(Value))); }
    }
    public sealed class Hidden : Base { public new int Value { get { _ = base.Value; throw new InvalidOperationException("hidden value"); } } }
    public class Amount(int tag)
    {
        public int Tag { get; } = tag;
        private static int _additions;
        public static int Additions { get => System.Threading.Volatile.Read(ref _additions); set => System.Threading.Volatile.Write(ref _additions,value); }
        private static int _negations;
        public static int Negations { get => System.Threading.Volatile.Read(ref _negations); set => System.Threading.Volatile.Write(ref _negations,value); }
        private static int _conversions;
        public static int Conversions { get => System.Threading.Volatile.Read(ref _conversions); set => System.Threading.Volatile.Write(ref _conversions,value); }
        public static int operator +(Amount left, Amount right) { Additions++; return left.Tag + right.Tag; }
        public static int operator -(Amount value) { Negations++; return -value.Tag; }
        public static implicit operator int(Amount value) { Conversions++; return value.Tag; }
        public static int Add(Amount left,Amount right) => left + right;
        public int Negate() => -this;
        public int ToInt32() => (int)this;
    }
    public sealed class DerivedAmount(int tag) : Amount(tag)
    {
        private static int _wrongOperations;
        public static int WrongOperations { get => System.Threading.Volatile.Read(ref _wrongOperations); set => System.Threading.Volatile.Write(ref _wrongOperations,value); }
        public static int operator +(DerivedAmount left, DerivedAmount right) { WrongOperations++; return 999; }
        public static int operator -(DerivedAmount value) { WrongOperations++; return 999; }
        public static implicit operator int(DerivedAmount value) { WrongOperations++; return 999; }
        public static int Add(DerivedAmount left,DerivedAmount right) => left + right;
        public new int Negate() => -this;
        public new int ToInt32() => (int)this;
    }
    public sealed class AmountModel<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel where T : Amount
    {
        private PropertyChangedEventHandler? _changed;
        private T _value = default!;
        public T Value { get { Reads++; return _value; } set { _value = value; } }
        public int Reads { get; private set; }
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void SetValue(T value) { _value = value; _changed?.Invoke(this,new(nameof(Value))); }
    }
    public static class ConstraintSelectors
    {
        public static Expression<Func<T,int>> Value<T>() where T : Base => x => x.Value;
        public static Expression<Func<AmountModel<T>,int>> Sum<T>() where T : Amount => x => x.Value + x.Value;
        public static Expression<Func<AmountModel<T>,int>> Negate<T>() where T : Amount => x => -x.Value;
        public static Expression<Func<AmountModel<T>,int>> Converted<T>() where T : Amount => x => x.Value;
        public static Expression<Func<AmountModel<T>,byte>> Checked<T>() where T : Amount => x => checked((byte)(int)x.Value);
    }
    public sealed class IdentityModel<T> : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel where T : class
    {
        private PropertyChangedEventHandler? _changed;
        public T Left { get; set; } = default!;
        public T Right { get; private set; } = default!;
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public void SetRight(T value) { Right = value; _changed?.Invoke(this,new(nameof(Right))); }
    }
    public static class IdentitySelectors
    {
        public static Expression<Func<IdentityModel<T>,bool>> Same<T>() where T : class => x => x.Left == x.Right;
        public static Expression<Func<IdentityModel<T>,string?>> Name<T>() where T : class => x => nameof(T) + ":" + nameof(x.Left);
    }
    public static class Fixture
    {
        public static bool Check() => CheckValues() && CheckNullable() && CheckConstraint() && CheckOperators() && CheckIdentity();
        private static bool CheckValues()
        {
            var model = new Model<int>(); using var context = model.ValidationContext;
            using var stored = model.ValidationRule(Selectors<int>.Value,value => value == 9,"stored");
            using var factory = model.ValidationRule(SelectorFactories.Value<int>(),value => value == 9,"factory");
            using var nested = model.ValidationRule(Selectors<int>.Nested<string>.Value,value => value == 9,"nested");
            using var boxed = model.ValidationRule(Selectors<int>.Boxed,value => Equals(value,9),"boxed");
            using var nullable = model.ValidationRule(SelectorFactories.Nullable<int>(),value => value == 9,"nullable");
            using var indexed = model.ValidationRule(Selectors<int>.Indexed,value => value == 9,"indexed");
            var dynamicCode = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
            if (!dynamicCode && !LiftedConstructionNeedsDynamicCode()) return false;
            using var lifted = dynamicCode
                ? model.ValidationRule(LiftedSelectors<int>.Lifted,value => value == 7,"lifted")
                : model.ValidationRule<Model<int>,int?>(x => x.Number + x.Number,value => value == 7,"lifted");
            if (context.GetIsValid() || model.ValueReads != 5 || model.ArrayReads != 1 || model.IndexReads != 1) return false;
            model.SetValue(9);
            if (!stored.IsValid || !factory.IsValid || !nested.IsValid || !boxed.IsValid || !nullable.IsValid
                || indexed.IsValid || model.ValueReads != 10) return false;
            model.SetIndexed(9); if (!indexed.IsValid || model.ArrayReads != 2 || model.IndexReads != 2) return false;
            model.SetIndex(1); if (indexed.IsValid || model.ArrayReads != 3 || model.IndexReads != 3) return false;
            model.SetIndexed(9); if (!indexed.IsValid || model.ArrayReads != 4 || model.IndexReads != 4) return false;
            Count.Additions = 0; Count.Conversions = 0; model.SetNumber(new Count(3));
            if (!lifted.IsValid || !context.GetIsValid() || Count.Additions != 1 || Count.Conversions != 1) return false;
            model.SetNumber(null); if (lifted.IsValid || Count.Additions != 1 || Count.Conversions != 1) return false;
            stored.Dispose(); factory.Dispose(); nested.Dispose(); boxed.Dispose(); nullable.Dispose(); indexed.Dispose(); lifted.Dispose();
            return model.Listeners == 0 && !context.Validations.Items.Any();
        }
        private static bool LiftedConstructionNeedsDynamicCode()
        {
            try { _ = LiftedSelectors<int>.Lifted; return false; }
            catch (TypeInitializationException exception) when (exception.InnerException is NotSupportedException) { return true; }
        }
        private static bool CheckNullable()
        {
            var model = new NullableModel<string?>(); using var context = model.ValidationContext;
            using var rule = model.ValidationRule(NullableSelectors<string?>.Value,value => value == "ok","nullable text");
            if (rule.IsValid) return false; model.SetValue("ok"); if (!rule.IsValid) return false;
            model.SetValue(null); if (rule.IsValid) return false; rule.Dispose();
            return model.Listeners == 0 && !context.Validations.Items.Any();
        }
        private static bool CheckConstraint()
        {
            var model = new Hidden(); using var context = model.ValidationContext;
            using var rule = model.ValidationRule(ConstraintSelectors.Value<Hidden>(),value => value == 7,"constraint");
            if (rule.IsValid) return false; model.SetValue(7); if (!rule.IsValid) return false;
            model.SetValue(8); if (rule.IsValid) return false; rule.Dispose();
            return model.Listeners == 0 && !context.Validations.Items.Any();
        }
        private static bool CheckOperators()
        {
            var model = new AmountModel<DerivedAmount> { Value = new DerivedAmount(3) }; using var context = model.ValidationContext;
            Amount.Additions = 0; Amount.Negations = 0; Amount.Conversions = 0; DerivedAmount.WrongOperations = 0;
            using var sum = model.ValidationRule(ConstraintSelectors.Sum<DerivedAmount>(),value => value == 14,"sum");
            using var negate = model.ValidationRule(ConstraintSelectors.Negate<DerivedAmount>(),value => value == -7,"negative");
            using var converted = model.ValidationRule(ConstraintSelectors.Converted<DerivedAmount>(),value => value == 7,"convert");
            using var narrow = model.ValidationRule(ConstraintSelectors.Checked<DerivedAmount>(),value => value == 7,"checked");
            if (context.GetIsValid() || Amount.Additions != 1 || Amount.Negations != 1 || Amount.Conversions != 2 || model.Reads != 5) return false;
            model.SetValue(new DerivedAmount(7));
            if (!sum.IsValid || !negate.IsValid || !converted.IsValid || !narrow.IsValid || model.Reads != 10
                || Amount.Additions != 2 || Amount.Negations != 2 || Amount.Conversions != 4 || DerivedAmount.WrongOperations != 0) return false;
            try { model.SetValue(new DerivedAmount(256)); return false; }
            catch (OverflowException) { }
            sum.Dispose(); negate.Dispose(); converted.Dispose(); narrow.Dispose();
            return DerivedAmount.WrongOperations == 0 && model.Listeners == 0 && !context.Validations.Items.Any();
        }
        private static bool CheckIdentity()
        {
            var model = new IdentityModel<string> { Left = new string('x',3) }; model.SetRight(new string('x',3));
            using var context = model.ValidationContext;
            using var same = model.ValidationRule(IdentitySelectors.Same<string>(),value => value,"reference identity");
            using var name = model.ValidationRule(IdentitySelectors.Name<string>(),value => value == "T:Left","declaration name");
            if (same.IsValid || !name.IsValid || model.Left != model.Right) return false;
            model.SetRight(model.Left); if (!same.IsValid || !name.IsValid) return false;
            same.Dispose(); name.Dispose();
            return model.Listeners == 0 && !context.Validations.Items.Any();
        }
    }
}

namespace RuleCapabilityCases.ProtectedTarget
{
    public class Base : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        public string Message { get; protected set; } = "initial";
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        protected void Signal() => _changed?.Invoke(this,new(nameof(Message)));
    }
    public sealed class Model : Base
    {
        public static bool Check()
        {
            var model = new Model(); using var context = model.ValidationContext;
            using var rule = model.ValidationRule(x => x.Message,value => value == "written","message");
            if (rule.IsValid) return false;
            var target = new ValidationTarget<Model,string>(current => new ValidationWritePlan<string>(
                () => ValidationTargetAccess<string>.Present(current,value => { current.Message = value; current.Signal(); }),
                Array.Empty<ValidationDependency>()));
            var values = new Values();
            using var binding = target.Bind(model).Bind(values);
            if (model.Message != "written" || !rule.IsValid || !context.GetIsValid() || values.Listeners != 1) return false;
            binding.Dispose(); values.Next("after disposal");
            if (model.Message != "written" || !rule.IsValid || values.Listeners != 0) return false;
            rule.Dispose();
            return model.Listeners == 0 && !context.Validations.Items.Any();
        }
        private sealed class Values : IObservable<string>
        {
            private IObserver<string>? _observer;
            public int Listeners { get; private set; }
            public IDisposable Subscribe(IObserver<string> observer)
            {
                _observer = observer; Listeners++; observer.OnNext("written");
                return new Token(() => { _observer = null; Listeners--; });
            }
            public void Next(string value) => _observer?.OnNext(value);
        }
        private sealed class Token(Action cleanup) : IDisposable
        {
            private Action? _cleanup = cleanup;
            public void Dispose() => System.Threading.Interlocked.Exchange(ref _cleanup,null)?.Invoke();
        }
    }
}

namespace RuleCapabilityCases.IndexedBoundary
{
    [Flags]
    public enum Kind { None = 0, First = 4 }
    public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
    {
        private PropertyChangedEventHandler? _changed;
        private int _key = 2;
        internal static readonly int[] ParamsTail = [1,2];
        public int Key { get { Order += "F"; return _key; } }
        public int Second { get { Order += "S"; return 9; } }
        public string Order { get; private set; } = "";
        public int GetterCalls { get; private set; }
        public int Listeners { get; private set; }
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        public new event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Listeners++; }
            remove { _changed -= value; Listeners--; }
        }
        public string this[int first,int second=7,Kind kind=Kind.First,Kind? nullableKind=Kind.First,string? label=null]
        {
            get { GetterCalls++; return $"{first}/{second}/{kind}/{nullableKind}/{label ?? "null"}"; }
        }
        public string this[int first,params int[] rest]
        {
            get { GetterCalls++; return $"{first}/{string.Join(",",rest)}"; }
        }
        private void SetKey(int value) { _key = value; _changed?.Invoke(this,new(nameof(Key))); }
        public static bool Check()
        {
            var model = new Model(); using var context = model.ValidationContext;
            using var defaults = model.ValidationRule(x => x[x.Key],value => value == "2/7/First/First/null","defaults");
            if (!defaults.IsValid || model.Order != "F" || model.GetterCalls != 1) return false;
            model.Order = "";
            var namedSelector = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                () => { var second = source.Second; var first = source.Key; var value = source[second:second,first:first];
                    return ValidationRead<string?>.Present(value,
                        new[] { ValidationPath.Structural("Item[]",(first,second,Kind.First,(Kind?)Kind.First,(string?)null),
                            System.Collections.Generic.EqualityComparer<(int,int,Kind,Kind?,string?)>.Default) }); },
                new[] { ValidationDependency.PropertyChanged(() => source,nameof(Second)),
                    ValidationDependency.PropertyChanged(() => source,nameof(Key)) },
                ValidationObservationOptions<string?>.Default));
            using var named = ValidationRuntime.RegisterRule(model,context,namedSelector,
                value => value == "2/9/First/First/null" ? ValidationState.Valid : new ValidationState(false,"named"));
            if (!named.IsValid || model.Order != "SF" || model.GetterCalls != 2) return false;
            model.Order = "";
            using var spread = model.ValidationRule(x => x[x.Key,1,2],value => value == "2/1,2","params");
            if (!spread.IsValid || model.Order != "F" || model.GetterCalls != 3) return false;
            Expression<Func<Model,string?>> selected = x => x[x.Key];
            using var registry = new ValidationPlanRegistry(1); using var attachment = registry.Attach(model);
            var descriptor = new ValidationSelector<Model,string?>(source => new ValidationAccessPlan<string?>(
                () => { var key = source.Key; var value = source[key]; return ValidationRead<string?>.Present(value,
                    new[] { ValidationPath.Structural("Item[]",(key,7,Kind.First,(Kind?)Kind.First,(string?)null),
                        System.Collections.Generic.EqualityComparer<(int,int,Kind,Kind?,string?)>.Default) }); },
                new[] { ValidationDependency.PropertyChanged(() => source,nameof(Key)) },
                ValidationObservationOptions<string?>.Default));
            using var registration = registry.RegisterSelector(ValidationPlanRole.RuleValue,selected,descriptor);
            model.Order = "";
            using var typed = Attach(model,selected);
            if (!typed.IsValid || model.Order != "F" || model.GetterCalls != 4 || !context.GetIsValid()) return false;
            model.Order = ""; model.SetKey(3);
            if (defaults.IsValid || named.IsValid || spread.IsValid || typed.IsValid || context.GetIsValid()
                || model.Order != "FSFFF" || model.GetterCalls != 8) return false;
            defaults.Dispose(); named.Dispose(); spread.Dispose(); typed.Dispose(); registration.Dispose();
            var before = model.GetterCalls; model.SetKey(4);
            return model.Listeners == 0 && model.GetterCalls == before && !context.Validations.Items.Any() && CheckExpressionParams();
        }
        private static bool CheckExpressionParams()
        {
            var model = new Model(); using var context = model.ValidationContext;
            using var rule = model.ValidationRule(
                (Expression<Func<Model,string?>>)(source => source[source.Key,Model.ParamsTail]),
                value => value == "2/1,2", "preconstructed expression params");
            if (!rule.IsValid || model.Order != "F" || model.GetterCalls != 1 || !context.GetIsValid()) return false;
            model.Order = ""; model.SetKey(3);
            if (rule.IsValid || model.Order != "F" || model.GetterCalls != 2 || context.GetIsValid()) return false;
            rule.Dispose(); model.SetKey(4);
            return model.Listeners == 0 && model.GetterCalls == 2 && !context.Validations.Items.Any();
        }
        [ValidationRuntimeDispatch]
        private static ValidationHelper Attach(Model model,Expression<Func<Model,string?>> selected) =>
            model.ValidationRule(selected,value => value == "2/7/First/First/null","typed defaults");
    }
}
