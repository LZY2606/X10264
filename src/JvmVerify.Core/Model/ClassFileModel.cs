using System.Text.Json.Serialization;

namespace JvmVerify.Core.Model;

// 所有原始量都携带坐标基准：常量池下标、字节码偏移(pc)、帧序号、异常表行号。
// 导入层只做保真表示；规范化/解析在派生层进行。
public sealed class ClassFileModel
{
    public int MinorVersion { get; init; }
    public int MajorVersion { get; init; }
    public int AccessFlags { get; init; }
    public string ThisClassName { get; init; } = "";
    public string SuperClassName { get; init; } = "";
    public string SourceFormat { get; init; } = "class"; // class | json
    public List<ConstantPoolEntry> ConstantPool { get; init; } = new();
    public List<MethodModel> Methods { get; init; } = new();
    // 无法转换/无法解释的值留在原位，只附诊断，绝不丢弃。
    public List<RawDiagnostic> Diagnostics { get; init; } = new();

    [JsonIgnore]
    public string VersionTag => $"major{MajorVersion}.minor{MinorVersion}";
}

public sealed class RawDiagnostic
{
    public string Code { get; init; } = "";
    public string Coordinate { get; init; } = "";   // 例如 cp#99 / ins@7 / frame#2
    public string Message { get; init; } = "";
    public string RawFragment { get; init; } = "";  // 原始片段保留
}

public sealed class ConstantPoolEntry
{
    public int Index { get; init; }                 // 1 基下标；long/double 占 Index 与 Index+1 两个槽
    public string Tag { get; init; } = "";          // Utf8|Class|NameAndType|Methodref|...
    public string RawText { get; init; } = "";      // 原始表示，导入时原样保留
    public int? Ref1 { get; init; }
    public int? Ref2 { get; init; }
    public long? IntegralValue { get; init; }
    public double? FloatingValue { get; init; }
    public bool IsPhantomSlot { get; init; }        // long/double 的第二槽
    public string StableId => $"cp#{Index}";
}

public sealed class MethodModel
{
    public int MethodIndex { get; init; }
    public int AccessFlags { get; init; }
    public string Name { get; init; } = "";
    public string Descriptor { get; init; } = "";   // 例：(JLjava/lang/String;)V
    public int MaxStack { get; init; }
    public int MaxLocals { get; init; }
    public byte[] Code { get; init; } = Array.Empty<byte>();
    public List<InstructionModel> Instructions { get; init; } = new();
    public List<ExceptionEntry> ExceptionTable { get; init; } = new();
    public List<StackMapFrameModel> StackMapFrames { get; init; } = new();
    public string StableId => $"method#{MethodIndex}";
}

public sealed class InstructionModel
{
    public int Offset { get; init; }                // 原字节偏移，单位 pc（1 字节码 = 1 pc）
    public int Opcode { get; init; }
    public string Mnemonic { get; init; } = "";
    public byte[] OperandBytes { get; init; } = Array.Empty<byte>();
    public int Length { get; init; }
    public int? BranchTarget { get; init; }         // 解析后的分支目标(pc)，null 表示非分支
    public int? ConstantPoolIndex { get; init; }
    public int? LocalIndex { get; init; }
    public int? Increment { get; init; }
    public string? ReferencedDescriptor { get; init; }
    public string? ReferencedClassName { get; init; }
    public bool WidePrefix { get; init; }
    public string StableId => $"ins@{Offset}";
}

public sealed class ExceptionEntry
{
    public int Row { get; init; }
    public int StartPc { get; init; }               // [StartPc, EndPc)
    public int EndPc { get; init; }
    public int HandlerPc { get; init; }
    public int CatchTypeCpIndex { get; init; }      // 0 => any/finally
    public string CatchClassName { get; init; } = "";
    public string StableId => $"ex#{Row}";
}

public sealed class StackMapFrameModel
{
    public int FrameIndex { get; init; }            // 属性内序号
    public int FrameType { get; init; }
    public int BytecodeOffset { get; init; }        // 绝对字节偏移(pc)，由 offset_delta 还原
    public int OffsetDelta { get; init; }           // 原始 delta 同时保留
    public List<VerificationTypeModel> Locals { get; init; } = new();
    public List<VerificationTypeModel> Stack { get; init; } = new();
    public string StableId => $"frame#{FrameIndex}";
}

public sealed class VerificationTypeModel
{
    public int Tag { get; init; }                   // verification_type_info tag 0..8
    public string Kind { get; init; } = "";         // Top|Integer|Float|Long|Double|Null|UninitializedThis|Object|Uninitialized
    public int? ClassCpIndex { get; init; }         // Object: cpool_info index
    public int? NewOffset { get; init; }            // Uninitialized: new 指令偏移(pc)
    public string? ClassName { get; init; }
}
