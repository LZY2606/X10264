namespace JvmBytecodeVerifier.Core.Raw;

// Constant pool tags, JVMS §4.4.
public enum CpTag : byte
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6, Class = 7,
    String = 8, Fieldref = 9, Methodref = 10, InterfaceMethodref = 11,
    NameAndType = 12, MethodHandle = 15, MethodType = 16, Dynamic = 17,
    InvokeDynamic = 18, Module = 19, Package = 20
}

public abstract record CpEntry(CpTag Tag);

public sealed record CpUtf8(string Value) : CpEntry(CpTag.Utf8);

public sealed record CpInteger(int Value, string RawHex) : CpEntry(CpTag.Integer);

public sealed record CpFloat(uint Bits, string RawHex) : CpEntry(CpTag.Float);

public sealed record CpLong(long Value, string RawHex) : CpEntry(CpTag.Long);

public sealed record CpDouble(ulong Bits, string RawHex) : CpEntry(CpTag.Double);

// Index references keep the raw constant-pool index (1-based) and the resolved text.
public sealed record CpClass(ushort NameIndex, string Name) : CpEntry(CpTag.Class);

public sealed record CpString(ushort Utf8Index, string Value) : CpEntry(CpTag.String);

public sealed record CpRef(CpTag Tag, ushort ClassIndex, string ClassName,
    ushort NameAndTypeIndex, string MemberName, string Descriptor) : CpEntry(Tag);

public sealed record CpNameAndType(ushort NameIndex, string Name,
    ushort DescriptorIndex, string Descriptor) : CpEntry(CpTag.NameAndType);

public sealed record CpMethodHandle(byte ReferenceKind, ushort ReferenceIndex) : CpEntry(CpTag.MethodHandle);

public sealed record CpMethodType(ushort DescriptorIndex, string Descriptor) : CpEntry(CpTag.MethodType);

public sealed record CpDynamic(CpTag Tag, ushort BootstrapMethodAttrIndex,
    ushort NameAndTypeIndex, string Name, string Descriptor) : CpEntry(Tag);

public sealed record CpModule(CpTag Tag, ushort NameIndex, string Name) : CpEntry(Tag);

public sealed record RawAttribute(string Name, ushort NameIndex, byte[] Info, int InfoOffset);

public sealed record RawFieldOrMethod(ushort AccessFlags, string Name, string Descriptor,
    IReadOnlyList<RawAttribute> Attributes);

public sealed record RawExceptionEntry(ushort StartPc, ushort EndPc, ushort HandlerPc,
    ushort CatchTypeIndex, string CatchType, int Order);

public enum StackMapFrameKind : byte
{
    Same = 0, SameLocals1StackItem = 64, SameLocals1StackItemExtended = 247,
    Chop = 248, SameFrameExtended = 251, Append = 252, FullFrame = 255
}

// Verification type info tags, JVMS §4.7.4.
public enum VerificationTypeTag : byte
{
    Top = 0, Integer = 1, Float = 2, Double = 3, Long = 4, Null = 5,
    UninitializedThis = 6, Object = 7, Uninitialized = 8
}

public sealed record VerificationTypeRaw(VerificationTypeTag Tag, ushort? CpIndexOrOffset,
    string Name, int ByteOffset);

public sealed record RawStackMapFrame(byte FrameType, StackMapFrameKind Kind,
    ushort FrameOffsetDelta, int OffsetDeltaSourceByte,
    IReadOnlyList<VerificationTypeRaw> Locals, IReadOnlyList<VerificationTypeRaw> Stack,
    ushort DeclaredOffset, int FrameTypeByteOffset, int? DeltaFieldOffset,
    int RawOffset, int RawLength, bool CorruptOffset);

public sealed record RawCode(ushort MaxStack, ushort MaxLocals, byte[] Bytecode,
    IReadOnlyList<RawExceptionEntry> ExceptionTable,
    IReadOnlyList<RawStackMapFrame> StackMapTable,
    IReadOnlyList<RawAttribute> OtherAttributes,
    int CodeAttributeOffset, int BytecodeOffset);

public sealed record RawMethod(ushort AccessFlags, string Name, string Descriptor,
    IReadOnlyList<RawAttribute> Attributes, RawCode Code)
{
    public bool IsStatic => (AccessFlags & 0x0008) != 0;
    public bool IsConstructor => Name == "<init>";
}

public sealed class ClassFile
{
    public uint Magic { get; init; }
    public ushort MinorVersion { get; init; }
    public ushort MajorVersion { get; init; }
    public ushort ConstantPoolCount { get; init; }
    public IReadOnlyList<CpEntry> ConstantPool { get; init; } = Array.Empty<CpEntry>();
    public ushort AccessFlags { get; init; }
    public string ThisClass { get; init; } = "";
    public string SuperClass { get; init; } = "";
    public IReadOnlyList<string> Interfaces { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RawFieldOrMethod> Fields { get; init; } = Array.Empty<RawFieldOrMethod>();
    public IReadOnlyList<RawMethod> Methods { get; init; } = Array.Empty<RawMethod>();
    public IReadOnlyList<RawAttribute> Attributes { get; init; } = Array.Empty<RawAttribute>();
    public byte[] OriginalBytes { get; init; } = Array.Empty<byte>();

    public CpEntry At(int oneBasedIndex) =>
        oneBasedIndex >= 1 && oneBasedIndex <= ConstantPool.Count
            ? ConstantPool[oneBasedIndex - 1]
            : throw new ClassFormatException($"Invalid constant pool index {oneBasedIndex}", $"cp:#{oneBasedIndex}");

    public string Utf8At(ushort index) => At(index) is CpUtf8 u ? u.Value
        : throw new ClassFormatException($"Constant pool #{index} is not Utf8", $"cp:#{index}");
}
