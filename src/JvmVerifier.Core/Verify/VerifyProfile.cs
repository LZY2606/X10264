namespace JvmVerifier.Core.Verify;

/// <summary>
/// Version-specific verification semantics. The coordinate is the classfile
/// major version; legacy pre-StakMap behaviour is selected for &lt;=50.
/// Rules (JVMS 4.10.1 / 4.10.2.1):
///  - major &lt; 50: legacy inference verifier; StackMapTable absent/incomplete is
///    tolerated for reachable targets; jsr/ret allowed.
///  - major == 50: split verifier; present StackMapTable is honored strictly,
///    otherwise legacy inference; jsr/ret still permitted.
///  - major &gt;= 51: type-checking verifier; every reachable branch target MUST be
///    certified by a StackMapTable frame (missing frames are never reconstructed
///    from later frames); jsr/ret forbidden.
/// </summary>
public sealed record VerifyProfile(int MajorVersion)
{
    public bool IsLegacy => MajorVersion < 50;
    public bool IsSplit => MajorVersion == 50;
    public bool IsTypeChecking => MajorVersion >= 51;

    /// <summary>Reachable target without a declared frame is an error.</summary>
    public bool MissingFrameIsError(bool frameAttributePresent) =>
        IsTypeChecking || (IsSplit && frameAttributePresent);

    /// <summary>jsr/ret subroutines are only legal in legacy/split classes.</summary>
    public bool JsrRetAllowed => !IsTypeChecking;

    /// <summary>Handler frame must exactly match declared stack map on 50+.</summary>
    public bool ExceptionFrameStrict => MajorVersion >= 50;

    public static readonly VerifyProfile Legacy = new(49);
    public static readonly VerifyProfile Split = new(50);
    public static readonly VerifyProfile Modern = new(52);
}
