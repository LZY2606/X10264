namespace JvmVerify.Core;

/// <summary>Result of merging two verification types. Multi-solution merges keep
/// every legal candidate instead of collapsing to a single answer.</summary>
public sealed record MergeResult(
    bool Compatible,
    VType? Single,
    IReadOnlyList<VType> Candidates,
    string? ConflictReason)
{
    public static MergeResult Of(VType t) => new(true, t, new[] { t }, null);
    public static MergeResult Multi(IReadOnlyList<VType> candidates) =>
        new(true, null, candidates, null);
    public static MergeResult Conflict(string reason) =>
        new(false, null, Array.Empty<VType>(), reason);
}

public static class TypeMerge
{
    public static bool IsAssignable(VType from, VType to, IClassHierarchy hierarchy)
    {
        if (from.Equals(to)) return true;
        return (from, to) switch
        {
            (_, VType.Top) => true,
            (VType.Null, VType.Ref) => true,
            (VType.Ref a, VType.Ref b) => IsSubclassOrImplements(a.ClassName, b.ClassName, hierarchy),
            _ => false,
        };
    }

    public static bool IsSubclassOrImplements(string a, string b, IClassHierarchy hierarchy)
    {
        if (a == b) return true;
        if (b == "java/lang/Object") return true;
        var seen = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(a);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!seen.Add(cur)) continue;
            if (cur == b) return true;
            var sup = hierarchy.SuperOf(cur);
            if (sup is not null) queue.Enqueue(sup);
            foreach (var iface in hierarchy.InterfacesOf(cur)) queue.Enqueue(iface);
        }
        return false;
    }

    /// <summary>Merges two types. Reference merges that admit several common
    /// supertypes return all of them as candidates (first = closest superclass
    /// chain meet, then shared interfaces) — nothing is swallowed.</summary>
    public static MergeResult Merge(VType a, VType b, IClassHierarchy hierarchy)
    {
        if (a.Equals(b)) return MergeResult.Of(a);
        if (a is VType.Top) return MergeResult.Of(b);
        if (b is VType.Top) return MergeResult.Of(a);
        if (a is VType.Null) return MergeResult.Of(b);
        if (b is VType.Null) return MergeResult.Of(a);
        if (a is VType.Ref ra && b is VType.Ref rb)
            return MergeResult.Multi(CommonSupertypes(ra.ClassName, rb.ClassName, hierarchy)
                .Select(n => (VType)new VType.Ref(n)).ToArray());
        if (a is VType.UninitializedThis || b is VType.UninitializedThis ||
            a is VType.Uninitialized || b is VType.Uninitialized)
            return MergeResult.Conflict(
                $"uninitialized values cannot merge: {a} vs {b}");
        return MergeResult.Conflict($"incompatible types at join: {a} vs {b}");
    }

    /// <summary>All common supertypes of two classes: the closest common
    /// superclass first, then all shared interfaces. Order is stable.</summary>
    public static IReadOnlyList<string> CommonSupertypes(string a, string b, IClassHierarchy hierarchy)
    {
        var aChain = SuperChain(a, hierarchy);
        var bChain = SuperChain(b, hierarchy);
        var meet = aChain.FirstOrDefault(bChain.Contains) ?? "java/lang/Object";
        var aIfaces = AllInterfaces(a, hierarchy);
        var bIfaces = AllInterfaces(b, hierarchy);
        var shared = aIfaces.Intersect(bIfaces).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var result = new List<string> { meet };
        result.AddRange(shared.Where(s => s != meet));
        return result;
    }

    private static List<string> SuperChain(string c, IClassHierarchy hierarchy)
    {
        var chain = new List<string>();
        var cur = c;
        while (cur is not null && !chain.Contains(cur))
        {
            chain.Add(cur);
            cur = hierarchy.SuperOf(cur);
        }
        return chain;
    }

    private static HashSet<string> AllInterfaces(string c, IClassHierarchy hierarchy)
    {
        var set = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(c);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var iface in hierarchy.InterfacesOf(cur))
                if (set.Add(iface)) queue.Enqueue(iface);
            var sup = hierarchy.SuperOf(cur);
            if (sup is not null) queue.Enqueue(sup);
        }
        return set;
    }
}
