namespace BytecodeSeal.Core;

/// <summary>One decoded instruction. Offset is a byte offset with the method code
/// array as coordinate base (0-based, unit = byte). Id is the stable identity
/// shown in the UI, e.g. "insn@12".</summary>
public sealed record Instruction(
    string Id,
    int Offset,
    string Mnemonic,
    byte Opcode,
    int Length,
    int? LocalIndex,
    int? ConstantPoolIndex,
    int? IntOperand,
    IReadOnlyList<int> BranchTargets,
    bool IsConditional,
    bool IsGoto,
    bool IsReturn,
    bool IsThrow,
    bool Unsupported)
{
    public bool EndsFlow => IsReturn || IsThrow || IsGoto;
}

public static class Decoder
{
    public static List<Instruction> Decode(byte[] code)
    {
        var list = new List<Instruction>();
        int pos = 0;
        while (pos < code.Length)
        {
            int offset = pos;
            byte op = code[pos++];
            var info = Opcodes.Table[op];
            if (info == null)
            {
                list.Add(new Instruction($"insn@{offset}", offset, $"bad:0x{op:X2}", op, 1,
                    null, null, null, Array.Empty<int>(), false, false, false, false, true));
                continue;
            }
            int? local = null, cp = null, iop = null;
            var targets = new List<int>();
            bool cond = false, isGoto = false, isRet = false, isThrow = false, unsup = false;
            switch (info.Format)
            {
                case OperandFormat.None: break;
                case OperandFormat.U1: local = code[pos++]; break;
                case OperandFormat.I1: iop = (sbyte)code[pos++]; break;
                case OperandFormat.U2: iop = (code[pos] << 8) | code[pos + 1]; pos += 2; break;
                case OperandFormat.I2: iop = (short)((code[pos] << 8) | code[pos + 1]); pos += 2; break;
                case OperandFormat.Cp1: cp = code[pos++]; break;
                case OperandFormat.Cp2: cp = (code[pos] << 8) | code[pos + 1]; pos += 2; break;
                case OperandFormat.Cp2U1Zero: cp = (code[pos] << 8) | code[pos + 1]; pos += 4; break;
                case OperandFormat.Cp2ZeroZero: cp = (code[pos] << 8) | code[pos + 1]; pos += 4; break;
                case OperandFormat.Iinc: local = code[pos++]; iop = (sbyte)code[pos++]; break;
                case OperandFormat.Branch2:
                {
                    int rel = (short)((code[pos] << 8) | code[pos + 1]); pos += 2;
                    targets.Add(offset + rel);
                    break;
                }
                case OperandFormat.Branch4:
                {
                    int rel = (code[pos] << 24) | (code[pos + 1] << 16) | (code[pos + 2] << 8) | code[pos + 3]; pos += 4;
                    targets.Add(offset + rel);
                    break;
                }
                case OperandFormat.Unsupported: unsup = true; break;
            }
            var m = info.Mnemonic;
            if (m.StartsWith("if_") || m is "ifeq" or "ifne" or "iflt" or "ifge" or "ifgt" or "ifle" or "ifnull" or "ifnonnull") cond = true;
            if (m is "goto" or "goto_w") isGoto = true;
            if (m.EndsWith("return")) isRet = true;
            if (m == "athrow") isThrow = true;
            if (m == "jsr" || m == "ret") unsup = true;
            list.Add(new Instruction($"insn@{offset}", offset, m, op, pos - offset,
                local, cp, iop, targets, cond, isGoto, isRet, isThrow, unsup));
        }
        return list;
    }
}
