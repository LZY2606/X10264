namespace BytecodeSeal.Core;

/// <summary>Verification types for abstract interpretation (JVMS §4.10.1.2).
/// Category-2 values are represented as a (Long|Double, Long2|Double2) pair so
/// misuse of either half is detectable.</summary>
public abstract record VType
{
    public sealed record Top : VType { public override string ToString() => "top"; }
    public sealed record Null : VType { public override string ToString() => "null"; }
    public sealed record Int : VType { public override string ToString() => "int"; }
    public sealed record Float : VType { public override string ToString() => "float"; }
    public sealed record Long : VType { public override string ToString() => "long"; }
    public sealed record Long2 : VType { public override string ToString() => "long/2"; }
    public sealed record Double : VType { public override string ToString() => "double"; }
    public sealed record Double2 : VType { public override string ToString() => "double/2"; }
    public sealed record Obj(string ClassName) : VType { public override string ToString() => $"ref:{ClassName}"; }
    public sealed record UninitThis : VType { public override string ToString() => "uninitializedThis"; }
    public sealed record UninitNew(int Offset) : VType { public override string ToString() => $"uninitialized@{Offset}"; }

    public static readonly VType TopT = new Top();
    public static readonly VType NullT = new Null();
    public static readonly VType IntT = new Int();
    public static readonly VType FloatT = new Float();
    public static readonly VType LongT = new Long();
    public static readonly VType Long2T = new Long2();
    public static readonly VType DoubleT = new Double();
    public static readonly VType Double2T = new Double2();
    public static readonly VType UninitThisT = new UninitThis();

    public bool IsCategory2First => this is Long or Double;
    public bool IsCategory2Second => this is Long2 or Double2;
    public bool IsReference => this is Null or Obj or UninitThis or UninitNew;

    /// <summary>Assignable check: can a value of this type be used where
    /// <paramref name="target"/> is expected (frame conformance, JVMS §4.10.1.2).</summary>
    public bool IsAssignableTo(VType target)
    {
        if (Equals(target)) return true;
        if (this is Null && target.IsReference) return true;
        if (this is Obj o && target is Obj t)
            return t.ClassName == "java/lang/Object" || o.ClassName == t.ClassName;
        return false;
    }

    /// <summary>Merge two types at a control-flow join. Returns Top when the
    /// values cannot be unified; callers must not silently pick one side.</summary>
    public static VType Merge(VType a, VType b)
    {
        if (a.Equals(b)) return a;
        if (a is Top || b is Top) return TopT;
        if (a is Null && b.IsReference) return b;
        if (b is Null && a.IsReference) return a;
        if (a is Obj oa && b is Obj ob)
        {
            // Simplified common-superclass rule: without a loaded class hierarchy the
            // only safe generalization we can prove is java/lang/Object.
            return new Obj("java/lang/Object");
        }
        // Uninitialized values of different provenance, or category mismatches,
        // have no unique merge: keep the conflict visible as Top.
        return TopT;
    }
}

/// <summary>Local variables + operand stack snapshot. Immutable.</summary>
public sealed record FrameState(IReadOnlyList<VType> Locals, IReadOnlyList<VType> Stack)
{
    public static FrameState Empty(int locals)
        => new(Enumerable.Repeat(VType.TopT, locals).ToArray(), Array.Empty<VType>());

    public FrameState WithLocal(int i, VType v)
    {
        var l = Locals.ToArray();
        l[i] = v;
        return this with { Locals = l };
    }

    public FrameState Push(VType v)
        => this with { Stack = Stack.Concat(new[] { v }).ToArray() };

    public FrameState PushCat2(VType first, VType second)
        => this with { Stack = Stack.Concat(new[] { first, second }).ToArray() };

    public (FrameState State, VType? Value) Pop()
    {
        if (Stack.Count == 0) return (this, null);
        var s = Stack.ToArray();
        var v = s[^1];
        Array.Resize(ref s, s.Length - 1);
        return (this with { Stack = s }, v);
    }

    public bool IsAssignableTo(FrameState target)
    {
        if (Locals.Count != target.Locals.Count || Stack.Count != target.Stack.Count) return false;
        for (int i = 0; i < Locals.Count; i++)
            if (!Locals[i].IsAssignableTo(target.Locals[i])) return false;
        for (int i = 0; i < Stack.Count; i++)
            if (!Stack[i].IsAssignableTo(target.Stack[i])) return false;
        return true;
    }

    public static FrameState Merge(FrameState a, FrameState b)
    {
        var locals = new VType[a.Locals.Count];
        for (int i = 0; i < locals.Length; i++) locals[i] = VType.Merge(a.Locals[i], b.Locals[i]);
        var stack = new VType[a.Stack.Count];
        for (int i = 0; i < stack.Length; i++) stack[i] = VType.Merge(a.Stack[i], b.Stack[i]);
        return new FrameState(locals, stack);
    }

    public string Format()
        => $"L[{string.Join(",", Locals.Select(l => l.ToString()))}] S[{string.Join(",", Stack.Select(s => s.ToString()))}]";
}
