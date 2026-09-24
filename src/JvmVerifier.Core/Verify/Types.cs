namespace JvmVerifier.Core.Verify;

/// <summary>Verification types (JVMS §4.10.1.2). Category-2 values (Long/Double) occupy two local slots.</summary>
public abstract record VType
{
    public sealed record TopT : VType;
    public sealed record IntT : VType;
    public sealed record FloatT : VType;
    public sealed record LongT : VType;
    public sealed record DoubleT : VType;
    public sealed record NullT : VType;
    public sealed record ObjectT(string Name) : VType;
    public sealed record UninitializedThisT : VType;
    public sealed record UninitializedT(int Offset) : VType;

    public static readonly VType Top = new TopT();
    public static readonly VType Int = new IntT();
    public static readonly VType Float = new FloatT();
    public static readonly VType Long = new LongT();
    public static readonly VType Double = new DoubleT();
    public static readonly VType Null = new NullT();

    public bool IsCategory2 => this is LongT or DoubleT;

    public string Display => this switch
    {
        TopT => "top",
        IntT => "int",
        FloatT => "float",
        LongT => "long",
        DoubleT => "double",
        NullT => "null",
        ObjectT o => $"ref:{o.Name}",
        UninitializedThisT => "uninitializedThis",
        UninitializedT u => $"uninitialized@{u.Offset}",
        _ => "?"
    };

    public static VType Parse(string s)
    {
        if (s.StartsWith("ref:")) return new ObjectT(s[4..]);
        if (s.StartsWith("uninitialized@")) return new UninitializedT(int.Parse(s[14..]));
        return s switch
        {
            "top" => Top, "int" => Int, "float" => Float, "long" => Long,
            "double" => Double, "null" => Null, "uninitializedThis" => new UninitializedThisT(),
            _ => Top
        };
    }
}

/// <summary>Immutable local/stack snapshot at a program point.</summary>
public sealed record FrameState(VType[] Locals, VType[] Stack)
{
    public string[] LocalStrings => Locals.Select(l => l.Display).ToArray();
    public string[] StackStrings => Stack.Select(s => s.Display).ToArray();

    public FrameState With(VType[]? locals = null, VType[]? stack = null) =>
        new(locals ?? Locals, stack ?? Stack);

    public static VType? JoinType(VType a, VType b)
    {
        if (a.Equals(b)) return a;
        if (a is VType.NullT && b is VType.ObjectT) return b;
        if (b is VType.NullT && a is VType.ObjectT) return a;
        if (a is VType.ObjectT ao && b is VType.ObjectT bo)
            return ao.Name == bo.Name ? a : new VType.ObjectT(CommonSuperclass(ao.Name, bo.Name));
        return null; // conflict: callers must preserve both candidates
    }

    private static string CommonSuperclass(string a, string b)
    {
        if (a == b) return a;
        if (a is "java/lang/Object") return a;
        if (b is "java/lang/Object") return b;
        // No class hierarchy database in the archive: the only provable common supertype is Object.
        return "java/lang/Object";
    }

    /// <summary>Joins two states. Returns null when locals/stack heights are incompatible;
    /// conflicting slots are reported via <paramref name="conflicts"/> and joined as Top only
    /// when <paramref name="tolerateConflicts"/> is set (conflicts are always recorded).</summary>
    public static FrameState? Join(FrameState a, FrameState b, List<string> conflicts, bool tolerateConflicts = false)
    {
        if (a.Stack.Length != b.Stack.Length)
        {
            conflicts.Add($"stack height mismatch {a.Stack.Length} vs {b.Stack.Length}");
            if (!tolerateConflicts) return null;
        }
        int n = Math.Min(a.Stack.Length, b.Stack.Length);
        var stack = new VType[n];
        for (int i = 0; i < n; i++)
        {
            var j = JoinType(a.Stack[i], b.Stack[i]);
            if (j == null)
            {
                conflicts.Add($"stack[{i}]: {a.Stack[i].Display} vs {b.Stack[i].Display}");
                if (!tolerateConflicts) return null;
                j = VType.Top;
            }
            stack[i] = j;
        }
        int nl = Math.Max(a.Locals.Length, b.Locals.Length);
        var locals = new VType[nl];
        for (int i = 0; i < nl; i++)
        {
            var la = i < a.Locals.Length ? a.Locals[i] : VType.Top;
            var lb = i < b.Locals.Length ? b.Locals[i] : VType.Top;
            var j = JoinType(la, lb);
            if (j == null)
            {
                conflicts.Add($"local[{i}]: {la.Display} vs {lb.Display}");
                if (!tolerateConflicts) return null;
                j = VType.Top;
            }
            locals[i] = j;
        }
        return new FrameState(locals, stack);
    }

    /// <summary>Is every slot of <paramref name="actual"/> assignable to the declared frame?</summary>
    public static bool IsAssignableTo(FrameState actual, FrameState declared, List<string> mismatches)
    {
        bool ok = true;
        for (int i = 0; i < declared.Locals.Length; i++)
        {
            var d = declared.Locals[i];
            var a = i < actual.Locals.Length ? actual.Locals[i] : VType.Top;
            if (d is VType.TopT) continue;
            if (!SlotAssignable(a, d)) { mismatches.Add($"local[{i}]: inferred {a.Display} not assignable to declared {d.Display}"); ok = false; }
        }
        if (actual.Stack.Length != declared.Stack.Length)
        {
            mismatches.Add($"stack height {actual.Stack.Length} != declared {declared.Stack.Length}");
            ok = false;
        }
        else
        {
            for (int i = 0; i < declared.Stack.Length; i++)
            {
                if (!SlotAssignable(actual.Stack[i], declared.Stack[i]))
                { mismatches.Add($"stack[{i}]: inferred {actual.Stack[i].Display} not assignable to declared {declared.Stack[i].Display}"); ok = false; }
            }
        }
        return ok;
    }

    private static bool SlotAssignable(VType a, VType d)
    {
        if (a.Equals(d)) return true;
        if (a is VType.NullT && d is VType.ObjectT) return true;
        if (a is VType.ObjectT ao && d is VType.ObjectT dob)
            return dob.Name == "java/lang/Object" || ao.Name == dob.Name;
        if (a is VType.UninitializedThisT && d is VType.UninitializedThisT) return true;
        return false;
    }
}
