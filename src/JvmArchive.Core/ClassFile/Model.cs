namespace JvmArchive.Core.ClassFile;

/// <summary>
/// Raw-preserving classfile model. Every domain quantity keeps its original
/// representation (raw bytes, original offsets, descriptor text); the derived
/// layer normalizes. Values that cannot be converted stay in place and carry
/// a diagnostic instead of being dropped.
/// </summary>
public sealed record ClassFileModel(
    string Identity,            // stable identity: sha256 of raw bytes
    int MinorVersion,
    int MajorVersion,
    IReadOnlyList<CpEntry> ConstantPool,
    string ThisClass,
    IReadOnlyList<MethodModel> Methods,
    byte[] RawBytes,
    IReadOnlyList<string> Diagnostics);

public sealed record CpEntry(
    int Index,
    int Tag,
    string TagName,
    byte[] RawBytes,            // original constant-pool item bytes (tag excluded)
    string Display,             // derived, human readable
    IReadOnlyList<int> Refs);   // referenced cp indexes (kept as-is)

public sealed record MethodModel(
    string Identity,            // mth:<class>.<name><descriptor>
    string Name,
    string Descriptor,          // raw descriptor text, preserved verbatim
    int MaxStack,
    int MaxLocals,
    int CodeFileOffset,         // file offset of the first code byte (coordinate base)
    IReadOnlyList<Instruction> Instructions,
    IReadOnlyList<ExceptionRange> Exceptions,
    IReadOnlyList<StackMapFrame> Frames,
    bool HasStackMapTable);

public sealed record Instruction(
    int Offset,                 // byte offset within code array (original)
    int FileOffset,             // absolute file offset (original)
    string Mnemonic,
    byte[] RawBytes,            // original instruction bytes
    IReadOnlyList<int> BranchTargets);

public sealed record ExceptionRange(
    int StartPc, int EndPc, int HandlerPc, string CatchType);

public sealed record StackMapFrame(
    int FrameIndex,
    int OffsetDelta,            // raw offset_delta as stored
    int ComputedOffset,         // derived bytecode offset
    string FrameType,           // same / same_locals_1 / chop / append / full
    IReadOnlyList<string> Locals,
    IReadOnlyList<string> Stack,
    bool Corrupted,             // set when offset does not land on an instruction
    string? Diagnostic);        // diagnostic kept at original position
