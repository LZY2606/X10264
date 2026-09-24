namespace Jv.Core.Verify;

// JVM 验证类型（JVMS 4.10.1）。保留与 StackMapTable verification_type_info
// 对应的原始 tag：0 top,1 int,2 float,3 double,4 long,5 null,6 uninitializedThis,
// 7 Object(cpIndex),8 Uninitialized(offset)。
public enum VKind
{
    Top, Int, Float, Long, Double, Null,
    Object,                 // name = 内部名 java/lang/String
    UninitializedObject,    // newOffset
    UninitializedThis,
    Conflict // 抽象解释中 join 冲突后的 TOP，区别于显式 Top
}

public readonly record struct VType(VKind Kind, string? Name = null, int Data = 0, int CpIndex = 0)
{
    public static readonly VType Top = new(VKind.Top);
    public static readonly VType Int = new(VKind.Int);
    public static readonly VType Float = new(VKind.Float);
    public static readonly VType Long = new(VKind.Long);
    public static readonly VType Double = new(VKind.Double);
    public static readonly VType Null = new(VKind.Null);
    public static readonly VType UninitThis = new(VKind.UninitializedThis);

    public static VType Obj(string internalName, int cpIndex = 0) =>
        new(VKind.Object, internalName, 0, cpIndex);
    public static VType Uninit(int newOffset) =>
        new(VKind.UninitializedObject, null, newOffset);

    // JVMS 2.11.1-7：long/double 为 category 2，占两个局部变量/两个操作数栈位。
    public int Category => Kind is VKind.Long or VKind.Double ? 2 : 1;

    public bool IsReference => Kind is VKind.Object or VKind.Null or
        VKind.UninitializedObject or VKind.UninitializedThis;

    // 与 StackMapTable tag 的对照（用于帧差异/档案展示）。
    public int FrameTag => Kind switch
    {
        VKind.Top => 0,
        VKind.Int => 1,
        VKind.Float => 2,
        VKind.Double => 3,
        VKind.Long => 4,
        VKind.Null => 5,
        VKind.UninitializedThis => 6,
        VKind.Object => 7,
        VKind.UninitializedObject => 8,
        _ => 0
    };

    public override string ToString() => Kind switch
    {
        VKind.Object => Name ?? "?",
        VKind.UninitializedObject => $"uninit@{Data}",
        VKind.UninitializedThis => "uninitThis",
        _ => Kind.ToString().ToLowerInvariant()
    };
}
