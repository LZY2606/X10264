namespace BytecodeSeal.Core;

/// <summary>Constant pool entry. Raw tag and indexes are preserved exactly as read.</summary>
public abstract record CpEntry(int Index)
{
    public sealed record Utf8(int Index, string Value) : CpEntry(Index);
    public sealed record IntConst(int Index, int Value) : CpEntry(Index);
    public sealed record LongConst(int Index, long Value) : CpEntry(Index);
    public sealed record ClassRef(int Index, int NameIndex, string Name) : CpEntry(Index);
    public sealed record NameAndTypeRef(int Index, int NameIndex, int DescIndex, string Name, string Descriptor) : CpEntry(Index);
    public sealed record MethodRef(int Index, int ClassIndex, int NtIndex, string ClassName, string Name, string Descriptor) : CpEntry(Index);
    public sealed record FieldRef(int Index, int ClassIndex, int NtIndex, string ClassName, string Name, string Descriptor) : CpEntry(Index);
    public sealed record StringConst(int Index, int Utf8Index, string Value) : CpEntry(Index);
    /// <summary>Tags we do not model are kept raw so nothing is dropped on import.</summary>
    public sealed record Raw(int Index, int Tag, byte[] Data) : CpEntry(Index);
}

public sealed record ExceptionHandler(int StartPc, int EndPc, int HandlerPc, string? CatchType);

public abstract record StackMapFrame(int OffsetDelta, int Offset)
{
    public sealed record Same(int OffsetDelta, int Offset) : StackMapFrame(OffsetDelta, Offset);
    public sealed record Same1Stack(int OffsetDelta, int Offset, VerificationTypeInfo StackItem) : StackMapFrame(OffsetDelta, Offset);
    public sealed record Chop(int OffsetDelta, int Offset, int K) : StackMapFrame(OffsetDelta, Offset);
    public sealed record Append(int OffsetDelta, int Offset, IReadOnlyList<VerificationTypeInfo> Added) : StackMapFrame(OffsetDelta, Offset);
    public sealed record Full(int OffsetDelta, int Offset, IReadOnlyList<VerificationTypeInfo> Locals, IReadOnlyList<VerificationTypeInfo> Stack) : StackMapFrame(OffsetDelta, Offset);
}

/// <summary>verification_type_info as stored in the classfile (raw tag + payload).</summary>
public sealed record VerificationTypeInfo(int Tag, int? CpIndexOrOffset, string Display)
{
    public const int TTop = 0, TInt = 1, TFloat = 2, TLong = 4, TDouble = 3,
                     TNull = 5, TUninitThis = 6, TObject = 7, TUninit = 8;
}

public sealed record CodeAttribute(
    int MaxStack, int MaxLocals, byte[] Bytes,
    IReadOnlyList<ExceptionHandler> Exceptions,
    IReadOnlyList<StackMapFrame>? StackMapFrames,
    bool StackMapAttributePresent);

public sealed record MethodModel(
    string Name, string Descriptor, ushort AccessFlags, CodeAttribute? Code);

public sealed record FieldModel(string Name, string Descriptor, ushort AccessFlags);

public sealed record ClassFile(
    byte[] RawBytes,
    ushort MinorVersion, ushort MajorVersion,
    IReadOnlyList<CpEntry?> ConstantPool, // 1-based; index 0 and long/double second slots are null
    string ThisClass, string? SuperClass,
    IReadOnlyList<FieldModel> Fields,
    IReadOnlyList<MethodModel> Methods)
{
    public string VersionDisplay => $"{MajorVersion}.{MinorVersion}";
    public CpEntry? Cp(int index) => index >= 1 && index < ConstantPool.Count ? ConstantPool[index] : null;
}
