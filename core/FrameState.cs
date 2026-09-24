namespace JvmVerify.Core;

/// <summary>Type state at a program point: locals (fixed slots, category-2 values
/// occupy two slots with the second marked Top) and the operand stack.</summary>
public sealed class FrameState
{
    public VType[] Locals;
    public List<VType> Stack;

    public FrameState(VType[] locals, List<VType> stack) { Locals = locals; Stack = stack; }

    public FrameState Clone() => new((VType[])Locals.Clone(), new List<VType>(Stack));

    public string FormatLocals() => "[" + string.Join(", ", Locals.Select(t => t.ToString())) + "]";
    public string FormatStack() => "[" + string.Join(", ", Stack.Select(t => t.ToString())) + "]";
    public override string ToString() => $"locals={FormatLocals()} stack={FormatStack()}";

    public bool SameShape(FrameState other) =>
        Locals.Length == other.Locals.Length && Stack.Count == other.Stack.Count;

    public static bool StateEquals(FrameState a, FrameState b) =>
        a.Locals.SequenceEqual(b.Locals) && a.Stack.SequenceEqual(b.Stack);
}
