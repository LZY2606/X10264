namespace BytecodeSeal.Core;

/// <summary>A diagnostic attached to a value that could not be normalized. The
/// original value stays in place; the diagnostic explains why.</summary>
public sealed record Diagnostic(string Id, string Coordinate, string Message);

/// <summary>One constant-pool entry. Raw keeps the original byte-level
/// representation captured at import time; normalization happens later.</summary>
public sealed record CpEntry(int Index, string Tag, string Value, string RawHex, IReadOnlyList<int> Refs);

public sealed record VerificationTypeInfo(string Kind, int? ClassIndex, string? ClassName, int? Offset);

/// <summary>A raw StackMapTable frame as stored in the classfile. AbsoluteOffset
/// uses the method code array as coordinate base, unit = byte, 0-based.</summary>
public sealed record StackMapFrame(
    int AbsoluteOffset,
    int OffsetDelta,
    string FrameType,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack);

public sealed record ExceptionHandler(
    int StartPc, int EndPc, int HandlerPc, int CatchTypeIndex, string CatchType);

public sealed record MethodModel(
    string Name,
    string Descriptor,
    int AccessFlags,
    int MaxStack,
    int MaxLocals,
    string CodeHex,
    IReadOnlyList<ExceptionHandler> ExceptionTable,
    IReadOnlyList<StackMapFrame> Frames);

public sealed record ClassFileModel(
    int MinorVersion,
    int MajorVersion,
    string ThisClass,
    string SuperClass,
    IReadOnlyList<CpEntry> ConstantPool,
    IReadOnlyList<MethodModel> Methods,
    IReadOnlyList<Diagnostic> Diagnostics,
    string SourceHash)
{
    public string SemanticsMode => MajorVersion >= 50 ? "check-frames" : "infer";
}
