namespace Verifier.Core;

/// <summary>字节偏移量。单位固定为“字节”，坐标原点由 <see cref="Origin"/> 标明。</summary>
public readonly record struct ByteOffset(int Value, string Origin = "Code[0]")
{
    public string Id => $"@{Value}";
    public override string ToString() => $"{Value}B@{Origin}";
}

/// <summary>带坐标基准的原始区间：起始字节偏移 + 长度，全部以字节为单位、原点为文件头或 Code[0]。</summary>
public readonly record struct RawSpan(int Start, int Length, string Origin, string Label)
{
    public int End => Start + Length;
    public string Id => $"{Origin}:{Start}+{Length}:{Label}";
}

/// <summary>解析阶段诊断；无法转换的原值留在原位并附带该诊断，绝不丢弃。</summary>
public sealed record ParseDiagnostic(string Code, string Severity, string Message, int? Offset, string? StableId);
