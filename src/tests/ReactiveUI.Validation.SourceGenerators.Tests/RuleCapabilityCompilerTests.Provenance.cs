// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks finite selector provenance and explicit catalogs for runtime-selected expressions.</summary>
public sealed partial class RuleCapabilityCompilerTests
{
    /// <summary>Finite concrete factories and immutable storage preserve the exact selected property.</summary>
    private const string StableProvenanceSource = """
        using System;
        using System.ComponentModel;
        using System.Linq;
        using System.Linq.Expressions;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
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
        """;

    /// <summary>Runtime dispatch and rewritten storage use the exact registered expression and typed property plan.</summary>
    private const string RuntimeProvenanceSource = """
        using System;
        using System.ComponentModel;
        using System.Linq;
        using System.Linq.Expressions;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Capabilities;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        using __VALIDATION_ROOT__.Helpers;
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
        """;

    /// <summary>Constructed generic storage and generic factories retain the closed model and selected value type.</summary>
    private const string GenericProvenanceSource = """
        using System;
        using System.ComponentModel;
        using System.Linq;
        using System.Linq.Expressions;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        public static class Selectors<T>
        {
            public static readonly Expression<Func<Model<T>,T>> Value = x => x.Value;
            public static readonly Expression<Func<Model<T>,T>> Indexed = x => x.Values[x.Index];
            public static readonly Expression<Func<Model<T>,object?>> Boxed = x => (object)x.Value!;
            public static readonly Expression<Func<Model<T>,int?>> Lifted = x => x.Number + x.Number;
            public static class Nested<TUnused>
            {
                public static Expression<Func<Model<T>,T>> Value { get { return x => x.Value; } }
            }
        }
        public static class SelectorFactories
        {
            public static Expression<Func<Model<T>,T>> Value<T>() { return x => x.Value; }
            public static Expression<Func<Model<T>,T?>> Nullable<T>() where T : struct => x => (T?)x.Value;
        }
        public readonly struct Count(int value)
        {
            public int Value { get; } = value;
            public static int Additions;
            public static int Conversions;
            public static Count operator +(Count left, Count right) { Additions++; return new(left.Value + right.Value + 1); }
            public static implicit operator int(Count value) { Conversions++; return value.Value; }
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
        public sealed class Hidden : Base { public new int Value => throw new InvalidOperationException("hidden value"); }
        public class Amount(int tag)
        {
            public int Tag { get; } = tag;
            public static int Additions;
            public static int Negations;
            public static int Conversions;
            public static int operator +(Amount left, Amount right) { Additions++; return left.Tag + right.Tag; }
            public static int operator -(Amount value) { Negations++; return -value.Tag; }
            public static implicit operator int(Amount value) { Conversions++; return value.Tag; }
        }
        public sealed class DerivedAmount(int tag) : Amount(tag)
        {
            public static int WrongOperations;
            public static int operator +(DerivedAmount left, DerivedAmount right) { WrongOperations++; return 999; }
            public static int operator -(DerivedAmount value) { WrongOperations++; return 999; }
            public static implicit operator int(DerivedAmount value) { WrongOperations++; return 999; }
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
                using var lifted = model.ValidationRule(Selectors<int>.Lifted,value => value == 7,"lifted");
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
        """;

    /// <summary>The legal derived receiver owns its authored protected target and actual normal rule state.</summary>
    private const string ProtectedTargetSource = """
        using System;
        using System.ComponentModel;
        using System.Linq;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Capabilities;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
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
        """;

    /// <summary>Optional defaults and params preserve ordinary reads while reordered named operands use an authored factory.</summary>
    private const string IndexedBoundarySource = """
        using System;
        using System.ComponentModel;
        using System.Linq;
        using System.Linq.Expressions;
        using __UI_ROOT__;
        using __VALIDATION_ROOT__.Abstractions;
        using __VALIDATION_ROOT__.Capabilities;
        using __VALIDATION_ROOT__.Contexts;
        using __VALIDATION_ROOT__.Extensions;
        using __VALIDATION_ROOT__.Helpers;
        using __VALIDATION_ROOT__.States;
        public enum Kind { First = 4 }
        public sealed class Model : ReactiveObject, INotifyPropertyChanged, IValidatableViewModel
        {
            private PropertyChangedEventHandler? _changed;
            private int _key = 2;
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
                return model.Listeners == 0 && model.GetterCalls == before && !context.Validations.Items.Any();
            }
            [ValidationRuntimeDispatch]
            private static ValidationHelper Attach(Model model,Expression<Func<Model,string?>> selected) =>
                model.ValidationRule(selected,value => value == "2/7/First/First/null","typed defaults");
        }
        """;

    /// <summary>Finite source factories select their declared property without confusing it with another path.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and runtime assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StableFactoryProvenanceKeepsNameAndOtherDistinct(bool reactive)
    {
        var source = ExpandFixture(StableProvenanceSource, reactive);
        using var originalHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await originalHost.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Uses typed scoped catalogs after polymorphic dispatch and changed storage select another member.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and runtime assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RegisteredRuntimeProvenanceKeepsActualOtherSelection(bool reactive)
    {
        var source = ExpandFixture(RuntimeProvenanceSource, reactive);
        using var originalHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await originalHost.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Substitutes constructed storage and method type arguments before emitting normal rule reads.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and runtime assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConstructedGenericProvenanceKeepsClosedValueType(bool reactive)
    {
        var source = ExpandFixture(GenericProvenanceSource, reactive);
        using var originalHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await originalHost.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(FactoryEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Uses the legal declaring-class target factory with normal rule observation and owned disposal.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and runtime assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AuthoredProtectedTargetUsesLegalDerivedReceiver(bool reactive)
    {
        var source = ExpandFixture(ProtectedTargetSource, reactive);
        using var originalHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await originalHost.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }

    /// <summary>Preserves enum/nullable defaults, params and authored named-order reads with owned lifecycle.</summary>
    /// <param name="reactive">Whether to use the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and runtime assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IndexedDefaultsAndTypedNamedOrderRemainExact(bool reactive)
    {
        var source = ExpandFixture(IndexedBoundarySource, reactive);
        using var originalHost = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var original = await originalHost.RunAsync(source);
        await Assert.That(original.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        using var host = CapabilityCompilerHost.Create(reactive);
        var result = await host.RunAsync(source);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
        await Assert.That(result.ExecuteBoolean(ModelEntryType, EntryMethod)).IsTrue();
    }
}
