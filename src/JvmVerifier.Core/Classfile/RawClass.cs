using System.Text.Json.Serialization;
using JvmVerifier.Core;

namespace JvmVerifier.Core.Classfile;

/// <summary>
/// 原始 classfile 镜像：导入时保留原始表示，不做规范化。
/// 所有字段均可溯源到 classfile 字节或离线案例包中的等价材料。
/// </summary>
public sealed class RawClass
{
    public string ClassName { get; set; } = "";
    public int MinorVersion { get; set; }
    public int MajorVersion { get; set; }
    public List<RawConstantPoolEntry> ConstantPool { get; set; } = new();
    public List<RawMethod> Methods { get; set; } = new();
    /// <summary>无法转换、留在原位置的输入诊断。</summary>
    public List<RawDiagnostic> Diagnostics { get; set; } = new();
    /// <summary>来源：binary（真实 class 字节）或 cases-pack（离线领域材料）。</summary>
    public string Provenance { get; set; } = "cases-pack";
    /// <summary>原始字节的稳定摘要（SHA-256），用于身份。</summary>
    public string Sha256 { get; set; } = "";
}

public sealed class RawConstantPoolEntry
{
    public CpIndex Index { get; set; }
    /// <summary>常量池 tag（JVMS 4.4），如 1=Utf8,7=Class,10=Methodref。</summary>
    public int Tag { get; set; }
    public string Kind { get; set; } = "";
    /// <summary>原始字符串表示（Utf8 文本 / 数字原文）。</summary>
    public string? RawText { get; set; }
    /// <summary>组合项的原始下标引用（如 NameAndType 的 name_index/descriptor_index）。</summary>
    public List<CpIndex> RawRefs { get; set; } = new();
    public string? ResolvedName { get; set; }
    public string? ResolvedDescriptor { get; set; }
}

public sealed class RawMethod
{
    public string Name { get; set; } = "";
    /// <summary>descriptor 原文保留，如 (ILjava/lang/Object;)V。</summary>
    public string Descriptor { get; set; } = "";
    public int AccessFlags { get; set; }
    public RawCode? Code { get; set; }
}

public sealed class RawCode
{
    public int MaxStack { get; set; }
    public int MaxLocals { get; set; }
    public List<RawInstruction> Instructions { get; set; } = new();
    public List<RawExceptionEntry> ExceptionTable { get; set; } = new();
    /// <summary>StackMapTable 的原始帧；损坏偏移也原样保留并打诊断。</summary>
    public List<RawStackMapFrame> StackMapFrames { get; set; } = new();
    public bool StackMapTableAttributePresent { get; set; }
}

public sealed class RawInstruction
{
    /// <summary>原字节偏移（指令起始）。</summary>
    public Bci Offset { get; set; }
    public int Opcode { get; set; }
    public string Mnemonic { get; set; } = "";
    /// <summary>操作数原始字节（hex）。</summary>
    public string OperandHex { get; set; } = "";
    /// <summary>分支跳转目标（原始解析值），非分支为 null。</summary>
    public Bci? BranchTarget { get; set; }
    /// <summary>常量池引用（原始 cp 下标）。</summary>
    public CpIndex? CpRef { get; set; }
    /// <summary>指令长度（字节）。</summary>
    public int Length { get; set; }
    /// <summary>wide 前缀修饰。</summary>
    public bool Wide { get; set; }
}

public sealed class RawExceptionEntry
{
    public Bci StartPc { get; set; }
    /// <summary>JVMS 语义为半开 [startPc,endPc)。</summary>
    public Bci EndPc { get; set; }
    public Bci HandlerPc { get; set; }
    /// <summary>catch_type=0 表示 finally（any）。</summary>
    public CpIndex? CatchType { get; set; }
    public string? CatchTypeName { get; set; }
}

/// <summary>StackMapTable 帧的原始编码信息（frame_type 决定 append/chop 等）。</summary>
public sealed class RawStackMapFrame
{
    /// <summary>帧在表中的次序（0-based），坐标基准 frame-order。</summary>
    public int FrameOrder { get; set; }
    public int FrameType { get; set; }
    public string FrameKind { get; set; } = "";
    /// <summary>字节流声明的 offset_delta 原文。</summary>
    public int OffsetDelta { get; set; }
    /// <summary>依据前帧解析出的字节偏移；损坏时可能为 null 或越界。</summary>
    public Bci? ParsedOffset { get; set; }
    /// <summary>声明的局部类型（VerificationTypeInfo 原始 tag 与文本）。</summary>
    public List<RawVerificationType> Locals { get; set; } = new();
    public List<RawVerificationType> Stack { get; set; } = new();
    public bool CorruptOffset { get; set; }
    public string? OffsetDiagnostic { get; set; }
}

public sealed class RawVerificationType
{
    /// <summary>VerificationTypeInfo tag（0=top,1=int,...,7=object,8=uninitializedThis）。</summary>
    public int Tag { get; set; }
    public string Name { get; set; } = "";
    /// <summary>object_variable_info 的 cp 下标 / uninitialized 的 bci 原文。</summary>
    public int RawPayload { get; set; }
    public CpIndex? ObjectCp { get; set; }
    public Bci? UninitializedAt { get; set; }
    public string? ObjectName { get; set; }
}

public sealed record RawDiagnostic(string Code, string Message, string? Where)
{
    /// <summary>诊断是否致命（无法继续解析该结构）。</summary>
    public bool Fatal { get; init; }
}
