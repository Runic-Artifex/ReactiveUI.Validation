using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Read-only IL inspection; never loads or executes the target assembly.
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(x => x.FieldType == typeof(OpCode)).Select(x => (OpCode)x.GetValue(null)!)
    .ToDictionary(x => unchecked((ushort)x.Value));
foreach (var path in args)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    Console.WriteLine($"ASSEMBLY {Path.GetFileName(path)}");
    foreach (var handle in metadata.MethodDefinitions)
    {
        var method = metadata.GetMethodDefinition(handle);
        var type = metadata.GetTypeDefinition(method.GetDeclaringType());
        var typeName = metadata.GetString(type.Name);
        var name = metadata.GetString(method.Name);
        if (typeName is not ("ValidationContext" or "ValidationHelper") || name != ".ctor" || method.RelativeVirtualAddress == 0) continue;
        var bytes = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort code = bytes[offset++];
            if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
            var opcode = opcodes[code];
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(bytes, offset);
                var called = Name(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token));
                if (called.Contains("ToProperty", StringComparison.Ordinal)) Console.WriteLine($"{typeName}.{name} -> {called}");
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

    string Name(EntityHandle handle)
    {
        if (handle.Kind == HandleKind.MethodSpecification) return Name(metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
        if (handle.Kind == HandleKind.MethodDefinition)
        {
            var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
            return metadata.GetString(metadata.GetTypeDefinition(method.GetDeclaringType()).Name) + "." + metadata.GetString(method.Name);
        }
        if (handle.Kind == HandleKind.MemberReference) return metadata.GetString(metadata.GetMemberReference((MemberReferenceHandle)handle).Name);
        return handle.Kind.ToString();
    }
}
