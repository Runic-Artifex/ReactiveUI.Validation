// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Proves exact lexical intrinsic signatures and outward reference transport.</summary>
public sealed partial class AccessBridgeCompilerTests
{
    /// <summary>Reports the exact nonpartial generic-owner boundary instead of emitting a trim-unsafe accessor.</summary>
    /// <returns>The asynchronous diagnostic assertions.</returns>
    [Test]
    public async Task NonpartialGenericOwnerRequiresTypedReplacementFactory()
    {
        var original = Create();
        var proof = CreateSource(original.Source.Replace("public static partial class Host", "public static class Host", StringComparison.Ordinal), "NonpartialAccessProof", []);
        var owner = proof.Compilation.GetTypeByMetadataName(HiddenGenericTypeName)!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        await Assert.That(AccessBridgeEmitter.TryEmit(proof.Site, owner.GetMembers(ValueMemberName).Single(), true, out _, out var reason)).IsFalse();
        await Assert.That(reason).Contains("partial enclosing type");
        await Assert.That(reason).Contains("ValidationCell/ValidationLens replacement factory");
        await Assert.That(proof.Site.AdditionalSources).IsEmpty();
    }

    /// <summary>Keeps generic host prefixes and private-owner suffixes in their original CLR VAR positions.</summary>
    /// <returns>The asynchronous exact source and runtime assertions.</returns>
    [Test]
    public async Task LexicalGenericHostPreservesNestedVarPrefix()
    {
        var proof = Create("""
            namespace Proof.Consumer
            {
                public sealed partial class GenericHost<T> where T : class
                {
                    private sealed class Child<U> where U : class, new()
                    {
                        MARKER public T? Initial { get; init; }
                        MARKER public readonly U? Storage;
                        public Child() { Storage = null; }
                    }
                    private sealed class Token { public Token() { } }
                    public static object Create() => new Child<Token>();
                    public static object CreateToken() => new Token();
                    public static T? ReadInitial(object owner) => ((Child<Token>)owner).Initial;
                    public static object? ReadStorage(object owner) => ((Child<Token>)owner).Storage;
                }
                public static class GenericFactory<U> where U : class
                {
                    public static bool Verify(U value)
                    {
                        var target = GenericHost<U>.Create();
                        var stored = GenericHost<U>.CreateToken();
                        // HOST_VAR_ASSIGNMENTS
                        return Equals(GenericHost<U>.ReadInitial(target), value) && ReferenceEquals(GenericHost<U>.ReadStorage(target), stored);
                    }
                }
            }
            """);
        var parameter = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.GenericFactory`1")!.TypeParameters.Single();
        var host = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.GenericHost`1")!.Construct(parameter);
        var owner = host.GetTypeMembers(ChildMemberName).Single().Construct(host.GetTypeMembers("Token").Single());
        var initial = Emit(proof, owner.GetMembers(InitialMemberName).Single(), true);
        var storage = Emit(proof, owner.GetMembers(StorageMemberName).Single(), true);
        var assignments = $"{Invoke(initial, TargetExpression, "value")};\n{Invoke(storage, TargetExpression, "stored")};";
        await Execute(proof with { Source = proof.Source.Replace("// HOST_VAR_ASSIGNMENTS", assignments, StringComparison.Ordinal) }, """
            if (!GenericFactory<string>.Verify("nested")) throw new InvalidOperationException("The enclosing prefix or private host-relative suffix drifted.");
            """);
        var source = string.Join("\n", proof.Site.AdditionalSources.Select(static fragment => fragment.Source));
        await Assert.That(source).Contains("AccessCore<__T1>");
        await Assert.That(source).Contains("GenericHost<@T>.Child<__T1>");
        await Assert.That(source).Contains("where __T1 : class, new()");
        await Assert.That(source).DoesNotContain(TypeAttributeName);
    }

    /// <summary>Types erased generic return, input, index and field signatures inside their lexical owner.</summary>
    /// <returns>The asynchronous nullable contract and runtime assertions.</returns>
    [Test]
    public async Task LexicalGenericSignaturePositionsPreserveNullableTransport()
    {
        var proof = Create("""
            namespace Proof.Consumer
            {
                public sealed partial class SignatureHost<T> where T : class
                {
                    private sealed class Part { }
                    MARKER private Part? Child { get; set; }
                    MARKER private readonly Part? Storage;
                    MARKER private string this[Part key] { get => key == Child ? "matched" : "other"; [param: global::System.Diagnostics.CodeAnalysis.AllowNull] set => Child = null; }
                    public SignatureHost() { Storage = new Part(); }
                }
            }
            """);
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.SignatureHost`1")!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        var childRead = Emit(proof, owner.GetMembers(ChildMemberName).Single(), false, false);
        var childWrite = Emit(proof, owner.GetMembers(ChildMemberName).Single(), true);
        var fieldRead = Emit(proof, owner.GetMembers(StorageMemberName).Single(), false, false);
        var indexWrite = Emit(proof, owner.GetMembers("this[]").Single(), true);
        await Execute(proof, $$"""
            var target = new SignatureHost<string>();
            var part = {{Invoke(fieldRead, TargetExpression)}};
            if (part is null) throw new InvalidOperationException("The typed field value is absent.");
            {{Invoke(childWrite, TargetExpression, "part")}};
            if (!ReferenceEquals({{Invoke(childRead, TargetExpression)}}, part)) throw new InvalidOperationException("The typed return/value contract drifted.");
            {{Invoke(indexWrite, TargetExpression, "null").Replace(AccessBridgeEmitter.IndexPlaceholder(0), "part", StringComparison.Ordinal)}};
            if ({{Invoke(childRead, TargetExpression)}} is not null) throw new InvalidOperationException("The nullable indexed setter was not invoked.");
            """);
        var source = string.Join("\n", proof.Site.AdditionalSources.Select(static fragment => fragment.Source));
        await Assert.That(source).Contains("object? Read(");
        await Assert.That(source).Contains("Part index0, global::System.String? value");
        await Assert.That(source).DoesNotContain(TypeAttributeName);
    }

    /// <summary>Closes private type arguments and exact private constraints behind a legal transport wrapper.</summary>
    /// <returns>The asynchronous original type identity and runtime assertions.</returns>
    [Test]
    public async Task ClosedLexicalArgumentsRetainPrivateConstraints()
    {
        const string tokenExpression = "token";
        var original = Create();
        var source = original.Source.Replace(
            "private class ConstraintBase { }",
            """
            private class ConstraintBase { }
            public static object CreateConstrained() => new HiddenConstrained<ConstraintBase>();
            public static object CreateToken() => new ConstraintBase();
            public static string ReadConstrainedIndex(object owner, object key) => ((HiddenConstrained<ConstraintBase>)owner)[(ConstraintBase)key];
            """,
            StringComparison.Ordinal);
        var proof = CreateSource(source, "ClosedPrivateConstraintProof", []);
        var argument = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.Host+ConstraintBase")!;
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.Host+HiddenConstrained`1")!.Construct(argument);
        var member = owner.GetMembers(ValueMemberName).Single();
        var write = Emit(proof, member, true);
        var read = Emit(proof, member, false, false);
        var indexed = Emit(proof, owner.GetMembers("this[]").Single(), true);
        await Execute(proof, $$"""
            var target = Host.CreateConstrained();
            var token = Host.CreateToken();
            {{Invoke(write, TargetExpression, tokenExpression)}};
            if (!ReferenceEquals({{Invoke(read, TargetExpression)}}, token)) throw new InvalidOperationException("The private generic argument was not retained.");
            {{Invoke(write, TargetExpression, "null")}};
            if ({{Invoke(read, TargetExpression)}} is not null) throw new InvalidOperationException("The nullable private value transport drifted.");
            {{Invoke(indexed, TargetExpression, "null").Replace(AccessBridgeEmitter.IndexPlaceholder(0), tokenExpression, StringComparison.Ordinal)}};
            if (Host.ReadConstrainedIndex(target, token) != "cleared") throw new InvalidOperationException("The constructed private generic index was not transported.");
            """);
        var generated = string.Join("\n", proof.Site.AdditionalSources.Select(static fragment => fragment.Source));
        await Assert.That(generated).Contains("private static class AccessCore<__T0>");
        await Assert.That(generated).Contains("where __T0 : global::Proof.Consumer.Host.ConstraintBase");
        await Assert.That(write).DoesNotContain("ConstraintBase");
        await Assert.That(generated).Contains("object index0, global::System.String? value");
        await Assert.That(generated).DoesNotContain(TypeAttributeName);
    }

    /// <summary>Retains ref owner transport for nameable generic struct storage with a private reference signature.</summary>
    /// <returns>The asynchronous exact byref signature and mutation assertions.</returns>
    [Test]
    public async Task LexicalGenericStructOwnerRetainsRefStorage()
    {
        var proof = Create("""
            namespace Proof.Consumer
            {
                public partial struct ValueSignatureHost<T> where T : class
                {
                    private sealed class Part { }
                    MARKER private Part? Child { get; set; }
                    public bool HasValue => Child is not null;
                    public static object CreateToken() => new Part();
                }
            }
            """);
        var owner = proof.Compilation.GetTypeByMetadataName("Proof.Consumer.ValueSignatureHost`1")!.Construct(proof.Compilation.GetSpecialType(SpecialType.System_String));
        var member = owner.GetMembers(ChildMemberName).Single();
        var write = Emit(proof, member, true);
        var read = Emit(proof, member, false, false);
        await Execute(proof, $$"""
            var target = new ValueSignatureHost<string>();
            var token = ValueSignatureHost<string>.CreateToken();
            {{Invoke(write, TargetExpression, "token")}};
            if (!target.HasValue || !ReferenceEquals({{Invoke(read, TargetExpression)}}, token)) throw new InvalidOperationException("The value owner was boxed or copied.");
            {{Invoke(write, TargetExpression, "null")}};
            if (target.HasValue) throw new InvalidOperationException("The typed nullable assignment did not reach original struct storage.");
            """);
        await Assert.That(string.Join("\n", proof.Site.AdditionalSources.Select(static fragment => fragment.Source)))
            .Contains("ref global::Proof.Consumer.ValueSignatureHost<@T> owner");
    }

    /// <summary>Substitutes annotated closed arguments once while preserving exact class-nullable constraints.</summary>
    /// <returns>The asynchronous exact nullable signature and runtime assertions.</returns>
    [Test]
    public async Task ClosedLexicalNullableArgumentsRetainSingleAnnotation()
    {
        var original = Create();
        var source = original.Source.Replace(
            "private sealed class HiddenGeneric<T> where T : class",
            "private sealed class HiddenGeneric<T> where T : class?",
            StringComparison.Ordinal);
        var proof = CreateSource(source, "ClosedNullableArgumentProof", []);
        var argument = proof.Compilation.GetSpecialType(SpecialType.System_String).WithNullableAnnotation(NullableAnnotation.Annotated);
        var owner = proof.Compilation.GetTypeByMetadataName(HiddenGenericTypeName)!.Construct(argument);
        var write = Emit(proof, owner.GetMembers(ValueMemberName).Single(), true);
        await Execute(proof, $$"""
            var target = Host.CreateGeneric();
            {{Invoke(write, TargetExpression, "null")}};
            if (Host.ReadGeneric(target) is not null) throw new InvalidOperationException("The nullable argument did not retain its input contract.");
            """);
        var generated = proof.Site.AdditionalSources.Single().Source;
        await Assert.That(generated).Contains("where __T0 : class?");
        await Assert.That(generated).Contains("global::System.String? value");
        await Assert.That(generated).DoesNotContain("String??");
    }
}
