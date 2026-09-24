namespace JvmVerify.Core;

/// <summary>
/// A byte offset that always carries its coordinate basis. Raw classfile
/// offsets ("classfile" coordinate) are never silently converted to code-array
/// offsets ("code" coordinate); conversion happens only in the derived layer.
/// </summary>
public readonly record struct ByteOffset(long Value, string Coordinate)
{
    public static ByteOffset ClassFile(long v) => new(v, "classfile");
    public static ByteOffset Code(long v) => new(v, "code");
    public override string ToString() => $"{Value}@{Coordinate}";
}

/// <summary>A classfile version, kept as raw major/minor plus derived semantics.</summary>
public readonly record struct ClassFileVersion(int Major, int Minor)
{
    public VerificationSemantics Semantics =>
        Major >= 50 ? VerificationSemantics.StackMapDriven : VerificationSemantics.LegacyInference;
    public override string ToString() => $"{Major}.{Minor}";
}

public enum VerificationSemantics
{
    /// <summary>classfile version &lt; 50: full dataflow type inference, no StackMapTable required.</summary>
    LegacyInference,
    /// <summary>classfile version &gt;= 50: StackMapTable-driven verification; missing frames are errors,
    /// never back-filled from later frames.</summary>
    StackMapDriven,
}

/// <summary>A diagnostic attached to a value that could not be converted into the
/// derived/normalized layer. The raw value stays in place.</summary>
public sealed record Diagnostic(string Code, string Message, string RawValue, string Coordinate);
