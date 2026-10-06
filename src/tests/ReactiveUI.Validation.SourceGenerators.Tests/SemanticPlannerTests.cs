// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI.Validation.SourceGenerators;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Executes shared semantic reads and lenses independently of overload frontends.</summary>
public sealed class SemanticPlannerTests
{
    /// <summary>The generated root variable in trusted probes.</summary>
    private const string RootVariable = "model";

    /// <summary>The test-only result carrier namespace.</summary>
    private const string ProofNamespace = "Proof";

    /// <summary>The trusted fixture's setup replacement slot.</summary>
    private const string SetupMarker = "__SETUP__";

    /// <summary>The trusted fixture's selector replacement slot.</summary>
    private const string SelectorMarker = "__SELECTOR__";

    /// <summary>The actionable typed descriptor route in planner diagnostics.</summary>
    private const string TypedSelectorAlternative = "typed ValidationSelector";

    /// <summary>The original selected fixture member.</summary>
    private const string ValueMember = "Value";

    /// <summary>The alternate selected fixture member.</summary>
    private const string BonusMember = "Bonus";

    /// <summary>The original fixture value.</summary>
    private const int DefaultSelectedValue = 2;

    /// <summary>The alternate fixture value.</summary>
    private const int AlternateSelectedValue = 3;

    /// <summary>The current Reactive runtime flavor namespace.</summary>
    private const string ReactiveNamespace = "ReactiveUI.Validation.Reactive";

    /// <summary>The current Primitives runtime flavor namespace.</summary>
    private const string PrimitivesNamespace = "ReactiveUI.Validation";

    /// <summary>The public constrained method-generic interception fixture.</summary>
    private const string GenericFixture = """
              using System;
              using System.Linq.Expressions;
              public interface ICarrier { int Value { get; } }
              public sealed class Model : ICarrier
              {
                  public int Value => 9;
                  public static bool Register<TSource,TValue>(TSource source,Expression<Func<TSource,TValue>> selector) where TSource:class => false;
                  public static bool Attach<T>(T model) where T:class,ICarrier => Register(model,x => ((ICarrier)x).Value);
                  public static bool Check() => Attach(new Model());
              }
              """;

    /// <summary>The lexical generic host fixture with coincident original payload slots.</summary>
    private const string HostedFixture = """
              using System;
              using System.Linq.Expressions;
              public sealed partial class Model<U>
              {
                  public int T => 7;
                  public Model<U> Self => this;
                  public static bool Register<TSource,TValue>(TSource source,Expression<Func<TSource,TValue>> selector) where TSource:class => false;
                  public static bool Attach<T>(Model<T> model) => Register(model,x => x.Self);
                  public static bool Check() => Model<int>.Attach<int>(new Model<int>());
              }
              public static class Probe { public static bool Check() => Model<int>.Check(); }
              """;

    /// <summary>Default static interface bodies and concrete implementations select different members.</summary>
    private const string StaticFactories = """
        public interface ISelector
        {
            static virtual Expression<Func<Model,int>> Selector => x => x.Value;
            static virtual Expression<Func<Model,int>> Create() => x => x.Value;
            static sealed Expression<Func<Model,int>> FixedSelector => x => x.Value;
            static sealed Expression<Func<Model,int>> FixedCreate() => x => x.Value;
        }
        public sealed class AlternateSelector : ISelector
        {
            public static Expression<Func<Model,int>> Selector => x => x.Bonus;
            public static Expression<Func<Model,int>> Create() => x => x.Bonus;
        }
        public static class ConcreteSelector
        {
            public static Expression<Func<Model,int>> Selector => x => x.Bonus;
            public static Expression<Func<Model,int>> Create() => x => x.Bonus;
        }
        """;

    /// <summary>The fixed trusted source fixture, with selector and executable probe slots.</summary>
    private const string Fixture = """
        using System;
        using System.Collections.Generic;
        using System.Collections.ObjectModel;
        using System.ComponentModel;
        using System.Linq.Expressions;
        public interface ICarrier { int Value { get; } }
        public struct Flag
        {
            public bool Value;
            public static int Combines;
            public static int TruthChecks;
            public static bool operator true(Flag value) { TruthChecks++; return value.Value; }
            public static bool operator false(Flag value) { TruthChecks++; return !value.Value; }
            public static Flag operator &(Flag left,Flag right) { Combines++; return new Flag { Value = left.Value && right.Value }; }
            public static Flag operator |(Flag left,Flag right) { Combines++; return new Flag { Value = left.Value || right.Value }; }
        }
        public enum Key { First = 1 }
        public struct Point { public int Value { get; set; } public int Kept { get; set; } }
        public struct Box { public Point Point { get; set; } }
        public sealed partial class Model : INotifyPropertyChanged, ICarrier
        {
            private PropertyChangedEventHandler? _changed;
            private int _value = 2;
            public int Listeners { get; private set; }
            public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Listeners++; } remove { _changed -= value; Listeners--; } }
            public void Signal(string name) => _changed?.Invoke(this,new PropertyChangedEventArgs(name));
            public Action? BeforeValueRead { get; set; }
            public int Value { get { BeforeValueRead?.Invoke(); return _value; } set { _value = value; } }
            int ICarrier.Value => 9;
            public int Bonus = 3;
            public bool UsePrimary { get; set; } = true;
            private Model? _other;
            private Flag _flag;
            public int OtherReads { get; private set; }
            public int FlagReads { get; private set; }
            public Action? BeforeFlagRead { get; set; }
            public Flag Flag { get { FlagReads++; BeforeFlagRead?.Invoke(); return _flag; } set { _flag = value; } }
            public Model? Other { get { OtherReads++; return _other; } set { _other = value; } }
            public dynamic Payload { get; set; } = "payload";
            public Model? Primary { get; set; }
            public Model? Secondary => throw new Exception("inactive branch");
            public string? Text { get; set; }
            public string Fallback => "fallback";
            public bool Throwing => throw new Exception("inactive operand");
            private string MetadataOnly => throw new Exception("metadata leaf");
            public Box Box { get; set; }
            public int SelectedIndex { get; set; }
            public int KeyReads { get; private set; }
            public int CurrentKey { get { KeyReads++; return SelectedIndex; } }
            public int ThrowingIndex => throw new Exception("inactive index");
            public ObservableCollection<Model> Children { get; } = new();
            public Dictionary<Key,int> EnumMap { get; } = new() { [Key.First] = 21 };
            public int IndexReads { get; private set; }
            public int Index { get { IndexReads++; return 0; } }
            public ObservableCollection<Point> Rows { get; } = new() { new Point { Value = 7, Kept = 42 } };
            public Dictionary<string,int> Map { get; } = new() { ["__runic_selector_root"] = 13 };
            public static void Register<TSource,TValue>(TSource source, Expression<Func<TSource,TValue>> selector) { }
            public void Plan()
            {
                __SETUP__
                Register(this, __SELECTOR__);
            }
            __PROBE__
        }
        namespace Proof.Capabilities
        {
            public sealed class ValidationPath
            {
                public object? Key { get; private init; }
                public string Display { get; private init; } = "";
                public static ValidationPath Legacy(string path) => new() { Display = path };
                public static ValidationPath Structural<TKey>(string path,TKey key,IEqualityComparer<TKey> comparer) => new() { Display=path, Key=key };
            }
            public readonly struct ValidationRead<T>
            {
                private ValidationRead(bool available,T value,IReadOnlyList<ValidationPath> paths) { HasOwner=available; Value=value; Paths=paths; }
                public bool HasOwner { get; }
                public T Value { get; }
                public IReadOnlyList<ValidationPath> Paths { get; }
                public static ValidationRead<T> Present(T value,IReadOnlyList<ValidationPath> paths) => new(true,value,paths);
                public static ValidationRead<T> Missing(IReadOnlyList<ValidationPath> paths) => new(false,default!,paths);
            }
        }
        """;

    /// <summary>Preserves casts, computation, branch order and ordinary nullable-leaf values.</summary>
    /// <param name="selector">The semantic selector.</param>
    /// <param name="type">The exact selected type.</param>
    /// <param name="setup">The model setup statements.</param>
    /// <param name="expectation">The behavior assertion.</param>
    /// <returns>The asynchronous assertion.</returns>
    /// <exception cref="InvalidOperationException">The trusted selector failed semantic planning.</exception>
    [Test]
    [Arguments("x => ((ICarrier)x).Value", "int", "", "read.HasOwner && read.Value == 9")]
    [Arguments("x => checked(x.Value + x.Bonus)", "int", "", "read.HasOwner && read.Value == 5")]
    [Arguments("x => x.UsePrimary ? x.Primary!.Value : x.Secondary!.Value", "int", "model.Primary = new Model { Value = 17 };", "read.HasOwner && read.Value == 17")]
    [Arguments("x => x.Text ?? x.Fallback", "string", "", "read.HasOwner && read.Value == \"fallback\"")]
    [Arguments("x => x.UsePrimary || x.Throwing", "bool", "", "read.HasOwner && read.Value")]
    [Arguments("x => x.Text", "string?", "", "read.HasOwner && read.Value is null")]
    [Arguments("x => x.Primary!.Value", "int", "", "!read.HasOwner")]
    public async Task SemanticReadPreservesEvaluation(string selector, string type, string setup, string expectation)
    {
        var (site, expression, source) = await PlanAsync(selector);
        if (!GeneratorHelpers.TrySelector(site, expression, out var plan, out var reason, false))
        {
            throw new InvalidOperationException(reason);
        }

        await Assert.That(reason).IsEmpty();
        var read = plan!.EmitRead(RootVariable, ProofNamespace);
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                {{setup}}
                Func<Proof.Capabilities.ValidationRead<{{type}}>> getter = {{read}};
                var read = getter();
                return {{expectation}};
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Preserves checked overflow instead of relying on a non-overflowing arithmetic example.</summary>
    /// <param name="selector">The checked operator expression.</param>
    /// <param name="value">The overflowing source operand.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments("x => checked(x.Value + x.Bonus)", "int.MaxValue")]
    [Arguments("x => checked(-x.Value)", "int.MinValue")]
    public async Task CheckedOperatorsRetainOverflow(string selector, string value)
    {
        var (site, expression, source) = await PlanAsync(selector);
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var plan, out _, false)).IsTrue();
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model { Value = {{value}}, Bonus = 1 };
                Func<Proof.Capabilities.ValidationRead<int>> read = {{plan!.EmitRead(RootVariable, ProofNamespace)}};
                try { _ = read(); return false; } catch (OverflowException) { return true; }
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Hoists branch keys without evaluating inactive getters in value or metadata reads.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task BranchIndexReadAndMetadataRetainActivation()
    {
        var (site, expression, source) = await PlanAsync("x => x.UsePrimary ? x.Rows[x.Index].Value : x.Rows[x.ThrowingIndex].Value");
        await Assert.That(GeneratorHelpers.TryMetadataSelector(site, expression, out var plan, out _)).IsTrue();
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                Func<Proof.Capabilities.ValidationRead<int>> read = {{plan!.EmitRead(RootVariable, ProofNamespace)}};
                Func<Proof.Capabilities.ValidationRead<Proof.Capabilities.ValidationPath>> metadata = {{plan.EmitMetadataRead(RootVariable, ProofNamespace)}};
                var value = read(); var paths = metadata();
                return value.Value == 7 && value.Paths.Count == 1 && paths.Paths.Count == 1 && model.IndexReads == 2
                    && ((string,int))value.Paths[0].Key! == ((string,int))paths.Paths[0].Key!;
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Keeps enum constants typed in dynamic index storage and metadata.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task EnumConstantRetainsDeclaredIndexType()
    {
        var (site, expression, source) = await PlanAsync("x => x.EnumMap[key]", "const Key key = Key.First;");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var plan, out _, false)).IsTrue();
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                Func<Proof.Capabilities.ValidationRead<int>> read = {{plan!.EmitRead(RootVariable, ProofNamespace)}};
                var value = read();
                return value.Value == 21 && ((string,Key))value.Paths[0].Key! is { Item2: Key.First };
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Allocates independent read caches when a second observer subscribes during the first leaf getter.</summary>
    /// <param name="reactive">The runtime flavor.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CachedOwnersAreAtomicAndIndependent(bool reactive)
    {
        var (site, expression, source) = await PlanAsync("x => x.Children[x.CurrentKey].Value");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var selector, out _)).IsTrue();
        var ns = reactive ? ReactiveNamespace : PrimitivesNamespace;
        var probe = $$"""
            private sealed class Capture<T> : IObserver<T>
            {
                public T Last = default!;
                public int Count;
                public void OnNext(T value) { Last = value; Count++; }
                public void OnError(Exception error) => throw error;
                public void OnCompleted() { }
            }
            public static bool Check()
            {
                var model = new Model();
                var firstRow = new Model { Value = 10 }; var secondRow = new Model { Value = 20 };
                model.Children.Add(firstRow); model.Children.Add(secondRow);
                var plan = {{selector!.EmitAccessPlan(RootVariable, ns)}};
                var first = new Capture<{{ns}}.Capabilities.ValidationRead<int>>();
                var second = new Capture<{{ns}}.Capabilities.ValidationRead<int>>();
                IDisposable? secondSubscription = null;
                firstRow.BeforeValueRead = () => {
                    firstRow.BeforeValueRead = null; model.SelectedIndex = 1;
                    secondSubscription = plan.Observe().Subscribe(second); model.SelectedIndex = 0;
                };
                var firstSubscription = plan.Observe().Subscribe(first);
                var atomic = model.KeyReads == 2 && firstRow.Listeners == 1 && secondRow.Listeners == 1
                    && first.Last.Value == 10 && second.Last.Value == 20
                    && first.Last.Paths[0].Equals({{selector.PathPlans[0].Emit(ns, ["0"])}})
                    && second.Last.Paths[0].Equals({{selector.PathPlans[0].Emit(ns, ["1"])}});
                firstSubscription.Dispose(); secondSubscription!.Dispose();
                return atomic && firstRow.Listeners == 0 && secondRow.Listeners == 0 && model.Listeners == 0;
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe, reactive)).IsTrue();
    }

    /// <summary>Does not acquire a coalesce fallback notification owner when its selected left leaf is present.</summary>
    /// <param name="reactive">The runtime flavor.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InactiveCoalesceOwnerIsNotEvaluated(bool reactive)
    {
        var (site, expression, source) = await PlanAsync("x => x.Text ?? x.Secondary!.Text");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var selector, out _)).IsTrue();
        var ns = reactive ? ReactiveNamespace : PrimitivesNamespace;
        var probe = $$"""
            private sealed class Capture : IObserver<{{ns}}.Capabilities.ValidationRead<string?>>
            {
                public string? Last;
                public void OnNext({{ns}}.Capabilities.ValidationRead<string?> value) => Last = value.Value;
                public void OnError(Exception error) => throw error;
                public void OnCompleted() { }
            }
            public static bool Check()
            {
                var model = new Model { Text = "selected" };
                var plan = {{selector!.EmitAccessPlan(RootVariable, ns)}};
                var observer = new Capture(); var subscription = plan.Observe().Subscribe(observer);
                subscription.Dispose(); return observer.Last == "selected" && model.Listeners == 0;
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe, reactive)).IsTrue();
    }

    /// <summary>Requires authoritative metadata for a computation and executes the actual typed alternative without reading its value.</summary>
    /// <param name="reactive">The runtime flavor.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ComputedPropertyMetadataUsesAuthoredIdentity(bool reactive)
    {
        var (site, expression, source) = await PlanAsync("x => x.Value + x.Bonus");
        await Assert.That(GeneratorHelpers.TryMetadataSelector(site, expression, out _, out var reason)).IsFalse();
        await Assert.That(reason.Contains("exactly one current path", StringComparison.Ordinal)).IsTrue();
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var rule, out _, false)).IsTrue();
        await Assert.That(rule!.Paths).Contains("Value");
        await Assert.That(rule.Paths).Contains("Bonus");
        var ns = reactive ? ReactiveNamespace : PrimitivesNamespace;
        var probe = $$"""
            private sealed class Capture : IObserver<IReadOnlyList<{{ns}}.Capabilities.ValidationPath>>
            {
                public int Count;
                public void OnNext(IReadOnlyList<{{ns}}.Capabilities.ValidationPath> paths) { if (paths.Count != 1) throw new Exception("metadata count"); Count++; }
                public void OnError(Exception error) => throw error;
                public void OnCompleted() { }
            }
            public static bool Check()
            {
                var model = new Model(); var reads = 0;
                var path = {{ns}}.Capabilities.ValidationPath.Structural("Computed", "Model.Value+Model.Bonus", EqualityComparer<string>.Default);
                var paths = new[] { path };
                var dependencies = new[] {
                    {{ns}}.Capabilities.ValidationDependency.PropertyChanged(() => model,"Value"),
                    {{ns}}.Capabilities.ValidationDependency.PropertyChanged(() => model,"Bonus")
                };
                var plan = new {{ns}}.Capabilities.ValidationAccessPlan<int>(
                    () => { reads++; return {{ns}}.Capabilities.ValidationRead<int>.Present(model.Value+model.Bonus,paths); },
                    dependencies, {{ns}}.Capabilities.ValidationObservationOptions<int>.Default, () => paths, dependencies);
                var observer = new Capture(); var subscription = plan.ObservePaths().Subscribe(observer);
                model.Signal("Value"); var metadataOnly = reads == 0 && observer.Count == 2;
                subscription.Dispose(); var value = plan.Read();
                return metadataOnly && reads == 1 && value.Value == 5 && value.Paths[0].Equals(path) && model.Listeners == 0;
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe, reactive)).IsTrue();
    }

    /// <summary>Preserves exact custom truth operators, lazy owner registration, missing-owner exits and user exceptions.</summary>
    /// <param name="reactive">The runtime flavor.</param>
    /// <param name="or">Whether the custom operator is conditional OR.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task CustomTruthOperatorsPreserveAtomicDependencies(bool reactive, bool or)
    {
        var (site, expression, source) = await PlanAsync(or ? "x => x.Primary!.Flag || x.Other!.Flag" : "x => x.Primary!.Flag && x.Other!.Flag");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var selector, out _)).IsTrue();
        var ns = reactive ? ReactiveNamespace : PrimitivesNamespace;
        var skip = or ? "true" : "false";
        var active = or ? "false" : "true";
        var probe = $$"""
            private sealed class Capture : IObserver<{{ns}}.Capabilities.ValidationRead<Flag>>
            {
                public {{ns}}.Capabilities.ValidationRead<Flag> Last;
                public int Count;
                public void OnNext({{ns}}.Capabilities.ValidationRead<Flag> value) { Last = value; Count++; }
                public void OnError(Exception error) => throw error;
                public void OnCompleted() { }
            }
            public static bool Check()
            {
                var model = new Model(); var left = new Model { Flag = new Flag { Value = {{skip}} } };
                var right = new Model { Flag = new Flag { Value = true } }; model.Primary = left; model.Other = right;
                Flag.Combines = 0; Flag.TruthChecks = 0;
                var plan = {{selector!.EmitAccessPlan(RootVariable, ns)}};
                var observer = new Capture(); var subscription = plan.Observe().Subscribe(observer);
                if (model.OtherReads != 0 || right.FlagReads != 0 || right.Listeners != 0 || Flag.Combines != 0
                    || Flag.TruthChecks != 1 || observer.Last.Paths.Count != 1 || observer.Count != 1) return false;
                left.Flag = new Flag { Value = {{active}} }; left.Signal("Flag");
                if (model.OtherReads != 1 || right.FlagReads != 1 || right.Listeners != 1 || Flag.Combines != 1
                    || Flag.TruthChecks != 2 || observer.Last.Paths.Count != 2 || observer.Count != 2) return false;
                left.Flag = new Flag { Value = {{skip}} }; left.Signal("Flag");
                if (right.Listeners != 0 || observer.Last.Paths.Count != 1 || Flag.Combines != 1) return false;
                subscription.Dispose(); left.Flag = new Flag { Value = {{active}} }; model.Other = null; Flag.Combines = 0;
                var missing = plan.Read(); if (missing.HasOwner || Flag.Combines != 0) return false;
                model.Other = right; var error = new InvalidOperationException("user error"); right.BeforeFlagRead = () => throw error;
                try { _ = plan.Read(); return false; } catch (InvalidOperationException actual) {
                    return ReferenceEquals(actual,error) && Flag.Combines == 0 && model.Listeners == 0 && left.Listeners == 0 && right.Listeners == 0;
                }
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe, reactive)).IsTrue();
    }

    /// <summary>Rejects generated DLR conversion while preserving an explicit static object bridge.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task DynamicReadRequiresStaticObjectConversion()
    {
        var (badSite, badExpression, _) = await PlanAsync("x => (string)x.Payload");
        await Assert.That(GeneratorHelpers.TrySelector(badSite, badExpression, out _, out var reason, false)).IsFalse();
        await Assert.That(reason).Contains(TypedSelectorAlternative);
        var (site, expression, source) = await PlanAsync("x => (string)(object)x.Payload");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var selector, out _, false)).IsTrue();
        var read = selector!.EmitRead(RootVariable, ProofNamespace);
        await Assert.That(read).DoesNotContain("dynamic");
        await Assert.That(read).DoesNotContain("RuntimeBinder");
        var probe = $$"""
            public static bool Check() { var model = new Model(); Func<Proof.Capabilities.ValidationRead<string>> getter = {{read}}; return getter().Value == "payload"; }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Peels only a harmless read-side reference cast while retaining the actual writable storage type.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task TargetReferenceWideningKeepsActualLeaf()
    {
        var (site, expression, source) = await PlanAsync("x => (object)x.Text!");
        await Assert.That(GeneratorHelpers.TryAccessPlan(site, expression, out var target, out _)).IsTrue();
        await Assert.That(target!.ValueType.SpecialType == SpecialType.System_String).IsTrue();
        var probe = $$"""
            public static bool Check() { var model = new Model(); Action<string> assign = value => {{target.EmitAssignment(RootVariable, "value")}}; assign("stored"); return model.Text == "stored"; }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Uses the same acquired index for both selected value and structural metadata.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task IndexSnapshotMatchesReadAndMetadata()
    {
        var (site, expression, source) = await PlanAsync("x => x.Rows[x.Index].Value");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var plan, out _, false)).IsTrue();
        var read = plan!.EmitRead(RootVariable, ProofNamespace);
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                Func<Proof.Capabilities.ValidationRead<int>> getter = {{read}};
                var read = getter();
                var key = ((string,int))read.Paths[0].Key!;
                return read.HasOwner && read.Value == 7 && model.IndexReads == 1 && key.Item2 == 0;
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
        await Assert.That(plan.Paths).Contains("Rows[Index].Value");
        await Assert.That(plan.Paths).DoesNotContain("Index");
    }

    /// <summary>Never calls the selected leaf getter merely to produce binding metadata.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task MetadataDoesNotEvaluateThrowingLeaf()
    {
        var (site, expression, source) = await PlanAsync("x => x.Secondary");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var plan, out _, false)).IsTrue();
        var read = plan!.EmitMetadataRead(RootVariable, ProofNamespace);
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                Func<Proof.Capabilities.ValidationRead<Proof.Capabilities.ValidationPath>> getter = {{read}};
                var read = getter();
                return read.HasOwner && read.Value.Display == "Secondary";
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Private final metadata does not require a leaf accessor or lexical host.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task PrivateFinalMetadataNeedsNoAccessor()
    {
        var (site, expression, source) = await PlanAsync("x => x.MetadataOnly");
        await Assert.That(GeneratorHelpers.TryMetadataSelector(site, expression, out var selector, out _)).IsTrue();
        await Assert.That(site.RequiresLexicalAccess).IsFalse();
        await Assert.That(site.AdditionalSources).IsEmpty();
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                Func<Proof.Capabilities.ValidationRead<Proof.Capabilities.ValidationPath>> read = {{selector!.EmitMetadataRead(RootVariable, ProofNamespace)}};
                return read().Value.Display == "MetadataOnly";
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Writes nested struct values outward and acquires dynamic keys only once.</summary>
    /// <param name="selector">The actual target path.</param>
    /// <param name="expectation">The mutated storage assertion.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments("x => x.Box.Point.Value", "model.Box.Point.Value == 23")]
    [Arguments("x => x.Rows[x.Index].Value", "model.Rows[0].Value == 23 && model.Rows[0].Kept == 42 && model.IndexReads == 1")]
    public async Task LensMutatesOwnedStorage(string selector, string expectation)
    {
        var (site, expression, source) = await PlanAsync(selector);
        await Assert.That(GeneratorHelpers.TryAccessPlan(site, expression, out var plan, out var reason, false)).IsTrue();
        await Assert.That(reason).IsEmpty();
        var write = plan!.EmitAssignment(RootVariable, "value");
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                Action<int> assign = value => {{write}};
                assign(23);
                return {{expectation}};
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Preserves marker-like strings while rebinding only the generated root token.</summary>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    public async Task StringKeysRemainUnchanged()
    {
        var (site, expression, source) = await PlanAsync("x => x.Map[\"__runic_selector_root\"]");
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out var plan, out _, false)).IsTrue();
        var read = plan!.EmitRead(RootVariable, ProofNamespace);
        var probe = $$"""
            public static bool Check()
            {
                var model = new Model();
                Func<Proof.Capabilities.ValidationRead<int>> getter = {{read}};
                return getter().Value == 13;
            }
            """;
        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Recovers finite locals and rejects writes or borrowed aliases before freezing provenance.</summary>
    /// <param name="setup">The selector storage declaration.</param>
    /// <param name="supported">Whether provenance is immutable.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments("Expression<Func<Model,int>> stored = x => x.Value;", true)]
    [Arguments("Expression<Func<Model,int>> stored = x => x.Value; stored = x => x.Bonus;", false)]
    [Arguments("Expression<Func<Model,int>> stored = x => x.Value; Expression<Func<Model,int>> other = x => x.Bonus; (stored,other) = (other,stored);", false)]
    [Arguments("Expression<Func<Model,int>> stored = x => x.Value; ref var alias = ref stored; alias = x => x.Bonus;", false)]
    public async Task ProvenanceDoesNotFreezeChangedStorage(string setup, bool supported)
    {
        var (site, expression, _) = await PlanAsync("stored", setup);
        await Assert.That(GeneratorHelpers.TrySelector(site, expression, out _, out var reason, false)).IsEqualTo(supported);
        if (!supported)
        {
            await Assert.That(reason).Contains("typed");
        }
    }

    /// <summary>Never substitutes a default interface body for its actual static polymorphic implementation.</summary>
    /// <param name="expression">The selected static factory contract.</param>
    /// <param name="supported">Whether dispatch has a finite source body.</param>
    /// <param name="other">Whether actual dispatch selects the alternate value.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments("TSelector.Selector", false, true)]
    [Arguments("TSelector.Create()", false, true)]
    [Arguments("ConcreteSelector.Selector", true, true)]
    [Arguments("ConcreteSelector.Create()", true, true)]
    [Arguments("ISelector.FixedSelector", true, false)]
    [Arguments("ISelector.FixedCreate()", true, false)]
    public async Task StaticFactoryProvenancePreservesDispatch(string expression, bool supported, bool other)
    {
        var source = Fixture.Replace(SetupMarker, string.Empty).Replace(SelectorMarker, expression)
            .Replace("public void Plan()", "public void Plan<TSelector>() where TSelector : ISelector") + StaticFactories;
        var (site, syntax, _) = await PlanSourceAsync(source);
        await Assert.That(GeneratorHelpers.TrySelector(site, syntax, out var selector, out var reason, false)).IsEqualTo(supported);
        var probe = supported
            ? $$"""
                public static bool Check()
                {
                    var model = new Model();
                    Func<Proof.Capabilities.ValidationRead<int>> read = {{selector!.EmitRead(RootVariable, ProofNamespace)}};
                    return read().Value == {{(other ? AlternateSelectedValue : DefaultSelectedValue)}};
                }
                """
            : StaticRegistrationProbe(expression, other);
        if (!supported)
        {
            await Assert.That(reason).Contains("Polymorphic static");
            await Assert.That(reason).Contains(TypedSelectorAlternative);
        }

        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Checks readonly initialization writes and borrowed aliases across all partial declarations.</summary>
    /// <param name="initialization">The actual static constructor statements.</param>
    /// <param name="supported">Whether the field initializer remains final.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments("", true)]
    [Arguments("Selector = x => x.Bonus;", false)]
    [Arguments("Expression<Func<Model,int>> other = x => x.Bonus; (Selector,other) = (other,Selector);", false)]
    [Arguments("Change(ref Selector);", false)]
    public async Task StaticReadonlyProvenanceChecksPartialInitialization(string initialization, bool supported)
    {
        var source = Fixture.Replace(SetupMarker, string.Empty).Replace(SelectorMarker, "StoredSelector.Selector")
            + $$"""
                public static partial class StoredSelector
                {
                    public static readonly Expression<Func<Model,int>> Selector = x => x.Value;
                }
                public static partial class StoredSelector
                {
                    static StoredSelector() { {{initialization}} }
                    private static void Change(ref Expression<Func<Model,int>> selector) { selector = x => x.Bonus; }
                }
                """ + StaticFactories;
        var (site, syntax, _) = await PlanSourceAsync(source);
        await Assert.That(GeneratorHelpers.TrySelector(site, syntax, out var selector, out var reason, false)).IsEqualTo(supported);
        var probe = supported
            ? $$"""
                public static bool Check()
                {
                    var model = new Model();
                    Func<Proof.Capabilities.ValidationRead<int>> read = {{selector!.EmitRead(RootVariable, ProofNamespace)}};
                    return read().Value == {{DefaultSelectedValue}};
                }
                """
            : StaticRegistrationProbe("StoredSelector.Selector", true);
        if (!supported)
        {
            await Assert.That(reason).Contains(TypedSelectorAlternative);
        }

        await Assert.That(await ExecuteAsync(source, probe)).IsTrue();
    }

    /// <summary>Proves original method arity under actual Roslyn interception, including distinct coincident payload slots.</summary>
    /// <param name="hosted">Whether a lexical generic host is needed.</param>
    /// <returns>The asynchronous assertion.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [SuppressMessage("Usage", "RSEXPERIMENTAL002", Justification = "The pinned Roslyn 5.9 encoded-location API is the shipping interceptor contract.")]
    public async Task GenericInterceptorsPreserveOriginalArity(bool hosted)
    {
        var source = hosted ? HostedFixture : GenericFixture;
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false);
        var original = await host.RunAsync(source);
        var tree = original.Compilation.SyntaxTrees.First();
        var semantic = original.Compilation.GetSemanticModel(tree);
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static node => node.Expression.ToString() == "Register");
        var method = (IMethodSymbol)semantic.GetSymbolInfo(invocation).Symbol!;
        var attribute = semantic.GetInterceptableLocation(invocation)!.GetInterceptsLocationAttributeSyntax();
        var site = new CallSite(semantic, invocation, method, ProofNamespace, attribute, 0);
        var body = hosted ? "return @source.@T == 7 && @selector is not null;" : "return ((global::ICarrier)(object)@source).Value == 9;";
        await Assert.That(HostedPlanEmitter.TryWrap(site, body, out var interceptor, out var reason)).IsTrue();
        await Assert.That(reason).IsEmpty();
        var bridge = $$"""
            // <auto-generated/>
            #nullable enable
            namespace System.Runtime.CompilerServices
            {
                [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]
                file sealed class InterceptsLocationAttribute : global::System.Attribute
                {
                    public InterceptsLocationAttribute(int version,string data) { }
                }
            }
            namespace ReactiveUI.Validation.Generated.Proof
            {
                internal static class Interceptors
                {
                    {{interceptor}}
                }
            }
            """;
        var added = site.AdditionalSources.Select(fragment => CSharpSyntaxTree.ParseText(fragment.Source, (CSharpParseOptions)tree.Options, fragment.HintName))
            .Append(CSharpSyntaxTree.ParseText(bridge, (CSharpParseOptions)tree.Options, "PlannerBridge.g.cs"));
        var result = original with { Compilation = original.Compilation.AddSyntaxTrees(added) };
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.ExecuteBoolean(hosted ? "Probe" : "Model", "Check")).IsTrue();
    }

    /// <summary>Builds a fixed trusted semantic call fixture.</summary>
    /// <param name="selector">The selector expression.</param>
    /// <param name="setup">Optional local storage declarations.</param>
    /// <returns>The original call and selector syntax with source.</returns>
    private static async Task<(CallSite Site, ExpressionSyntax Expression, string Source)> PlanAsync(string selector, string setup = "")
    {
        var source = Fixture.Replace(SetupMarker, setup).Replace(SelectorMarker, selector);
        return await PlanSourceAsync(source);
    }

    /// <summary>Builds a trusted fixture while preserving its original invocation and defining members.</summary>
    /// <param name="source">The complete source fixture.</param>
    /// <returns>The call and selector syntax with original source.</returns>
    private static async Task<(CallSite Site, ExpressionSyntax Expression, string Source)> PlanSourceAsync(string source)
    {
        using var host = CapabilityCompilerHost.Create(false, includeValidation: false);
        var result = await host.RunAsync(source.Replace("__PROBE__", string.Empty));
        var compilation = result.Compilation;
        var tree = compilation.SyntaxTrees.First();
        var model = compilation.GetSemanticModel(tree);
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static invocation => invocation.Expression.ToString() == "Register");
        var method = (IMethodSymbol)model.GetSymbolInfo(invocation).Symbol!;
        var site = new CallSite(model, invocation, method, ProofNamespace, string.Empty, 0);
        return (site, site.GetArgument("selector")!, source);
    }

    /// <summary>Executes the typed scoped catalog alternative with the actual expression instance and closed value choice.</summary>
    /// <param name="expression">The runtime factory or initialized field.</param>
    /// <param name="other">Whether the final expression selects the alternate value.</param>
    /// <returns>The bounded managed behavior probe.</returns>
    private static string StaticRegistrationProbe(string expression, bool other) => $$"""
        public static bool Check() => Check<AlternateSelector>();
        private static bool Check<TSelector>() where TSelector : ISelector
        {
            var model = new Model(); var original = {{expression}};
            var path = ReactiveUI.Validation.Capabilities.ValidationPath.Legacy("{{(other ? BonusMember : ValueMember)}}");
            var paths = new[] { path };
            var descriptor = new ReactiveUI.Validation.Capabilities.ValidationSelector<Model,int>(source =>
                new ReactiveUI.Validation.Capabilities.ValidationAccessPlan<int>(
                    () => ReactiveUI.Validation.Capabilities.ValidationRead<int>.Present(source.{{(other ? BonusMember : ValueMember)}},paths),
                    Array.Empty<ReactiveUI.Validation.Capabilities.ValidationDependency>(),
                    ReactiveUI.Validation.Capabilities.ValidationObservationOptions<int>.Default));
            using var registry = new ReactiveUI.Validation.Capabilities.ValidationPlanRegistry(1);
            using var attachment = registry.Attach(model);
            using var registration = registry.RegisterSelector(ReactiveUI.Validation.Capabilities.ValidationPlanRole.RuleValue,original,descriptor);
            var resolved = ReactiveUI.Validation.Capabilities.ValidationRuntime.ResolveSelector(model,original,
                ReactiveUI.Validation.Capabilities.ValidationPlanRole.RuleValue,string.Empty).Bind(model).Read();
            return ((MemberExpression)original.Body).Member.Name == "{{(other ? BonusMember : ValueMember)}}"
                && resolved.Value == {{(other ? AlternateSelectedValue : DefaultSelectedValue)}} && resolved.Paths[0].Equals(path);
        }
        """;

    /// <summary>Compiles and executes emitted direct code in the shared bounded host.</summary>
    /// <param name="source">The original source fixture.</param>
    /// <param name="probe">The generated Boolean behavior probe.</param>
    /// <param name="reactive">The actual runtime flavor for capability observation probes.</param>
    /// <returns>Whether the actual compiled operation satisfies its assertion.</returns>
    /// <exception cref="InvalidOperationException">The emitted operation did not compile.</exception>
    private static async Task<bool> ExecuteAsync(string source, string probe, bool reactive = false)
    {
        using var host = CapabilityCompilerHost.Create(reactive, includeValidation: false);
        var result = await host.RunAsync(source.Replace("__PROBE__", probe));
        if (result.CompilationDiagnostics.Any(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
        {
            throw new InvalidOperationException(probe + string.Join(Environment.NewLine, result.CompilationDiagnostics));
        }

        return result.ExecuteBoolean("Model", "Check");
    }
}
