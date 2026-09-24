namespace JvmVerifier.Core.ClassFile;

public enum OperandKind
{
    None, LocalIndex, CpIndex, CpIndexWide, BranchOffset, BranchOffsetW,
    NewArrayType, InvokeInterface, MultiArray, Iinc
}

public sealed class InstructionSpec
{
    public required byte Opcode { get; init; }
    public required string Mnemonic { get; init; }
    public required int OperandSize { get; init; }
    public OperandKind Kind { get; init; } = OperandKind.None;
    public bool IsBranch { get; init; }
    public bool IsReturn { get; init; }
    public bool IsThrow { get; init; }
    public bool IsUnconditional { get; init; }
}

public sealed class Instruction
{
    public required int Offset { get; init; }
    public required InstructionSpec Spec { get; init; }
    public required byte[] OperandBytes { get; init; }
    public int Length => 1 + OperandBytes.Length;
    public int EndOffset => Offset + Length;
    public string Mnemonic => Spec.Mnemonic;

    public int SignedBranch()
    {
        if (Spec.Kind == OperandKind.BranchOffsetW)
            return (OperandBytes[0] << 24) | (OperandBytes[1] << 16) |
                   (OperandBytes[2] << 8) | OperandBytes[3];
        return (short)((OperandBytes[0] << 8) | OperandBytes[1]);
    }

    public int BranchTarget => Offset + SignedBranch();
    public int OperandU1 => OperandBytes.Length > 0 ? OperandBytes[0] : 0;
    public int OperandU2 => OperandBytes.Length >= 2 ? (OperandBytes[0] << 8) | OperandBytes[1] : 0;
}
