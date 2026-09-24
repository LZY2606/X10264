namespace BytecodeAtlas.Core;

/// <summary>A diagnostic attached to a value that could not be converted/normalized.
/// The raw value stays in place; the diagnostic explains why.</summary>
public sealed record Diagnostic(string Code, string Message, string Coordinate);

/// <summary>Constant pool entries. Index is the original 1-based pool index.
/// Unknown tags are preserved raw (never dropped) with a diagnostic.</summary>
public abstract record CpEntry(int Index, string Tag)
{
    public sealed record Utf8(int Index, string Value) : CpEntry(Index, "Utf8");
    public sealed record Class(int Index, int NameIndex) : CpEntry(Index, "Class");
    public sealed record NameAndType(int Index, int NameIndex, int DescIndex) : CpEntry(Index, "NameAndType");
    public sealed record Ref(int Index, string Kind, int ClassIndex, int NameAndTypeIndex) : CpEntry(Index, Kind);
    public sealed record Str(int Index, int StringIndex) : CpEntry(Index, "String");
    public sealed record Int32(int Index, int Value) : CpEntry(Index, "Integer");
    public sealed record Flt(int Index, float Value) : CpEntry(Index, "Float");
    public sealed record Int64(int Index, long Value) : CpEntry(Index, "Long");
    public sealed record Dbl(int Index, double Value) : CpEntry(Index, "Double");
    public sealed record MethodHandle(int Index, int RefKind, int RefIndex) : CpEntry(Index, "MethodHandle");
    public sealed record MethodType(int Index, int DescIndex) : CpEntry(Index, "MethodType");
    public sealed record InvokeDynamic(int Index, int Bootstrap, int NameAndTypeIndex) : CpEntry(Index, "InvokeDynamic");
    public sealed record Unknown(int Index, byte TagByte, byte[] Raw) : CpEntry(Index, "Unknown");
}

/// <summary>One parsed stack-map frame. RawBytes preserve the original encoding;
/// Offset is derived (previous frame offset + delta + 1, or delta for the first).</summary>
public sealed record StackMapFrame(
    int FrameType,
    int OffsetDelta,
    int Offset,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    byte[] RawBytes,
    int FileOffset);

/// <summary>verification_type_info, raw tag preserved.</summary>
public sealed record VerificationTypeInfo(int Tag, int? CpIndexOrOffset);

public sealed record ExceptionHandler(int StartPc, int EndPc, int HandlerPc, int CatchTypeIndex, string? CatchType);

public sealed record InstructionInfo(int Offset, int Opcode, string Mnemonic, byte[] OperandBytes, int Length)
{
    public int EndOffset => Offset + Length;
}

public sealed class MethodDef
{
    public required string Name { get; init; }
    public required string Descriptor { get; init; }
    public required int AccessFlags { get; init; }
    public required int MaxStack { get; init; }
    public required int MaxLocals { get; init; }
    public required byte[] Code { get; init; }
    public required List<ExceptionHandler> ExceptionTable { get; init; }
    public required List<StackMapFrame> Frames { get; init; }
    public required List<InstructionInfo> Instructions { get; init; }
    public List<Diagnostic> Diagnostics { get; } = new();
    public int CodeAttributeFileOffset { get; init; }
}

public sealed class ClassFile
{
    public required int Minor { get; init; }
    public required int Major { get; init; }
    public required List<CpEntry?> Pool { get; init; } // slot 0 and cat-2 second slots are null
    public required string ThisClass { get; init; }
    public required string SuperClass { get; init; }
    public required List<MethodDef> Methods { get; init; }
    public required byte[] RawBytes { get; init; }
    public required string Sha256 { get; init; }
    public List<Diagnostic> Diagnostics { get; } = new();

    public CpEntry? Entry(int index) =>
        index > 0 && index < Pool.Count ? Pool[index] : null;

    public string? Utf8(int index) => Entry(index) is CpEntry.Utf8 u ? u.Value : null;

    public string? ClassName(int classIndex) => Entry(classIndex) is CpEntry.Class c ? Utf8(c.NameIndex) : null;

    public (string? Name, string? Desc) NameAndType(int ntIndex) =>
        Entry(ntIndex) is CpEntry.NameAndType nt ? (Utf8(nt.NameIndex), Utf8(nt.DescIndex)) : (null, null);

    public (string? Class, string? Name, string? Desc) RefParts(int refIndex) =>
        Entry(refIndex) is CpEntry.Ref r
            ? (ClassName(r.ClassIndex), NameAndType(r.NameAndTypeIndex).Name, NameAndType(r.NameAndTypeIndex).Desc)
            : (null, null, null);
}
