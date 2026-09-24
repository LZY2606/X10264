namespace JvmVerifier.Core;

/// <summary>
/// In-memory model of a classfile. Every domain quantity keeps its original
/// representation (raw bytes, original offsets, constant-pool indices);
/// normalisation only happens in derived layers.
/// </summary>
public sealed record ClassFileModel(
    int MinorVersion,
    int MajorVersion,
    IReadOnlyList<CpEntry> ConstantPool,
    string ThisClass,
    string SuperClass,
    IReadOnlyList<MethodModel> Methods,
    byte[] RawBytes)
{
    public string CpUtf8(int index) => ((CpEntry.Utf8)ConstantPool[index]).Value;
    public string CpClassName(int index) => CpUtf8(((CpEntry.Class)ConstantPool[index]).NameIndex);
    public CpEntry.NameAndType NameAndType(int index) => (CpEntry.NameAndType)ConstantPool[index];
}

public abstract record CpEntry
{
    public sealed record Utf8(string Value) : CpEntry;
    public sealed record Class(int NameIndex) : CpEntry;
    public sealed record NameAndType(int NameIndex, int DescriptorIndex) : CpEntry;
    public sealed record Fieldref(int ClassIndex, int NameAndTypeIndex) : CpEntry;
    public sealed record Methodref(int ClassIndex, int NameAndTypeIndex) : CpEntry;
    public sealed record InterfaceMethodref(int ClassIndex, int NameAndTypeIndex) : CpEntry;
    public sealed record StringInfo(int StringIndex) : CpEntry;
    public sealed record IntInfo(int Value) : CpEntry;
    public sealed record FloatInfo(float Value) : CpEntry;
    public sealed record LongInfo(long Value) : CpEntry;
    public sealed record DoubleInfo(double Value) : CpEntry;
    public sealed record MethodHandle(int RefKind, int RefIndex) : CpEntry;
    public sealed record MethodType(int DescriptorIndex) : CpEntry;
    public sealed record InvokeDynamic(int BootstrapIndex, int NameAndTypeIndex) : CpEntry;
    /// <summary>Placeholder for the unusable slot after a long/double entry.</summary>
    public sealed record WidePadding() : CpEntry;
    public sealed record Unknown(int Tag, byte[] Data) : CpEntry;
}

public sealed record MethodModel(
    string Name,
    string Descriptor,
    IReadOnlyList<string> AccessFlags,
    CodeModel? Code);

public sealed record CodeModel(
    int MaxStack,
    int MaxLocals,
    byte[] Bytes,
    IReadOnlyList<ExceptionHandler> ExceptionTable,
    IReadOnlyList<StackMapFrame> StackMapTable,
    /// <summary>File offset where the code bytes start (for provenance).</summary>
    int CodeFileOffset);

public sealed record ExceptionHandler(int StartPc, int EndPc, int HandlerPc, int CatchTypeIndex);

public enum StackMapFrameKind { Same, SameLocals1StackItem, Chop, Append, Full }

/// <summary>
/// A parsed stack map frame. OffsetDelta is kept as written in the file;
/// the derived absolute offset is computed by the verifier and validated.
/// </summary>
public sealed record StackMapFrame(
    StackMapFrameKind Kind,
    int OffsetDelta,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack);

public abstract record VerificationTypeInfo
{
    public sealed record Top() : VerificationTypeInfo;
    public sealed record Int() : VerificationTypeInfo;
    public sealed record Float() : VerificationTypeInfo;
    public sealed record Long() : VerificationTypeInfo;
    public sealed record Double() : VerificationTypeInfo;
    public sealed record Null() : VerificationTypeInfo;
    public sealed record UninitializedThis() : VerificationTypeInfo;
    public sealed record Object(int CpIndex) : VerificationTypeInfo;
    public sealed record Uninitialized(int Offset) : VerificationTypeInfo;
}
