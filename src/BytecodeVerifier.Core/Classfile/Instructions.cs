using System.Text.Json.Serialization;

namespace BytecodeVerifier.Core.Classfile;

/// <summary>
/// 一条已解码指令。Offset/Length 的基准是 Code 属性 code[]（单位字节，坐标 bci）。
/// Operands 保留原始 u1/u2/u4 数值；CpRef 指向被引用的常量池项（不复制其内容）；
/// SwitchCases 保留绝对 bci 与原始 key。
/// </summary>
public sealed class Instruction
{
    [JsonPropertyName("bci")] public int Bci { get; init; }
    [JsonPropertyName("opcode")] public int Opcode { get; init; }
    [JsonPropertyName("mnemonic")] public string Mnemonic { get; init; } = "";
    [JsonPropertyName("length")] public int Length { get; init; }
    [JsonPropertyName("operands")] public long[] Operands { get; init; } = System.Array.Empty<long>();
    [JsonPropertyName("cpRef")] public int? CpRef { get; init; }
    [JsonPropertyName("branchTarget")] public int? BranchTarget { get; init; }
    [JsonPropertyName("switchCases")] public SwitchCase[] SwitchCases { get; init; } = System.Array.Empty<SwitchCase>();
    [JsonPropertyName("switchDefault")] public int? SwitchDefault { get; init; }

    public override string ToString() => $"{Bci,4}: {Mnemonic}";
}

public sealed record SwitchCase([property: JsonPropertyName("key")] int Key, [property: JsonPropertyName("target")] int Target);
