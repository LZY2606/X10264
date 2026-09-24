namespace Veriflow.ClassFile;

public sealed class ClassFormatException : Exception
{
    public ClassFormatException(string message) : base(message) { }
}

/// <summary>常量池项。Index 单位：slot@constant-pool-1-based。</summary>
public sealed record CpEntry(int Index, string Tag, string Summary);

/// <summary>异常表项。StartPc/EndPc/HandlerPc 单位：index@code-array-start。</summary>
public sealed record ExceptionHandler(int StartPc, int EndPc, int HandlerPc, string CatchType);

/// <summary>StackMapTable 中的 verification_type_info。</summary>
public sealed record VTypeInfo(string Kind, string? ClassName, int? Offset);

/// <summary>解析后的栈映帧。Offset 单位：index@code-array-start。</summary>
public sealed record StackMapFrame(int Offset, string FrameType, IReadOnlyList<VTypeInfo> Locals, IReadOnlyList<VTypeInfo> Stack);

public sealed record CodeAttribute(
    int MaxStack,
    int MaxLocals,
    byte[] Code,
    IReadOnlyList<ExceptionHandler> Handlers,
    IReadOnlyList<StackMapFrame> Frames);

public sealed record MethodModel(string Name, string Descriptor, string Access, CodeAttribute? Code);

public sealed record ClassModel(
    int Minor,
    int Major,
    IReadOnlyList<CpEntry> ConstantPool,
    string ThisClass,
    string SuperClass,
    IReadOnlyList<MethodModel> Methods,
    byte[] RawBytes)
{
    public string Sha256 { get; } =
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(RawBytes)).ToLowerInvariant();

    public MethodModel RequireMethod(string name) =>
        Methods.FirstOrDefault(m => m.Name == name)
        ?? throw new ClassFormatException($"method not found: {name}");
}
