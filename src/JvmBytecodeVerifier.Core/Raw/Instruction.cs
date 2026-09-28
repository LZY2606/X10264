namespace JvmBytecodeVerifier.Core.Raw;

public sealed record DecodeError(int Offset, string Code, string Message, string Coordinate);

public sealed record InstructionOperand(string Kind, string RawText, object Value, int OperandOffset);

public sealed record Instruction(byte Opcode, string Mnemonic, int Offset, int Length,
    IReadOnlyList<InstructionOperand> Operands,
    IReadOnlyList<int> BranchTargets,
    int? ConstantPoolIndex,
    bool IsTerminator,
    string Note)
{
    public string StableId => $"insn@{Offset}";
    public int EndExclusive => Offset + Length;
}

public sealed record DecodeResult(IReadOnlyList<Instruction> Instructions, IReadOnlyList<DecodeError> Errors);
