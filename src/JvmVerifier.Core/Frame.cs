namespace JvmVerifier.Core;

// 一个程序点的抽象状态。Locals 按槽位展开（category-2 后随 TopHalf），Stack 同理。
public sealed class FrameState : IEquatable<FrameState>
{
    public int Pc { get; init; }
    public VeriType?[] Locals { get; init; } = Array.Empty<VeriType?>();
    public List<VeriType> Stack { get; init; } = new();
    public string Origin { get; init; } = "computed"; // computed | stackmap | exception-entry | initial
    public int? SourceFrameIndex { get; init; }       // 来自哪条原始栈映帧（证据坐标）

    public FrameState Clone(int pc) => new()
    {
        Pc = pc,
        Locals = (VeriType?[])Locals.Clone(),
        Stack = new List<VeriType>(Stack),
        Origin = Origin,
        SourceFrameIndex = SourceFrameIndex
    };

    public void Push(VeriType t)
    {
        Stack.Add(t);
        if (t.IsCategory2) Stack.Add(VeriType.TopHalf);
    }

    public VeriType Pop()
    {
        if (Stack.Count == 0)
            throw new VerifyTypeException("stack underflow", Pc);
        var top = Stack[^1];
        Stack.RemoveAt(Stack.Count - 1);
        if (top.Kind == VeriType.KindCode.TopHalf)
        {
            if (Stack.Count == 0) throw new VerifyTypeException("malformed category-2 slot", Pc);
            top = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
        }
        return top;
    }

    public void SetLocal(int slot, VeriType t)
    {
        if (slot < 0 || slot >= Locals.Length)
            throw new VerifyTypeException($"local index {slot} out of range (maxLocals={Locals.Length})", Pc);
        Locals[slot] = t;
        if (t.IsCategory2)
        {
            if (slot + 1 >= Locals.Length)
                throw new VerifyTypeException($"category-2 value overruns maxLocals at slot {slot}", Pc);
            Locals[slot + 1] = VeriType.TopHalf;
        }
    }

    public VeriType? GetLocal(int slot)
    {
        if (slot < 0 || slot >= Locals.Length)
            throw new VerifyTypeException($"local index {slot} out of range", Pc);
        return Locals[slot];
    }

    public bool Equals(FrameState? other)
    {
        if (other is null) return false;
        if (Locals.Length != other.Locals.Length) return false;
        if (!Locals.SequenceEqual(other.Locals)) return false;
 return Stack.SequenceEqual(other.Stack);
    }

    public string StackText => string.Join(", ", Stack.Select(s => s.Display));
    public string LocalsText => "[" + string.Join(", ", Locals.Select((t, i) => $"{i}:{(t is null ? "<undef>" : t.Display)}")) + "]";
}

public sealed class VerifyTypeException : Exception
{
    public int Pc { get; }
    public VerifyTypeException(string message, int pc) : base(message) => Pc = pc;
}
