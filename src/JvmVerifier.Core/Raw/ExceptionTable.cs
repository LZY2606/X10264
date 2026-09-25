namespace JvmVerifier.Core.Raw;

/// <summary>
/// One exception_table entry, JVMS 4.7.3. Offsets are Code[] byte offsets.
/// CatchTypeCpIndex == 0 means finally/catch-any.
/// </summary>
public sealed class RawExceptionHandler
{
    public required Coords StartPc { get; set; }
    public required Coords EndPc { get; set; }
    public required Coords HandlerPc { get; set; }
    public int? CatchTypeCpIndex { get; set; }
    public required string Id { get; init; }
}
