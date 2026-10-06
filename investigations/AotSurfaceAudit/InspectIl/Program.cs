using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Read-only metadata/IL audit. Target assemblies are never loaded or executed.
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(x => x.FieldType == typeof(OpCode)).Select(x => (OpCode)x.GetValue(null)!)
    .ToDictionary(x => unchecked((ushort)x.Value));
var metadataOnly = args.Length > 0 && args[0] == "--metadata-only";
foreach (var path in metadataOnly ? args.Skip(1) : args)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var names = new Names(metadata);
    var reportedTypes = new HashSet<TypeDefinitionHandle>();
    stream.Position = 0;
    Console.WriteLine($"ASSEMBLY {Path.GetFileName(path)} SHA256 {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))}");
    var definition = metadata.GetAssemblyDefinition();
    Console.WriteLine($"IDENTITY {metadata.GetString(definition.Name)} VERSION {definition.Version} MVID {metadata.GetGuid(metadata.GetModuleDefinition().Mvid)}");
    foreach (var handle in metadata.CustomAttributes)
    {
        var attribute = metadata.GetCustomAttribute(handle);
        var name = names.Member(attribute.Constructor);
        if (name.Contains("AssemblyMetadataAttribute", StringComparison.Ordinal))
        {
            var blob = metadata.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() == 1) Console.WriteLine($"METADATA {blob.ReadSerializedString()}={blob.ReadSerializedString()}");
        }
    }
    if (metadataOnly) continue;
    foreach (var handle in metadata.MethodDefinitions)
    {
        var method = metadata.GetMethodDefinition(handle);
        if (method.RelativeVirtualAddress == 0) continue;
        var typeName = names.Type(method.GetDeclaringType());
        var methodName = names.Member(handle);
        if (!(typeName.StartsWith("ReactiveUI.Validation", StringComparison.Ordinal)
            || typeName.StartsWith("Splat.AppLocator", StringComparison.Ordinal)
            || typeName.StartsWith("Splat.InternalLocator", StringComparison.Ordinal)
            || typeName.StartsWith("Splat.InstanceGenericFirstDependencyResolver", StringComparison.Ordinal)
            || typeName.Contains("RuntimeObservationFallback", StringComparison.Ordinal)
            || typeName.Contains("ReactiveUIBindingExtensions", StringComparison.Ordinal)
            || typeName.Contains("Expressions.Reflection", StringComparison.Ordinal)
            || typeName.Contains("ReactiveUIBindingBuilder", StringComparison.Ordinal)
            || typeName.Contains("ReactiveUIBindingModule", StringComparison.Ordinal)
            || typeName.Contains(".Advanced.FromAsync", StringComparison.Ordinal)
            || typeName.Contains("AsyncSubscriptionLifetime", StringComparison.Ordinal)
            || typeName.Contains(".Operators.ObserveOnObservable", StringComparison.Ordinal)
            || typeName.Contains(".Internal.ScheduledDrainState", StringComparison.Ordinal)
            || (typeName.Contains("ReactiveExtensions", StringComparison.Ordinal) && methodName.Contains("ObserveOnSafe", StringComparison.Ordinal))
            || (typeName.Contains(".Signals.Signal", StringComparison.Ordinal) && methodName.Contains(".FromAsync(", StringComparison.Ordinal))
            || typeName.Contains("__ReactiveUIGeneratedBindings", StringComparison.Ordinal))) continue;
        if (reportedTypes.Add(method.GetDeclaringType()))
        {
            foreach (var attrHandle in metadata.GetTypeDefinition(method.GetDeclaringType()).GetCustomAttributes())
            {
                var attr = metadata.GetCustomAttribute(attrHandle);
                var name = names.Member(attr.Constructor);
                if (name.Contains("RequiresUnreferencedCode", StringComparison.Ordinal) || name.Contains("RequiresDynamicCode", StringComparison.Ordinal)
                    || name.Contains("DynamicDependency", StringComparison.Ordinal) || name.Contains("UnconditionalSuppress", StringComparison.Ordinal))
                    Console.WriteLine($"TYPE_ATTRIBUTE {typeName} {name}");
            }
        }
        Console.WriteLine($"METHOD {methodName}");
        foreach (var attrHandle in method.GetCustomAttributes())
        {
            var attr = metadata.GetCustomAttribute(attrHandle);
            var name = names.Member(attr.Constructor);
            if (name.Contains("RequiresUnreferencedCode", StringComparison.Ordinal) || name.Contains("RequiresDynamicCode", StringComparison.Ordinal)
                || name.Contains("DynamicDependency", StringComparison.Ordinal) || name.Contains("UnconditionalSuppress", StringComparison.Ordinal))
                Console.WriteLine($" ATTRIBUTE {name}");
        }
        var bytes = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
        for (var offset = 0; offset < bytes.Length;)
        {
            var instruction = offset;
            ushort code = bytes[offset++];
            if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
            var opcode = opcodes[code];
            if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                var token = BitConverter.ToInt32(bytes, offset);
                var target = names.Member(MetadataTokens.EntityHandle(token));
                Console.WriteLine($" IL_{instruction:x4} {opcode.Name} {target}");
            }
            offset += opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4
            };
        }
    }
}

sealed class Names(MetadataReader metadata) : ISignatureTypeProvider<string, object?>
{
    public string Type(EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeFromDefinition(metadata, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeReference => GetTypeFromReference(metadata, (TypeReferenceHandle)handle, 0),
        HandleKind.TypeSpecification => GetTypeFromSpecification(metadata, null, (TypeSpecificationHandle)handle, 0),
        _ => handle.Kind.ToString()
    };

    public string Member(EntityHandle handle)
    {
        if (handle.Kind == HandleKind.MethodSpecification)
        {
            var spec = metadata.GetMethodSpecification((MethodSpecificationHandle)handle);
            return Member(spec.Method) + "<" + string.Join(",", spec.DecodeSignature(this, null)) + ">";
        }
        if (handle.Kind == HandleKind.MethodDefinition)
        {
            var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
            var signature = method.DecodeSignature(this, null);
            return Type(method.GetDeclaringType()) + "." + metadata.GetString(method.Name)
                + "(" + string.Join(",", signature.ParameterTypes) + ")";
        }
        if (handle.Kind == HandleKind.MemberReference)
        {
            var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
            return Type(member.Parent) + "." + metadata.GetString(member.Name);
        }
        if (handle.Kind == HandleKind.FieldDefinition)
        {
            var field = metadata.GetFieldDefinition((FieldDefinitionHandle)handle);
            return Type(field.GetDeclaringType()) + "." + metadata.GetString(field.Name);
        }
        return Type(handle);
    }

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeDefinition(handle);
        var owner = type.GetDeclaringType();
        return owner.IsNil ? reader.GetString(type.Namespace) + "." + reader.GetString(type.Name) : Type(owner) + "/" + reader.GetString(type.Name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeReference(handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "methodptr";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSZArrayType(string elementType) => elementType + "[]";
}
