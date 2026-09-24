namespace JvmVerifier.Core.Verify;

public enum EdgeKind
{
    Fallthrough,
    Branch,
    Switch,
    Exception,
    Unconditional
}

public sealed class FrameSnapshot
{
    public required IReadOnlyList<string> Locals { get; init; }
    public required IReadOnlyList<string> Stack { get; init; }
    public string Label => "[" + string.Join(", ", Locals) + "] | " + string.Join(", ", Stack);

    public static FrameSnapshot From(VFrame f) => new()
    {
        Locals = f.LocalTokens,
        Stack = f.StackTokens
    };

    public static FrameSnapshot FromTokens(IEnumerable<string> l, IEnumerable<string> s) =>
        new() { Locals = l.ToArray(), Stack = s.ToArray() };
}

public sealed class CfgEdge
{
    public required int FromBlock { get; init; }
    public required int ToBci { get; init; }
    public required EdgeKind Kind { get; init; }
    /// <summary>Catch type token for exception edges (null for catch-all); null otherwise.</summary>
    public string? CatchType { get; init; }
    public int? ExceptionOrdinal { get; init; }
    public FrameSnapshot? StateAtOrigin { get; set; }
    public required int Ordinal { get; init; }
    public string StableId => $"e{Ordinal}";
}

public sealed class BasicBlock
{
    public required int Index { get; init; }
    public required int Start { get; init; }
    public required int EndExclusive { get; init; }
    public required List<CfgEdge> OutEdges { get; init; } = new();
    public required List<CfgEdge> InEdges { get; init; } = new();
    public bool Reachable { get; set; }
    public string StableId => $"bb{Index}@{Start}";
}

public sealed class Cfg
{
    public required List<BasicBlock> Blocks { get; init; }
    public required List<CfgEdge> Edges { get; init; }
    public BasicBlock? BlockAt(int bci) => Blocks.FirstOrDefault(b => b.Start == bci);
    public BasicBlock BlockContaining(int bci) =>
        Blocks.First(b => bci >= b.Start && bci < b.EndExclusive);
}
