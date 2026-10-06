// Copyright (c) 2019-2026 ReactiveUI and Contributors. All rights reserved.
// ReactiveUI and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace ReactiveUI.Validation.SourceGenerators.Tests;

/// <summary>Retains verified producer assembly cohorts for one compiler test process.</summary>
internal static class CapabilityProducerAssemblies
{
    /// <summary>Publishes only complete assembly sets, keyed by ordered package, path and content identities.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<ImmutableArray<Assembly>>> Assemblies = new(StringComparer.Ordinal);

    /// <summary>Loads a verified immutable cohort once, without retaining any host or generator instance.</summary>
    /// <param name="inputs">The exact ordered packaged producer inputs.</param>
    /// <returns>The process-lifetime producer assemblies in their original order.</returns>
    /// <exception cref="InvalidOperationException">The verified producer bytes changed.</exception>
    internal static ImmutableArray<Assembly> Get(ImmutableArray<CapabilityProducerInput> inputs)
    {
        foreach (var input in inputs)
        {
            if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input.Path))) != input.Sha256)
            {
                throw new InvalidOperationException("Producer analyzer bytes do not match the verified compiler manifest.");
            }
        }

        var identity = string.Join('\n', inputs.Select(static input => $"{input.Package}|{Path.GetFullPath(input.Path)}|{input.Sha256}"));
        return Assemblies.GetOrAdd(identity, static (_, cohort) => new(() => Load(cohort), LazyThreadSafetyMode.ExecutionAndPublication), inputs).Value;
    }

    /// <summary>Resolves one exact producer cohort with an immutable directory snapshot.</summary>
    /// <param name="inputs">The verified producer files.</param>
    /// <returns>The fully loaded assembly set.</returns>
    private static ImmutableArray<Assembly> Load(ImmutableArray<CapabilityProducerInput> inputs)
    {
        var directories = inputs.Select(static input => Path.GetDirectoryName(Path.GetFullPath(input.Path))!)
            .Distinct(StringComparer.Ordinal).ToImmutableArray();
        var loader = new ProducerLoadContext(directories);
        return [.. inputs.Select(input => loader.LoadFromAssemblyPath(Path.GetFullPath(input.Path)))];
    }

    /// <summary>Matches compiler-process assembly lifetime while isolating each verified dependency cohort.</summary>
    /// <param name="directories">The immutable producer sibling directories.</param>
    private sealed class ProducerLoadContext(ImmutableArray<string> directories) : AssemblyLoadContext(isCollectible: false)
    {
        /// <inheritdoc />
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true)
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }

            foreach (var directory in directories)
            {
                var path = Path.Combine(directory, $"{assemblyName.Name}.dll");
                if (File.Exists(path))
                {
                    return LoadFromAssemblyPath(path);
                }
            }

            return null;
        }
    }
}
