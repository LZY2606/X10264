namespace JvmVerify.Core;

/// <summary>A machine-readable verification event. Rule ids are stable so that
/// downstream tooling can pattern-match: JVM-UNINIT-USE, JVM-UNINIT-CTOR,
/// JVM-CAT2-SLOT, JVM-EXC-ENTRY, JVM-JOIN-CONFLICT, JVM-JOIN-MERGE,
/// JVM-MISSING-FRAME, JVM-FRAME-OFFSET, JVM-STACK-*, JVM-UNREACHABLE, ...</summary>
public sealed record Finding(
    string Rule,
    ByteOffset Offset,
    string Message,
    string Severity, // "error" | "warning" | "info"
    IReadOnlyDictionary<string, string> Evidence);

public sealed record MergeEvent(
    ByteOffset Offset,
    string LeftType,
    string RightType,
    IReadOnlyList<string> Candidates,
    string ChosenRule);

public sealed record FrameStateSnapshot(string[] Locals, string[] Stack)
{
    public static FrameStateSnapshot Of(FrameState s) =>
        new(s.Locals.Select(t => t.ToString()).ToArray(),
            s.Stack.Select(t => t.ToString()).ToArray());
}

public sealed record EdgeState(
    int FromOffset, int ToOffset, string EdgeKind, FrameStateSnapshot State);

public sealed record FrameDiff(int FrameOffset, IReadOnlyList<string> Differences);

public sealed record MethodVerification(
    string MethodName,
    string RawDescriptor,
    VerificationSemantics Semantics,
    IReadOnlyList<Finding> Findings,
    IReadOnlyDictionary<int, FrameStateSnapshot> IncomingStates,
    IReadOnlyList<EdgeState> Edges,
    IReadOnlyList<FrameDiff> FrameDiffs,
    IReadOnlyList<int> EarliestFailurePath,
    IReadOnlyList<int> UnreachableOffsets,
    IReadOnlyList<MergeEvent> MergeEvents)
{
    public bool HasErrors => Findings.Any(f => f.Severity == "error");
}
