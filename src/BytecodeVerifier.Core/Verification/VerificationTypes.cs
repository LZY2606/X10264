using System.Text.Json.Serialization;

namespace BytecodeVerifier.Core.Verification;

/// <summary>
/// StackMapTable verification_type_info 标签（JVMS §4.7.4），
/// 同时作为抽象解释器的类型域。Top 表示不可读槽（long/double 的第二槽或未知）。
/// </summary>
public enum Vtk
{
    Top = 0,
    Integer = 1,
    Float = 2,
    Double = 3,
    Long = 4,
    Null = 5,
    UninitializedThis = 6,
    Object = 7,
    Uninitialized = 8,
}

/// <summary>
/// 一个验证类型。原始标签保留在 <see cref="Tag"/>；Object 的类名、Uninitialized 的
/// new 指令 bci 以原始形式保留，派生层才做 join/规范化。
/// </summary>
public sealed record VType
{
    [JsonPropertyName("tag")] public Vtk Tag { get; init; }
    /// <summary>Object: 内部名（如 java/lang/String，原始常量池 Utf8）。</summary>
    [JsonPropertyName("className")] public string? ClassName { get; init; }
    /// <summary>Uninitialized: 对应 new 指令的字节偏移（基准 bci）。</summary>
    [JsonPropertyName("newBci")] public int? NewBci { get; init; }
    /// <summary>原始 verification_type_info tag 值，损坏帧场景下可能与 Tag 不一致（诊断用）。</summary>
    [JsonPropertyName("rawTag")] public int RawTag { get; init; }

    public static readonly VType TTop = new() { Tag = Vtk.Top, RawTag = 0 };
    public static readonly VType TInt = new() { Tag = Vtk.Integer, RawTag = 1 };
    public static readonly VType TFloat = new() { Tag = Vtk.Float, RawTag = 2 };
    public static readonly VType TDouble = new() { Tag = Vtk.Double, RawTag = 3 };
    public static readonly VType TLong = new() { Tag = Vtk.Long, RawTag = 4 };
    public static readonly VType TNull = new() { Tag = Vtk.Null, RawTag = 5 };
    public static readonly VType TUninitThis = new() { Tag = Vtk.UninitializedThis, RawTag = 6 };
    public static VType Obj(string internalName) => new() { Tag = Vtk.Object, ClassName = internalName, RawTag = 7 };
    public static VType Uninit(int newBci) => new() { Tag = Vtk.Uninitialized, NewBci = newBci, RawTag = 8 };

    /// <summary>JVM 计算类型类别：long/double = category 2（占两槽），其余 category 1。</summary>
    [JsonIgnore] public int Category => Tag is Vtk.Long or Vtk.Double ? 2 : 1;

    public bool IsReference => Tag is Vtk.Null or Vtk.Object or Vtk.Uninitialized or Vtk.UninitializedThis;

    public string Mnemonic => Tag switch
    {
        Vtk.Top => "top",
        Vtk.Integer => "int",
        Vtk.Float => "float",
        Vtk.Double => "double",
        Vtk.Long => "long",
        Vtk.Null => "null",
        Vtk.UninitializedThis => "uninitializedThis",
        Vtk.Object => ClassName ?? "object?",
        Vtk.Uninitialized => $"uninitialized@{NewBci}",
        _ => "?",
    };

    public override string ToString() => Mnemonic;
}

/// <summary>
/// 抽象解释器在某条边上的帧：locals（固定长度数组，未使用槽为 Top）与
/// operand stack（栈底→栈顶，category-2 逻辑上只放一个元素，物理第二槽在比较时折叠）。
/// </summary>
public sealed record FrameState
{
    [JsonPropertyName("bci")] public int Bci { get; init; }
    [JsonPropertyName("locals")] public VType[] Locals { get; init; } = System.Array.Empty<VType>();
    [JsonPropertyName("stack")] public VType[] Stack { get; init; } = System.Array.Empty<VType>();
    /// <summary>帧来源：初始帧、栈映帧声明、前序推导、异常入口或 join。</summary>
    [JsonPropertyName("origin")] public FrameOrigin Origin { get; init; }

    public FrameState Clone() => new()
    {
        Bci = Bci,
        Locals = (VType[])Locals.Clone(),
        Stack = (VType[])Stack.Clone(),
        Origin = Origin,
    };

    public string Render()
    {
        var loc = string.Join(", ", Locals.Select(t => t.Mnemonic));
        var stk = string.Join(", ", Stack.Select(t => t.Mnemonic));
        return $"@{Bci} L[{loc}] S[{stk}] ({Origin})";
    }
}

public enum FrameOrigin
{
    /// <summary>方法入参初始帧（bci=0）。</summary>
    Initial,
    /// <summary>来自 class 文件 StackMapTable 的声明帧（原证据）。</summary>
    DeclaredStackMap,
    /// <summary>由前驱指令数据流推导。</summary>
    Derived,
    /// <summary>异常处理入口：栈为单元素异常对象。</summary>
    ExceptionEdge,
    /// <summary>多个前驱 join 后的帧。</summary>
    Join,
    /// <summary>不可达块标记帧（locals/stack 全 Top）。</summary>
    Dead,
}
