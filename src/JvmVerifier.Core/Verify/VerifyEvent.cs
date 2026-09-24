namespace JvmVerifier.Core.Verify;

public enum Severity { Info, Warning, Failure }

public enum VerifyEventCode
{
    ImportDiagnostic,
    InstructionDecode,
    UninitializedUse,
    UninitializedJoin,
    WideStackMismatch,
    ExceptionEdge,
    ExceptionFrameMismatch,
    MissingFrame,
    CorruptFrameOffset,
    UnreachableBlock,
    LegacyInference,
    JsrRetVersion,
    JoinConflict,
    JoinAmbiguity,
    TypeMismatch,
    StackUnderflow,
    LocalOutOfRange,
    BranchOutOfRange,
    MaxStackExceeded,
    ManualDecision,
    CandidateRetained
}

/// <summary>
/// Machine-readable verification event. Carries a stable id, the exact coordinate
/// (bci / block / edge / frame ordinal) and provenance describing which input
/// fragment and which rule produced it.
/// </summary>
public sealed class VerifyEvent
{
    public required string EventId { get; init; }
    public required VerifyEventCode Code { get; init; }
    public required Severity Severity { get; init; }
    public required string Message { get; init; }
    public int? Bci { get; init; }
    public int? BlockIndex { get; init; }
    public string? EdgeId { get; init; }
    public int? FrameOrdinal { get; init; }
    public int? ExceptionOrdinal { get; init; }
    /// <summary>Direct input fragment responsible (e.g. "code", "stackmap[1].offset_delta").</summary>
    public string? SourceFragment { get; init; }
    /// <summary>Rule that produced the event (version-dependent).</summary>
    public string? Rule { get; init; }
    /// <summary>Ordered bci path from entry to the failing instruction.</summary>
    public IReadOnlyList<int>? EarliestPath { get; init; }
    /// <summary>All legal candidates when a join/decision is not uniquely determined.</summary>
    public IReadOnlyList<string>? Candidates { get; init; }
    /// <summary>Frame states participating in a difference (join frame vs declared).</summary>
    public FrameSnapshot? ExpectedFrame { get; init; }
    public FrameSnapshot? ActualFrame { get; init; }

    public bool IsFailure => Severity == Severity.Failure;
}
