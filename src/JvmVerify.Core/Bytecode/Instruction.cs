using JvmVerify.Core;

namespace JvmVerify.Core.Bytecode;

public enum OperandKind
{
    None,
    CpIndex,
    BranchOffset,
    LocalIndex,
    Byte,
    Short,
    WideLocalIndex,
    TableSwitch,
    LookupSwitch,
    IincIncr,
    WideIinc,
    NewarrayType,
    Multianewarray
}

public sealed record SwitchData(int DefaultOffset, IReadOnlyList<int> Keys, IReadOnlyList<int> Offsets);

/// <summary>
/// One decoded instruction. Offset is a RawByte quantity measured from the first code byte.
/// OperandRaw preserves the exact operand bytes. Branches/exception edges are derived later.
/// </summary>
public sealed class Instruction
{
    public required Quantity<int> Offset { get; init; }
    public required byte Opcode { get; init; }
    public required string Mnemonic { get; init; }
    public required int Length { get; init; }
    public required OperandKind OperandKind { get; init; }
    public int IntOperand { get; init; }
    public SwitchData? Switch { get; init; }
    public required string OperandRaw { get; init; }
    /// <summary>Raw token text for cp references, e.g. "#4".</summary>
    public string? CpRawToken { get; init; }
    public bool DecodeError { get; init; }
    public string? DecodeDiagnostic { get; init; }

    public bool IsReturn => Opcode is >= 172 and <= 177;
    public bool IsUnconditionalJump => Mnemonic is "goto" or "goto_w" or "jsr" or "jsr_w"
        || (Opcode == 170) || (Opcode == 201) || (Opcode == 200) || (Opcode == 169);
    public bool IsAthrow => Opcode == 191;

    public IEnumerable<int> JumpTargets()
    {
        switch (Mnemonic)
        {
            case "goto": case "goto_w": case "jsr": case "jsr_w":
                yield return Offset.Value + IntOperand;
                break;
            case "if_icmpeq" or "if_icmpne" or "if_icmplt" or "if_icmpgt"
                 or "if_icmple" or "if_icmpge" or "ifeq" or "ifne" or "iflt"
                 or "ifgt" or "ifle" or "ifge" or "ifnull" or "ifnonnull":
                yield return Offset.Value + IntOperand;
                break;
            default:
                if (Switch is not null)
                {
                    yield return Offset.Value + Switch.DefaultOffset;
                    foreach (var o in Switch.Offsets) yield return Offset.Value + o;
                }
                break;
        }
    }
}
