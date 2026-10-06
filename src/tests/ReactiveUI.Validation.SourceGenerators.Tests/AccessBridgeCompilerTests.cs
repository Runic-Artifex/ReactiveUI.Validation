// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI.Validation.SourceGenerators;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Proves explicitly selected static access signatures against the actual .NET 10 compiler and runtime.</summary>
public sealed partial class AccessBridgeCompilerTests
{
    /// <summary>The fixed source marker replaced with generated bridge invocations.</summary>
    private const string BodyMarker = "// BRIDGE_BODY";

    /// <summary>The original lexical private-reference fixture body.</summary>
    private const string PrivateBodyMarker = "// PRIVATE_BRIDGE_BODY";

    /// <summary>The exact test marker contract; runtime package markers have the same suffix in their flavor namespace.</summary>
    private const string Marker = "[Proof.Validation.Capabilities.GeneratedValidationAccess]";

    /// <summary>The actual init property selected by the fixed source fixture.</summary>
    private const string InitialMemberName = "Initial";

    /// <summary>The exact storage property used by generic and friend assembly fixtures.</summary>
    private const string ValueMemberName = "Value";

    /// <summary>The exact readonly field used by value-owner and nullable-input fixtures.</summary>
    private const string StorageMemberName = "Storage";

    /// <summary>The stable target expression in fixed fixture invocations.</summary>
    private const string TargetExpression = "target";

    /// <summary>The stable owner expression in fixed fixture invocations.</summary>
    private const string OwnerExpression = "owner";

    /// <summary>The fixed nullable output expression accepted by annotated writer inputs.</summary>
    private const string AbsentExpression = "absent";

    /// <summary>The exact declaring reference-type constraint retained by generic accessor bridges.</summary>
    private const string ReferenceConstraint = "where __T0 : class";

    /// <summary>The exact CLR name of the inaccessible reference owner.</summary>
    private const string HiddenTypeName = "Proof.Consumer.Host+Hidden";

    /// <summary>The exact CLR name of the private generic reference owner.</summary>
    private const string HiddenGenericTypeName = "Proof.Consumer.Host+HiddenGeneric`1";

    /// <summary>The original fixture's selected marker/runtime namespace.</summary>
    private const string RuntimeNamespace = "Proof.Validation";

    /// <summary>The fixed private context property selected in the caller's legal lexical scope.</summary>
    private const string ContextMemberName = "Context";

    /// <summary>The exact erased signature attribute excluded from typed lexical intrinsics.</summary>
    private const string TypeAttributeName = "UnsafeAccessorType";

    /// <summary>The private child value and nested owner fixture name.</summary>
    private const string ChildMemberName = "Child";

    /// <summary>The consumer with an original legal private setter scope and an unrelated executable host.</summary>
    private const string FriendConsumerFixture = """
        using System;
        namespace Proof.Consumer
        {
            public sealed class ConsumerOwner
            {
                private string Value { get; set; } = "original";
                public string Snapshot => Value;
                public static void Tick() { }
                public static void Plan() => Tick();
            }
            public static class Probe
            {
                public static bool Run()
                {
                    // BRIDGE_BODY
                    return true;
                }
            }
        }
        """;

    /// <summary>The exact CLR storage with independently annotated writer inputs.</summary>
    private const string NullableInputFixture = """
        namespace Proof.Consumer
        {
            public sealed class NullableTarget
            {
                private string _value = "original";
                [global::System.Diagnostics.CodeAnalysis.AllowNull]
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                public readonly string Storage = "original";
                [global::System.Diagnostics.CodeAnalysis.AllowNull]
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                public string Value { get => _value; set => _value = value ?? "member cleared"; }
                public string Parameter { get => _value; [param: global::System.Diagnostics.CodeAnalysis.AllowNull] set => _value = value ?? "parameter cleared"; }
                public string? Narrow { get; [param: global::System.Diagnostics.CodeAnalysis.DisallowNull] set; }
                [global::System.Diagnostics.CodeAnalysis.DisallowNull]
                public string? Conflict { get; [param: global::System.Diagnostics.CodeAnalysis.AllowNull] set; }
            }
        }
        """;

    /// <summary>The generic indexed setter whose input nullability differs from its readable value.</summary>
    private const string IndexedInputFixture = """
        namespace Proof.Consumer
        {
            public sealed class IndexedTarget<T> where T : class
            {
                private readonly T _fallback;
                private T _value;
                public IndexedTarget(T fallback) => _value = _fallback = fallback;
                public int Writes { get; private set; }
                public int Reads { get; private set; }
                public int ObservedFirst { get; private set; }
                public decimal ObservedSecond { get; private set; }
                public T this[int first, decimal second]
                {
                    get { Reads++; return _value; }
                    [param: global::System.Diagnostics.CodeAnalysis.AllowNull]
                    set { Writes++; ObservedFirst = first; ObservedSecond = second; _value = value ?? _fallback; }
                }
            }
        }
        """;

    /// <summary>The actual storage whose output postconditions differ from its declared nullable types.</summary>
    private const string ReadPostconditionFixture = """
        namespace Proof.Consumer
        {
            public sealed class ReadTarget
            {
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                [global::System.Diagnostics.CodeAnalysis.NotNull]
                public string? Certain { private get; set; } = "certain";
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                public string? ReturnCertain { [return: global::System.Diagnostics.CodeAnalysis.NotNull] private get => "returned"; set { } }
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                [global::System.Diagnostics.CodeAnalysis.MaybeNull]
                public string Possible { private get => null; set { } }
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                [global::System.Diagnostics.CodeAnalysis.NotNull]
                public readonly string? CertainStorage = "field";
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                [global::System.Diagnostics.CodeAnalysis.MaybeNull]
                public readonly string PossibleStorage = "maybe field";
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                [global::System.Diagnostics.CodeAnalysis.NotNull]
                public int? Number { private get => 42; set { } }
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                [global::System.Diagnostics.CodeAnalysis.NotNull]
                public readonly int? NumberStorage = 42;
            }
        }
        """;

    /// <summary>The nullable value storage whose input contract forbids null without changing its CLR type.</summary>
    private const string NullableValueInputFixture = """
        namespace Proof.Consumer
        {
            public readonly record struct InputPayload(int Value);
            public sealed class ValueInputTarget
            {
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                public int? Number { get; [param: global::System.Diagnostics.CodeAnalysis.DisallowNull] set; } = 0;
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                [global::System.Diagnostics.CodeAnalysis.DisallowNull]
                public readonly InputPayload? Storage = new InputPayload(0);
            }
        }
        """;

    /// <summary>Builds real existing target storage without expression compilation or reflection-based access.</summary>
    private const string Fixture = """
        #nullable enable
        using System;
        namespace Proof.Validation.Capabilities
        {
            [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
            public sealed class GeneratedValidationAccessAttribute : Attribute { }
        }
        namespace Proof.Consumer
        {
            public sealed class Target<T> where T : class
            {
                MARKER private readonly string _storage = "original";
                MARKER public string Initial { get; init; } = "original";
                MARKER public T? Private { private get; set; }
                public string Snapshot => _storage;
            }
            public struct ValueTarget<T> where T : class
            {
                MARKER public readonly T? Storage = null;
                MARKER public T? Initial { get; init; }
                public ValueTarget() { }
            }
            public sealed class Outer<T> where T : class
            {
                public sealed class Inner<U> where U : class, new()
                {
                    MARKER public T? Value { get; private set; }
                }
            }
            public static partial class Host
            {
                public static object Create() => new Hidden();
                public static string ReadInitial(object target) => ((Hidden)target).Initial;
                public static string ReadStorage(object target) => ((Hidden)target)._storage;
                public static object CreateGeneric() => new HiddenGeneric<string>();
                public static string? ReadGeneric(object target) => ((HiddenGeneric<string>)target).Value;
                public static bool PrivateReferenceProbe()
                {
                    var owner = new Hidden();
                    var child = new Hidden();
                    _ = owner;
                    _ = child;
                    // PRIVATE_BRIDGE_BODY
                    return true;
                }
                private sealed class Hidden
                {
                    MARKER internal readonly string _storage = "original";
                    MARKER internal string Initial { get; init; } = "original";
                    MARKER internal Hidden? Self = null;
                    MARKER internal Hidden? Child { get; private set; }
                    MARKER internal Hidden[]? Children { get; private set; }
                }
                private struct PrivateValue
                {
                    MARKER internal int Value = 0;
                    public PrivateValue() { }
                }
                private sealed class HiddenGeneric<T> where T : class
                {
                    MARKER public T? Value { get; private set; }
                }
                private class ConstraintBase { }
                private sealed class HiddenConstrained<T> where T : ConstraintBase
                {
                    MARKER public T? Value { get; private set; }
                    private string _indexed = "original";
                    MARKER public string this[T key]
                    {
                        get => _indexed;
                        [param: global::System.Diagnostics.CodeAnalysis.AllowNull]
                        set => _indexed = value ?? "cleared";
                    }
                }
            }
            public sealed class Unmarked
            {
                public string Initial { get; init; } = "original";
                MARKER public string GetterOnly { get; } = "original";
                private int _storage;
                MARKER public ref int Reference => ref _storage;
                public string SetterSelected { private get; [Proof.Validation.Capabilities.GeneratedValidationAccess] set; } = "original";
                public string GetterSelected { [Proof.Validation.Capabilities.GeneratedValidationAccess] get; private set; } = "original";
            }
            public class VirtualBase
            {
                MARKER public virtual string Initial { get; init; } = "base";
            }
            public sealed class VirtualDerived : VirtualBase
            {
                public int Writes { get; private set; }
                public override string Initial { get; init { field = value; Writes++; } } = "derived";
            }
            public static class Probe
            {
                public static void Tick() { }
                public static bool Run()
                {
                    Tick();
                    // BRIDGE_BODY
                    return true;
                }
            }
        }
        """;

    /// <summary>Preserves init setter lookup, exact readonly storage and private accessor execution on generic targets.</summary>
    /// <returns>The asynchronous test assertions.</returns>
    [Test]
    public async Task SelectedExistingStorageCompilesAndExecutes()
    {
        var proof = Create();
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.Target`1")!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        var initial = Emit(proof, owner.GetMembers(InitialMemberName).Single(), true);
        var storage = Emit(proof, owner.GetMembers("_storage").Single(), true);
        var setter = Emit(proof, owner.GetMembers("Private").Single(), true);
        var getter = Emit(proof, owner.GetMembers("Private").Single(), false);
        var body = $$"""
            var target = new Target<string>();
            {{Invoke(initial, TargetExpression, "\"after construction\"")}};
            {{Invoke(storage, TargetExpression, "\"readonly changed\"")}};
            {{Invoke(setter, TargetExpression, "\"private changed\"")}};
            if (target.Initial != "after construction" || target.Snapshot != "readonly changed" || {{Invoke(getter, TargetExpression)}} != "private changed")
                throw new InvalidOperationException("The generated static bridge did not mutate the actual existing owner.");
            """;
        await Execute(proof, body);
        var emitted = string.Join("\n", proof.Site.AdditionalSources.Select(static source => source.Source));
        await Assert.That(emitted).Contains("class AccessBridge0_0<__T0>");
        await Assert.That(emitted).Contains(ReferenceConstraint);
        await Assert.That(emitted).Contains("Target<__T0>");
        await Assert.That(emitted).Contains("Name = \"set_Initial\"");
        await Assert.That(emitted).Contains("Name = \"_storage\"");
        await Assert.That(emitted).DoesNotContain("<Initial>k__BackingField");
    }

    /// <summary>Uses ref owner signatures to mutate explicitly selected existing value-type storage.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task AccessibleValueOwnerPreservesByrefStorage()
    {
        var proof = Create();
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.ValueTarget`1")!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        var initial = Emit(proof, owner.GetMembers(InitialMemberName).Single(), true);
        var storage = Emit(proof, owner.GetMembers(StorageMemberName).Single(), true);
        var read = Emit(proof, owner.GetMembers(StorageMemberName).Single(), false);
        await Execute(proof, $$"""
            var owner = new ValueTarget<string>();
            {{Invoke(initial, OwnerExpression, "\"value init\"")}};
            {{Invoke(storage, OwnerExpression, "\"value readonly\"")}};
            if (owner.Initial != "value init" || owner.Storage != "value readonly" || {{Invoke(read, OwnerExpression)}} != "value readonly")
                throw new InvalidOperationException("The value owner bridge mutated a detached copy.");
            """);
        await Assert.That(proof.Site.AdditionalSources[0].Source).Contains("ref global::Proof.Consumer.ValueTarget<__T0> owner");
        await Assert.That(initial).Contains("Write(ref __RUNIC_OWNER__");
    }

    /// <summary>Preserves containing and nested declaring generic parameters in VAR order.</summary>
    /// <returns>The asynchronous test assertions.</returns>
    [Test]
    public async Task NestedGenericDeclaringPositionsAndConstraintsExecute()
    {
        var proof = Create();
        var outer = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.Outer`1")!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        var inner = outer.GetTypeMembers("Inner").Single().Construct(proof.Compilation.GetSpecialType(SpecialType.System_Object));
        var setter = Emit(proof, inner.GetMembers(ValueMemberName).Single(), true);
        await Execute(proof, $$"""
            var target = new Outer<string>.Inner<object>();
            {{Invoke(setter, TargetExpression, "\"nested\"")}};
            if (target.Value != "nested") throw new InvalidOperationException("Nested declaring VAR signature mismatch.");
            """);
        var emitted = proof.Site.AdditionalSources.Single().Source;
        await Assert.That(emitted).Contains("Outer<__T0>.Inner<__T1>");
        await Assert.That(emitted).Contains("where __T1 : class, new()");
        await Assert.That(emitted).DoesNotContain("Write<");
    }

    /// <summary>Uses .NET 10 erased reference owners while retaining a known field return type.</summary>
    /// <returns>The asynchronous test assertions.</returns>
    [Test]
    public async Task InaccessibleReferenceOwnerUsesExactMetadataIdentity()
    {
        var proof = Create();
        var owner = proof.Compilation.GetTypeByMetadataName(HiddenTypeName)!;
        var initial = Emit(proof, owner.GetMembers(InitialMemberName).Single(), true);
        var storage = Emit(proof, owner.GetMembers("_storage").Single(), true);
        await Execute(proof, $$"""
            var target = Host.Create();
            {{Invoke(initial, TargetExpression, "\"hidden init\"")}};
            {{Invoke(storage, TargetExpression, "\"hidden readonly\"")}};
            if (Host.ReadInitial(target) != "hidden init" || Host.ReadStorage(target) != "hidden readonly")
                throw new InvalidOperationException("The erased reference owner did not resolve exact storage.");
            """);
        await Assert.That(proof.Site.AdditionalSources[0].Source).Contains($"Proof.Consumer.Host+Hidden, {proof.Compilation.AssemblyName}");
        await Assert.That(proof.Site.AdditionalSources[1].Source).Contains("extern ref global::System.String Storage");
    }

    /// <summary>Preserves VAR placeholders inside .NET 10 metadata identities for private generic reference owners.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task InaccessibleGenericReferenceOwnerPreservesMetadataVars()
    {
        var proof = Create();
        var owner = proof.Compilation.GetTypeByMetadataName(HiddenGenericTypeName)!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        var setter = Emit(proof, owner.GetMembers(ValueMemberName).Single(), true);
        await Execute(proof, $$"""
            var target = Host.CreateGeneric();
            {{Invoke(setter, TargetExpression, "\"generic erased\"")}};
            if (Host.ReadGeneric(target) != "generic erased") throw new InvalidOperationException("The private generic reference owner lost VAR signature identity.");
            """);
        await Assert.That(proof.Site.AdditionalSources.Single().Source).Contains("global::Proof.Consumer.Host.HiddenGeneric<__T0> owner");
        await Assert.That(proof.Site.AdditionalSources.Single().Source).DoesNotContain(TypeAttributeName);
        await Assert.That(proof.Site.AdditionalSources.Single().Source).Contains(ReferenceConstraint);
    }

    /// <summary>Uses reference-only erased parameter and return signatures while preserving the caller's legal private cast.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task InaccessibleReferenceValueUsesCallerLexicalCast()
    {
        var proof = Create();
        var owner = proof.Compilation.GetTypeByMetadataName(HiddenTypeName)!;
        var setter = Emit(proof, owner.GetMembers("Child").Single(), true);
        var getter = Emit(proof, owner.GetMembers("Child").Single(), false);
        var body = $$"""
            {{Invoke(setter, OwnerExpression, "child")}};
            if (!ReferenceEquals({{Invoke(getter, OwnerExpression)}}, child)) throw new InvalidOperationException("The erased private reference value changed identity.");
            """;
        var lexical = proof with { Source = proof.Source.Replace(PrivateBodyMarker, body, StringComparison.Ordinal) };
        await Execute(lexical, "if (!Host.PrivateReferenceProbe()) throw new InvalidOperationException(\"Lexical private reference fixture failed.\");");
        await Assert.That(proof.Site.AdditionalSources[1].Source).Contains("[return: global::System.Runtime.CompilerServices.UnsafeAccessorType");
        await Assert.That(getter).Contains("global::Proof.Consumer.Host.Hidden?");
    }

    /// <summary>Keeps an erased private-reference array suffix in the type name before full assembly qualification.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task InaccessibleReferenceArrayPreservesFullAssemblyIdentity()
    {
        var proof = Create();
        var owner = proof.Compilation.GetTypeByMetadataName(HiddenTypeName)!;
        var setter = Emit(proof, owner.GetMembers("Children").Single(), true);
        var getter = Emit(proof, owner.GetMembers("Children").Single(), false);
        var body = $$"""
            {{Invoke(setter, OwnerExpression, "new[] { child }")}};
            if (!ReferenceEquals({{Invoke(getter, OwnerExpression)}}![0], child)) throw new InvalidOperationException("The erased private reference array changed identity.");
            """;
        var lexical = proof with { Source = proof.Source.Replace(PrivateBodyMarker, body, StringComparison.Ordinal) };
        await Execute(lexical, "if (!Host.PrivateReferenceProbe()) throw new InvalidOperationException(\"Lexical private reference array fixture failed.\");");
        await Assert.That(proof.Site.AdditionalSources[0].Source).Contains($"Proof.Consumer.Host+Hidden[], {proof.Compilation.Assembly.Identity.GetDisplayName()}");
        await Assert.That(proof.Site.AdditionalSources[1].Source).Contains("Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
    }

    /// <summary>Preserves virtual getter/setter dispatch when the existing selected member is declared on a base type.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task SelectedVirtualAccessorsPreserveOverrideDispatch()
    {
        var proof = Create();
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.VirtualBase")!;
        var setter = Emit(proof, owner.GetMembers(InitialMemberName).Single(), true);
        var getter = Emit(proof, owner.GetMembers(InitialMemberName).Single(), false);
        await Execute(proof, $$"""
            var derived = new VirtualDerived();
            VirtualBase target = derived;
            {{Invoke(setter, TargetExpression, "\"override\"")}};
            if (target.Initial != "override" || {{Invoke(getter, TargetExpression)}} != "override" || derived.Writes != 1)
                throw new InvalidOperationException("The existing selected accessor lost virtual override dispatch.");
            """);
    }

    /// <summary>Preserves ordinary private helper/context reads and ordinary private writes in an unrelated generated host.</summary>
    /// <param name="reactive">Whether actual signatures name the System.Reactive flavor.</param>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LegalOriginalPrivateAccessExecutesAcrossUnrelatedHosts(bool reactive)
    {
        var root = reactive ? "ReactiveUI.Validation.Reactive" : "ReactiveUI.Validation";
        var source = $$"""
            namespace Proof.Consumer
            {
                public sealed class SourceModel
                {
                    private global::{{root}}.Helpers.ValidationHelper? Helper { get { HelperReads++; return null; } }
                    private global::{{root}}.Contexts.IValidationContext? Context { get { ContextReads++; return null; } }
                    private string Target { get; set; } = "original";
                    public int HelperReads { get; private set; }
                    public int ContextReads { get; private set; }
                    public string Snapshot => Target;
                    public static void SourceAnchor() { }
                    public static void LegalCaller() => SourceAnchor();
                }
            }
            """;
        var proof = Create(source);
        var syntax = (await proof.Tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static expression => expression.Expression.ToString() == "SourceAnchor");
        var model = proof.Compilation.GetSemanticModel(proof.Tree);
        var method = (IMethodSymbol)model.GetSymbolInfo(syntax).Symbol!;
        var site = new CallSite(model, syntax, method, RuntimeNamespace, string.Empty, 0);
        var lexical = proof with { Site = site };
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.SourceModel")!;
        var helper = Emit(lexical, owner.GetMembers("Helper").Single(), false);
        var context = Emit(lexical, owner.GetMembers(ContextMemberName).Single(), false);
        var setter = Emit(lexical, owner.GetMembers("Target").Single(), true);
        site.AdditionalSources.Add(new("UnrelatedPrivateAccessHost.g.cs", $$"""
            #nullable enable
            namespace Proof.Consumer;
            internal static class UnrelatedPrivateAccessHost
            {
                internal static void Run(SourceModel owner)
                {
                    if ({{Invoke(helper, OwnerExpression)}} is not null || {{Invoke(context, OwnerExpression)}} is not null)
                        throw new global::System.InvalidOperationException("Private typed getter signature mismatch.");
                    {{Invoke(setter, OwnerExpression, "\"ordinary private write\"")}};
                }
            }
            """));
        await Execute(lexical, """
            var owner = new SourceModel();
            UnrelatedPrivateAccessHost.Run(owner);
            if (owner.HelperReads != 1 || owner.ContextReads != 1 || owner.Snapshot != "ordinary private write")
                throw new InvalidOperationException("Legal original private access was not preserved across generated host placement.");
            """);
    }

    /// <summary>Uses the original exact receiver type for automatic protected access, retaining an illegal through-type control.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task ProtectedAccessRequiresTheLegalOriginalThroughType()
    {
        const string source = """
            namespace Proof.Consumer
            {
                public class ProtectedBase { protected string Target { get; set; } = "original"; }
                public sealed class ProtectedDerived : ProtectedBase
                {
                    public string Snapshot => Target;
                    public static void ProtectedAnchor() { }
                    public static void LegalCaller() => ProtectedAnchor();
                }
            }
            """;
        var proof = Create(source);
        var syntax = (await proof.Tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static expression => expression.Expression.ToString() == "ProtectedAnchor");
        var model = proof.Compilation.GetSemanticModel(proof.Tree);
        var site = new CallSite(model, syntax, (IMethodSymbol)model.GetSymbolInfo(syntax).Symbol!, RuntimeNamespace, string.Empty, 0);
        var declaring = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.ProtectedBase")!;
        var through = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.ProtectedDerived")!;
        var member = declaring.GetMembers("Target").Single();
        await Assert.That(AccessBridgeEmitter.TryEmit(site, member, true, out var setter, out _, through)).IsTrue();
        await Assert.That(AccessBridgeEmitter.TryEmit(site, member, false, out var getter, out _, through)).IsTrue();
        var count = site.AdditionalSources.Count;
        await Assert.That(AccessBridgeEmitter.TryEmit(site, member, false, out _, out var failure, declaring)).IsFalse();
        await Assert.That(failure).Contains("typed");
        await Assert.That(site.AdditionalSources.Count).IsEqualTo(count);
        await Execute(proof with { Site = site }, $$"""
            var owner = new ProtectedDerived();
            {{Invoke(setter, OwnerExpression, "\"protected write\"")}};
            if (owner.Snapshot != "protected write" || {{Invoke(getter, OwnerExpression)}} != "protected write")
                throw new InvalidOperationException("The original legal protected receiver was not preserved.");
            """);
    }

    /// <summary>Leaves an originally illegal private access as a compiler error and emits no bypass.</summary>
    /// <returns>The asynchronous compiler assertions.</returns>
    [Test]
    public async Task OriginallyIllegalAccessCannotReceiveAnAutomaticBridge()
    {
        const string source = """
            namespace Proof.Consumer
            {
                public static class IllegalCaller
                {
                    public static void Call()
                    {
                        var owner = new Unmarked();
                        _ = owner.SetterSelected;
                        Probe.Tick();
                    }
                }
            }
            """;
        var proof = Create(source);
        var syntax = (await proof.Tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static expression => expression.Expression.ToString() == "Probe.Tick");
        var model = proof.Compilation.GetSemanticModel(proof.Tree);
        var site = new CallSite(model, syntax, (IMethodSymbol)model.GetSymbolInfo(syntax).Symbol!, RuntimeNamespace, string.Empty, 0);
        var member = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.Unmarked")!.GetMembers("SetterSelected").Single();
        await Assert.That(AccessBridgeEmitter.TryEmit(site, member, false, out _, out _)).IsFalse();
        await Assert.That(site.AdditionalSources).IsEmpty();
        await Assert.That(proof.Compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS0271")).IsTrue();
    }

    /// <summary>Rejects absent selection, absent setters, borrowed byrefs and unsupported erased signatures before emission.</summary>
    /// <param name="typeName">The actual declaring type.</param>
    /// <param name="memberName">The selected actual member.</param>
    /// <param name="write">Whether assignment is requested.</param>
    /// <param name="reason">The actionable reason required.</param>
    /// <returns>The asynchronous test assertions.</returns>
    [Test]
    [Arguments("Proof.Consumer.Unmarked", InitialMemberName, true, "explicit GeneratedValidationAccess")]
    [Arguments("Proof.Consumer.Unmarked", "GetterOnly", true, "getter-only")]
    [Arguments("Proof.Consumer.Unmarked", "Reference", false, "borrowed byref property")]
    [Arguments("Proof.Consumer.Unmarked", "SetterSelected", false, "explicit GeneratedValidationAccess")]
    [Arguments("Proof.Consumer.Unmarked", "GetterSelected", true, "explicit GeneratedValidationAccess")]
    [Arguments("Proof.Consumer.Host+PrivateValue", "Value", true, "inaccessible reference signatures only")]
    [Arguments(HiddenTypeName, "Self", false, "inaccessible byref field return")]
    [Arguments("Proof.Consumer.Host+HiddenConstrained`1", "Value", true, "declaring generic constraints")]
    public async Task UnsupportedSignaturesProduceTypedAlternatives(string typeName, string memberName, bool write, string reason)
    {
        var proof = Create();
        var member = proof.Compilation.GetTypeByMetadataName(typeName)!.GetMembers(memberName).Single();
        await Assert.That(AccessBridgeEmitter.TryEmit(proof.Site, member, write, out var invocation, out var failure)).IsFalse();
        await Assert.That(invocation).IsEmpty();
        await Assert.That(proof.Site.AdditionalSources).IsEmpty();
        await Assert.That(failure).Contains(reason);
        await Assert.That(failure).Contains("typed");
    }

    /// <summary>Proves runtime matching rejects both closed-generic lookup and method/type generic position drift.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task ClosedGenericAndMethodGenericDriftAreRejectedAtRuntime()
    {
        var proof = Create();
        const string invalid = """
            #nullable enable
            namespace Proof.Consumer;
            internal static class ClosedDrift
            {
                [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_Private")]
                internal static extern void Write(Target<string> owner, string value);
            }
            internal static class MethodDrift
            {
                [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_Private")]
                internal static extern void Write<T>(Target<T> owner, T value) where T : class;
            }
            """;
        proof.Site.AdditionalSources.Add(new("InvalidGenericSignatures.g.cs", invalid));
        await Execute(proof, """
            var target = new Target<string>();
            var rejected = 0;
            try { ClosedDrift.Write(target, "closed"); }
            catch (MissingMethodException) { rejected++; }
            try { MethodDrift.Write(target, "method"); }
            catch (MissingMethodException) { rejected++; }
            if (rejected != 2) throw new InvalidOperationException("A mismatched generic signature unexpectedly matched actual storage.");
            """);
    }

    /// <summary>Refuses unknown predicted member trees even when they copy the explicit marker.</summary>
    /// <returns>The asynchronous compiler assertions.</returns>
    [Test]
    public async Task UnknownProducerProjectionIsNotExistingStorage()
    {
        var proof = Create();
        var tree = CSharpSyntaxTree.ParseText(
            """
            namespace Proof.Consumer;
            public sealed class UnknownPredicted
            {
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                public string Initial { get; init; } = "predicted";
            }
            """,
            new(LanguageVersion.CSharp14),
            "UnknownPrediction.cs");
        var projected = proof.Compilation.AddSyntaxTrees(tree);
        var member = projected.GetTypeByMetadataName("Proof.Consumer.UnknownPredicted")!.GetMembers(InitialMemberName).Single();
        var site = new CallSite(projected.GetSemanticModel(proof.Tree), proof.Site.Invocation, proof.Site.Method, RuntimeNamespace, string.Empty, 0, originalCompilation: proof.Compilation);
        await Assert.That(AccessBridgeEmitter.TryEmit(site, member, true, out _, out var failure)).IsFalse();
        await Assert.That(failure).Contains("actual declared member");
        await Assert.That(site.AdditionalSources).IsEmpty();
    }

    /// <summary>Allows only the finite known producer member and executes its final emitted storage declaration.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task KnownProducerContractExecutesFinalDeclaredStorage()
    {
        var proof = Create();
        var tree = CSharpSyntaxTree.ParseText(
            """
            namespace Proof.Consumer;
            public sealed class KnownPredicted
            {
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                public string Initial { get; init; } = "predicted";
                [Proof.Validation.Capabilities.GeneratedValidationAccess]
                public string Other { get; init; } = "unselected";
            }
            """,
            new(LanguageVersion.CSharp14),
            "KnownProducerOutput.cs");
        var projected = proof.Compilation.AddSyntaxTrees(tree);
        var owner = projected.GetTypeByMetadataName("Proof.Consumer.KnownPredicted")!;
        var member = owner.GetMembers(InitialMemberName).Single();
        var site = new CallSite(projected.GetSemanticModel(proof.Tree), proof.Site.Invocation, proof.Site.Method, RuntimeNamespace, string.Empty, 0, originalCompilation: proof.Compilation)
        {
            ProducerMemberKeys = [CallSite.MemberContractKey(member)],
        };
        await Assert.That(AccessBridgeEmitter.TryEmit(site, member, true, out var setter, out _)).IsTrue();
        await Assert.That(AccessBridgeEmitter.TryEmit(site, owner.GetMembers("Other").Single(), true, out _, out _)).IsFalse();
        await Assert.That(site.AdditionalSources.Count).IsEqualTo(1);
        await Execute(proof with { Compilation = projected, Site = site }, $$"""
            var owner = new KnownPredicted();
            {{Invoke(setter, OwnerExpression, "\"actual producer storage\"")}};
            if (owner.Initial != "actual producer storage" || owner.Other != "unselected")
                throw new InvalidOperationException("The finite producer contract did not match its actual final storage.");
            """);
    }

    /// <summary>Refuses a same-name marker contract from an assembly other than the selected runtime.</summary>
    /// <returns>The asynchronous compiler assertions.</returns>
    [Test]
    public async Task MarkerMustBelongToTheSelectedRuntimeAssembly()
    {
        var proof = Create();
        var otherRuntime = proof.Compilation.GetSpecialType(SpecialType.System_Object).GetMembers(nameof(ReferenceEquals)).OfType<IMethodSymbol>().Single();
        var site = new CallSite(proof.Site.Model, proof.Site.Invocation, otherRuntime, RuntimeNamespace, string.Empty, 0);
        var member = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.Target`1")!.GetMembers(InitialMemberName).Single();
        await Assert.That(AccessBridgeEmitter.TryEmit(site, member, true, out _, out var failure)).IsFalse();
        await Assert.That(failure).Contains("matching Validation runtime");
        await Assert.That(site.AdditionalSources).IsEmpty();
    }

    /// <summary>Compiles a real friend peer with the same bridge IDs and preserves distinct assembly-isolated type names.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task FriendAssembliesWithIdenticalBridgeIdsDoNotCollide()
    {
        var identity = Guid.NewGuid().ToString("N");
        var peerName = $"AccessBridgeFriend.{identity}";
        var consumerName = $"AccessBridgeFriend-{identity}";
        var peer = CreateSource(
            $$"""
            [assembly: global::System.Runtime.CompilerServices.InternalsVisibleTo("{{consumerName}}")]
            namespace Friend.Consumer;
            public sealed class PeerOwner
            {
                private string Value { get; set; } = "original";
                public string Snapshot => Value;
                public static void Tick() { }
                public static bool Run()
                {
                    Tick();
                    // BRIDGE_BODY
                    return true;
                }
            }
            """,
            peerName,
            []);
        var peerOwner = peer.Compilation.GetTypeByMetadataName("Friend.Consumer.PeerOwner")!;
        var peerWrite = Emit(peer, peerOwner.GetMembers(ValueMemberName).Single(), true);
        var peerBinary = await Compile(peer, $$"""
            var owner = new PeerOwner();
            {{Invoke(peerWrite, OwnerExpression, "\"peer write\"")}};
            if (owner.Snapshot != "peer write") throw new global::System.InvalidOperationException("Peer bridge changed its storage contract.");
            """);
        var peerReference = MetadataReference.CreateFromImage(peerBinary);
        var consumer = CreateSource(FriendConsumerFixture, consumerName, [peerReference]);
        var importedName = $"ReactiveUI.Validation.Generated.{ValidationGenerator.AssemblyIdentity(peerName)}.Accessors.AccessBridge0_0";
        var importedBridge = consumer.Compilation.GetTypeByMetadataName(importedName)!;
        await Assert.That(consumer.Compilation.IsSymbolAccessibleWithin(importedBridge, consumer.Compilation.Assembly)).IsTrue();
        var consumerOwner = consumer.Compilation.GetTypeByMetadataName("Proof.Consumer.ConsumerOwner")!;
        var consumerWrite = Emit(consumer, consumerOwner.GetMembers(ValueMemberName).Single(), true);
        await Execute(consumer, $$"""
            var owner = new ConsumerOwner();
            {{Invoke(consumerWrite, OwnerExpression, "\"consumer write\"")}};
            if (owner.Snapshot != "consumer write") throw new InvalidOperationException("Consumer bridge did not use its own existing storage.");
            """);
    }

    /// <summary>Preserves nullable setter and field input separately from getter and CLR signatures.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task NullableWriteInputsPreserveReadableContracts()
    {
        var proof = Create(NullableInputFixture);
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.NullableTarget")!;
        var member = owner.GetMembers(ValueMemberName).Single();
        var parameter = owner.GetMembers("Parameter").Single();
        var narrow = owner.GetMembers("Narrow").Single();
        var field = owner.GetMembers(StorageMemberName).Single();
        await Assert.That(AccessBridgeEmitter.RequiresWriteContractBridge(parameter)).IsTrue();
        await Assert.That(AccessBridgeEmitter.RequiresWriteContractBridge(member)).IsFalse();
        await Assert.That(AccessBridgeEmitter.RequiresWriteContractBridge(narrow)).IsFalse();
        await Assert.That(AccessBridgeEmitter.RequiresWriteContractBridge(owner.GetMembers("Conflict").Single())).IsFalse();
        var writeMember = Emit(proof, member, true);
        var readMember = Emit(proof, member, false);
        var writeParameter = Emit(proof, parameter, true);
        var writeField = Emit(proof, field, true);
        var readField = Emit(proof, field, false);
        var writeNarrow = Emit(proof, narrow, true);
        var readNarrow = Emit(proof, narrow, false);
        await Execute(proof, $$"""
            var owner = new NullableTarget();
            string? absent = null;
            {{Invoke(writeMember, OwnerExpression, AbsentExpression)}};
            if ({{Invoke(readMember, OwnerExpression)}} != "member cleared") throw new InvalidOperationException("Member input annotation was lost.");
            {{Invoke(writeParameter, OwnerExpression, AbsentExpression)}};
            if ({{Invoke(readMember, OwnerExpression)}} != "parameter cleared") throw new InvalidOperationException("Setter parameter input annotation was lost.");
            {{Invoke(writeField, OwnerExpression, AbsentExpression)}};
            if ({{Invoke(readField, OwnerExpression)}} != null) throw new InvalidOperationException("Readonly field nullable input was lost.");
            {{Invoke(writeNarrow, OwnerExpression, "\"narrow\"")}};
            if ({{Invoke(readNarrow, OwnerExpression)}} != "narrow") throw new InvalidOperationException("DisallowNull changed the CLR storage signature.");
            """);
        await Assert.That(proof.Site.AdditionalSources[0].Source).Contains("global::System.String? value");
        await Assert.That(proof.Site.AdditionalSources[1].Source).Contains("extern global::System.String Read");
        await Assert.That(proof.Site.AdditionalSources[3].Source).Contains("extern ref global::System.String? Storage");
        await Assert.That(proof.Site.AdditionalSources[4].Source).Contains("extern ref global::System.String Storage");
        await Assert.That(proof.Site.AdditionalSources[5].Source).Contains("global::System.String value");
        await Assert.That(proof.Site.AdditionalSources[6].Source).Contains("extern global::System.String? Read");
    }

    /// <summary>Executes closed declaring VAR indexed access with nullable input and already acquired keys.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task IndexedNullableSetterPreservesGenericSignatureAndCachedKeys()
    {
        var proof = Create(IndexedInputFixture);
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.IndexedTarget`1")!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        var member = owner.GetMembers().OfType<IPropertySymbol>().Single(static property => property.IsIndexer);
        await Assert.That(AccessBridgeEmitter.RequiresWriteContractBridge(member)).IsTrue();
        var setter = Emit(proof, member, true)
            .Replace(AccessBridgeEmitter.IndexPlaceholder(0), "first", StringComparison.Ordinal)
            .Replace(AccessBridgeEmitter.IndexPlaceholder(1), "second", StringComparison.Ordinal);
        var getter = Emit(proof, member, false)
            .Replace(AccessBridgeEmitter.IndexPlaceholder(0), "first", StringComparison.Ordinal)
            .Replace(AccessBridgeEmitter.IndexPlaceholder(1), "second", StringComparison.Ordinal);
        await Execute(proof, $$"""
            var owner = new IndexedTarget<string>("cleared");
            var firstEvaluations = 0;
            var secondEvaluations = 0;
            var first = ++firstEvaluations;
            var second = (decimal)++secondEvaluations;
            string? absent = null;
            {{Invoke(setter, OwnerExpression, AbsentExpression)}};
            if (owner.Writes != 1 || owner.Reads != 0 || owner.ObservedFirst != first || owner.ObservedSecond != second || firstEvaluations != 1 || secondEvaluations != 1)
                throw new InvalidOperationException("The indexed setter changed cached key evaluation or read the selected leaf.");
            if ({{Invoke(getter, OwnerExpression)}} != "cleared" || owner.Reads != 1)
                throw new InvalidOperationException("The exact generic indexed accessor input or getter signature was lost.");
            """);
        await Assert.That(proof.Site.AdditionalSources[0].Source).Contains(ReferenceConstraint);
        await Assert.That(proof.Site.AdditionalSources[0].Source).Contains("global::System.Int32 index0, global::System.Decimal index1, __T0? value");
        await Assert.That(proof.Site.AdditionalSources[1].Source).Contains("extern __T0 Read");
    }

    /// <summary>Preserves property, getter-return and field output flow without strengthening MaybeNull reads.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task ReadPostconditionsPreserveReferenceAndNullableValueFlow()
    {
        var proof = Create(ReadPostconditionFixture);
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.ReadTarget")!;
        var certain = Emit(proof, owner.GetMembers("Certain").Single(), false);
        var returned = Emit(proof, owner.GetMembers("ReturnCertain").Single(), false);
        var possible = Emit(proof, owner.GetMembers("Possible").Single(), false);
        var field = Emit(proof, owner.GetMembers("CertainStorage").Single(), false);
        var possibleField = Emit(proof, owner.GetMembers("PossibleStorage").Single(), false);
        var number = Emit(proof, owner.GetMembers("Number").Single(), false);
        var numberField = Emit(proof, owner.GetMembers("NumberStorage").Single(), false);
        await Execute(proof, $$"""
            var owner = new ReadTarget();
            static int Require(string value) => value.Length;
            if (Require({{Invoke(certain, OwnerExpression)}}) != "certain".Length
                || Require({{Invoke(returned, OwnerExpression)}}) != "returned".Length
                || Require({{Invoke(field, OwnerExpression)}}) != "field".Length)
                throw new InvalidOperationException("A declared non-null read postcondition was lost.");
            string? possible = {{Invoke(possible, OwnerExpression)}};
            string? possibleField = {{Invoke(possibleField, OwnerExpression)}};
            if (possible != null || possibleField != "maybe field") throw new InvalidOperationException("MaybeNull changed actual getter or field results.");
            if ({{Invoke(number, OwnerExpression)}}.Value != 42 || {{Invoke(numberField, OwnerExpression)}}.Value != 42)
                throw new InvalidOperationException("Nullable value read postconditions changed CLR storage.");
            """);
        var negative = FixtureCompilation(proof, $$"""
            var owner = new ReadTarget();
            static int Require(string value) => value.Length;
            _ = Require({{Invoke(possible, OwnerExpression)}});
            """);
        var diagnostics = negative.GetDiagnostics().Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error);
        await Assert.That(string.Join(",", diagnostics.Select(static diagnostic => diagnostic.Id))).IsEqualTo("CS8604");
    }

    /// <summary>Requires nullable value input restrictions while executing unchanged Nullable CLR signatures.</summary>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [Test]
    public async Task NullableValueInputsPreserveDisallowNullAndClrStorage()
    {
        var proof = Create(NullableValueInputFixture);
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.ValueInputTarget")!;
        var number = owner.GetMembers("Number").Single();
        var storage = owner.GetMembers(StorageMemberName).Single();
        var writeNumber = Emit(proof, number, true);
        var writeStorage = Emit(proof, storage, true);
        var readNumber = Emit(proof, number, false);
        var readStorage = Emit(proof, storage, false);
        await Execute(proof, $$"""
            var owner = new ValueInputTarget();
            {{Invoke(writeNumber, OwnerExpression, "42")}};
            {{Invoke(writeStorage, OwnerExpression, "new InputPayload(7)")}};
            var number = {{Invoke(readNumber, OwnerExpression)}};
            var payload = {{Invoke(readStorage, OwnerExpression)}};
            if (number != 42 || !payload.HasValue || payload.GetValueOrDefault().Value != 7)
                throw new InvalidOperationException("DisallowNull changed the Nullable value storage signature.");
            """);
        var negative = FixtureCompilation(proof, $$"""
            var owner = new ValueInputTarget();
            int? absent = null;
            InputPayload? empty = null;
            {{Invoke(writeNumber, OwnerExpression, AbsentExpression)}};
            {{Invoke(writeStorage, OwnerExpression, "empty")}};
            """);
        var diagnostics = negative.GetDiagnostics().Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error);
        await Assert.That(string.Join(",", diagnostics.Select(static diagnostic => diagnostic.Id))).IsEqualTo("CS8607,CS8607");
    }

    /// <summary>Creates a compiler-backed source site independent of generator interception ordering.</summary>
    /// <param name="extraSource">Optional actual source declarations for lexical access proofs.</param>
    /// <returns>The original compilation and exact call site.</returns>
    private static Proof Create(string extraSource = "")
    {
        var source = (Fixture + extraSource).Replace("MARKER", Marker, StringComparison.Ordinal);
        return CreateSource(source, $"AccessBridgeProof{Guid.NewGuid():N}", []);
    }

    /// <summary>Creates an original semantic source site with optional real compiled peer references.</summary>
    /// <param name="source">The fixed trusted compiler fixture.</param>
    /// <param name="assemblyName">Its exact assembly name used for generated namespace isolation.</param>
    /// <param name="additionalReferences">Any fixed trusted compiled friend assemblies.</param>
    /// <returns>The original exact source compilation and call site.</returns>
    private static Proof CreateSource(string source, string assemblyName, IEnumerable<MetadataReference> additionalReferences)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new(LanguageVersion.CSharp14), "AccessBridgeCaller.cs");
        var references = CompilerTestReferences.CreateDefault();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [tree],
            references.Concat(additionalReferences),
            new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var model = compilation.GetSemanticModel(tree);
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single(static expression => expression.Expression.ToString() == "Tick");
        var method = (IMethodSymbol)model.GetSymbolInfo(invocation).Symbol!;
        return new(compilation, new(model, invocation, method, RuntimeNamespace, string.Empty, 0), source, tree);
    }

    /// <summary>Builds an actual selected static bridge and retains diagnostics on failure.</summary>
    /// <param name="proof">The fixed compiler fixture.</param>
    /// <param name="member">The actual selected member.</param>
    /// <param name="write">Whether to assign storage.</param>
    /// <param name="retainLexicalReadCast">Whether the original caller can name the exact private return type.</param>
    /// <returns>The emitted invocation template.</returns>
    /// <exception cref="InvalidOperationException">The fixed fixture selected an unsupported static storage contract.</exception>
    private static string Emit(Proof proof, ISymbol member, bool write, bool retainLexicalReadCast = true)
    {
        if (!AccessBridgeEmitter.TryEmit(proof.Site, member, write, out var invocation, out var reason, retainLexicalReadCast: retainLexicalReadCast))
        {
            throw new InvalidOperationException(reason);
        }

        return invocation;
    }

    /// <summary>Supplies owner and value expressions to an emitter invocation template.</summary>
    /// <param name="template">The bridge template.</param>
    /// <param name="owner">The actual stable owner expression.</param>
    /// <param name="value">The assigned value expression.</param>
    /// <returns>The fixed fixture invocation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Invoke(string template, string owner, string value = "") => template
        .Replace(AccessBridgeEmitter.OwnerPlaceholder, owner, StringComparison.Ordinal)
        .Replace(AccessBridgeEmitter.ValuePlaceholder, value, StringComparison.Ordinal);

    /// <summary>Compiles and executes the actual generated source without suppressing compiler warnings.</summary>
    /// <param name="proof">The original exact member compilation.</param>
    /// <param name="body">The emitted runtime assertions.</param>
    /// <returns>The asynchronous compiler and runtime assertions.</returns>
    [SuppressMessage("Security", "SES1402", Justification = "This test invokes only its own fixed trusted in-memory fixture; generated static access performs all storage operations.")]
    private static async Task Execute(Proof proof, string body)
    {
        var binary = await Compile(proof, body);
        var assembly = System.Reflection.Assembly.Load(binary);
        await Assert.That((bool)assembly.GetType("Proof.Consumer.Probe")!.GetMethod("Run")!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>Compiles the actual emitted bridge source and rejects compiler warnings before runtime execution.</summary>
    /// <param name="proof">The fixed trusted original exact member compilation.</param>
    /// <param name="body">The actual generated bridge calls in its fixed executable body.</param>
    /// <returns>The real compiled fixture assembly bytes.</returns>
    private static async Task<byte[]> Compile(Proof proof, string body)
    {
        var compilation = FixtureCompilation(proof, body);
        var failures = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error);
        await Assert.That(string.Join("\n", failures)).IsEmpty();
        await using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        await Assert.That(string.Join("\n", emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))).IsEmpty();
        return stream.ToArray();
    }

    /// <summary>Combines the exact existing source with all emitted bridges for positive and negative flow controls.</summary>
    /// <param name="proof">The fixed trusted original compilation.</param>
    /// <param name="body">The fixed trusted caller operations.</param>
    /// <returns>The actual complete generated compilation.</returns>
    private static CSharpCompilation FixtureCompilation(Proof proof, string body)
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var original = CSharpSyntaxTree.ParseText(proof.Source.Replace(BodyMarker, body, StringComparison.Ordinal), options, "AccessBridgeCaller.cs");
        return proof.Compilation.ReplaceSyntaxTree(proof.Tree, original)
            .AddSyntaxTrees(proof.Site.AdditionalSources.Select(fragment => CSharpSyntaxTree.ParseText(fragment.Source, options, fragment.HintName)));
    }

    /// <summary>Retains the exact semantic source and auxiliary emitted access signatures.</summary>
    /// <param name="Compilation">The actual original compilation.</param>
    /// <param name="Site">The original source call.</param>
    /// <param name="Source">The fixed fixture source.</param>
    /// <param name="Tree">The original tree identity.</param>
    private sealed record Proof(CSharpCompilation Compilation, CallSite Site, string Source, SyntaxTree Tree);
}
