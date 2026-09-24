namespace JvmVerifier.Core.ClassFile;

/// <summary>Offset of an instruction within the Code array (coordinate base: bytes, zero based).</summary>
public readonly record struct ByteOffset(int Value)
{
    public static implicit operator int(ByteOffset o) => o.Value;
    public override string ToString() => $"bci:{Value}";
}

public sealed class ExceptionTableEntry
{
    public required int StartPc { get; init; }
    public required int EndPc { get; init; }
    public required int HandlerPc { get; init; }
    /// <summary>0 = catch-all (finally), otherwise constant pool Class index.</summary>
    public required int CatchType { get; init; }
    /// <summary>Stable identity within the method (declaration order).</summary>
    public required int Ordinal { get; init; }
}

/// <summary>A single verification_type_info structure exactly as encoded (JVMS 4.7.4).</summary>
public sealed class VerificationType
{
    public required byte Tag { get; init; }
    /// <summary>cpool ref for Object_variable_info (7) or bci for Uninitialized_variable_info (8); else 0.</summary>
    public required int Operand { get; init; }
    public required int Ordinal { get; init; }

    public const byte Top = 0;
    public const byte Integer = 1;
    public const byte FloatV = 2;
    public const byte LongV = 4;
    public const byte DoubleV = 3;
    public const byte Null = 5;
    public const byte UninitializedThis = 6;
    public const byte Object = 7;
    public const byte Uninitialized = 8;

    public string KindName => Tag switch
    {
        0 => "top", 1 => "int", 2 => "float", 3 => "double", 4 => "long",
        5 => "null", 6 => "uninitializedThis", 7 => "object", 8 => "uninitialized",
        _ => $"bad({Tag})"
    };
}

/// <summary>One stack_map_frame with its original frame type byte and decoded offsets.</summary>
public sealed class StackMapFrame
{
    public required int Ordinal { get; init; }
    public required int FrameType { get; init; }
    /// <summary>Absolute offset of the instruction this frame certifies (delta already applied).</summary>
    public required int Offset { get; init; }
    /// <summary>Raw delta as encoded (offset_delta); retained for forensics.</summary>
    public required int OffsetDelta { get; init; }
    public required IReadOnlyList<VerificationType> Locals { get; init; }
    public required IReadOnlyList<VerificationType> Stack { get; init; }
    /// <summary>Byte span of this frame inside the StackMapTable attribute.</summary>
    public required int RawLength { get; init; }
    /// <summary>Byte start within the attribute payload.</summary>
    public required int RawStart { get; init; }
}

public sealed class AttributeInfo
{
    public required int NameIndex { get; init; }
    public required byte[] Data { get; init; }
}

public sealed class CodeAttribute
{
    public required int MaxStack { get; init; }
    public required int MaxLocals { get; init; }
    public required byte[] Code { get; init; }
    public required IReadOnlyList<ExceptionTableEntry> ExceptionTable { get; init; }
    public required IReadOnlyList<AttributeInfo> Attributes { get; init; }
    public StackMapTable? StackMapTable { get; init; }
}

public sealed class StackMapTable
{
    public required IReadOnlyList<StackMapFrame> Frames { get; init; }
    /// <summary>True if the attribute bytes failed structural decoding (corruption evidence kept).</summary>
    public required bool DecodeError { get; init; }
    public string? DecodeErrorMessage { get; init; }
}

public sealed class MethodInfo
{
    public required ushort AccessFlags { get; init; }
    public required int NameIndex { get; init; }
    public required int DescriptorIndex { get; init; }
    public required IReadOnlyList<AttributeInfo> Attributes { get; init; }
    public CodeAttribute? Code { get; init; }
    public required int Ordinal { get; init; }

    public string? Name(ConstantPool cp) => cp.Utf8(NameIndex);
    public string? Descriptor(ConstantPool cp) => cp.Utf8(DescriptorIndex);
}

public sealed class ClassFileModel
{
    /// <summary>Raw minor/major classfile version pair (the version coordinate, e.g. 0/52).</summary>
    public required ushort MinorVersion { get; init; }
    public required ushort MajorVersion { get; init; }
    public required ConstantPool ConstantPool { get; init; }
    public required ushort AccessFlags { get; init; }
    public required int ThisClass { get; init; }
    public required int SuperClass { get; init; }
    public required IReadOnlyList<int> Interfaces { get; init; }
    public required IReadOnlyList<MethodInfo> Methods { get; init; }
    /// <summary>The exact original bytes the model was imported from.</summary>
    public required byte[] RawBytes { get; init; }

    public string ThisName => ConstantPool.ClassName(ThisClass) ?? "?";
    public string? SuperName => SuperClass == 0 ? null : ConstantPool.ClassName(SuperClass);

    public MethodInfo? FindMethod(string name) =>
        Methods.FirstOrDefault(m => m.Name(ConstantPool) == name);
}
