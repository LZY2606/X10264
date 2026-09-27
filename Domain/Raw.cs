using System.Text.Json.Serialization;

namespace JvmVerifier.Domain;

public sealed record RawQuantity(long Value, string Unit, string CoordinateBasis, string OriginalText)
{
    public override string ToString() => $"{Value}{Unit}";
}

public abstract record ConstantPoolEntry(int Index, int Tag)
{
    public abstract string StableId { get; }
}

public sealed record Utf8Entry(int Index, string Value) : ConstantPoolEntry(Index, 1)
{
    public override string StableId => $"cp#{Index}:Utf8";
}
public sealed record IntegerEntry(int Index, int Value) : ConstantPoolEntry(Index, 3)
{
    public override string StableId => $"cp#{Index}:Integer";
}
public sealed record FloatEntry(int Index, int RawBits) : ConstantPoolEntry(Index, 4)
{
    public override string StableId => $"cp#{Index}:Float";
}
public sealed record LongEntry(int Index, long Value) : ConstantPoolEntry(Index, 5)
{
    public override string StableId => $"cp#{Index}:Long";
}
public sealed record DoubleEntry(int Index, long RawBits) : ConstantPoolEntry(Index, 6)
{
    public override string StableId => $"cp#{Index}:Double";
}
public sealed record ClassEntry(int Index, int NameIndex) : ConstantPoolEntry(Index, 7)
{
    public override string StableId => $"cp#{Index}:Class";
}
public sealed record StringEntry(int Index, int Utf8Index) : ConstantPoolEntry(Index, 8)
{
    public override string StableId => $"cp#{Index}:String";
}
public sealed record RefEntry(int Index, int Tag, int ClassIndex, int NameAndTypeIndex, string Kind) : ConstantPoolEntry(Index, Tag)
{
    public override string StableId => $"cp#{Index}:{Kind}";
}
public sealed record NameAndTypeEntry(int Index, int NameIndex, int DescriptorIndex) : ConstantPoolEntry(Index, 12)
{
    public override string StableId => $"cp#{Index}:NameAndType";
}
public sealed record SimpleEntry(int Index, int Tag, string Kind, IReadOnlyList<int> Refs, string? Text = null) : ConstantPoolEntry(Index, Tag)
{
    public override string StableId => $"cp#{Index}:{Kind}";
}
public sealed record EmptyConstantPoolSlot(int Index) : ConstantPoolEntry(Index, 0)
{
    public override string StableId => $"cp#{Index}:phantom";
}

public sealed record RawOperand(string Name, object? Value, string OriginalText, int Offset, int Length);

public sealed record RawInstruction(
    int Offset,
    int Opcode,
    string Mnemonic,
    IReadOnlyList<RawOperand> Operands,
    int Length,
    string RawBytesBase64)
{
    public string StableId => $"instruction@{Offset}";
}

public sealed record RawExceptionEntry(
    int Index,
    int StartPc,
    int EndPc,
    int HandlerPc,
    int CatchType,
    string RawBytesBase64)
{
    public string StableId => $"exception#{Index}";
}

public abstract record VerificationTypeInfo(int Tag, string Kind, int RawOffset, int RawLength);
public sealed record TopVariableInfo(int RawOffset, int RawLength) : VerificationTypeInfo(0, "top", RawOffset, RawLength);
public sealed record IntegerVariableInfo(int RawOffset, int RawLength) : VerificationTypeInfo(1, "integer", RawOffset, RawLength);
public sealed record FloatVariableInfo(int RawOffset, int RawLength) : VerificationTypeInfo(2, "float", RawOffset, RawLength);
public sealed record DoubleVariableInfo(int RawOffset, int RawLength) : VerificationTypeInfo(3, "double", RawOffset, RawLength);
public sealed record LongVariableInfo(int RawOffset, int RawLength) : VerificationTypeInfo(4, "long", RawOffset, RawLength);
public sealed record NullVariableInfo(int RawOffset, int RawLength) : VerificationTypeInfo(5, "null", RawOffset, RawLength);
public sealed record UninitializedThisVariableInfo(int RawOffset, int RawLength) : VerificationTypeInfo(6, "uninitializedThis", RawOffset, RawLength);
public sealed record ObjectVariableInfo(int CpIndex, int RawOffset, int RawLength) : VerificationTypeInfo(7, "object", RawOffset, RawLength);
public sealed record UninitializedVariableInfo(int Offset, int RawOffset, int RawLength) : VerificationTypeInfo(8, "uninitialized", RawOffset, RawLength);

public sealed record RawStackMapFrame(
    int Index,
    int FrameType,
    int OffsetDelta,
    int? ExplicitBytecodeOffset,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    string RawBytesBase64,
    IReadOnlyList<string> Diagnostics)
{
    public string StableId => $"frame#{Index}";
}

public sealed record RawAttribute(string Name, string RawBytesBase64);

public sealed record RawMethod(
    int Index,
    int AccessFlags,
    int NameIndex,
    int DescriptorIndex,
    string Name,
    string Descriptor,
    int MaxStack,
    int MaxLocals,
    string CodeBase64,
    IReadOnlyList<RawInstruction> Instructions,
    IReadOnlyList<RawExceptionEntry> ExceptionTable,
    IReadOnlyList<RawStackMapFrame> Frames,
    IReadOnlyList<RawAttribute> Attributes)
{
    public string StableId => $"method#{Index}:{Name}:{Descriptor}";
}

public sealed record RawClassFile(
    string CaseId,
    uint Magic,
    int MinorVersion,
    int MajorVersion,
    IReadOnlyList<ConstantPoolEntry> ConstantPool,
    int ThisClass,
    int SuperClass,
    IReadOnlyList<int> Interfaces,
    IReadOnlyList<RawMethod> Methods,
    IReadOnlyList<RawAttribute> Attributes,
    string OriginalSource,
    string RawBytesBase64,
    IReadOnlyList<ImportDiagnostic> Diagnostics)
{
    public string VersionCoordinate => $"{MajorVersion}.{MinorVersion}";
    public string StableId => $"classfile:{CaseId}:v{VersionCoordinate}";
    public string ClassName => ConstantPool.OfType<ClassEntry>().FirstOrDefault(c => c.Index == ThisClass) is { } cls && ConstantPool.OfType<Utf8Entry>().FirstOrDefault(u => u.Index == cls.NameIndex) is { } utf8 ? utf8.Value.Replace('/', '.') : $"this#{ThisClass}";
}

public sealed record ImportDiagnostic(string Code, string Message, string? Coordinate, IReadOnlyList<string> EvidenceIds)
{
    public string StableId => $"{Code}:{Coordinate ?? "classfile"}:{string.Join("|", EvidenceIds)}";
}
