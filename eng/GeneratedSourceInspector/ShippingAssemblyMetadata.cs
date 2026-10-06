// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

/// <summary>Reads actual shipping metadata without executing a consumer assembly.</summary>
internal static class ShippingAssemblyMetadata
{
    internal static IEnumerable<object> Read(IEnumerable<string> references)
    {
        foreach (var path in references.Where(static path => Path.GetFileName(path) is "ReactiveUI.Validation.dll" or "ReactiveUI.Validation.Reactive.dll"))
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            var definition = reader.GetAssemblyDefinition();
            var metadata = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var handle in definition.GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind != HandleKind.MemberReference)
                {
                    continue;
                }

                var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (constructor.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                if (reader.GetString(type.Namespace) != "System.Reflection" || reader.GetString(type.Name) != "AssemblyMetadataAttribute")
                {
                    continue;
                }

                var value = reader.GetBlobReader(attribute.Value);
                if (value.ReadUInt16() != 1)
                {
                    throw new InvalidDataException("Invalid assembly metadata attribute encoding.");
                }

                var key = value.ReadSerializedString() ?? throw new InvalidDataException("Assembly metadata has a null key.");
                metadata.Add(key, value.ReadSerializedString());
            }

            yield return new
            {
                path,
                assembly = reader.GetString(definition.Name),
                sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                metadata,
            };
        }
    }
}
