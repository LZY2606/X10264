namespace JvmVerifier.Core.Raw;

public enum VerificationTypeInfoTag
{
    Top = 0,
    Integer = 1,
    Float = 2,
    Double = 3,
    Long = 4,
    Null = 5,
    UninitializedThis = 6,
    Object = 7,
    Uninitialized = 8,
}

/// <summary>One verification_type_info, verbatim from StackMapTable.</summary>
public sealed class RawVerificationTypeInfo
{
    public required VerificationTypeInfoTag Tag { get; set; }
    public int? CpoolIndex { get; set; }
    public Coords? Offset { get; set; }
    public required string Id { get; init; }
}

/// <summary>
/// One stack_map_frame. <see cref="FrameType"/> and <see cref="RawOffsetDelta"/>
/// preserve the original encoding; <see cref="AbsolutePc"/> is the decoded byte
/// offset. Corrupted-offset exercises mutate OffsetDelta while leaving the
/// declared frame order untouched.
/// </summary>
public sealed class RawStackMapFrame
{
    public required int Index { get; init; }
    public required int FrameType { get; init; }
    public required int RawOffsetDelta { get; set; }
    public required int AbsolutePc { get; set; }
    public List<RawVerificationTypeInfo> Locals { get; init; } = new();
    public List<RawVerificationTypeInfo> Stack { get; init; } = new();
    public required string Id { get; init; }
}
