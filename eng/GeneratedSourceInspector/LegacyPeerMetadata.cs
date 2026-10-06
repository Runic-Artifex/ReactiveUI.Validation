// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

/// <summary>Proves historical peers retain original normal API calls and callable IL.</summary>
internal static class LegacyPeerMetadata
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields()
        .Where(static field => field.FieldType == typeof(OpCode))
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static code => code.Value);

    internal static object Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var calls = new List<object>();
        var invocationCount = 0;
        var callableCount = 0;
        var references = reader.AssemblyReferences.Select(handle => reader.GetAssemblyReference(handle)).ToArray();
        var validation = references.Where(reference => reader.GetString(reference.Name) is "ReactiveUI.Validation" or "ReactiveUI.Validation.Reactive").ToArray();
        if (validation.Length != 1 || reader.TypeReferences.Any(handle =>
            reader.GetString(reader.GetTypeReference(handle).Namespace).Contains(".Capabilities", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Historical peer must reference exactly one old Validation flavor and no new capability API.");
        }

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            for (var offset = 0; offset < il.Length;)
            {
                var start = offset;
                var first = il[offset++];
                var value = first == 0xfe ? unchecked((short)(0xfe00 | il[offset++])) : (short)first;
                var code = Codes[value];
                if (code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Ldftn)
                {
                    var target = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                    var specification = target.Kind == HandleKind.MethodSpecification
                        ? reader.GetMethodSpecification((MethodSpecificationHandle)target) : default;
                    if (target.Kind == HandleKind.MethodSpecification)
                    {
                        target = specification.Method;
                    }

                    if (target.Kind == HandleKind.MemberReference)
                    {
                        var member = reader.GetMemberReference((MemberReferenceHandle)target);
                        var owner = TypeName(reader, member.Parent);
                        if (reader.GetString(member.Name) == "ValidationRule" && owner.EndsWith(".Extensions.ValidatableViewModelExtensions", StringComparison.Ordinal))
                        {
                            invocationCount += code == OpCodes.Ldftn ? 0 : 1;
                            callableCount += code == OpCodes.Ldftn ? 1 : 0;
                            calls.Add(new
                            {
                                method = TypeName(reader, method.GetDeclaringType()) + "." + reader.GetString(method.Name),
                                offset = start,
                                opcode = code.Name,
                                owner,
                                target = reader.GetString(member.Name),
                                signatureSha256 = Convert.ToHexStringLower(SHA256.HashData(reader.GetBlobBytes(member.Signature))),
                                typeArgumentsSha256 = specification.Signature.IsNil ? null
                                    : Convert.ToHexStringLower(SHA256.HashData(reader.GetBlobBytes(specification.Signature))),
                            });
                        }
                    }
                }

                offset += OperandSize(code.OperandType, il, offset);
            }
        }

        if (invocationCount == 0 || callableCount == 0)
        {
            throw new InvalidDataException("Historical peer is missing original normal API invocation or method-group IL.");
        }

        return new
        {
            completed = true,
            path = Path.GetFullPath(path),
            sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
            assembly = reader.GetString(reader.GetAssemblyDefinition().Name),
            assemblyReferences = references.Select(reference => new { name = reader.GetString(reference.Name), version = reference.Version.ToString() }).ToArray(),
            validationAssembly = reader.GetString(validation[0].Name),
            newCapabilityReferences = false,
            invocationCount,
            callableCount,
            calls,
        };
    }

    private static int OperandSize(OperandType type, byte[] il, int offset) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
        _ => 4,
    };

    private static string TypeName(MetadataReader reader, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)handle).Namespace) + "."
            + reader.GetString(reader.GetTypeReference((TypeReferenceHandle)handle).Name),
        HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)handle).Namespace) + "."
            + reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
        _ => handle.Kind.ToString(),
    };
}
