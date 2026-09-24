namespace Jbcv.Core.ClassFile;

/// <summary>Classfile version coordinate. 50=Java 6, 51=Java 7 (StackMapTable mandatory).</summary>
public sealed record ClassFileVersion(int Minor, int Major)
{
    public bool FramesMandatory => Major >= 51;
    public bool StackMapsRecognized => Major >= 50;
    public string Label => $"major{Major}.minor{Minor}";
}

/// <summary>Constant pool entry. Original index/tag/bytes are preserved; resolution is a derived step.</summary>
public abstract record ConstantPoolEntry(byte Tag, int Index, string RawHex)
{
    public virtual int Slots => 1;
}
public sealed record CpUtf8(int Index, string Text, string RawHex) : ConstantPoolEntry(1, Index, RawHex);
public sealed record CpInteger(int Index, int Value, string RawHex) : ConstantPoolEntry(3, Index, RawHex);
public sealed record CpFloat(int Index, int RawBits, string RawHex) : ConstantPoolEntry(4, Index, RawHex);
public sealed record CpLong(int Index, long Value, string RawHex) : ConstantPoolEntry(5, Index, RawHex)
{ public override int Slots => 2; }
public sealed record CpDouble(int Index, long RawBits, string RawHex) : ConstantPoolEntry(6, Index, RawHex)
{ public override int Slots => 2; }
public sealed record CpClass(int Index, int NameIndex, string RawHex) : ConstantPoolEntry(7, Index, RawHex);
public sealed record CpString(int Index, int StringIndex, string RawHex) : ConstantPoolEntry(8, Index, RawHex);
public sealed record CpFieldref(int Index, int ClassIndex, int NameAndTypeIndex, string RawHex) : ConstantPoolEntry(9, Index, RawHex);
public sealed record CpMethodref(int Index, int ClassIndex, int NameAndTypeIndex, string RawHex) : ConstantPoolEntry(10, Index, RawHex);
public sealed record CpInterfaceMethodref(int Index, int ClassIndex, int NameAndTypeIndex, string RawHex) : ConstantPoolEntry(11, Index, RawHex);
public sealed record CpNameAndType(int Index, int NameIndex, int DescriptorIndex, string RawHex) : ConstantPoolEntry(12, Index, RawHex);
public sealed record CpMethodHandle(int Index, byte ReferenceKind, int ReferenceIndex, string RawHex) : ConstantPoolEntry(15, Index, RawHex);
public sealed record CpMethodType(int Index, int DescriptorIndex, string RawHex) : ConstantPoolEntry(16, Index, RawHex);
public sealed record CpInvokeDynamic(int Index, int BootstrapMethodAttrIndex, int NameAndTypeIndex, string RawHex) : ConstantPoolEntry(18, Index, RawHex);

/// <summary>Raw instruction evidence: original offset, opcode and uninterpreted operand bytes.</summary>
public sealed record RawInstruction(int Offset, int Opcode, string Mnemonic, byte[] Operands, int Length)
{
    public int EndOffset => Offset + Length;
    public string OperandHex => Convert.ToHexString(Operands);
}

/// <summary>Exception_table_entry coordinates (JVMS 4.7.3), catch_type cp index 0 = finally/any.</summary>
public sealed record RawExceptionHandler(int RowIndex, int StartPc, int EndPc, int HandlerPc, int CatchType);

/// <summary>verification_type_info as it appears in a StackMapTable frame (tags 0..8).</summary>
public sealed record RawVerificationType(byte Tag, int? CpIndex, int? NewOffset)
{
    public string TagName => Tag switch
    {
        0 => "Top_variable_info",
        1 => "Integer_variable_info",
        2 => "Float_variable_info",
        3 => "Double_variable_info",
        4 => "Long_variable_info",
        5 => "Null_variable_info",
        6 => "UninitializedThis_variable_info",
        7 => "Object_variable_info",
        8 => "Uninitialized_variable_info",
        _ => $"Unknown_tag_{Tag}"
    };
}

/// <summary>One stack_map_frame with original frame-type byte, offset_delta and raw type entries.</summary>
public sealed record RawStackMapFrame(
    int Ordinal,
    int FrameType,
    int OffsetDelta,
    IReadOnlyList<RawVerificationType> Locals,
    IReadOnlyList<RawVerificationType> Stack,
    int ByteOffsetRangeStart,
    int ByteOffsetRangeEnd,
    string RawHex)
{
    public string FrameKind => FrameType switch
    {
        <= 63 => "same",
        <= 127 => "same_locals_1_stack_item",
        247 => "same_locals_1_stack_item_extended",
        <= 250 when FrameType >= 248 => "chop",
        251 => "same_frame_extended",
        <= 254 => "append",
        255 => "full_frame",
        _ => "reserved"
    };
}

public sealed record RawAttribute(string Name, string Hex);

/// <summary>Code attribute evidence before any normalization.</summary>
public sealed record RawCode(
    int MaxStack,
    int MaxLocals,
    byte[] CodeBytes,
    IReadOnlyList<RawExceptionHandler> ExceptionTable,
    IReadOnlyList<RawStackMapFrame> StackMapFrames,
    IReadOnlyList<RawAttribute> OtherAttributes)
{
    public string CodeHex => Convert.ToHexString(CodeBytes);
}

public sealed record RawMethod(
    int MethodIndex,
    ushort AccessFlags,
    int NameIndex,
    int DescriptorIndex,
    RawCode? Code)
{
    public bool IsStatic => (AccessFlags & 0x0008) != 0;
}

/// <summary>Raw classfile: offsets, constant pool, descriptors, frames, exception table and version retained verbatim.</summary>
public sealed record RawClassFile(
    ClassFileVersion Version,
    IReadOnlyList<ConstantPoolEntry> ConstantPool,
    string ThisClassName,
    string? SuperClassName,
    IReadOnlyList<RawMethod> Methods,
    string SourceLabel)
{
    public ConstantPoolEntry? Entry(int cpIndex) =>
        cpIndex >= 1 && cpIndex <= ConstantPool.Count ? ConstantPool[cpIndex - 1] : null;
    public string Utf8Text(int cpIndex) => Entry(cpIndex) is CpUtf8 u ? u.Text : $"#{cpIndex}";
    public string MethodName(RawMethod m) => Utf8Text(m.NameIndex);
    public string MethodDescriptor(RawMethod m) => Utf8Text(m.DescriptorIndex);
}
