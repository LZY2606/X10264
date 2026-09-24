namespace JvmVerifier.Core;

/// <summary>
/// Verification-time type. Category-2 values (Long/Double) occupy two slots,
/// the second being <see cref="VTop"/> (the "top half" marker).
/// A slot may hold several candidates when a join is ambiguous; we never
/// collapse ambiguity silently.
/// </summary>
public abstract record VType
{
    public sealed record VTop() : VType;             // unusable / second half of category-2
    public sealed record VNull() : VType;
    public sealed record VInt() : VType;
    public sealed record VFloat() : VType;
    public sealed record VLong() : VType;
    public sealed record VDouble() : VType;
    public sealed record VObject(string ClassName) : VType;
    public sealed record VUninitializedThis() : VType;
    public sealed record VUninitialized(int NewOffset) : VType;
    /// <summary>Ambiguous slot: several legal candidates retained.</summary>
    public sealed record VConflict(IReadOnlyList<VType> Candidates) : VType;

    public bool IsCategory2 => this is VLong or VDouble;

    public string Display() => this switch
    {
        VTop => "top",
        VNull => "null",
        VInt => "int",
        VFloat => "float",
        VLong => "long",
        VDouble => "double",
        VObject o => $"ref:{o.ClassName}",
        VUninitializedThis => "uninitThis",
        VUninitialized u => $"uninit@{u.NewOffset}",
        VConflict c => "conflict[" + string.Join("|", c.Candidates.Select(x => x.Display())) + "]",
        _ => "?",
    };
}

public static class VTypes
{
    public static readonly VType.VTop Top = new();
    public static readonly VType.VNull Null = new();
    public static readonly VType.VInt Int = new();
    public static readonly VType.VFloat Float = new();
    public static readonly VType.VLong Long = new();
    public static readonly VType.VDouble Double = new();
    public static readonly VType.VUninitializedThis UninitializedThis = new();
    public static VType.VObject Obj(string name) => new(name);

    public static bool IsAssignable(VType from, VType to)
    {
        if (from.Equals(to)) return true;
        return (from, to) switch
        {
            (VType.VNull, VType.VObject) => true,
            (VType.VObject a, VType.VObject b) => b.ClassName == "java/lang/Object" || a.ClassName == b.ClassName,
            (VType.VConflict c, _) => c.Candidates.All(x => IsAssignable(x, to)),
            (_, VType.VConflict c) => c.Candidates.Any(x => IsAssignable(from, x)),
            _ => false,
        };
    }

    /// <summary>
    /// Merge two types at a control-flow join. Returns null when the merge is
    /// a hard conflict; the caller records a VConflict with both candidates so
    /// the ambiguity is preserved rather than swallowed.
    /// </summary>
    public static VType Merge(VType a, VType b)
    {
        if (a.Equals(b)) return a;
        return (a, b) switch
        {
            (VType.VNull, VType.VObject) => b,
            (VType.VObject, VType.VNull) => a,
            (VType.VObject x, VType.VObject y) =>
                x.ClassName == y.ClassName ? x : Obj("java/lang/Object"),
            (VType.VConflict c, _) when c.Candidates.Any(x => x.Equals(b)) => a,
            (_, VType.VConflict c) when c.Candidates.Any(x => x.Equals(a)) => b,
            _ => new VType.VConflict([a, b]),
        };
    }

    public static bool IsConflict(VType t) => t is VType.VConflict;

    public static VType FromInfo(VerificationTypeInfo info, ClassFileModel cf) => info switch
    {
        VerificationTypeInfo.Top => Top,
        VerificationTypeInfo.Int => Int,
        VerificationTypeInfo.Float => Float,
        VerificationTypeInfo.Long => Long,
        VerificationTypeInfo.Double => Double,
        VerificationTypeInfo.Null => Null,
        VerificationTypeInfo.UninitializedThis => UninitializedThis,
        VerificationTypeInfo.Object o => Obj(cf.CpClassName(o.CpIndex)),
        VerificationTypeInfo.Uninitialized u => new VType.VUninitialized(u.Offset),
        _ => throw new InvalidOperationException(),
    };
}
