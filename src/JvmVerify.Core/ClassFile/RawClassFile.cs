using System.Text.Json.Serialization;

namespace JvmVerify.Core.ClassFile;

// 坐标基准: classfile 内所有 pc 均为相对 Code 属性 code[] 字节数组的 0 基字节偏移 (unit: byte offset)
// 常量池索引为 class 文件 1 基槽位; Long/Double 占两个槽位 (后槽不出现)
public sealed record ClassFileVersion(int Minor, int Major)
{
    public string Text => $"major={Major}, minor={Minor}";
    public bool RequiresStackMapTable => Major >= 50;
}

public abstract record CpInfo(byte Tag, int Slot)
{
    public abstract string KindName { get; }
    public abstract string RawText { get; }
}

public sealed record Utf8Cp(byte Tag, int Slot, string Value) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_Utf8";
    public override string RawText => Value;
}

public sealed record IntegerCp(byte Tag, int Slot, int Value) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_Integer";
    public override string RawText => $"0x{Value:X8} ({Value})";
}

public sealed record FloatCp(byte Tag, int Slot, int RawBits, float Value) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_Float";
    public override string RawText => $"bits=0x{RawBits:X8} ({Value})";
}

public sealed record LongCp(byte Tag, int Slot, long Value) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_Long";
    public override string RawText => $"0x{Value:X16} ({Value}L) [category-2, 占两个 cp 槽]";
}

public sealed record DoubleCp(byte Tag, int Slot, long RawBits, double Value) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_Double";
    public override string RawText => $"bits=0x{RawBits:X16} ({Value}D) [category-2, 占两个 cp 槽]";
}

public sealed record ClassCp(byte Tag, int Slot, int NameIndex) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_Class";
    public override string RawText => $"name_index=#{NameIndex}";
}

public sealed record StringCp(byte Tag, int Slot, int StringIndex) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_String";
    public override string RawText => $"string_index=#{StringIndex}";
}

public sealed record RefCp(byte Tag, int Slot, int ClassIndex, int NameAndTypeIndex) : CpInfo(Tag, Slot)
{
    public override string KindName => Tag == 9 ? "CONSTANT_Fieldref" : Tag == 10 ? "CONSTANT_Methodref" : "CONSTANT_InterfaceMethodref";
    public override string RawText => $"class_index=#{ClassIndex}, name_and_type_index=#{NameAndTypeIndex}";
}

public sealed record NameAndTypeCp(byte Tag, int Slot, int NameIndex, int DescriptorIndex) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_NameAndType";
    public override string RawText => $"name_index=#{NameIndex}, descriptor_index=#{DescriptorIndex}";
}

public sealed record MethodHandleCp(byte Tag, int Slot, int ReferenceKind, int ReferenceIndex) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_MethodHandle";
    public override string RawText => $"reference_kind={ReferenceKind}, reference_index=#{ReferenceIndex}";
}

public sealed record MethodTypeCp(byte Tag, int Slot, int DescriptorIndex) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_MethodType";
    public override string RawText => $"descriptor_index=#{DescriptorIndex}";
}

public sealed record InvokeDynamicCp(byte Tag, int Slot, int BootstrapMethodAttrIndex, int NameAndTypeIndex) : CpInfo(Tag, Slot)
{
    public override string KindName => "CONSTANT_InvokeDynamic";
    public override string RawText => $"bootstrap_method_attr_index={BootstrapMethodAttrIndex}, name_and_type_index=#{NameAndTypeIndex}";
}

// 无法解析/无法转换的原始值留在原位, 不丢弃
public sealed record UnknownCp(byte Tag, int Slot, byte[] Raw) : CpInfo(Tag, Slot)
{
    public bool IsGhostSlot { get; init; }
    public override string KindName => IsGhostSlot ? "(Long/Double 第二槽, 不存在)" : $"CONSTANT_Unknown(tag={Tag})";
    public override string RawText => IsGhostSlot ? "<ghost slot>" : Convert.ToHexString(Raw);
}

public sealed record Instruction(
    int Offset,
    int Length,
    int Opcode,
    string Mnemonic,
    byte[] OperandRaw,
    int[] Operands,
    int? BranchTarget,
    IReadOnlyList<int> SwitchTargets,
    string OperandText)
{
    public string Coordinate => $"code[pc={Offset}..{Offset + Length - 1}]";
}

public enum VerificationTypeTag : byte
{
    Top = 0, Integer = 1, Float = 2, Double = 3, Long = 4, Null = 5,
    UninitializedThis = 6, Object = 7, Uninitialized = 8
}

public sealed record VerificationTypeInfo(
    byte Tag,
    string TypeName,
    int? CpIndex,
    int? NewPc,
    string RawText)
{
    public VerificationTypeTag TagE => (VerificationTypeTag)Tag;
}

public enum StackMapFrameKind { Same, SameExtended, SameLocals1StackItem, SameLocals1StackItemExtended, Chop, Append, Full, Damaged }

// StackMapTable 原始帧: offset_delta 原样保留; bytecodeOffset 为派生绝对 pc, 解析失败时为 null 并附诊断
public sealed record StackMapFrame(
    int FrameOrdinal,
    int FrameTypeByte,
    StackMapFrameKind Kind,
    int OffsetDelta,
    int? BytecodeOffset,
    bool OffsetResolved,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    string RawText);

public sealed record ExceptionEntry(
    int StartPc, int EndPc, int HandlerPc, int CatchTypeCpIndex,
    string? CatchTypeName,
    string RawText,
    string? Diagnostic)
{
    public string Coordinate => $"exception_table[startPc={StartPc}, endPc={EndPc}, handlerPc={HandlerPc}]";
    public bool IsCatchAll => CatchTypeCpIndex == 0;
}

public sealed record CodeAttribute(
    int MaxStack, int MaxLocals,
    byte[] Bytecode,
    IReadOnlyList<Instruction> Instructions,
    IReadOnlyList<ExceptionEntry> ExceptionTable,
    IReadOnlyList<StackMapFrame>? StackMapTable,
    bool StackMapAttributePresent,
    IReadOnlyList<string> Diagnostics);

public sealed record MethodInfo(
    int AccessFlags,
    string Name,
    string Descriptor,
    CodeAttribute? Code,
    string Coordinate);

public sealed record RawAttribute(string Name, byte[] Bytes);

public sealed record RawClassFile(
    ClassFileVersion Version,
    IReadOnlyList<CpInfo> ConstantPool,
    string ThisClass,
    string? SuperClass,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<MethodInfo> Methods,
    IReadOnlyList<RawAttribute> OtherAttributes,
    byte[] OriginalBytes,
    string Sha256,
    IReadOnlyList<string> Diagnostics)
{
    public CpInfo? Cp(int slot) => slot is >= 1 && slot - 1 < ConstantPool.Count ? ConstantPool[slot - 1] : null;
}
