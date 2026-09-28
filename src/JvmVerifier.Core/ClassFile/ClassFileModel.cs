using System;
using System.Collections.Generic;
using System.Text;

namespace JvmVerifier.Core.ClassFile;

/// <summary>
/// 常量池项。原始表示保留在 RawText / 各索引字段中；Raw 表示是导入时未规范化的证据。
/// 所有字节偏移以 classfile 字节为坐标基准（0-based）。
/// </summary>
public sealed record ConstantPoolEntry(
    int Index,
    string Tag,
    string RawText,
    int? ClassNameIndex = null,
    int? NameAndTypeIndex = null,
    int? Utf8Index = null,
    string? Utf8Text = null,
    int? RefKind = null,
    int? RefIndex = null,
    int? IntegerValue = null,
    float? FloatValue = null,
    long? LongValue = null,
    double? DoubleValue = null,
    int? StringIndex = null,
    int? DescriptorIndex = null,
    int? BootstrapMethodAttrIndex = null,
    int ByteOffset = -1);

public sealed record RawOperand(string Kind, int ByteOffset, int Length, long Value, string Text);

public sealed record Instruction(
    int Offset,
    int Length,
    int Opcode,
    string Mnemonic,
    IReadOnlyList<RawOperand> Operands,
    int? BranchTarget,
    bool IsBranch,
    bool IsReturn,
    bool IsThrow,
    bool IsUnconditionalJump)
{
    public int EndOffsetExclusive => Offset + Length;
}

public sealed record ExceptionEntry(
    int StartPc,
    int EndPc,
    int HandlerPc,
    int CatchTypeIndex,
    string CatchTypeDescriptor,
    bool CatchAll,
    int ByteOffset);

public sealed record VerificationTypeInfo(int Tag, string Name, int? CpIndex, int? NewOffset, int ByteOffset);

public sealed record StackMapFrame(
    int FrameIndex,
    int RawFrameType,
    int ByteOffsetDelta,
    int OffsetDelta,
    int? ExplicitByteOffset,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    int ByteOffset,
    int Length,
    bool Corrupt = false,
    string? CorruptionDiagnostic = null)
{
    public int TargetOffset => ExplicitByteOffset ?? ByteOffsetDelta;
}

public sealed record AttributeInfo(string Name, int ByteOffset, int Length, byte[] RawBytes);

public sealed record MethodInfo(
    int AccessFlags,
    string Name,
    string Descriptor,
    int MaxStack,
    int MaxLocals,
    IReadOnlyList<Instruction> Instructions,
    IReadOnlyList<ExceptionEntry> ExceptionTable,
    IReadOnlyList<StackMapFrame> StackMapFrames,
    byte[] Code,
    int CodeAttributeByteOffset,
    IReadOnlyList<AttributeInfo> Attributes,
    bool StackMapTablePresent,
    int ByteOffset);

public sealed record FieldInfo(int AccessFlags, string Name, string Descriptor, int ByteOffset);

public sealed record ClassFileModel(
    uint Magic,
    int MinorVersion,
    int MajorVersion,
    string VersionLabel,
    IReadOnlyList<ConstantPoolEntry> ConstantPool,
    IReadOnlyList<FieldInfo> Fields,
    IReadOnlyList<MethodInfo> Methods,
    string ThisClassName,
    string SuperClassName,
    IReadOnlyList<string> Interfaces,
    int AccessFlags,
    byte[] RawBytes,
    IReadOnlyList<ParseDiagnostic> Diagnostics);

public sealed record ParseDiagnostic(string Code, string Message, int ByteOffset, string Severity);

public static class ConstantPool
{
    public const int Utf8 = 1;
    public const int Integer = 3;
    public const int Float = 4;
    public const int Long = 5;
    public const int Double = 6;
    public const int Class = 7;
    public const int String = 8;
    public const int Fieldref = 9;
    public const int Methodref = 10;
    public const int InterfaceMethodref = 11;
    public const int NameAndType = 12;
    public const int MethodHandle = 15;
    public const int MethodType = 16;
    public const int Dynamic = 17;
    public const int InvokeDynamic = 18;
    public const int Module = 19;
    public const int Package = 20;

    public static string TagName(int tag) => tag switch
    {
        1 => "Utf8", 3 => "Integer", 4 => "Float", 5 => "Long", 6 => "Double",
        7 => "Class", 8 => "String", 9 => "Fieldref", 10 => "Methodref",
        11 => "InterfaceMethodref", 12 => "NameAndType", 15 => "MethodHandle",
        16 => "MethodType", 17 => "Dynamic", 18 => "InvokeDynamic",
        19 => "Module", 20 => "Package", _ => $"Unknown({tag})"
    };
}

public static class ClassFileVersions
{
    public static string Label(int major) => major switch
    {
        45 => "JDK 1.1 (major 45)",
        46 => "JDK 1.2 (major 46)",
        47 => "JDK 1.3 (major 47)",
        48 => "JDK 1.4 (major 48)",
        49 => "Java SE 5.0 (major 49)",
        50 => "Java SE 6 (major 50)",
        51 => "Java SE 7 (major 51)",
        52 => "Java SE 8 (major 52)",
        53 => "Java SE 9 (major 53)",
        55 => "Java SE 11 (major 55)",
        61 => "Java SE 17 (major 61)",
        65 => "Java SE 21 (major 65)",
        _ => $"major {major}"
    };
}
