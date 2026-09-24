using System.Text;

namespace BytecodeVerifier.Core.Model;

public enum ConstantPoolTag : byte
{
    Utf8 = 1, Integer = 3, Float = 4, Long = 5, Double = 6, Class = 7, String = 8,
    Fieldref = 9, Methodref = 10, InterfaceMethodref = 11, NameAndType = 12,
    MethodHandle = 15, MethodType = 16, Dynamic = 17, InvokeDynamic = 18,
    Module = 19, Package = 20
}

/// <summary>Raw constant pool entry. Original representation (tag + raw payload) is retained.</summary>
public sealed class RawCpInfo
{
    public ushort Index { get; init; }
    public ConstantPoolTag Tag { get; init; }
    public byte[] RawPayload { get; init; } = Array.Empty<byte>();
    public string RawText { get; init; } = "";
    public ushort? RefIndex1 { get; init; }
    public ushort? RefIndex2 { get; init; }
    public bool TakesTwoSlots { get; init; }
    public string? Diagnostic { get; set; }
}

/// <summary>Decoded instruction. Operands retain raw bytes plus normalized interpretation.</summary>
public sealed class RawInstruction
{
    public int ByteOffset { get; init; }
    public byte Opcode { get; init; }
    public string Mnemonic { get; init; } = "";
    public byte[] RawOperands { get; init; } = Array.Empty<byte>();
    public int Length { get; init; }
    public int? BranchTarget { get; init; }
    public ushort? CpIndex { get; init; }
    public int? LocalIndex { get; init; }
    public int? WideLocalIndex { get; init; }
    public string? Diagnostic { get; set; }
}

public enum VerificationTypeTag
{
    Top = 0, Integer = 1, Float = 2, Double = 3, Long = 4, Null = 5,
    UninitializedThis = 6, Object = 7, Uninitialized = 8
}

/// <summary>A single verification_type_info from a stack map frame (JVMS 4.7.4).</summary>
public sealed class VerificationType
{
    public VerificationTypeTag Tag { get; init; }
    public ushort? CpIndex { get; init; }       // Object -> CONSTANT_Class index
    public int? NewOffset { get; init; }        // Uninitialized -> new instruction bci
    public string RawLabel { get; init; } = "";
    public override string ToString() => RawLabel;
}

public enum StackMapFrameKind
{
    Same, SameLocals1StackItem, SameLocals1StackItemExtended, Chop, SameExtended, Append, Full
}

/// <summary>
/// One stack_map_frame. Both the declared byte_offset_delta and the resolved absolute
/// target bytecode offset are preserved; corrupt deltas are kept, not repaired.
/// </summary>
public sealed class RawStackMapFrame
{
    public int FrameIndex { get; init; }
    public StackMapFrameKind Kind { get; init; }
    public byte RawType { get; init; }
    public int ByteOffsetDelta { get; init; }
    public int? AbsoluteByteOffset { get; set; }
    public IReadOnlyList<VerificationType> Locals { get; init; } = Array.Empty<VerificationType>();
    public IReadOnlyList<VerificationType> Stack { get; init; } = Array.Empty<VerificationType>();
    public int? PreviousFrameOffset { get; init; }
    public string? Diagnostic { get; set; }
}

public sealed class RawExceptionHandler
{
    public int HandlerIndex { get; init; }
    public int StartPc { get; init; }
    public int EndPc { get; init; }
    public int HandlerPc { get; init; }
    public ushort CatchTypeCpIndex { get; init; }   // 0 => finally/catch-all
    public string CatchTypeRawName { get; init; } = "";
    public string? Diagnostic { get; set; }
}

public sealed class RawMethod
{
    public ushort MethodIndex { get; init; }
    public ushort AccessFlags { get; init; }
    public ushort NameCpIndex { get; init; }
    public ushort DescriptorCpIndex { get; init; }
    public string Name { get; init; } = "";
    public string Descriptor { get; init; } = "";
    public int MaxStack { get; init; }
    public int MaxLocals { get; init; }
    public byte[] CodeBytes { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<RawInstruction> Instructions { get; init; } = Array.Empty<RawInstruction>();
    public IReadOnlyList<RawExceptionHandler> ExceptionTable { get; init; } = Array.Empty<RawExceptionHandler>();
    public IReadOnlyList<RawStackMapFrame> StackMapFrames { get; init; } = Array.Empty<RawStackMapFrame>();
    public bool HasStackMapTableAttribute { get; init; }
    public string? Diagnostic { get; set; }

    public bool IsStatic => (AccessFlags & 0x0008) != 0;
    public bool IsConstructor => Name == "<init>";
}

/// <summary>Raw imported class file: offsets, CP, descriptors, frames, handlers, version all retained.</summary>
public sealed class RawClassFile
{
    public string ClassId { get; set; } = "";
    public string RawSha256 { get; set; } = "";
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();
    public ushort MinorVersion { get; init; }
    public ushort MajorVersion { get; init; }
    public string VersionText => $"{MajorVersion}.{MinorVersion} ({DomainQuantity.JavaVersionName(MajorVersion)})";
    public ushort ConstantPoolCount { get; init; }
    public IReadOnlyList<RawCpInfo> ConstantPool { get; init; } = Array.Empty<RawCpInfo>();
    public ushort AccessFlags { get; init; }
    public ushort ThisClassCp { get; init; }
    public ushort SuperClassCp { get; init; }
    public IReadOnlyList<ushort> InterfacesCp { get; init; } = Array.Empty<ushort>();
    public string ThisClassName { get; init; } = "";
    public string SuperClassName { get; init; } = "";
    public IReadOnlyList<string> InterfaceNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RawMethod> Methods { get; init; } = Array.Empty<RawMethod>();
    public IReadOnlyList<string> ImportDiagnostics { get; set; } = new List<string>();

    public RawCpInfo? Cp(ushort index) =>
        ConstantPool.FirstOrDefault(e => e.Index == index);
    public string CpClassName(ushort classCpIndex) =>
        Cp(classCpIndex) is { } e ? ResolveUtf8(e.RefIndex1 ?? 0) : $"#{classCpIndex}";
    public string ResolveUtf8(ushort index) =>
        Cp(index) is { } e ? e.RawText : $"#{index}";
}
