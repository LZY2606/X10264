namespace JvmVerify.Core;

/// <summary>JVM verification types (JVMS §4.10.1.2). Original form is preserved;
/// normalization happens only in derived layers.</summary>
public enum VKind { Top, Null, Int, Float, Long, Long2, Double, Double2, Object, Uninit, UninitThis }

public readonly record struct VType(VKind Kind, string? ClassName = null, int NewOffset = -1)
{
    public static readonly VType Top = new(VKind.Top);
    public static readonly VType Null = new(VKind.Null);
    public static readonly VType Int = new(VKind.Int);
    public static readonly VType Float = new(VKind.Float);
    public static readonly VType Long = new(VKind.Long);
    public static readonly VType Long2 = new(VKind.Long2);
    public static readonly VType Double = new(VKind.Double);
    public static readonly VType Double2 = new(VKind.Double2);
    public static VType ObjectOf(string name) => new(VKind.Object, name);
    public static VType UninitAt(int offset, string className) => new(VKind.Uninit, className, offset);
    public static VType UninitThisOf(string className) => new(VKind.UninitThis, className);

    public bool IsCategory2 => Kind is VKind.Long or VKind.Double;
    public bool IsSecondHalf => Kind is VKind.Long2 or VKind.Double2;
    public bool IsUninitialized => Kind is VKind.Uninit or VKind.UninitThis;

    public bool IsAssignableTo(VType other) =>
        Equals(other) ||
        (Kind == VKind.Null && other.Kind == VKind.Object) ||
        (Kind == VKind.Object && other.ClassName == "java/lang/Object") ||
        (Kind == VKind.Object && other.Kind == VKind.Object && other.ClassName == "java/lang/Throwable") ||
        (Kind == VKind.Top);

    public override string ToString() => Kind switch
    {
        VKind.Top => "top",
        VKind.Null => "null",
        VKind.Int => "int",
        VKind.Float => "float",
        VKind.Long => "long",
        VKind.Long2 => "long(2nd)",
        VKind.Double => "double",
        VKind.Double2 => "double(2nd)",
        VKind.Object => $"ref:{ClassName}",
        VKind.Uninit => $"uninit:{ClassName}@{NewOffset}",
        VKind.UninitThis => $"uninitThis:{ClassName}",
        _ => "?"
    };
}

/// <summary>Result of merging two types at a control-flow join. When the merge
/// cannot be decided uniquely, all legal candidates are preserved instead of
/// collapsing to a single answer.</summary>
public sealed record MergeResult(VType Merged, string? Error, IReadOnlyList<VType>? Candidates)
{
    public static MergeResult Ok(VType t) => new(t, null, null);
    public static MergeResult Ambiguous(VType merged, IReadOnlyList<VType> candidates) => new(merged, null, candidates);
    public static MergeResult Fail(string error) => new(VType.Top, error, null);
}

public static class VTypes
{
    public static MergeResult Merge(VType a, VType b)
    {
        if (a.Equals(b)) return MergeResult.Ok(a);
        if (a.Kind == VKind.Top) return MergeResult.Ok(b);
        if (b.Kind == VKind.Top) return MergeResult.Ok(a);
        if (a.Kind == VKind.Null && b.Kind == VKind.Object) return MergeResult.Ok(b);
        if (b.Kind == VKind.Null && a.Kind == VKind.Object) return MergeResult.Ok(a);
        if (a.IsUninitialized || b.IsUninitialized)
            return MergeResult.Fail($"uninitialized values cannot join: {a} vs {b}");
        if (a.IsCategory2 || b.IsCategory2 || a.IsSecondHalf || b.IsSecondHalf)
            return MergeResult.Fail($"category-2 value joins incompatible type: {a} vs {b}");
        if (a.Kind == VKind.Object && b.Kind == VKind.Object)
        {
            // No hierarchy database: keep every legal candidate, do not swallow the ambiguity.
            var common = a.ClassName == "java/lang/Object" || b.ClassName == "java/lang/Object"
                ? VType.ObjectOf("java/lang/Object")
                : VType.ObjectOf("java/lang/Object");
            return MergeResult.Ambiguous(common, new[] { a, b });
        }
        return MergeResult.Fail($"join conflict: {a} vs {b}");
    }
}
