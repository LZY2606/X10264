using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public sealed class VerificationResult
{
    public required bool Success { get; init; }
    public required IReadOnlyList<VerifyEvent> Events { get; init; }
    public required Cfg Cfg { get; init; }
    /// <summary>State entering each block after join (local/stack), keyed by block start.</summary>
    public required IReadOnlyDictionary<int, FrameSnapshot> BlockInputs { get; init; }
    /// <summary>State on each edge at its origin.</summary>
    public required IReadOnlyDictionary<string, FrameSnapshot> EdgeStates { get; init; }
    /// <summary>Declared frame vs computed frame at every StackMapTable offset.</summary>
    public required IReadOnlyList<FrameDiff> FrameDiffs { get; init; }
    public required IReadOnlyList<int> EarliestFailurePath { get; init; }
    public VerifyEvent? FirstFailure =>
        Events.Where(e => e.IsFailure).OrderBy(e => e.EarliestPath?.Count ?? int.MaxValue)
              .ThenBy(e => e.Bci ?? int.MaxValue).FirstOrDefault();
}

public sealed class FrameDiff
{
    public required int FrameOrdinal { get; init; }
    public required int Bci { get; init; }
    public required FrameSnapshot Declared { get; init; }
    public required FrameSnapshot Computed { get; init; }
    public required bool Matches { get; init; }
}
