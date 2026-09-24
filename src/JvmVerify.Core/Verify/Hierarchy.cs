namespace JvmVerify.Core.Verify;

/// <summary>
/// Minimal class hierarchy used by the verifier. Fixture classes declare their superclass
/// at import; everything else is treated conservatively as a direct subclass of
/// java/lang/Object (no multi-solution is invented). Arrays follow JLS rules.
/// </summary>
public sealed class Hierarchy
{
    private readonly Dictionary<string, string?> _super = new()
    {
        ["java/lang/Object"] = null,
        ["java/lang/Throwable"] = "java/lang/Object",
        ["java/lang/Exception"] = "java/lang/Throwable",
        ["java/lang/RuntimeException"] = "java/lang/Exception",
        ["java/lang/Error"] = "java/lang/Throwable",
        ["java/lang/IllegalStateException"] = "java/lang/RuntimeException",
        ["java/lang/NullPointerException"] = "java/lang/RuntimeException",
        ["java/lang/ArithmeticException"] = "java/lang/RuntimeException",
        ["java/lang/ArrayIndexOutOfBoundsException"] = "java/lang/RuntimeException",
        ["java/lang/String"] = "java/lang/Object",
        ["java/lang/StringBuilder"] = "java/lang/Object",
    };

    public IReadOnlyDictionary<string, string?> Declared => _super;

    public void Declare(string binaryName, string? superBinaryName)
    {
        _super.TryAdd(binaryName, superBinaryName ?? "java/lang/Object");
    }

    public bool IsSubclassOf(string child, string ancestor)
    {
        if (child == ancestor) return true;
        var seen = new HashSet<string>();
        var cur = child;
        while (cur is not null && seen.Add(cur))
        {
            if (cur == ancestor) return true;
            if (!_super.TryGetValue(cur, out var sup))
                sup = cur == "java/lang/Object" ? null : "java/lang/Object";
            cur = sup!;
        }
        return false;
    }

    /// <summary>Reference assignability including array rules of JVMS §4.10.1.2.</summary>
    public bool IsAssignable(VType.Reference from, VType.Reference to)
    {
        if (from.BinaryName == to.BinaryName) return true;
        bool fa = from.IsArray, ta = to.IsArray;
        if (!fa && !ta) return IsSubclassOf(from.BinaryName, to.BinaryName);
        if (fa && !ta)
            return to.BinaryName is "java/lang/Object" or "java/lang/Cloneable" or "java/io/Serializable";
        if (!fa && ta) return false;
        var fe = ElementName(from.BinaryName);
        var te = ElementName(to.BinaryName);
        if (fe.Length != te.Length) return false;
        var fc = fe[^1];
        var tc = te[^1];
        if (PrimitiveChar(fc) || PrimitiveChar(tc)) return fe == te;
        return IsSubclassOf(new string(fc), new string(tc));
    }

    private static bool PrimitiveChar(char c) => "BCDFIJSZ".IndexOf(c) >= 0;

    private static char[] ElementName(string arrayDesc)
    {
        int i = 0;
        while (i < arrayDesc.Length && arrayDesc[i] == '[') i++;
        return arrayDesc[i..].ToCharArray();
    }

    /// <summary>Common super reference: nearest declared superclass; arrays collapse to Object/array rules.</summary>
    public VType.Reference CommonSuper(VType.Reference a, VType.Reference b)
    {
        if (IsAssignable(a, b)) return b;
        if (IsAssignable(b, a)) return a;
        if (a.IsArray || b.IsArray)
        {
            if (a.IsArray && b.IsArray)
            {
                int da = a.BinaryName.TakeWhile(c => c == '[').Count();
                int db = b.BinaryName.TakeWhile(c => c == '[').Count();
                if (da == db) return new VType.Reference("[Ljava/lang/Object;", IsArray: true);
            }
            return new VType.Reference("java/lang/Object");
        }
        var chain = new HashSet<string>();
        var cur = a.BinaryName;
        while (cur is not null)
        {
            chain.Add(cur);
            cur = ResolveSuper(cur);
        }
        cur = b.BinaryName;
        while (cur is not null && !chain.Contains(cur)) cur = ResolveSuper(cur);
        return new VType.Reference(cur ?? "java/lang/Object");
    }

    private string? ResolveSuper(string name)
        => _super.TryGetValue(name, out var s) ? s : (name == "java/lang/Object" ? null : "java/lang/Object");
}

public enum JoinConflict
{
    None,
    Incompatible,
    UninitializedDestruction,
    ReturnAddressMerge
}

/// <summary>Result of a lattice join; conflicts never silently become a value — the conflict is reported.</summary>
public sealed record JoinResult(VType Type, JoinConflict Conflict, string? Note)
{
    public bool HasConflict => Conflict != JoinConflict.None;
}

public static class TypeLattice
{
    public static JoinResult Join(VType a, VType b, Hierarchy h, bool legacy)
    {
        if (a.Equals(b)) return new JoinResult(a, JoinConflict.None, null);
        switch (a, b)
        {
            case (VType.TopType, _): case (_, VType.TopType):
                return new JoinResult(VType.Top, JoinConflict.None, "top absorbs");
            case (VType.Null, VType.Reference r): case (VType.Reference r, VType.Null):
                return new JoinResult(r, JoinConflict.None, null);
            case (VType.Null, VType.Null):
                return new JoinResult(VType.Null, JoinConflict.None, null);
            case (VType.Reference x, VType.Reference y):
                var cs = h.CommonSuper(x, y);
                var note = cs.BinaryName != x.BinaryName && cs.BinaryName != y.BinaryName
                    ? $"branches widened to common superclass {cs.BinaryName}"
                    : null;
                return new JoinResult(cs, JoinConflict.None, note);
            case (VType.UninitializedOffset, VType.UninitializedOffset):
            case (VType.UninitializedThis, VType.UninitializedThis):
                return new JoinResult(VType.Top, JoinConflict.UninitializedDestruction,
                    "distinct uninitialized types merged; verifier marks slot top (JVMS §4.10.1.2)");
            case (VType.UninitializedOffset, _): case (_, VType.UninitializedOffset):
            case (VType.UninitializedThis, _): case (_, VType.UninitializedThis):
                return new JoinResult(VType.Top, JoinConflict.UninitializedDestruction,
                    "uninitialized type merged with initialized type; slot becomes top");
            case (VType.ReturnAddress ra, VType.ReturnAddress rb):
                if (legacy)
                    return new JoinResult(ra.ReturnPc == rb.ReturnPc
                        ? ra
                        : VType.Top, ra.ReturnPc == rb.ReturnPc
                            ? JoinConflict.None
                            : JoinConflict.ReturnAddressMerge,
                        ra.ReturnPc == rb.ReturnPc ? null : "jsr targets differ");
                return new JoinResult(VType.Top, JoinConflict.ReturnAddressMerge, "jsr/ret forbidden by modern verifier");
            case (VType.Long, VType.Double): case (VType.Double, VType.Long):
                return new JoinResult(VType.Top, JoinConflict.Incompatible, "category-2 long vs double");
            default:
                if (a.GetType() == b.GetType())
                    return new JoinResult(a, JoinConflict.None, null);
                return new JoinResult(VType.Top, JoinConflict.Incompatible,
                    $"incompatible verification types {a.Name} and {b.Name}");
        }
    }

    /// <summary>Subtype/assignment test at a branch edge (declared frame must accept incoming frame).</summary>
    public static bool IsAssignable(VType from, VType to, Hierarchy h)
    {
        if (to is VType.TopType) return true;
        if (from.Equals(to)) return true;
        if (from is VType.Null && to is VType.Reference) return true;
        if (from is VType.Reference fr && to is VType.Reference tr) return h.IsAssignable(fr, tr);
        return false;
    }
}
