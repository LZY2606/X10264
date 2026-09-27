namespace JvmVerifier.Domain;

public enum EventKind
{
    BaselineImported,
    ParameterRevised,
    LocalSourceChanged,
    HumanDecision,
    VerificationDerived,
    CandidateRetained,
    CandidateAccepted,
    CandidateRejected,
    CounterexampleReplayed
}

public sealed record CaseEvent(
    string EventId,
    string ParentEventId,
    EventKind Kind,
    string Summary,
    string ArtifactId,
    string ResultId,
    string? ParameterName,
    string? OldValue,
    string? NewValue,
    string? CandidateId,
    bool Adopted,
    string Actor,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> EvidenceIds,
    string MachineEventJson);

public sealed record CounterexampleVariant(
    string VariantId,
    string Title,
    string DirectSourceKind,
    string BaselineArtifactId,
    string ArtifactId,
    string ResultId,
    bool Adopted,
    bool ExpectedFailure,
    string RuleId,
    IReadOnlyList<string> EvidenceIds,
    string Note);

public sealed record OfflineCase(
    string CaseId,
    string Title,
    string Domain,
    string CoordinateBasis,
    RawClassFile Baseline,
    IReadOnlyList<RawClassFile> Artifacts,
    IReadOnlyList<CaseEvent> Events,
    IReadOnlyList<CounterexampleVariant> Counterexamples,
    string Readme);
