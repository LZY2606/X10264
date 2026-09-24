namespace JvmVerifier.ClassFile;

/// <summary>A domain quantity: every value carries its unit / coordinate basis.</summary>
public sealed record Quantity(int Value, string Unit)
{
    public override string ToString() => $"{Value} {Unit}";
}

public sealed record CpEntry(int Index, string Tag, int RawOffset, byte[] RawBytes, string Display);

public sealed record ExceptionRange(int RawOffset, int StartPc, int EndPc, int HandlerPc, int CatchTypeIndex, string CatchType);

public sealed record VerificationTypeInfo(string Kind, string? ClassName, int? Offset)
{
    public static readonly VerificationTypeInfo Top = new("top", null, null);
    public static readonly VerificationTypeInfo Integer = new("integer", null, null);
    public static readonly VerificationTypeInfo Float = new("float", null, null);
    public static readonly VerificationTypeInfo Long = new("long", null, null);
    public static readonly VerificationTypeInfo Double = new("double", null, null);
    public static readonly VerificationTypeInfo Null = new("null", null, null);
    public static readonly VerificationTypeInfo UninitializedThis = new("uninitializedThis", null, null);
    public static VerificationTypeInfo Object(string name) => new("object", name, null);
    public static VerificationTypeInfo Uninitialized(int offset) => new("uninitialized", null, offset);
}

public sealed record StackMapFrame(
    int Offset,
    string FrameKind,
    IReadOnlyList<VerificationTypeInfo> Locals,
    IReadOnlyList<VerificationTypeInfo> Stack,
    int RawOffset,
    byte[] RawBytes);

public sealed record CodeAttribute(
    int MaxStack,
    int MaxLocals,
    byte[] Code,
    IReadOnlyList<ExceptionRange> Exceptions,
    IReadOnlyList<StackMapFrame> Frames,
    byte[] RawStackMapTable);

public sealed record MethodInfo(string Name, string Descriptor, ushort AccessFlags, CodeAttribute Code)
{
    public bool IsStatic => (AccessFlags & 0x0008) != 0;
}

public sealed record MethodRefInfo(string ClassName, string Name, string Descriptor);

public sealed class ClassFileModel
{
    public required int Minor { get; init; }
    public required int Major { get; init; }
    public required IReadOnlyList<CpEntry> ConstantPool { get; init; }
    public required IReadOnlyDictionary<int, string> Utf8 { get; init; }
    public required IReadOnlyDictionary<int, string> ClassNames { get; init; }
    public required IReadOnlyDictionary<int, MethodRefInfo> MethodRefs { get; init; }
    public required IReadOnlyDictionary<int, object?> Constants { get; init; }
    public required string ThisClass { get; init; }
    public required IReadOnlyList<MethodInfo> Methods { get; init; }
    public required byte[] RawBytes { get; init; }

    public string ClassName(int cpIndex) => ClassNames.TryGetValue(cpIndex, out var n) ? n : $"#{cpIndex}";
}
