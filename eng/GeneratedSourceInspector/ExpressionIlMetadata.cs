// Copyright (c) 2026 Runic Artifex. Licensed under the MIT license.
using System.Collections.Immutable;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

/// <summary>Inspects original caller expression construction without loading or executing the PE.</summary>
internal static class ExpressionIlMetadata
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
        var methods = new List<object>();
        var factories = new List<object>();
        var entryCalls = new List<object>();
        var normalApiCalls = new List<object>();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var name = TypeName(reader, method.GetDeclaringType()) + "." + reader.GetString(method.Name);
            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            methods.Add(new { method = name, token = MetadataTokens.GetToken(handle), ilSha256 = Convert.ToHexStringLower(SHA256.HashData(il)) });
            for (var offset = 0; offset < il.Length;)
            {
                var start = offset;
                var first = il[offset++];
                var code = Codes[first == 0xfe ? unchecked((short)(0xfe00 | il[offset++])) : (short)first];
                if (code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Ldftn)
                {
                    var target = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                    if (target.Kind == HandleKind.MethodSpecification)
                    {
                        target = reader.GetMethodSpecification((MethodSpecificationHandle)target).Method;
                    }

                    if (target.Kind == HandleKind.MemberReference)
                    {
                        var member = reader.GetMemberReference((MemberReferenceHandle)target);
                        var owner = TypeName(reader, member.Parent);
                        var targetName = reader.GetString(member.Name);
                        if (targetName == "ValidationRule" && owner is "ReactiveUI.Validation.Extensions.ValidatableViewModelExtensions"
                            or "ReactiveUI.Validation.Reactive.Extensions.ValidatableViewModelExtensions")
                        {
                            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                            if (type.ResolutionScope.Kind != HandleKind.AssemblyReference)
                            {
                                throw new InvalidDataException("Normal Validation API must resolve to an explicit shipping assembly.");
                            }

                            var signature = member.DecodeMethodSignature(new Types(), (object?)null);
                            normalApiCalls.Add(new
                            {
                                method = name,
                                offset = start,
                                opcode = code.Name,
                                owner,
                                target = targetName,
                                assembly = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name),
                                returnType = signature.ReturnType,
                                parameters = signature.ParameterTypes,
                                signatureSha256 = Convert.ToHexStringLower(SHA256.HashData(reader.GetBlobBytes(member.Signature))),
                            });
                        }

                        if (owner == "RuleExpressionParamsNegative.Program" && targetName == "Run")
                        {
                            entryCalls.Add(new { method = name, offset = start, opcode = code.Name, owner, target = targetName });
                        }

                        if (owner == "System.Linq.Expressions.Expression" && targetName is "NewArrayInit" or "NewArrayBounds")
                        {
                            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
                            if (type.ResolutionScope.Kind != HandleKind.AssemblyReference)
                            {
                                throw new InvalidDataException("Expression array factory must resolve to an explicit framework assembly.");
                            }

                            var assembly = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
                            var signature = member.DecodeMethodSignature(new Types(), (object?)null);
                            factories.Add(new
                            {
                                method = name,
                                offset = start,
                                opcode = code.Name,
                                owner,
                                target = targetName,
                                assembly = reader.GetString(assembly.Name),
                                returnType = signature.ReturnType,
                                parameters = signature.ParameterTypes,
                                signatureSha256 = Convert.ToHexStringLower(SHA256.HashData(reader.GetBlobBytes(member.Signature))),
                            });
                        }
                    }
                }

                offset += OperandSize(code.OperandType, il, offset);
            }
        }

        return new
        {
            completed = true,
            path = Path.GetFullPath(path),
            sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
            assembly = reader.GetString(reader.GetAssemblyDefinition().Name),
            mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid),
            methods,
            arrayFactories = factories,
            entryCalls,
            normalApiCalls,
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

    private static string TypeName(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeDefinition)
        {
            var definition = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
            var owner = definition.GetDeclaringType();
            var prefix = owner.IsNil ? reader.GetString(definition.Namespace) : TypeName(reader, owner);
            return (prefix.Length == 0 ? "" : prefix + ".") + reader.GetString(definition.Name);
        }

        if (handle.Kind == HandleKind.TypeReference)
        {
            var reference = reader.GetTypeReference((TypeReferenceHandle)handle);
            var scope = reference.ResolutionScope;
            return (scope.Kind == HandleKind.TypeReference ? TypeName(reader, scope) : reader.GetString(reference.Namespace)) + "." + reader.GetString(reference.Name);
        }

        return handle.Kind.ToString();
    }

    private sealed class Types : ISignatureTypeProvider<string, object?>
    {
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fn(" + string.Join(",", signature.ParameterTypes) + ")";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType + (isRequired ? " modreq(" : " modopt(") + modifier + ")";
        public string GetPinnedType(string elementType) => elementType + " pinned";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode;
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => TypeName(reader, handle);
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => TypeName(reader, handle);
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
