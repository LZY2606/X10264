using System.Collections.Generic;

namespace Jbc.Core.Classfile;

// 所有领域量都携带坐标基准/单位；原始表示原样保留，派生层才做规范化。
public sealed record Qty(long Value, string Unit, string? RawText = null)
{
    public override string ToString() => RawText ?? $"{Value}{Unit}";
}

public enum CpTag
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6, Class = 7,
    String = 8, Fieldref = 9, Methodref = 10, InterfaceMethodref = 11,
    NameAndType = 12, MethodHandle = 15, MethodType = 16, Dynamic = 17,
    InvokeDynamic = 18, Module = 19, Package = 20
}

public sealed record RawCpEntry(int Index, CpTag Tag, string Raw, string? Text = null,
    int? Ref1 = null, int? Ref2 = null, long? Numeric = null);

public enum StackFrameKind
{
    Same, SameLocals1StackItem, Chop, Append, Full, Reserved
}

public sealed record RawStackFrame(Qty BytecodeOffset, StackFrameKind Kind, int Tag,
    IReadOnlyList<string> Locals, IReadOnlyList<string> Stack,
    string? CorruptNote = null, bool OffsetTrusted = true);

public sealed record RawException(Qty StartPc, Qty EndPc, Qty HandlerPc, int CatchTypeCp,
    string CatchClassName, string? FinallyName = null);

public sealed record RawInstruction(Qty Offset, int Opcode, string Mnemonic,
    IReadOnlyList<int> OperandCpRefs, IReadOnlyList<int> OperandBranches,
    string? Descriptor = null, int? WideLocal = null, int? Line = null)
{
    public int Length { get; init; }
}

public sealed record RawCode(Qty MaxStack, Qty MaxLocals,
    IReadOnlyList<RawInstruction> Instructions,
    IReadOnlyList<RawException> ExceptionTable,
    IReadOnlyList<RawStackFrame> StackMapTable,
    bool StackMapPresent);

public sealed record RawMethod(string Name, string Descriptor, int AccessFlags, RawCode Code);

public sealed record RawClassFile(
    Qty MinorVersion, Qty MajorVersion,
    IReadOnlyList<RawCpEntry> ConstantPool,
    string ThisClassName, string? SuperClassName,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<RawMethod> Methods,
    string SourceFormat,
    IReadOnlyList<string> ParseDiagnostics)
{
    public string VersionText => $"{MajorVersion.Value}.{MinorVersion.Value}";
}
