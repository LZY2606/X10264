namespace JvmVerifier.Domain;

public abstract record VerifierType(string Kind)
{
    public abstract bool Category2 { get; }
    public virtual int Slots => Category2 ? 2 : 1;
}
public sealed record TopType() : VerifierType("top")
{
    public override bool Category2 => false;
}
public sealed record IntegerType() : VerifierType("integer")
{
    public override bool Category2 => false;
}
public sealed record FloatType() : VerifierType("float")
{
    public override bool Category2 => false;
}
public sealed record LongType() : VerifierType("long")
{
    public override bool Category2 => true;
}
public sealed record DoubleType() : VerifierType("double")
{
    public override bool Category2 => true;
}
public sealed record NullReferenceType() : VerifierType("null")
{
    public override bool Category2 => false;
}
public sealed record ObjectType(string Name) : VerifierType("object")
{
    public override bool Category2 => false;
}
public sealed record UninitializedThisType() : VerifierType("uninitializedThis")
{
    public override bool Category2 => false;
}
public sealed record UninitializedObjectType(int NewOffset) : VerifierType("uninitializedObject")
{
    public override bool Category2 => false;
}

public sealed record FrameState(
    IReadOnlyList<VerifierType?> Locals,
    IReadOnlyList<VerifierType> Stack,
    IReadOnlyList<string> EvidenceIds,
    string? CandidateId = null)
{
    public int SlotCount => Locals.Count;
    public int ValueCount => Stack.Count;
}

public sealed record FlowEdge(
    string EdgeId,
    int FromOffset,
    int ToOffset,
    string Kind,
    FrameState? Before,
    FrameState? After,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<FrameState> Candidates);

public sealed record BasicBlock(int StartOffset, int EndOffset, IReadOnlyList<int> Successors, IReadOnlyList<int> ExceptionHandlers)
{
    public string BlockId => $"block@{StartOffset}";
}

public sealed record FrameDifference(
    string FrameId,
    int Offset,
    FrameState? Inferred,
    FrameState? Declared,
    IReadOnlyList<string> LocalDiffs,
    IReadOnlyList<string> StackDiffs,
    IReadOnlyList<string> EvidenceIds);

public sealed record VerifierEvent(
    string EventId,
    string Kind,
    string RuleId,
    int? Offset,
    string Severity,
    string Message,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyDictionary<string, object?> Facts,
    string AtEventId);

public sealed record VerificationFailure(
    string FailureId,
    string RuleId,
    int? Offset,
    string EdgeId,
    string Message,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<VerifierEvent> Events,
    IReadOnlyList<FrameState> Candidates,
    IReadOnlyList<int> EarliestPath);

public sealed record DependencyNode(string NodeId, string Kind, string EvidenceId, int Offset, string Hash);
public sealed record DependencyEdge(string FromNodeId, string ToNodeId, string Reason);
public sealed record DependencyGraph(IReadOnlyList<DependencyNode> Nodes, IReadOnlyList<DependencyEdge> Edges);

public sealed record VerificationResult(
    string MethodId,
    int MajorVersion,
    int MinorVersion,
    bool Success,
    IReadOnlyList<BasicBlock> Blocks,
    IReadOnlyList<FlowEdge> Edges,
    IReadOnlyList<FrameDifference> FrameDifferences,
    IReadOnlyList<VerificationFailure> Failures,
    IReadOnlyList<VerifierEvent> Events,
    DependencyGraph DependencyGraph,
    IReadOnlyDictionary<string, string> RecomputeReasons);
