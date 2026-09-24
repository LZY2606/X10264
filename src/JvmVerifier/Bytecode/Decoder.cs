namespace JvmVerifier.Bytecode;

public sealed record Instruction(
    int Offset, int Length, int Opcode, string Mnemonic,
    IReadOnlyList<int> Operands,
    IReadOnlyList<int> BranchTargets,   // 绝对偏移
    int? CpIndex = null)
{
    public int NextOffset => Offset + Length;
    public bool IsTerminal => Mnemonic is "return" or "ireturn" or "lreturn" or "areturn" or "athrow" or "goto";
}

public static class Decoder
{
    public static List<Instruction> Decode(byte[] code)
    {
        var list = new List<Instruction>();
        int pos = 0;
        while (pos < code.Length)
        {
            int start = pos;
            int op = code[pos++];
            switch (op)
            {
                case 0x00: list.Add(new(start, 1, op, "nop", [], [])); break;
                case 0x01: list.Add(new(start, 1, op, "aconst_null", [], [])); break;
                case >= 0x02 and <= 0x08: list.Add(new(start, 1, op, $"iconst_{op - 0x03}", [], [])); break;
                case 0x09: list.Add(new(start, 1, op, "lconst_0", [], [])); break;
                case 0x0A: list.Add(new(start, 1, op, "lconst_1", [], [])); break;
                case 0x12: { int idx = code[pos++]; list.Add(new(start, 2, op, "ldc", [idx], [], idx)); break; }
                case 0x14: { int idx = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "ldc2_w", [idx], [], idx)); break; }
                case 0x15: { int i = code[pos++]; list.Add(new(start, 2, op, "iload", [i], [])); break; }
                case 0x16: { int i = code[pos++]; list.Add(new(start, 2, op, "lload", [i], [])); break; }
                case 0x19: { int i = code[pos++]; list.Add(new(start, 2, op, "aload", [i], [])); break; }
                case >= 0x1A and <= 0x1D: list.Add(new(start, 1, op, $"iload_{op - 0x1A}", [op - 0x1A], [])); break;
                case >= 0x1E and <= 0x21: list.Add(new(start, 1, op, $"lload_{op - 0x1E}", [op - 0x1E], [])); break;
                case >= 0x2A and <= 0x2D: list.Add(new(start, 1, op, $"aload_{op - 0x2A}", [op - 0x2A], [])); break;
                case 0x36: { int i = code[pos++]; list.Add(new(start, 2, op, "istore", [i], [])); break; }
                case 0x37: { int i = code[pos++]; list.Add(new(start, 2, op, "lstore", [i], [])); break; }
                case 0x3A: { int i = code[pos++]; list.Add(new(start, 2, op, "astore", [i], [])); break; }
                case >= 0x3B and <= 0x3E: list.Add(new(start, 1, op, $"istore_{op - 0x3B}", [op - 0x3B], [])); break;
                case >= 0x3F and <= 0x42: list.Add(new(start, 1, op, $"lstore_{op - 0x3F}", [op - 0x3F], [])); break;
                case >= 0x4B and <= 0x4E: list.Add(new(start, 1, op, $"astore_{op - 0x4B}", [op - 0x4B], [])); break;
                case 0x57: list.Add(new(start, 1, op, "pop", [], [])); break;
                case 0x59: list.Add(new(start, 1, op, "dup", [], [])); break;
                case 0x60: list.Add(new(start, 1, op, "iadd", [], [])); break;
                case 0x61: list.Add(new(start, 1, op, "ladd", [], [])); break;
                case 0x99 or 0x9A or 0x9F:
                {
                    int t = S2(code, pos); pos += 2;
                    string m = op == 0x99 ? "ifeq" : op == 0x9A ? "ifne" : "if_icmpeq";
                    list.Add(new(start, 3, op, m, [t], [start + t]));
                    break;
                }
                case 0xA7: { int t = S2(code, pos); pos += 2; list.Add(new(start, 3, op, "goto", [t], [start + t])); break; }
                case 0xAC: list.Add(new(start, 1, op, "ireturn", [], [])); break;
                case 0xAD: list.Add(new(start, 1, op, "lreturn", [], [])); break;
                case 0xB0: list.Add(new(start, 1, op, "areturn", [], [])); break;
                case 0xB1: list.Add(new(start, 1, op, "return", [], [])); break;
                case 0xB2: { int i = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "getstatic", [i], [], i)); break; }
                case 0xB5: { int i = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "putfield", [i], [], i)); break; }
                case 0xB6: { int i = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "invokevirtual", [i], [], i)); break; }
                case 0xB7: { int i = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "invokespecial", [i], [], i)); break; }
                case 0xB8: { int i = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "invokestatic", [i], [], i)); break; }
                case 0xBB: { int i = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "new", [i], [], i)); break; }
                case 0xBF: list.Add(new(start, 1, op, "athrow", [], [])); break;
                case 0xC0: { int i = U2(code, pos); pos += 2; list.Add(new(start, 3, op, "checkcast", [i], [], i)); break; }
                default: throw new InvalidDataException($"unsupported opcode 0x{op:X2} at {start}");
            }
        }
        return list;
    }

    public static HashSet<int> InstructionBoundaries(byte[] code) => Decode(code).Select(i => i.Offset).ToHashSet();

    private static int U2(byte[] b, int p) => (b[p] << 8) | b[p + 1];
    private static int S2(byte[] b, int p) => (short)((b[p] << 8) | b[p + 1]);
}
