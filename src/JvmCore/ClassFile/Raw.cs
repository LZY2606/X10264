namespace JvmCore.ClassFile;

// 常量池标签（JVMS §4.4）。原始 tag 字节与解码后的值都保留。
public enum CpTag : byte
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6,
    Class = 7, String = 8, Fieldref = 9, Methodref = 10,
    InterfaceMethodref = 11, NameAndType = 12, MethodHandle = 15,
    MethodType = 16, Dynamic = 17, InvokeDynamic = 18, Module = 19, Package = 20
}

public sealed record CpEntry(int Index, CpTag Tag, string RawHex, string? Text, int[]? Refs);

public sealed record RawInstruction(
    int Offset,
    int Opcode,
    string Mnemonic,
    int[] Operands,        // 解码后的操作数（分支目标已是绝对字节偏移，坐标基准：方法字节码起点 0）
    string OperandHex,     // 原始操作数字节，未规范化
    int Length);

public sealed record RawExceptionRange(
    int StartPc, int EndPc, int HandlerPc, int CatchType, // CatchType=0 => finally(any)
    string RawHex);

public enum VerificationTypeTag : byte
{
    Top = 0, Integer = 1, Float = 2, Double = 3, Long = 4, Null = 5,
    UninitializedThis = 6, Object = 7, Uninitialized = 8
}

public sealed record RawVerificationType(VerificationTypeTag Tag, ushort ConstPoolIndex, int NewOffset, string RawHex);

public enum StackFrameKind
{
    Same, SameLocals1StackItem, Chop, Append, Full, ReservedSameExtended
}

public sealed record RawStackMapFrame(
    int FrameType,
    StackFrameKind Kind,
    int OffsetDelta,
    int BytecodeOffset,        // 规范化坐标：绝对字节偏移（派生层产物，非原始证据）
    RawVerificationType[]? Locals,
    RawVerificationType[]? Stack,
    string RawHex,
    int RawOrder);             // 在 StackMapTable 中的原始序号

public sealed record RawCodeAttribute(
    int MaxStack, int MaxLocals,
    RawInstruction[] Instructions,
    RawExceptionRange[] ExceptionTable,
    RawStackMapFrame[] StackMapTable,
    string CodeHex,            // 完整 Code 原始字节（含属性体），无法转换时留在原位置
    bool StackMapPresent);

public sealed record RawMethod(
    ushort AccessFlags, string Name, string Descriptor,
    RawCodeAttribute? Code, string RawHex);

public sealed record RawClassFile(
    uint Minor, uint Major,           // classfile 版本，原始保留
    string VersionLabel,
    CpEntry[] ConstantPool,
    RawMethod[] Methods,
    byte[] OriginalBytes,            // 导入时的原始二进制（JSON 来源则为规范化 canonical bytes）
    string Source,                   // "binary" | "case-pack-json"
    IReadOnlyList<string> Diagnostics)
{
    public string? Utf8(int index) =>
        index > 0 && index < ConstantPool.Length + 1 && ConstantPool[index - 1] is { Tag: CpTag.Utf8 } u ? u.Text : null;
}
