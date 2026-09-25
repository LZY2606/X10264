using JvmVerifier.Core.Raw;

namespace JvmVerifier.Core.Types;

public enum VerifKind
{
    Top,
    Int,
    Float,
    Long,
    Double,
    Null,
    UninitializedThis,
    Object,
    Uninitialized,
}

/// <summary>
/// A JVMS 4.10.1 verification type. <see cref="NewPc"/> carries the byte offset
/// of the new instruction for Uninitialized(Offset) types, keeping the original
/// coordinate basis. Two uninitialized instances created at different offsets
/// are distinct types.
/// </summary>
public sealed record VerificationType(VerifKind Kind, string? ClassName = null, Coords? NewPc = null)
{
    public static readonly VerificationType Top = new(VerifKind.Top);
    public static readonly VerificationType Int = new(VerifKind.Int);
    public static readonly VerificationType Float = new(VerifKind.Float);
    public static readonly VerificationType Long = new(VerifKind.Long);
    public static readonly VerificationType Double = new(VerifKind.Double);
    public static readonly VerificationType Null = new(VerifKind.Null);
    public static readonly VerificationType UninitializedThis = new(VerifKind.UninitializedThis);
    public static VerificationType Obj(string internalName) => new(VerifKind.Object, internalName);
    public static VerificationType Uninit(Coords newPc) => new(VerifKind.Uninitialized, NewPc: newPc);

    /// <summary>Long/Double occupy two local/stack slots (category-2).</summary>
    public bool IsCategory2 => Kind is VerifKind.Long or VerifKind.Double;
    public bool IsReference => Kind is VerifKind.Null or VerifKind.Object or VerifKind.UninitializedThis or VerifKind.Uninitialized;
    public bool IsUninitialized => Kind is VerifKind.UninitializedThis or VerifKind.Uninitialized;

    public string StableId => Kind switch
    {
        VerifKind.Object => $"obj:{ClassName}",
        VerifKind.Uninitialized => $"uninit@pc{NewPc!.Value.Value}",
        _ => Kind.ToString().ToLowerInvariant(),
    };

    public string Render() => Kind switch
    {
        VerifKind.Object => ClassName!.Replace('/', '.'),
        VerifKind.Uninitialized => $"uninitialized(new@{NewPc!.Value.Value})",
        VerifKind.UninitializedThis => "uninitializedThis",
        _ => Kind.ToString().ToLowerInvariant(),
    };

    public override string ToString() => Render();

    /// <summary>Maps one raw verification_type_info into a verification type.</summary>
    public static VerificationType? FromRaw(RawVerificationTypeInfo raw, ConstantPool cp, List<RawDiagnostic>? diags = null)
    {
        switch (raw.Tag)
        {
            case VerificationTypeInfoTag.Top: return Top;
            case VerificationTypeInfoTag.Integer: return Int;
            case VerificationTypeInfoTag.Float: return Float;
            case VerificationTypeInfoTag.Double: return Double;
            case VerificationTypeInfoTag.Long: return Long;
            case VerificationTypeInfoTag.Null: return Null;
            case VerificationTypeInfoTag.UninitializedThis: return UninitializedThis;
            case VerificationTypeInfoTag.Object:
            {
                var name = cp.ClassName(raw.CpoolIndex!.Value, diags);
                return name is null ? null : Obj(name);
            }
            case VerificationTypeInfoTag.Uninitialized:
                return Uninit(raw.Offset!.Value);
            default: return null;
        }
    }
}
