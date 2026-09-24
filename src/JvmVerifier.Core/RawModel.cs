// 原始导入层：只保留 classfile 的原始表示，不做规范化。
// 坐标基准：pc = 方法 code[] 内字节偏移；slot = 局部变量槽；index = constant_pool 1-based 索引。
namespace JvmVerifier.Core;

// 诊断：无法转换的值留在原位置（RawText/原始字段），携带诊断而不是丢弃。
public sealed record RawDiagnostic
{
    public string Code { get; init; } = "";        // CP_TAG_UNSUPPORTED, OPERAND_TRUNCATED ...
    public string At { get; init; } = "";          // 坐标，如 insn@17 或 frame@33
    public string Message { get; init; } = "";
    public string RawSnippet { get; init; } = ""; // 原始片段，逐字保留
}

public sealed class ConstantPoolEntry
{
    public int Index { get; init; }                 // 1-based；Long/Double 的占位槽不出现实体
    public string Tag { get; init; } = "";          // Utf8/Class/.../Unknown
    public string RawText { get; init; } = "";      // 原始表示
    public int? ClassNameIndex { get; init; }
    public int? NameAndTypeIndex { get; init; }
    public int? StringIndex { get; init; }
    public int? RefKind { get; init; }
    public int? BootstrapMethodAttrIndex { get; init; }
    public bool IsCategory2 { get; init; }          // Long/Double
    public bool OccupiesTwoCpSlots { get; init; }
}

public sealed class RawInstruction
{
    public int Pc { get; init; }                    // 原字节偏移（坐标基准，单位 byte）
    public string Opcode { get; init; } = "";       // 助记符；未知时 unknown:0xXX
    public int OpcodeValue { get; init; }
    public List<int> OperandBytes { get; init; } = new(); // 原始操作数字节
    public int? CpIndex { get; init; }              // 字段/方法/类型指令引用的常量池项
    public int? BranchTarget { get; init; }         // 派生的绝对字节偏移；原始字节仍在 OperandBytes
    public int? WideLocalIndex { get; init; }       // wide 前缀后的局部变量索引
    public bool WidePrefix { get; init; }
    public int? IincIncrement { get; init; }
    public int Length { get; init; }                // 指令长度(byte)
}

// StackMapTable 原始帧（类型名按 classfile 常量池原文保留）。
public sealed class RawStackMapFrame
{
    public int FrameIndex { get; init; }            // 表中序号
    public int OffsetDelta { get; init; }           // 原始 offset_delta
    public int DeclaredPc { get; set; }             // 按字节流累积出的字节偏移（可能损坏）
    public string FrameType { get; init; } = "";    // same/chop/same_locals_1_stack_item/append/full
    public int RawType { get; init; }
    public List<string> LocalsRaw { get; init; } = new(); // VerificationTypeInfo 原文
    public List<string> StackRaw { get; init; } = new();
    public int? NewInstructionPc { get; init; }     // Uninitialized_variable_info 的 offset
}

public sealed class RawExceptionHandler
{
    public int Index { get; init; }
    public int StartPc { get; init; }               // 含 startPc，单位 byte
    public int EndPc { get; init; }                 // 不含 endPc
    public int HandlerPc { get; init; }
    public string CatchTypeRaw { get; init; } = "any"; // 常量池 Class 原文；catch_type=0 -> any
    public int CatchTypeCpIndex { get; init; }
}

public sealed class RawMethod
{
    public string MethodId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Descriptor { get; init; } = "";   // 原始 descriptor，逐字保留
    public bool IsStatic { get; init; }
    public int MaxStack { get; init; }
    public int MaxLocals { get; init; }
    public List<RawInstruction> Instructions { get; init; } = new();
    public List<RawStackMapFrame> Frames { get; init; } = new();
    public List<RawExceptionHandler> Handlers { get; init; } = new();
}

// 一次导入的完整基线。要求保留的七项原始材料都在这里；派生层不得改写。
public sealed class RawClassFile
{
    public string CaseId { get; init; } = "";
    public string Origin { get; init; } = "";       // 来源标识（fixture 文件名/导出档案）
    public int MajorVersion { get; set; }           // classfile 版本（可切换重验）
    public int MinorVersion { get; set; }
    public string ThisClassName { get; init; } = "";
    public string SuperClassName { get; init; } = "java/lang/Object";
    public List<string> InterfaceNames { get; init; } = new();
    public List<ClassInfo> ClassHierarchy { get; init; } = new(); // fixture 域知识：类层次
    public List<ConstantPoolEntry> ConstantPool { get; init; } = new();
    public List<RawMethod> Methods { get; init; } = new();
    public List<RawDiagnostic> Diagnostics { get; init; } = new();

    public string VersionLabel => $"major={MajorVersion},minor={MinorVersion}";
}

public sealed class ClassInfo
{
    public string Name { get; init; } = "";
    public string Super { get; init; } = "";
    public List<string> Interfaces { get; init; } = new();
    public bool IsInterface { get; init; }
}
