namespace JvmVerify.Core;

public enum OperandKind { None, LocalIndex, ConstPoolIndex, BranchOffset, IConst, Wide }

public sealed record Instruction(
    ByteOffset Offset,
    byte Opcode,
    string Mnemonic,
    int Length,
    int? LocalIndex,
    ushort? CpIndex,
    ByteOffset? BranchTarget,
    int? IntOperand,
    bool Wide,
    byte[] RawBytes);

/// <summary>Decodes the code array into instructions. Undecodable bytes are kept
/// raw and reported; decoding never silently drops bytes.</summary>
public static class InstructionDecoder
{
    private sealed record Op(string Name, OperandKind Kind, int FixedLen);

    private static readonly Dictionary<byte, Op> Table = new()
    {
        [0x00] = new("nop", OperandKind.None, 1),
        [0x01] = new("aconst_null", OperandKind.None, 1),
        [0x02] = new("iconst_m1", OperandKind.None, 1),
        [0x03] = new("iconst_0", OperandKind.None, 1),
        [0x04] = new("iconst_1", OperandKind.None, 1),
        [0x05] = new("iconst_2", OperandKind.None, 1),
        [0x06] = new("iconst_3", OperandKind.None, 1),
        [0x07] = new("iconst_4", OperandKind.None, 1),
        [0x08] = new("iconst_5", OperandKind.None, 1),
        [0x09] = new("lconst_0", OperandKind.None, 1),
        [0x0A] = new("lconst_1", OperandKind.None, 1),
        [0x0E] = new("dconst_0", OperandKind.None, 1),
        [0x0F] = new("dconst_1", OperandKind.None, 1),
        [0x10] = new("bipush", OperandKind.IConst, 2),
        [0x11] = new("sipush", OperandKind.IConst, 3),
        [0x12] = new("ldc", OperandKind.ConstPoolIndex, 2),
        [0x13] = new("ldc_w", OperandKind.ConstPoolIndex, 3),
        [0x14] = new("ldc2_w", OperandKind.ConstPoolIndex, 3),
        [0x15] = new("iload", OperandKind.LocalIndex, 2),
        [0x16] = new("lload", OperandKind.LocalIndex, 2),
        [0x19] = new("aload", OperandKind.LocalIndex, 2),
        [0x1A] = new("iload_0", OperandKind.None, 1),
        [0x1B] = new("iload_1", OperandKind.None, 1),
        [0x1C] = new("iload_2", OperandKind.None, 1),
        [0x1D] = new("iload_3", OperandKind.None, 1),
        [0x1E] = new("lload_0", OperandKind.None, 1),
        [0x1F] = new("lload_1", OperandKind.None, 1),
        [0x20] = new("lload_2", OperandKind.None, 1),
        [0x21] = new("lload_3", OperandKind.None, 1),
        [0x2A] = new("aload_0", OperandKind.None, 1),
        [0x2B] = new("aload_1", OperandKind.None, 1),
        [0x2C] = new("aload_2", OperandKind.None, 1),
        [0x2D] = new("aload_3", OperandKind.None, 1),
        [0x36] = new("istore", OperandKind.LocalIndex, 2),
        [0x37] = new("lstore", OperandKind.LocalIndex, 2),
        [0x3A] = new("astore", OperandKind.LocalIndex, 2),
        [0x3B] = new("istore_0", OperandKind.None, 1),
        [0x3C] = new("istore_1", OperandKind.None, 1),
        [0x3D] = new("istore_2", OperandKind.None, 1),
        [0x3E] = new("istore_3", OperandKind.None, 1),
        [0x3F] = new("lstore_0", OperandKind.None, 1),
        [0x40] = new("lstore_1", OperandKind.None, 1),
        [0x41] = new("lstore_2", OperandKind.None, 1),
        [0x42] = new("lstore_3", OperandKind.None, 1),
        [0x4B] = new("astore_0", OperandKind.None, 1),
        [0x4C] = new("astore_1", OperandKind.None, 1),
        [0x4D] = new("astore_2", OperandKind.None, 1),
        [0x4E] = new("astore_3", OperandKind.None, 1),
        [0x57] = new("pop", OperandKind.None, 1),
        [0x58] = new("pop2", OperandKind.None, 1),
        [0x59] = new("dup", OperandKind.None, 1),
        [0x5C] = new("dup2", OperandKind.None, 1),
        [0x60] = new("iadd", OperandKind.None, 1),
        [0x61] = new("ladd", OperandKind.None, 1),
        [0x64] = new("isub", OperandKind.None, 1),
        [0x65] = new("lsub", OperandKind.None, 1),
        [0x68] = new("imul", OperandKind.None, 1),
        [0x69] = new("lmul", OperandKind.None, 1),
        [0x6C] = new("idiv", OperandKind.None, 1),
        [0x6D] = new("ldiv", OperandKind.None, 1),
        [0x70] = new("irem", OperandKind.None, 1),
        [0x74] = new("ineg", OperandKind.None, 1),
        [0x78] = new("ishl", OperandKind.None, 1),
        [0x7A] = new("ishr", OperandKind.None, 1),
        [0x84] = new("iinc", OperandKind.IConst, 3),
        [0x85] = new("i2l", OperandKind.None, 1),
        [0x88] = new("l2i", OperandKind.None, 1),
        [0x94] = new("lcmp", OperandKind.None, 1),
        [0x99] = new("ifeq", OperandKind.BranchOffset, 3),
        [0x9A] = new("ifne", OperandKind.BranchOffset, 3),
        [0x9B] = new("iflt", OperandKind.BranchOffset, 3),
        [0x9C] = new("ifge", OperandKind.BranchOffset, 3),
        [0x9D] = new("ifgt", OperandKind.BranchOffset, 3),
        [0x9E] = new("ifle", OperandKind.BranchOffset, 3),
        [0x9F] = new("if_icmpeq", OperandKind.BranchOffset, 3),
        [0xA0] = new("if_icmpne", OperandKind.BranchOffset, 3),
        [0xA1] = new("if_icmplt", OperandKind.BranchOffset, 3),
        [0xA2] = new("if_icmpge", OperandKind.BranchOffset, 3),
        [0xA3] = new("if_icmpgt", OperandKind.BranchOffset, 3),
        [0xA4] = new("if_icmple", OperandKind.BranchOffset, 3),
        [0xA5] = new("if_acmpeq", OperandKind.BranchOffset, 3),
        [0xA6] = new("if_acmpne", OperandKind.BranchOffset, 3),
        [0xA7] = new("goto", OperandKind.BranchOffset, 3),
        [0xAC] = new("ireturn", OperandKind.None, 1),
        [0xAD] = new("lreturn", OperandKind.None, 1),
        [0xB0] = new("areturn", OperandKind.None, 1),
        [0xB1] = new("return", OperandKind.None, 1),
        [0xB2] = new("getstatic", OperandKind.ConstPoolIndex, 3),
        [0xB3] = new("putstatic", OperandKind.ConstPoolIndex, 3),
        [0xB4] = new("getfield", OperandKind.ConstPoolIndex, 3),
        [0xB5] = new("putfield", OperandKind.ConstPoolIndex, 3),
        [0xB6] = new("invokevirtual", OperandKind.ConstPoolIndex, 3),
        [0xB7] = new("invokespecial", OperandKind.ConstPoolIndex, 3),
        [0xB8] = new("invokestatic", OperandKind.ConstPoolIndex, 3),
        [0xB9] = new("invokeinterface", OperandKind.ConstPoolIndex, 5),
        [0xBB] = new("new", OperandKind.ConstPoolIndex, 3),
        [0xBF] = new("athrow", OperandKind.None, 1),
        [0xC0] = new("checkcast", OperandKind.ConstPoolIndex, 3),
        [0xC1] = new("instanceof", OperandKind.ConstPoolIndex, 3),
        [0xC4] = new("wide", OperandKind.Wide, 0),
        [0xC6] = new("ifnull", OperandKind.BranchOffset, 3),
        [0xC7] = new("ifnonnull", OperandKind.BranchOffset, 3),
        [0xC8] = new("goto_w", OperandKind.BranchOffset, 5),
    };

    public static IReadOnlyList<Instruction> Decode(byte[] code, List<Diagnostic> diagnostics)
    {
        var result = new List<Instruction>();
        var pc = 0;
        while (pc < code.Length)
        {
            var start = pc;
            var op = code[pc];
            if (!Table.TryGetValue(op, out var info))
            {
                diagnostics.Add(new Diagnostic("OPCODE-UNKNOWN",
                    $"unknown opcode 0x{op:X2} at code offset {pc}; byte kept raw",
                    $"0x{op:X2}", "code"));
                result.Add(new Instruction(ByteOffset.Code(pc), op, $"<unknown:0x{op:X2}>", 1,
                    null, null, null, null, false, code[pc..(pc + 1)]));
                pc++;
                continue;
            }

            var len = info.FixedLen;
            int? localIndex = null;
            ushort? cpIndex = null;
            ByteOffset? branchTarget = null;
            int? intOperand = null;
            var wide = false;

            switch (info.Kind)
            {
                case OperandKind.LocalIndex:
                    localIndex = code[pc + 1];
                    break;
                case OperandKind.ConstPoolIndex:
                    cpIndex = info.FixedLen == 2
                        ? code[pc + 1]
                        : (ushort)((code[pc + 1] << 8) | code[pc + 2]);
                    break;
                case OperandKind.BranchOffset:
                    if (info.FixedLen == 3)
                    {
                        var delta = (short)((code[pc + 1] << 8) | code[pc + 2]);
                        branchTarget = ByteOffset.Code(pc + delta);
                    }
                    else
                    {
                        var delta = (code[pc + 1] << 24) | (code[pc + 2] << 16) | (code[pc + 3] << 8) | code[pc + 4];
                        branchTarget = ByteOffset.Code(pc + delta);
                    }
                    break;
                case OperandKind.IConst:
                    intOperand = info.FixedLen == 2 ? (sbyte)code[pc + 1]
                        : (short)((code[pc + 1] << 8) | code[pc + 2]);
                    break;
                case OperandKind.Wide:
                    wide = true;
                    var widened = code[pc + 1];
                    if (widened == 0x84) // wide iinc
                    {
                        len = 6;
                        localIndex = (code[pc + 2] << 8) | code[pc + 3];
                        intOperand = (short)((code[pc + 4] << 8) | code[pc + 5]);
                    }
                    else
                    {
                        len = 4;
                        localIndex = (code[pc + 2] << 8) | code[pc + 3];
                    }
                    info = Table.TryGetValue(widened, out var w) ? w : info;
                    break;
            }

            if (pc + len > code.Length)
            {
                diagnostics.Add(new Diagnostic("INSTR-TRUNCATED",
                    $"instruction {info.Name} at code offset {pc} overruns code array",
                    info.Name, "code"));
                len = code.Length - pc;
            }

            result.Add(new Instruction(ByteOffset.Code(pc), op, info.Name, len,
                localIndex, cpIndex, branchTarget, intOperand, wide, code[pc..(pc + len)]));
            pc += len;
        }
        return result;
    }
}
