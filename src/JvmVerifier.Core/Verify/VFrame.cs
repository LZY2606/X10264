namespace JvmVerifier.Core.Verify;

/// <summary>
/// Local variable array + operand stack over the abstract domain. Category-2
/// values physically occupy two slots; the companion slot is TOP. Stack stores
/// logical values (the verifier handles popping category 2 correctly).
/// </summary>
public sealed class VFrame
{
    public VType[] Locals { get; }
    public List<VType> Stack { get; }

    public VFrame(int localCount)
    {
        Locals = new VType[localCount];
        for (int i = 0; i < localCount; i++) Locals[i] = VType.Top;
        Stack = new List<VType>();
    }

    public VFrame(VType[] locals, IEnumerable<VType> stack)
    {
        Locals = (VType[])locals.Clone();
        Stack = stack.ToList();
    }

    public int StackDepth => Stack.Sum(v => v.Slots);
    public int LogicalDepth => Stack.Count;

    public VFrame Clone()
    {
        var f = new VFrame(Locals.Length);
        Array.Copy(Locals, f.Locals, Locals.Length);
        f.Stack.AddRange(Stack);
        return f;
    }

    public void Push(VType v) => Stack.Add(v);

    public VType Pop()
    {
        int n = Stack.Count;
        if (n == 0) throw new StackUnderflowException();
        var v = Stack[n - 1];
        Stack.RemoveAt(n - 1);
        return v;
    }

    public void SetLocal(int index, VType v)
    {
        if (index < 0 || index >= Locals.Length)
            throw new LocalAccessException(index, Locals.Length);
        Locals[index] = v;
        if (v.IsCategory2)
        {
            if (index + 1 >= Locals.Length)
                throw new LocalAccessException(index + 1, Locals.Length);
            Locals[index + 1] = VType.Top;
        }
    }

    public VType GetLocal(int index)
    {
        if (index < 0 || index >= Locals.Length)
            throw new LocalAccessException(index, Locals.Length);
        return Locals[index];
    }

    public string[] StackTokens => Stack.Select(v => v.Token).ToArray();
    public string[] LocalTokens => Locals.Select(v => v.Token).ToArray();
}

public sealed class StackUnderflowException : Exception { public StackUnderflowException() : base("operand stack underflow") { } }
public sealed class LocalAccessException : Exception
{
    public int Index { get; }
    public LocalAccessException(int index, int size) : base($"local slot {index} out of range [0,{size})") => Index = index;
}
