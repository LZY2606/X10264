using System.Text.Json.Serialization;

namespace JvmVerifier.Core.Raw;

/// <summary>
/// One decoded instruction. Operand values are kept verbatim with their
/// coordinate basis; <see cref="Raw"/> holds the exact bytes in Code[] order.
/// Nothing here is resolved against the constant pool — that is a derived step.
/// </summary>
public sealed class RawInstruction
{
    public int Opcode { get; set; }
    public string Mnemonic { get; set; } = "";
    public Coords Pc { get; set; }

    public RawInstruction(int opcode, string mnemonic, Coords pc, byte[] raw)
    {
        Opcode = opcode; Mnemonic = mnemonic; Pc = pc; Raw = raw;
    }
    public int Length { get; set; }

    /// <summary>Constant-pool index operand (1-based) where applicable.</summary>
    public int? CpIndex { get; set; }
    /// <summary>Branch target resolved as an absolute Code[] byte offset (derived at decode),
    /// while <see cref="BranchDelta"/> keeps the original signed operand.</summary>
    public int? BranchTarget { get; set; }
    public Coords? BranchTargetCoord { get; set; }
    public int? BranchDelta { get; set; }

    public int? LocalIndex { get; set; }
    public int? IncrementConst { get; set; }
    public int? ArrayTypeCode { get; set; }

    /// <summary>multianewarray dimensions.</summary>
    public int? Dimensions { get; set; }

    /// <summary>tableswitch / lookupswitch preserved operands.</summary>
    public int? SwitchDefault { get; set; }
    public int? SwitchLow { get; set; }
    public int? SwitchHigh { get; set; }
    public List<int> SwitchOffsets { get; set; } = new();
    public List<int> SwitchKeys { get; set; } = new();

    /// <summary>Raw operand bytes (without the opcode), original representation.</summary>
    public byte[] Raw { get; set; } = Array.Empty<byte>();

    [JsonIgnore]
    public string Id => $"insn@{Pc.Value}";
}
