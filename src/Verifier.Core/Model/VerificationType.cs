namespace Verifier.Core.Model;

/// <summary>
/// JVM verification types (JVMS §4.10.1.2). Category-2 types (long/double)
/// occupy two local-variable slots; the second half is represented by
/// <see cref="SecondHalfType"/> so wide-value violations are detectable.
/// </summary>
public abstract record VerificationType
{
    public static readonly TopType Top = new();
    public static readonly IntType Int = new();
    public static readonly FloatType Float = new();
    public static readonly LongType Long = new();
    public static readonly DoubleType Double = new();
    public static readonly NullType Null = new();
    public static readonly SecondHalfType SecondHalf = new();
    public static readonly UninitializedThisType UninitializedThis = new();

    /// <summary>Unusable slot (Top in JVMS terms).</summary>
    public sealed record TopType : VerificationType;
    public sealed record IntType : VerificationType;
    public sealed record FloatType : VerificationType;
    public sealed record LongType : VerificationType;
    public sealed record DoubleType : VerificationType;
    public sealed record NullType : VerificationType;

    /// <summary>Second slot of a category-2 value.</summary>
    public sealed record SecondHalfType : VerificationType;

    public sealed record RefType(string ClassName) : VerificationType;
    public sealed record UninitializedThisType : VerificationType;
    public sealed record UninitializedType(int NewOffset) : VerificationType;

    public int Category =>
        this is LongType or DoubleType ? 2 : 1;

    public bool IsReference => this is RefType or NullType;

    public string Display => this switch
    {
        TopType => "top(unusable)",
        IntType => "int",
        FloatType => "float",
        LongType => "long",
        DoubleType => "double",
        NullType => "null",
        SecondHalfType => "«2nd-half»",
        RefType r => $"ref:{r.ClassName}",
        UninitializedThisType => "uninitializedThis",
        UninitializedType u => $"uninitialized@{u.NewOffset}",
        _ => "?"
    };

    public static VerificationType Parse(string s)
    {
        if (s.StartsWith("ref:", StringComparison.Ordinal)) return new RefType(s[4..]);
        if (s.StartsWith("uninitialized@", StringComparison.Ordinal)) return new UninitializedType(int.Parse(s[14..]));
        return s switch
        {
            "top(unusable)" => Top,
            "int" => Int,
            "float" => Float,
            "long" => Long,
            "double" => Double,
            "null" => Null,
            "«2nd-half»" => SecondHalf,
            "uninitializedThis" => UninitializedThis,
            _ => throw new FormatException($"unknown verification type: {s}")
        };
    }
}
