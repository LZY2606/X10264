namespace BytecodeVerifier.Core.Model;

public enum EventLevel
{
    Evidence,
    Inference,
    Uncertainty,
    Diagnostic,
    Failure
}

public enum EventCode
{
    RawImported,
    UninitializedUse,
    UninitializedRewritten,
    Category2Misaligned,
    ExceptionEdgeEntered,
    ExceptionEdgeInvalid,
    UnreachableBlock,
    LegacyInference,
    LegacyAmbiguousCandidates,
    JoinConflict,
    StackMapAccepted,
    MissingFrame,
    CorruptFrameOffset,
    VersionRuleSelected,
    DependencyValid,
    DependencyRecomputed,
    DecisionAccepted,
    DecisionRejected,
    DecisionParameterChanged
}

/// <summary>Machine-readable judgment record. Every claim is anchored to coordinates.</summary>
public sealed record DomainEvent(
    string EventId,
    string DerivationId,
    int Seq,
    EventCode Code,
    EventLevel Level,
    string Message,
    string? RuleId,
    string? SourceKind,
    string? SourceId,
    int? ByteOffset,
    IReadOnlyList<string> CandidateIds,
    string? PayloadJson)
{
    public bool IsFailure => Level == EventLevel.Failure;
}
