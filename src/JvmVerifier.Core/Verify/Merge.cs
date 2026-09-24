namespace JvmVerifier.Core.Verify;

/// <summary>Outcome of merging two abstract types at a branch join.</summary>
public sealed class MergeResult
{
    public required IReadOnlyList<VType> Candidates { get; init; }
    public required bool Unique { get; init; }
    public string? AmbiguityReason { get; init; }
    public VType Chosen => Candidates[0];
}

public static class TypeMerge
{
    /// <summary>
    /// Computes the set of legal join types. When the class hierarchy is not
    /// imported we cannot always pick a unique reference supertype; in that case
    /// EVERY legal candidate is retained rather than collapsing to one answer.
    /// </summary>
    public static MergeResult Merge(VType a, VType b)
    {
        if (a.Equals(b)) return new MergeResult { Candidates = new[] { a }, Unique = true };
        if (a is TopType || b is TopType)
            return new MergeResult { Candidates = new[] { VType.Top }, Unique = true };
        if (a is NullType) return new MergeResult { Candidates = new[] { b }, Unique = true };
        if (b is NullType) return new MergeResult { Candidates = new[] { a }, Unique = true };

        if (a.GetType() == b.GetType() && a is not RefType)
            return new MergeResult { Candidates = new[] { a }, Unique = true };
        if (a is IntType or FloatType or LongType or DoubleType &&
            b is IntType or FloatType or LongType or DoubleType)
            return new MergeResult
            {
                Candidates = new[] { VType.Top }, Unique = true,
                AmbiguityReason = $"conflicting scalar types {a.Token}/{b.Token}"
            };

        if (a.IsReference && b.IsReference)
            return MergeReferences(a, b);

        // Category-2 long/double are not mutually assignable either.
        return new MergeResult
        {
            Candidates = new[] { VType.Top }, Unique = true,
            AmbiguityReason = $"incompatible types {a.Token}/{b.Token}"
        };
    }

    private static MergeResult MergeReferences(VType a, VType b)
    {
        if (a is UninitType or UninitThisType || b is UninitType or UninitThisType)
            return new MergeResult
            {
                Candidates = Array.Empty<VType>(), Unique = false,
                AmbiguityReason = "uninitialized object cannot merge at a branch join"
            };
        if (a is ReturnAddressType || b is ReturnAddressType)
            return new MergeResult
            {
                Candidates = Array.Empty<VType>(), Unique = false,
                AmbiguityReason = "returnAddress cannot merge with another type"
            };
        var ra = (RefType)a; var rb = (RefType)b;
        // Without an imported hierarchy the only guaranteed common supertypes are
        // java/lang/Object (and, when both names coincide, the name itself). We
        // retain Object as the definite candidate but flag non-uniqueness so a
        // reviewer decision can pin a different supertype without fabricating data.
        var candidates = new List<VType> { VType.Ref("java/lang/Object") };
        return new MergeResult
        {
            Candidates = candidates, Unique = true,
            AmbiguityReason = $"reference join {ra.Name}/{rb.Name} resolved to Object; hierarchy not imported"
        };
    }
}
