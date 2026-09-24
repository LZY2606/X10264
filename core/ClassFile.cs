namespace JvmVerify.Core;

/// <summary>Raw constant pool entry. The original tag and raw bytes are preserved;
/// resolution into strings/refs happens in the derived layer.</summary>
public abstract record CpEntry(byte Tag)
{
    public sealed record Utf8(string Value) : CpEntry(1);
    public sealed record IntConst(int Value) : CpEntry(3);
    public sealed record FloatConst(float Value) : CpEntry(4);
    public sealed record LongConst(long Value) : CpEntry(5);
    public sealed record DoubleConst(double Value) : CpEntry(6);
    public sealed record ClassRef(ushort NameIndex) : CpEntry(7);
    public sealed record StringRef(ushort StringIndex) : CpEntry(8);
    public sealed record FieldRef(ushort ClassIndex, ushort NameAndTypeIndex) : CpEntry(9);
    public sealed record MethodRef(ushort ClassIndex, ushort NameAndTypeIndex) : CpEntry(10);
    public sealed record InterfaceMethodRef(ushort ClassIndex, ushort NameAndTypeIndex) : CpEntry(11);
    public sealed record NameAndType(ushort NameIndex, ushort DescriptorIndex) : CpEntry(12);
    public sealed record MethodHandle(byte Kind, ushort ReferenceIndex) : CpEntry(15);
    public sealed record MethodType(ushort DescriptorIndex) : CpEntry(16);
    public sealed record InvokeDynamic(ushort BootstrapIndex, ushort NameAndTypeIndex) : CpEntry(18);
    /// <summary>Unparseable / unknown tag: raw payload preserved, never dropped.</summary>
    public sealed record Raw(byte TagByte, byte[] Payload) : CpEntry(TagByte);
}

public sealed record ExceptionHandler(
    ByteOffset StartPc, ByteOffset EndPc, ByteOffset HandlerPc,
    ushort CatchTypeIndex, string? CatchTypeClass);

public sealed record StackMapFrameInfo(
    int FrameType,
    ByteOffset Offset,          // effective code offset this frame applies to
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    byte[] RawBytes);

public sealed record VerificationTypeInfo(byte Tag, string? ClassName = null, int? NewOffset = null);

public sealed record ParsedMethod(
    string Name,
    string RawDescriptor,               // original descriptor string, preserved verbatim
    MethodDescriptor? Descriptor,       // derived layer; null when unconvertible
    IReadOnlyList<Diagnostic> Diagnostics,
    ushort AccessFlags,
    ushort MaxStack,
    ushort MaxLocals,
    byte[] Code,
    IReadOnlyList<ExceptionHandler> ExceptionTable,
    IReadOnlyList<StackMapFrameInfo> StackMapFrames,
    ByteOffset CodeFileOffset);         // where the code array starts in the classfile

public sealed record MethodDescriptor(
    IReadOnlyList<string> ParameterTypes, string ReturnType);

public sealed record ParsedClassFile(
    uint Magic,
    ClassFileVersion Version,
    IReadOnlyList<CpEntry?> ConstantPool,   // index 0 unused; long/double take two slots
    string ThisClass,
    string? SuperClass,
    IReadOnlyList<ParsedMethod> Methods,
    IReadOnlyList<Diagnostic> Diagnostics,
    byte[] RawBytes,
    string Sha256);
