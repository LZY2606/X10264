namespace Veriflow.ClassFile;

/// <summary>
/// 领域量的单位与坐标基准。所有偏移/索引在存储与事件中都必须携带其中一个基准，
/// 原始表示先保留，派生层才做规范化。
/// </summary>
public static class Units
{
    /// <summary>字节偏移，基准：classfile 起始。</summary>
    public const string ByteOffset = "byte@classfile-start";
    /// <summary>指令位置，基准：code 数组起始（方法 Code.code[0]）。</summary>
    public const string CodeIndex = "index@code-array-start";
    /// <summary>常量池槽位，基准：常量池 1 基索引。</summary>
    public const string CpSlot = "slot@constant-pool-1-based";
    /// <summary>局部变量槽，基准：方法帧 local[0]。</summary>
    public const string LocalSlot = "slot@frame-local-0";
    /// <summary>操作数栈深度，基准：帧栈底。</summary>
    public const string StackDepth = "depth@frame-stack-bottom";
}
