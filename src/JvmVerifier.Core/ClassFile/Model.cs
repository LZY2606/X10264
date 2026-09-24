namespace JvmVerifier.Core.ClassFile;

/// <summary>A constant-pool entry. Raw representation (tag + decoded text + raw bytes) is preserved.</summary>
public sealed record CpEntry(int Index, byte Tag, string TagName, string Value, byte[] RawBytes);

/// <summary>A single decoded instruction. <see cref="Offset"/> is the original byte offset in the code array.</summary>
public sealed record InstructionInfo(
    int Offset, int Opcode, string Mnemonic, int[] Operands, int Length,
    int? BranchTarget, int? CpIndex, string? CpRef);

public sealed record ExceptionHandler(int StartPc, int EndPc, int HandlerPc, int? CatchTypeCpIndex, string CatchType);

/// <summary>A parsed stack-map frame. Corrupt frames are kept in place with a diagnostic, never silently dropped.</summary>
public sealed record FrameInfo(
    int Pc, int OffsetDelta, string FrameType,
    string[] Locals, string[] Stack,
    bool Corrupt, string? Diagnostic);

public sealed record MethodModel(
    string Name, string Descriptor, int AccessFlags, int MaxStack, int MaxLocals,
    byte[] Code, List<InstructionInfo> Instructions,
    List<ExceptionHandler> Exceptions, List<FrameInfo> Frames,
    bool HasStackMapTable)
{
    public bool IsStatic => (AccessFlags & 0x0008) != 0;
}

public sealed record ClassFileModel(
    int Minor, int Major, List<CpEntry> ConstantPool,
    string ThisClass, List<MethodModel> Methods, byte[] RawBytes)
{
    public string VersionLabel => $"{Major}.{Minor}";
}
