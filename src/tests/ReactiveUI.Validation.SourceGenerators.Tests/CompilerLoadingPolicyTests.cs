// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Checks process-lifetime assemblies, independently owned producers and edited consumer identity.</summary>
public sealed class CompilerLoadingPolicyTests
{
    /// <summary>The exact executable consumer entry type.</summary>
    private const string EntryType = "Model";

    /// <summary>The exact Boolean entry method.</summary>
    private const string EntryMethod = "Check";

    /// <summary>The independent consumer executions with the same compilation identity.</summary>
    private const int ExecutionCount = 2;

    /// <summary>The original generated producer property.</summary>
    private const string OriginalProperty = "Name";

    /// <summary>The edited generated producer property.</summary>
    private const string EditedProperty = "Title";

    /// <summary>Retains assembly bytes while isolating producer state, host disposal and same-name edited executions.</summary>
    /// <param name="reactive">Whether to select the System.Reactive producer cohort.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ProcessLifetimeLoadingKeepsProducerAndConsumerIsolation(bool reactive)
    {
        var firstHost = CapabilityCompilerHost.Create(reactive);
        try
        {
            using var secondHost = CapabilityCompilerHost.Create(reactive);
            await AssertProducerIsolationAsync(firstHost, secondHost);
            var source = Source(reactive);
            var first = await firstHost.RunAsync(source);
            await AssertCleanAsync(first);
            await Assert.That(first.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
            var edited = await firstHost.RunAsync(source.Replace(OriginalProperty, EditedProperty, StringComparison.Ordinal));
            await AssertCleanAsync(edited);
            await Assert.That(edited.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
            var consumers = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name == first.Compilation.AssemblyName).ToArray();
            await Assert.That(consumers.Length).IsEqualTo(ExecutionCount);
            var originalConsumer = consumers.Single(static assembly => assembly.GetType(EntryType)!.GetProperty(OriginalProperty) is not null);
            var editedConsumer = consumers.Single(static assembly => assembly.GetType(EntryType)!.GetProperty(EditedProperty) is not null);
            await Assert.That(originalConsumer.GetType(EntryType)!.GetProperty(EditedProperty)).IsNull();
            await Assert.That(editedConsumer.GetType(EntryType)!.GetProperty(OriginalProperty)).IsNull();
            await Assert.That(ReferenceEquals(originalConsumer, editedConsumer)).IsFalse();
            await Assert.That(ReferenceEquals(AssemblyLoadContext.GetLoadContext(originalConsumer), AssemblyLoadContext.GetLoadContext(editedConsumer))).IsFalse();
            await Assert.That(Array.TrueForAll(consumers, static assembly => !AssemblyLoadContext.GetLoadContext(assembly)!.IsCollectible)).IsTrue();
            firstHost.Dispose();
            await Assert.That(firstHost.ProducerInstances).IsEmpty();
            var independent = await secondHost.RunAsync(source);
            await AssertCleanAsync(independent);
            await Assert.That(independent.ExecuteBoolean(EntryType, EntryMethod)).IsTrue();
            await Assert.That(first.Compilation.AssemblyName).IsNotEqualTo(independent.Compilation.AssemblyName);
        }
        finally
        {
            firstHost.Dispose();
        }
    }

    /// <summary>Creates the actual property-generator consumer with a process-lifetime execution assertion.</summary>
    /// <param name="reactive">The selected runtime flavor.</param>
    /// <returns>The trusted executable source.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Source(bool reactive) => $$"""
        using {{(reactive ? "ReactiveUI.Reactive" : "ReactiveUI")}};
        using ReactiveUI.SourceGenerators;
        public sealed partial class Model : ReactiveObject
        {
            [Reactive] public partial string? Name { get; set; }
            public static bool Check()
            {
                var model = new Model { Name = "ok" };
                return model.Name == "ok" && !System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(Model).Assembly)!.IsCollectible;
            }
        }
        """;

    /// <summary>Requires shared exact assembly identities and fresh generator and suppressor instances.</summary>
    /// <param name="first">The first independent compiler host.</param>
    /// <param name="second">The second host for the same verified cohort.</param>
    /// <returns>The asynchronous identity assertions.</returns>
    private static async Task AssertProducerIsolationAsync(CapabilityCompilerHost first, CapabilityCompilerHost second)
    {
        await Assert.That(first.ProducerAssemblies.Length).IsEqualTo(second.ProducerAssemblies.Length);
        foreach (var pair in first.ProducerAssemblies.Zip(second.ProducerAssemblies))
        {
            await Assert.That(ReferenceEquals(pair.First, pair.Second)).IsTrue();
            await Assert.That(AssemblyLoadContext.GetLoadContext(pair.First)!.IsCollectible).IsFalse();
        }

        await Assert.That(first.ProducerInstances.Length).IsEqualTo(second.ProducerInstances.Length);
        await Assert.That(first.ProducerInstances).IsNotEmpty();
        foreach (var pair in first.ProducerInstances.Zip(second.ProducerInstances))
        {
            await Assert.That(pair.First.GetType()).IsEqualTo(pair.Second.GetType());
            await Assert.That(ReferenceEquals(pair.First, pair.Second)).IsFalse();
        }
    }

    /// <summary>Preserves strict actual producer/compiler checks.</summary>
    /// <param name="result">The actual combined producer result.</param>
    /// <returns>The asynchronous diagnostic assertions.</returns>
    private static async Task AssertCleanAsync(CapabilityCompilation result)
    {
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)).IsEmpty();
        await Assert.That(await result.GetDispatchDiagnosticsAsync()).IsEmpty();
    }
}
