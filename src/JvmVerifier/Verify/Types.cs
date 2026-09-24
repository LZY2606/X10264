namespace JvmVerifier.Verify;

/// <summary>验证类型。category-2 值以 (Long,Top)/(Double,Top) 两个槽表示。</summary>
public abstract record VType
{
    public sealed record Top : VType;                 // 不可用（冲突合并结果）
    public sealed record Int : VType;
    public sealed record Long : VType;
    public sealed record Float : VType;
    public sealed record Double : VType;
    public sealed record Null : VType;
    public sealed record Ref(string ClassName) : VType;
    public sealed record UninitializedThis : VType;
    public sealed record Uninitialized(int NewOffset) : VType;

    public bool IsCategory2 => this is Long or Double;

    public string Display => this switch
    {
        Top => "top(unusable)",
        Int => "int",
        Long => "long",
        Float => "float",
        Double => "double",
        Null => "null",
        Ref r => r.ClassName.Replace('/', '.'),
        UninitializedThis => "uninitializedThis",
        Uninitialized u => $"uninitialized@{u.NewOffset}",
        _ => "?"
    };
}

/// <summary>一条局部变量/栈槽状态。槽坐标以局部变量索引或栈深度为基准。</summary>
public sealed record FrameState(IReadOnlyList<VType> Locals, IReadOnlyList<VType> Stack)
{
    public string Key => string.Join(",", Locals.Select(t => t.Display)) + "|" + string.Join(",", Stack.Select(t => t.Display));
}

/// <summary>机器可读的验证事件。</summary>
public sealed record VerifyEvent(
    string Id,            // 稳定身份，如 evt:uninit-use:settle:31
    string Kind,          // uninitialized-use / category2-violation / exception-edge / unreachable-block / missing-frame / join-conflict / corrupted-frame-offset / frame-applied / verify-ok / verify-fail
    string Method,
    int Offset,           // 原字节偏移
    string Severity,      // info / warning / error
    string Message,
    IReadOnlyDictionary<string, object?> Detail)
{
    public bool IsError => Severity == "error";
}

public sealed record EdgeState(
    string Id,            // 稳定身份 edge:{method}:{from}->{to}
    int FromOffset, int ToOffset, string Kind,   // fallthrough / branch / exception
    FrameState State);

public sealed record VerifyReport(
    string Method, string Descriptor, int MajorVersion,
    bool Passed,
    IReadOnlyDictionary<int, FrameState> States,       // 每条指令入口状态
    IReadOnlyList<EdgeState> Edges,
    IReadOnlyList<VerifyEvent> Events,
    IReadOnlyList<int> EarliestFailurePath,            // 最早失败路径（偏移序列）
    IReadOnlyList<int> UnreachableOffsets);
