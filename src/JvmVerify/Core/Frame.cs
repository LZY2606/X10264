namespace JvmVerify.Core;

/// <summary>A local-variable / operand-stack state at one byte offset.
/// Slot coordinates are original classfile indices (the coordinate base).</summary>
public sealed class Frame
{
    public VType[] Locals;
    public List<VType> Stack;

    public Frame(VType[] locals, List<VType> stack) { Locals = locals; Stack = stack; }

    public Frame Clone() => new((VType[])Locals.Clone(), new List<VType>(Stack));

    public bool SameAs(Frame other) =>
        Locals.SequenceEqual(other.Locals) && Stack.SequenceEqual(other.Stack);

    public void ReplaceUninit(VType from, VType to)
    {
        for (var i = 0; i < Locals.Length; i++)
            if (Locals[i].Equals(from)) Locals[i] = to;
        for (var i = 0; i < Stack.Count; i++)
            if (Stack[i].Equals(from)) Stack[i] = to;
    }

    public bool ContainsUninit(out VType found)
    {
        foreach (var t in Locals.Concat(Stack))
            if (t.IsUninitialized) { found = t; return true; }
        found = default;
        return false;
    }

    public MergeResult MergeFrom(Frame other)
    {
        if (Locals.Length != other.Locals.Length)
            return MergeResult.Fail($"locals size mismatch: {Locals.Length} vs {other.Locals.Length}");
        if (Stack.Count != other.Stack.Count)
            return MergeResult.Fail($"stack height mismatch at join: {Stack.Count} vs {other.Stack.Count}");
        var candidates = new List<VType>();
        for (var i = 0; i < Locals.Length; i++)
        {
            var m = VTypes.Merge(Locals[i], other.Locals[i]);
            if (m.Error != null) return MergeResult.Fail($"local[{i}]: {m.Error}");
            Locals[i] = m.Merged;
            if (m.Candidates != null) candidates.AddRange(m.Candidates);
        }
        for (var i = 0; i < Stack.Count; i++)
        {
            var m = VTypes.Merge(Stack[i], other.Stack[i]);
            if (m.Error != null) return MergeResult.Fail($"stack[{i}]: {m.Error}");
            Stack[i] = m.Merged;
            if (m.Candidates != null) candidates.AddRange(m.Candidates);
        }
        return candidates.Count > 0 ? MergeResult.Ambiguous(this.Locals[0], candidates) : MergeResult.Ok(Locals.Length > 0 ? Locals[0] : VType.Top);
    }

    public override string ToString() =>
        $"locals=[{string.Join(", ", Locals.Select(t => t.ToString()))}] stack=[{string.Join(", ", Stack.Select(t => t.ToString()))}]";
}

/// <summary>Slot-level difference between two frames (computed vs declared).</summary>
public sealed record FrameDiff(int Offset, string Where, int Slot, string Computed, string Declared);

public static class Descriptors
{
    /// <summary>Parses a field descriptor starting at <paramref name="i"/>.</summary>
    public static VType ParseField(string d, ref int i)
    {
        switch (d[i])
        {
            case 'B' or 'C' or 'I' or 'S' or 'Z': i++; return VType.Int;
            case 'F': i++; return VType.Float;
            case 'J': i++; return VType.Long;
            case 'D': i++; return VType.Double;
            case 'L':
                var semi = d.IndexOf(';', i);
                var name = d[(i + 1)..semi];
                i = semi + 1;
                return VType.ObjectOf(name);
            case '[':
                i++;
                ParseField(d, ref i);
                return VType.ObjectOf("java/lang/Object"); // arrays collapse to reference
            default: throw new FormatException($"bad descriptor '{d}' at {i}");
        }
    }

    public static (List<VType> Params, VType? Return) ParseMethod(string d)
    {
        if (d[0] != '(') throw new FormatException($"bad method descriptor '{d}'");
        var i = 1;
        var ps = new List<VType>();
        while (d[i] != ')') ps.Add(ParseField(d, ref i));
        i++;
        var ret = d[i] == 'V' ? (VType?)null : ParseField(d, ref i);
        return (ps, ret);
    }

    /// <summary>Expands params into local slots; category-2 values occupy two slots
    /// (unit: local-variable slot, coordinate base = classfile local index).</summary>
    public static VType[] ToLocalSlots(IEnumerable<VType> types, int extra = 0)
    {
        var list = new List<VType>();
        foreach (var t in types)
        {
            list.Add(t);
            if (t.Kind == VKind.Long) list.Add(VType.Long2);
            else if (t.Kind == VKind.Double) list.Add(VType.Double2);
        }
        while (list.Count < extra) list.Add(VType.Top);
        return list.ToArray();
    }
}
