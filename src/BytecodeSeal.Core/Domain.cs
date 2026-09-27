using System.Text.Json.Serialization;

namespace BytecodeSeal.Core;

public sealed record ClassFileModel(
    string ClassId,
    string OriginalBase64,
    int MinorVersion,
    int MajorVersion,
    string ThisClassName,
    string SuperClassName,
    IReadOnlyList<ConstantPoolEntry> ConstantPool,
    IReadOnlyList<MethodModel> Methods,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public string VersionTag => $"major={MajorVersion},minor={MinorVersion}";
}

public sealed record ConstantPoolEntry(
    int Index,
    int Tag,
    string TagName,
    string RawRepresentation,
    string ResolvedText,
    IReadOnlyList<int> References);

public sealed record MethodModel(
    string MethodId,
    string Name,
    string Descriptor,
    int AccessFlags,
    CodeModel? Code);

public sealed record CodeModel(
    int MaxStack,
    int MaxLocals,
    IReadOnlyList<InstructionModel> Instructions,
    IReadOnlyList<ExceptionRange> ExceptionRanges,
    IReadOnlyList<StackMapFrame> StackMapFrames,
    IReadOnlyList<RawAttribute> RawAttributes);

public sealed record InstructionModel(
    int Offset,
    int Opcode,
    string Mnemonic,
    int Length,
    string RawBase64,
    string OperandText,
    int? ConstantPoolIndex,
    IReadOnlyList<int> RawBranchTargets)
{
    public string StableInstructionId => $"inst@{Offset}";
}

public sealed record ExceptionRange(
    int StartPc,
    int EndPc,
    int HandlerPc,
    int CatchTypeCpIndex,
    string CatchTypeName,
    string RawRepresentation);

public sealed record StackMapFrame(
    int FrameIndex,
    int Offset,
    int RawFrameType,
    string FrameKind,
    int OffsetDelta,
    IReadOnlyList<VerificationType> Locals,
    IReadOnlyList<VerificationType> Stack,
    string RawBase64,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record VerificationType(
    string Kind,
    string Name,
    int? NewInstructionOffset,
    string RawRepresentation)
{
    public int Category => Kind is "long" or "double" ? 2 : 1;
    public bool IsCategory2 => Category == 2;
    public bool IsUninitialized => Kind is "uninitializedThis" or "uninitialized";
}

public sealed record RawAttribute(string Name, int AttributeNameIndex, string RawBase64);

public sealed record Diagnostic(
    string Code,
    string Severity,
    int? Offset,
    string Message,
    string Evidence)
{
    public bool IsFatal => Severity is "error" or "fatal";
}

public sealed record CasePackage(
    string PackageId,
    string Title,
    string Narrative,
    string ClassBase64,
    IReadOnlyList<CaseMutation> Mutations,
    IReadOnlyList<ManualDecision> Decisions,
    IReadOnlyList<string> RequiredSituations)
{
    public const string DomainName = "字节码验章：JVM 栈映帧、异常边与指令状态证明";
}

public sealed record CaseMutation(
    string MutationId,
    string Kind,
    string Description,
    int? MethodOffset,
    int? InstructionOffset,
    string Parameter,
    string OriginalValue,
    string NewValue,
    string DirectSourceId);

public sealed record ManualDecision(
    string DecisionId,
    string Situation,
    string ChosenCandidateId,
    IReadOnlyList<string> RetainedCandidateIds,
    string Rationale);

public sealed record ReviewEvent(
    long Sequence,
    string EventId,
    string Kind,
    string At,
    string DirectSourceId,
    string RuleVersion,
    string Summary,
    string Before,
    string After,
    IReadOnlyList<string> Evidence,
    string? AcceptedCandidateId,
    IReadOnlyList<string> RetainedCandidateIds,
    string DerivationId)
{
    public bool IsDecision => Kind.StartsWith("decision.", StringComparison.Ordinal);
}

public sealed record DerivationNode(
    string NodeId,
    string SubjectId,
    string Kind,
    string InputHash,
    string RuleVersion,
    IReadOnlyList<string> DependsOn,
    string Status,
    string ReasonValidOrStale,
    string ResultJson,
    string ResultHash);

public sealed record SnapshotView(
    string EventId,
    string DerivationId,
    string RuleVersion,
    IReadOnlyList<ReviewEvent> Events,
    VerificationReport Report);

public sealed record VerificationReport(
    string ReportId,
    string MethodId,
    int ClassMajorVersion,
    string RuleVersion,
    bool Success,
    IReadOnlyList<VerificationState> States,
    IReadOnlyList<CfgEdge> Edges,
    IReadOnlyList<FrameDifference> FrameDifferences,
    IReadOnlyList<FailurePathItem> EarliestFailurePath,
    IReadOnlyList<VerificationCandidate> Candidates,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool HasAmbiguity => Candidates.Any(c => c.Status == "legal-ambiguous");
}

public sealed record VerificationState(
    int Offset,
    string Origin,
    IReadOnlyList<VerificationType> Locals,
    IReadOnlyList<VerificationType> Stack,
    string StableStateId)
{
    public int StackWords => Stack.Sum(x => x.Category);
}

public sealed record CfgEdge(
    string EdgeId,
    int From,
    int To,
    string Kind,
    VerificationState State,
    IReadOnlyList<string> Evidence)
{
    public string StableEdgeId => EdgeId;
}

public sealed record FrameDifference(
    string DifferenceId,
    int Offset,
    string ExpectedFrom,
    string ActualFrom,
    string Slot,
    string Expected,
    string Actual,
    string Severity);

public sealed record FailurePathItem(
    int Order,
    int Offset,
    string EdgeId,
    string EventCode,
    string Message,
    IReadOnlyList<string> Evidence);

public sealed record VerificationCandidate(
    string CandidateId,
    int Offset,
    string Situation,
    string Description,
    string Status,
    IReadOnlyList<VerificationType> AlternativeLocals,
    IReadOnlyList<VerificationType> AlternativeStack);

public sealed record ConflictInfo(string CurrentVersion, string SubmittedBaseVersion, string Message)
{
    public bool IsConflict => CurrentVersion != SubmittedBaseVersion;
}
