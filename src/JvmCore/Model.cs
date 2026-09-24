namespace JvmCore;

/// <summary>
/// A domain quantity that always carries its unit and coordinate basis.
/// Raw is the original representation exactly as imported; Value is the
/// derived/normalized view. When normalization fails Value is null and a
/// diagnostic is emitted; the raw value stays in place.
/// </summary>
public sealed record Quantity(string Raw, double? Value, string Unit, string Basis)
{
    public static Quantity ByteOffset(int v) => new(v.ToString(), v, "byte", "code-array-start");
    public static Quantity Slot(int v) => new(v.ToString(), v, "slot", "local-variable-array");
    public static Quantity Depth(int v) => new(v.ToString(), v, "slot", "operand-stack-top");
    public static Quantity Version(int v) => new(v.ToString(), v, "classfile-major-version", "JVM");
    public static Quantity CpIndex(int v) => new(v.ToString(), v, "constant-pool-index", "constant_pool[1..count-1]");
    public static Quantity Unconvertible(string raw, string unit, string basis) => new(raw, null, unit, basis);
}

/// <summary>Verification types (JVMS §4.10.1.2).</summary>
public enum VKind { Top, Int, Float, Long, Double, Null, Ref, UninitializedThis, Uninitialized, ReturnAddr, SecondSlot }

public sealed record VType(VKind Kind, string? ClassName = null, int? NewOffset = null)
{
    public static readonly VType Top = new(VKind.Top);
    public static readonly VType Int = new(VKind.Int);
    public static readonly VType Float = new(VKind.Float);
    public static readonly VType Long = new(VKind.Long);
    public static readonly VType Double = new(VKind.Double);
    public static readonly VType Null = new(VKind.Null);
    public static readonly VType SecondSlot = new(VKind.SecondSlot);
    public static VType RefOf(string name) => new(VKind.Ref, name);
    public static VType UninitializedAt(int offset) => new(VKind.Uninitialized, null, offset);
    public static readonly VType UninitializedThis = new(VKind.UninitializedThis);

    public bool IsCategory2 => Kind is VKind.Long or VKind.Double;
    public bool IsRefLike => Kind is VKind.Ref or VKind.Null;

    public override string ToString() => Kind switch
    {
        VKind.Ref => $"ref:{ClassName}",
        VKind.Uninitialized => $"uninit@{NewOffset}",
        VKind.UninitializedThis => "uninitThis",
        VKind.ReturnAddr => $"retAddr@{NewOffset}",
        VKind.SecondSlot => "cat2-hi",
        _ => Kind.ToString().ToLowerInvariant()
    };
}

/// <summary>One slot merge keeps every legal candidate; ambiguity is never collapsed silently.</summary>
public sealed record SlotMerge(VType Result, IReadOnlyList<VType> Candidates, bool Conflict);

/// <summary>Local-variable + operand-stack state at one program point.</summary>
public sealed class FrameState
{
    public VType[] Locals;
    public VType[] Stack;

    public FrameState(VType[] locals, VType[] stack) { Locals = locals; Stack = stack; }

    public FrameState Clone() => new((VType[])Locals.Clone(), (VType[])Stack.Clone());

    public string LocalsText() => "[" + string.Join(",", Locals.Select(l => l.ToString())) + "]";
    public string StackText() => "[" + string.Join(",", Stack.Select(s => s.ToString())) + "]";

    /// <summary>Merge two frames slot-by-slot. Stack heights must agree.</summary>
    public static (FrameState? merged, List<SlotMerge> slots, string? heightError) Merge(FrameState a, FrameState b)
    {
        var slots = new List<SlotMerge>();
        if (a.Stack.Length != b.Stack.Length)
            return (null, slots, $"stack height mismatch {a.Stack.Length} vs {b.Stack.Length}");
        var locals = new VType[a.Locals.Length];
        for (var i = 0; i < locals.Length; i++)
        {
            var m = MergeType(a.Locals[i], b.Locals[i]);
            slots.Add(m);
            locals[i] = m.Result;
        }
        var stack = new VType[a.Stack.Length];
        for (var i = 0; i < stack.Length; i++)
        {
            var m = MergeType(a.Stack[i], b.Stack[i]);
            slots.Add(m);
            stack[i] = m.Result;
        }
        return (new FrameState(locals, stack), slots, null);
    }

    public static SlotMerge MergeType(VType a, VType b)
    {
        if (a.Equals(b)) return new SlotMerge(a, new[] { a }, false);
        if (a.Kind == VKind.Top) return new SlotMerge(b, new[] { b }, false);
        if (b.Kind == VKind.Top) return new SlotMerge(a, new[] { a }, false);
        // category mismatch (e.g. long halves vs int) is a hard conflict
        if (a.IsCategory2 != b.IsCategory2 || a.Kind == VKind.SecondSlot || b.Kind == VKind.SecondSlot)
            return new SlotMerge(VType.Top, new[] { a, b }, true);
        if (a.IsRefLike && b.IsRefLike)
        {
            if (a.Kind == VKind.Null) return new SlotMerge(b, new[] { b }, false);
            if (b.Kind == VKind.Null) return new SlotMerge(a, new[] { a }, false);
            if (a.ClassName == b.ClassName) return new SlotMerge(a, new[] { a }, false);
            // multiple legal common supertypes may exist: keep all candidates.
            var candidates = new List<VType> { VType.RefOf("java/lang/Object") };
            return new SlotMerge(candidates[0], candidates, false);
        }
        if (a.Kind == VKind.UninitializedThis || b.Kind == VKind.UninitializedThis)
            return new SlotMerge(VType.Top, new[] { a, b }, true);
        if (a.Kind == VKind.Uninitialized || b.Kind == VKind.Uninitialized)
            return new SlotMerge(VType.Top, new[] { a, b }, true);
        return new SlotMerge(VType.Top, new[] { a, b }, true);
    }

    /// <summary>Is this state assignable to (at most as general as) the map frame?</summary>
    public bool AssignableTo(FrameState map, out string? reason)
    {
        reason = null;
        if (Stack.Length != map.Stack.Length) { reason = $"stack height {Stack.Length} != frame {Stack.Length} vs {map.Stack.Length}"; return false; }
        for (var i = 0; i < map.Locals.Length; i++)
            if (!TypeAssignable(Locals[i], map.Locals[i])) { reason = $"local[{i}] {Locals[i]} not assignable to frame {map.Locals[i]}"; return false; }
        for (var i = 0; i < map.Stack.Length; i++)
            if (!TypeAssignable(Stack[i], map.Stack[i])) { reason = $"stack[{i}] {Stack[i]} not assignable to frame {map.Stack[i]}"; return false; }
        return true;
    }

    public static bool TypeAssignable(VType from, VType to)
    {
        if (from.Equals(to) || to.Kind == VKind.Top) return true;
        if (from.Kind == VKind.Null && to.Kind == VKind.Ref) return true;
        if (from.Kind == VKind.Ref && to.Kind == VKind.Ref)
            return to.ClassName == "java/lang/Object" || from.ClassName == to.ClassName;
        return false;
    }
}
