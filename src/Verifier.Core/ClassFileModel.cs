using System.Text.Json.Serialization;

namespace Verifier.Core;

/// <summary>
/// 常量池项。Tag 为 classfile 原始标签（u1），Index 为 1 基常量池序号。
/// Raw 保留该 entry 在 classfile 中的原始字节区间。所有字段都保留原始表示；
/// 解析器无法解释的 entry 用 <see cref="CpTag.Unknown"/> 保留并挂诊断。
/// </summary>
public sealed record CpEntry(
    int Index,
    CpTag Tag,
    RawSpan Raw,
    string? Utf8 = null,
    int? ClassRef = null,
    int? NameAndTypeRef = null,
    string? Descriptor = null,
    int? StringRef = null,
    int? IntegerValue = null,
    long? LongValue = null,
    float? FloatValue = null,
    double? DoubleValue = null,
    int? RefKind = null,
    int? RefIndex = null,
    int? BootstrapMethodAttrIndex = null)
{
    /// <summary>常量池项的稳定身份（以序号为坐标）。</summary>
    [JsonIgnore] public string StableId => $"cp#{Index}";
}

public enum CpTag : byte
{
    Unknown = 0, Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6,
    Class = 7, String = 8, Fieldref = 9, Methodref = 10, InterfaceMethodref = 11,
    NameAndType = 2, MethodHandle = 15, MethodType = 16, Dynamic = 17,
    InvokeDynamic = 18, Module = 19, Package = 20
}

public sealed record FieldNode(AccessFlags Access, string Name, string Descriptor, int Index);

public sealed record MethodNode(AccessFlags Access, string Name, string Descriptor, int Index, CodeAttribute? Code);

[Flags]
public enum AccessFlags : ushort
{
    None = 0, Public = 0x0001, Private = 0x0002, Protected = 0x0004, Static = 0x0008,
    Final = 0x0010, Synchronized = 0x0020, Bridge = 0x0040, Varargs = 0x0080,
    Native = 0x0100, Abstract = 0x0400, Strict = 0x0800
}

/// <summary>一条方法 Code 属性：指令、异常表、StackMapTable 与原始字节全部保留。</summary>
public sealed record CodeAttribute(
    RawSpan Raw,
    int MaxStack,
    int MaxLocals,
    byte[] CodeBytes,
    IReadOnlyList<InstructionNode> Instructions,
    IReadOnlyList<ExceptionEntry> ExceptionTable,
    IReadOnlyList<StackMapFrameEntry> StackMapTable);

/// <summary>指令。Offset 以字节为单位、原点 Code[0]；Operands 为原始 operand 字节，OperandsText 为解码表示。</summary>
public sealed record InstructionNode(
    int Offset,
    int Opcode,
    string Mnemonic,
    byte[] Operands,
    string OperandsText,
    RawSpan Raw)
{
    public string StableId => $"insn@{Offset}";
}

/// <summary>异常表项：范围与处理器偏移全部以字节为单位、原点 Code[0]；CatchTypeCp 为常量池序号（0=finally）。</summary>
public sealed record ExceptionEntry(
    int StartPc, int EndPc, int HandlerPc, int CatchTypeCp, string CatchTypeName, RawSpan Raw)
{
    public string StableId => $"exn[{StartPc},{EndPc})->@{HandlerPc}";
}

public enum StackFrameKind
{
    Same, SameLocals1StackItem, SameLocals1StackItemExtended, Chop, SameFrameExtended, Append, Full
}

/// <summary>
/// StackMapTable 单帧。DeltaOffset/Offset 以字节为单位；Locals/Stack 为声明的验证类型；
/// RawFrame 保留原始字节。解析损坏时保留 OffsetDeltaRaw 与诊断，绝不以后续帧反推。
/// </summary>
public sealed record StackMapFrameEntry(
    int Index,
    StackFrameKind Kind,
    int OffsetDelta,
    int Offset,
    int? OffsetDeltaRaw,
    IReadOnlyList<VerifType> Locals,
    IReadOnlyList<VerifType> Stack,
    RawSpan RawFrame,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public string StableId => $"frame[{Index}]@{Offset}";
}

public sealed record ClassFileNode(
    byte[] RawBytes,
    int MinorVersion,
    int MajorVersion,
    string JavaVersion,
    IReadOnlyList<CpEntry> ConstantPool,
    AccessFlags Access,
    string ThisClass,
    string? SuperClass,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<FieldNode> Fields,
    IReadOnlyList<MethodNode> Methods,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public ClassVersion Version => new(MajorVersion, MinorVersion);
}

/// <summary>classfile 版本。Major 为 classfile major_version（坐标基准），Java 为对应平台版本。</summary>
public readonly record struct ClassVersion(int Major, int Minor)
{
    public string Label => Major switch
    {
        >= 69 => $"Java {Major - 44} (major {Major}.{Minor})",
        52 => $"Java 8 (major {Major}.{Minor})",
        51 => $"Java 7 (major {Major}.{Minor})",
        50 => $"Java 6 (major {Major}.{Minor})",
        49 => $"Java 5 (major {Major}.{Minor})",
        _ => $"major {Major}.{Minor}"
    };
    public bool RequiresStackMapTable => Major >= 50;
}
