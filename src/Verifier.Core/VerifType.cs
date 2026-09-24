namespace Verifier.Core;

/// <summary>
/// 验证类型（JVMS 4.10.1 verification type system）。
/// 每个类型都带“类别”坐标：category 1 占 1 个 slot，category 2（long/double）占 2 个 slot。
/// </summary>
public abstract record VerifType
{
    public abstract int Category { get; }
    public abstract string Name { get; }
    public sealed record Top() : VerifType { public override int Category => 1; public override string Name => "top"; }
    public sealed record Integer() : VerifType { public override int Category => 1; public override string Name => "int"; }
    public sealed record Float() : VerifType { public override int Category => 1; public override string Name => "float"; }
    public sealed record Long() : VerifType { public override int Category => 2; public override string Name => "long"; }
    public sealed record Double() : VerifType { public override int Category => 2; public override string Name => "double"; }
    public sealed record Null() : VerifType { public override int Category => 1; public override string Name => "null"; }
    public sealed record UninitializedThis() : VerifType { public override int Category => 1; public override string Name => "uninitializedThis"; }

    /// <summary>对象类型，BinaryName 为内部名（如 java/lang/String）。</summary>
    public sealed record Reference(string BinaryName) : VerifType
    {
        public override int Category => 1;
        public override string Name => BinaryName;
    }

    /// <summary>NEW 产生的未初始化对象，Offset 为 NEW 指令字节偏移（Code[0] 原点）。</summary>
    public sealed record Uninitialized(int Offset) : VerifType
    {
        public override int Category => 1;
        public override string Name => $"uninitialized@{Offset}";
    }

    /// <summary>无法唯一决定的引用：保留全部合法候选（不吞掉多解）。</summary>
    public sealed record Multi(IReadOnlyList<VerifType> Candidates) : VerifType
    {
        public override int Category => 1;
        public override string Name => "multi{" + string.Join("|", Candidates.Select(c => c.Name)) + "}";
    }

    public static VerifType TopT { get; } = new Top();
}

/// <summary>类型层级解析（最小化、面向 fixture：内置 java.lang 根类型）。</summary>
public interface ITypeHierarchy
{
    bool IsSubclassOf(string childBinaryName, string parentBinaryName);
    bool IsInterface(string binaryName);
}

public sealed class DefaultTypeHierarchy : ITypeHierarchy
{
    // java.lang 下常见的直接关系；其余类型默认父类为 java/lang/Object。
    private static readonly Dictionary<string, string> Parents = new()
    {
        ["java/lang/String"] = "java/lang/Object",
        ["java/lang/Throwable"] = "java/lang/Object",
        ["java/lang/Exception"] = "java/lang/Throwable",
        ["java/lang/RuntimeException"] = "java/lang/Exception",
        ["java/lang/IllegalArgumentException"] = "java/lang/RuntimeException",
        ["java/lang/NullPointerException"] = "java/lang/RuntimeException",
        ["java/lang/Integer"] = "java/lang/Object",
        ["java/io/IOException"] = "java/lang/Exception",
        ["demo/Foo"] = "java/lang/Object",
        ["demo/Bar"] = "demo/Foo",
    };

    public bool IsInterface(string binaryName) => false;

    public bool IsSubclassOf(string child, string parent)
    {
        if (child == parent) return true;
        if (parent == "java/lang/Object") return true;
        var cur = child;
        var seen = new HashSet<string>();
        while (Parents.TryGetValue(cur, out var p) && seen.Add(cur))
        {
            if (p == parent) return true;
            cur = p;
        }
        return false;
    }
}

public static class VerifTypes
{
    /// <summary>可指派性：actual 是否可在版本语义下赋给 declared（JVMS 4.10.1.2）。</summary>
    public static bool IsAssignable(VerifType actual, VerifType declared, ITypeHierarchy hier)
    {
        if (actual is VerifType.Multi m) return m.Candidates.All(c => IsAssignable(c, declared, hier));
        if (declared is VerifType.Multi dm) return dm.Candidates.Any(d => IsAssignable(actual, d, hier));
        return (actual, declared) switch
        {
            (_, VerifType.Top) => true,
            (VerifType.Top, _) => false,
            (VerifType.Integer, VerifType.Integer) => true,
            (VerifType.Float, VerifType.Float) => true,
            (VerifType.Long, VerifType.Long) => true,
            (VerifType.Double, VerifType.Double) => true,
            (VerifType.Null, VerifType.Reference) => true,
            (VerifType.Reference a, VerifType.Reference d) => hier.IsSubclassOf(a.BinaryName, d.BinaryName),
            (VerifType.UninitializedThis, VerifType.UninitializedThis) => true,
            (VerifType.Uninitialized a, VerifType.Uninitialized d) => a.Offset == d.Offset,
            _ => false
        };
    }

    /// <summary>
    /// 计算 join（公共超类型）。无法唯一决定时返回 Multi 保留全部合法候选，而不是强行收敛成 Object。
    /// </summary>
    public static VerifType Join(VerifType a, VerifType b, ITypeHierarchy hier)
    {
        if (a.Equals(b)) return a;
        if (a is VerifType.Multi am) return JoinCandidates(am.Candidates.Append(b), hier);
        if (b is VerifType.Multi bm) return JoinCandidates(bm.Candidates.Append(a), hier);
        if (a is VerifType.Null && b is VerifType.Reference rb) return rb;
        if (b is VerifType.Null && a is VerifType.Reference ra) return ra;
        if (a is VerifType.Reference x && b is VerifType.Reference y)
        {
            if (hier.IsSubclassOf(x.BinaryName, y.BinaryName)) return y;
            if (hier.IsSubclassOf(y.BinaryName, x.BinaryName)) return x;
            // 无唯一公共类型：保留两个候选（多解），由版本语义/人工决定处理。
            return new VerifType.Multi(new VerifType[] { x, y });
        }
        // 类别或顶层不同：top
        return VerifType.TopT;
    }

    private static VerifType JoinCandidates(IEnumerable<VerifType> types, ITypeHierarchy hier)
    {
        var acc = VerifType.TopT;
        var first = true;
        foreach (var t in types)
        {
            acc = first ? t : Join(acc, t, hier);
            first = false;
        }
        return acc;
    }
}
