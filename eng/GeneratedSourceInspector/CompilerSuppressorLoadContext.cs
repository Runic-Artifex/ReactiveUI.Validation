// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>Loads the exact consumer's producer suppressors without changing consumer references.</summary>
internal sealed class CompilerSuppressorLoadContext(IEnumerable<string> analyzerPaths) : AssemblyLoadContext(isCollectible: true), IDisposable
{
    private readonly string[] _directories = [.. analyzerPaths.Select(Path.GetDirectoryName).OfType<string>().Distinct(StringComparer.Ordinal)];

    internal ImmutableArray<DiagnosticAnalyzer> LoadSuppressors(IEnumerable<string> paths)
    {
        var result = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        foreach (var path in paths)
        {
            var names = SuppressorNames(path).ToArray();
            if (names.Length == 0)
            {
                continue;
            }

            var assembly = LoadFromAssemblyPath(path);
            foreach (var name in names)
            {
                var type = assembly.GetType(name, throwOnError: true)!;
                if (!typeof(DiagnosticSuppressor).IsAssignableFrom(type))
                {
                    throw new InvalidDataException($"Metadata suppressor has an unexpected runtime base: {name}");
                }
                result.Add((DiagnosticSuppressor)Activator.CreateInstance(type, nonPublic: true)!);
            }
        }

        return result.ToImmutable();
    }

    public void Dispose() => Unload();

    private static IEnumerable<string> SuppressorNames(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if ((type.Attributes & TypeAttributes.Abstract) != 0 || !IsSuppressor(reader, type.BaseType))
            {
                continue;
            }

            yield return FullName(reader, handle);
        }
    }

    private static bool IsSuppressor(MetadataReader reader, EntityHandle handle) => !handle.IsNil && handle.Kind switch
    {
        HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)handle).Namespace) == "Microsoft.CodeAnalysis.Diagnostics"
            && reader.GetString(reader.GetTypeReference((TypeReferenceHandle)handle).Name) == "DiagnosticSuppressor",
        HandleKind.TypeDefinition => IsSuppressor(reader, reader.GetTypeDefinition((TypeDefinitionHandle)handle).BaseType),
        _ => false,
    };

    private static string FullName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var parent = type.GetDeclaringType();
        var name = reader.GetString(type.Name);
        return parent.IsNil ? reader.GetString(type.Namespace) + "." + name : FullName(reader, parent) + "+" + name;
    }

    protected override Assembly? Load(AssemblyName name)
    {
        var shared = Default.Assemblies.FirstOrDefault(assembly => assembly.GetName().Name == name.Name);
        if (shared is not null)
        {
            return shared;
        }

        foreach (var directory in _directories)
        {
            var path = Path.Combine(directory, $"{name.Name}.dll");
            if (File.Exists(path))
            {
                return LoadFromAssemblyPath(path);
            }
        }

        return null;
    }
}
