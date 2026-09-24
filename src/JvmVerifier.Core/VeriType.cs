using System.Text.Json.Serialization;

namespace JvmVerifier.Core;

// 抽象解释中的验证类型。一个槽位一个 VeriType；category-2 的第二槽用 TopHalf 显式占位。
// 不确定（多解）时用 Candidates 保留全部合法候选，绝不压成单一答案。
public sealed class VeriType : IEquatable<VeriType>
{
    public enum KindCode
    {
        Top,            // 不可用作任何用途
        Integer, Float, Long, Double, // Long/Double = category-2
        Null,           // null 常量
        Reference,      // 已初始化对象引用
        UninitializedThis,      // <init> 入口的 this
        UninitializedNew,       // new 之后、invokespecial <init> 之前，带 new 指令字节偏移
        TopHalf,        // category-2 第二槽（结构性占位，单独使用即非法）
        Conflict        // 分支 join 不可调和的结果（不静默变成 Top）
    }

    public KindCode Kind { get; init; }
    public string? ClassName { get; init; }          // Reference 的类/接口原文名
    public int? NewPc { get; init; }                 // UninitializedNew 的来源字节偏移
    public bool IsInterface { get; init; }
    public IReadOnlyList<VeriType> Candidates { get; init; } = Array.Empty<VeriType>();

    public bool IsAmbiguous => Candidates.Count > 1;
    public int Category => Kind is KindCode.Long or KindCode.Double ? 2 : 1;

    public static readonly VeriType Top = new() { Kind = KindCode.Top };
    public static readonly VeriType TopHalf = new() { Kind = KindCode.TopHalf };
    public static readonly VeriType IntegerV = new() { Kind = KindCode.Integer };
    public static readonly VeriType FloatV = new() { Kind = KindCode.Float };
    public static readonly VeriType LongV = new() { Kind = KindCode.Long };
    public static readonly VeriType DoubleV = new() { Kind = KindCode.Double };
    public static readonly VeriType NullV = new() { Kind = KindCode.Null };
    public static VeriType Ref(string name, bool isInterface = false)
        => new() { Kind = KindCode.Reference, ClassName = name, IsInterface = isInterface };
    public static VeriType UninitThis() => new() { Kind = KindCode.UninitializedThis };
    public static VeriType UninitNew(int pc) => new() { Kind = KindCode.UninitializedNew, NewPc = pc };
    public static VeriType Ambiguous(IReadOnlyList<VeriType> cands)
        => new() { Kind = KindCode.Top, Candidates = cands };

    public bool IsCategory2 => Kind is KindCode.Long or KindCode.Double;
    public bool IsUninitialized => Kind is KindCode.UninitializedThis or KindCode.UninitializedNew;

    // 是否可作为引用使用（含未初始化/空引用的判定由调用点按规则区分）
    public bool IsReferenceLike => Kind is KindCode.Reference or KindCode.Null
        or KindCode.UninitializedThis or KindCode.UninitializedNew;

    public string Display => Kind switch
    {
        KindCode.Reference => ClassName ?? "?",
        KindCode.UninitializedThis => "uninitializedThis",
        KindCode.UninitializedNew => $"uninitialized@{NewPc}",
        KindCode.Top when IsAmbiguous => "ambiguous{" + string.Join("|", Candidates.Select(c => c.Display)) + "}",
        _ => Kind.ToString().ToLowerInvariant().Replace("integerv", "int").Replace("floatv", "float")
    };

    public bool Equals(VeriType? other) => other is not null && StructuralEquals(other);
    public override bool Equals(object? obj) => obj is VeriType t && Equals(t);
    public override int GetHashCode() => StructuralKey().GetHashCode();

    private string StructuralKey() =>
        Kind + "|" + ClassName + "|" + NewPc + "|" + IsInterface
        + (IsAmbiguous ? "|[" + string.Join(",", Candidates.Select(c => c.StructuralKey())) + "]" : "");

    private bool StructuralEquals(VeriType o) => StructuralKey() == o.StructuralKey();

    public static bool Same(VeriType? a, VeriType? b)
        => (a is null && b is null) || (a is not null && a.Equals(b));
}
