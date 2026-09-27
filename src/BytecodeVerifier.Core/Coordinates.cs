namespace BytecodeVerifier.Core;

/// <summary>
/// 所有领域量都携带坐标基准或单位。字节偏移以方法 Code 属性内的 bci(byte code index)为基准，
/// 常量池索引以 1 为起点（0 为哨兵），局部变量/栈槽按 JVM 计算类型 1 或 2 槽。
/// </summary>
public enum Unit
{
    /// <summary>字节码索引，基准 = Code 属性 code[] 起点，单位 = 1 字节。</summary>
    Bci,
    /// <summary>常量池索引，基准 = 1（class 文件 u2），单位 = 1 个 cp_info。</summary>
    ConstantPoolIndex,
    /// <summary>局部变量槽位，基准 = 0，category-2 占两个连续槽（第二槽为 Top）。</summary>
    LocalSlot,
    /// <summary>操作数栈逻辑槽位，基准 = 栈底 0，category-2 占两个槽。</summary>
    StackSlot,
    /// <summary>class 文件次版本/主版本号，无单位（版本坐标）。</summary>
    ClassVersion,
    /// <summary>描述符原文（方法/字段 type descriptor 字符串）。</summary>
    DescriptorText,
    /// <summary>无单位的标量（例如 tableswitch 的 key）。</summary>
    Scalar,
}

/// <summary>带单位的值：导入层保留原始表示，只有派生层才规范化。</summary>
public readonly record struct Quant(long Raw, Unit Unit, string? OriginalText = null)
{
    public override string ToString() => OriginalText ?? $"{Raw}[{Unit}]";
}
