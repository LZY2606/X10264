namespace JvmVerify.Core;

/// <summary>One decoded instruction. Offset is the original byte offset in the
/// code array (coordinate base for every derived artifact).</summary>
public sealed record Insn(
    int Offset, int Opcode, string Mnemonic, int Length,
    int Operand1 = -1, int Operand2 = -1, int BranchTarget = -1, int CpIndex = -1);

public static class Bytecode
{
    // name, fixed length (0 = variable, unsupported here)
    private static readonly (string Name, int Len)[] T = new (string, int)[256];

    static Bytecode()
    {
        void D(int op, string n, int len) => T[op] = (n, len);
        D(0x00, "nop", 1); D(0x01, "aconst_null", 1);
        D(0x02, "iconst_m1", 1); D(0x03, "iconst_0", 1); D(0x04, "iconst_1", 1);
        D(0x05, "iconst_2", 1); D(0x06, "iconst_3", 1); D(0x07, "iconst_4", 1); D(0x08, "iconst_5", 1);
        D(0x09, "lconst_0", 1); D(0x0a, "lconst_1", 1);
        D(0x10, "bipush", 2); D(0x11, "sipush", 3);
        D(0x12, "ldc", 2); D(0x13, "ldc_w", 3); D(0x14, "ldc2_w", 3);
        D(0x15, "iload", 2); D(0x16, "lload", 2); D(0x19, "aload", 2);
        D(0x1a, "iload_0", 1); D(0x1b, "iload_1", 1); D(0x1c, "iload_2", 1); D(0x1d, "iload_3", 1);
        D(0x1e, "lload_0", 1); D(0x1f, "lload_1", 1); D(0x20, "lload_2", 1); D(0x21, "lload_3", 1);
        D(0x2a, "aload_0", 1); D(0x2b, "aload_1", 1); D(0x2c, "aload_2", 1); D(0x2d, "aload_3", 1);
        D(0x36, "istore", 2); D(0x37, "lstore", 2); D(0x3a, "astore", 2);
        D(0x3b, "istore_0", 1); D(0x3c, "istore_1", 1); D(0x3d, "istore_2", 1); D(0x3e, "istore_3", 1);
        D(0x3f, "lstore_0", 1); D(0x40, "lstore_1", 1); D(0x41, "lstore_2", 1); D(0x42, "lstore_3", 1);
        D(0x4b, "astore_0", 1); D(0x4c, "astore_1", 1); D(0x4d, "astore_2", 1); D(0x4e, "astore_3", 1);
        D(0x57, "pop", 1); D(0x59, "dup", 1);
        D(0x60, "iadd", 1); D(0x61, "ladd", 1);
        D(0x84, "iinc", 3);
        D(0x99, "ifeq", 3); D(0x9a, "ifne", 3); D(0x9b, "iflt", 3); D(0x9c, "ifge", 3);
        D(0x9d, "ifgt", 3); D(0x9e, "ifle", 3);
        D(0x9f, "if_icmpeq", 3); D(0xa0, "if_icmpne", 3);
        D(0xa7, "goto", 3);
        D(0xac, "ireturn", 1); D(0xad, "lreturn", 1); D(0xae, "freturn", 1);
        D(0xb0, "areturn", 1); D(0xb1, "return", 1);
        D(0xb2, "getstatic", 3); D(0xb3, "putstatic", 3);
        D(0xb4, "getfield", 3); D(0xb5, "putfield", 3);
        D(0xb6, "invokevirtual", 3); D(0xb7, "invokespecial", 3); D(0xb8, "invokestatic", 3);
        D(0xbb, "new", 3); D(0xbf, "athrow", 1);
    }

    public static List<Insn> Decode(byte[] code)
    {
        var list = new List<Insn>();
        var pc = 0;
        while (pc < code.Length)
        {
            var op = code[pc];
            var (name, len) = T[op];
            if (name == null) throw new FormatException($"unsupported opcode 0x{op:x2} at offset {pc}");
            if (pc + len > code.Length) throw new FormatException($"truncated instruction at offset {pc}");
            int o1 = -1, o2 = -1, target = -1, cpIdx = -1;
            switch (op)
            {
                case 0x10: o1 = (sbyte)code[pc + 1]; break;
                case 0x11: o1 = (short)(code[pc + 1] << 8 | code[pc + 2]); break;
                case 0x12: cpIdx = code[pc + 1]; break;
                case 0x13: case 0x14:
                case 0xb2: case 0xb3: case 0xb4: case 0xb5:
                case 0xb6: case 0xb7: case 0xb8: case 0xbb:
                    cpIdx = code[pc + 1] << 8 | code[pc + 2]; break;
                case 0x15: case 0x16: case 0x19: case 0x36: case 0x37: case 0x3a:
                    o1 = code[pc + 1]; break;
                case 0x84: o1 = code[pc + 1]; o2 = (sbyte)code[pc + 2]; break;
                case >= 0x99 and <= 0xa7:
                    target = pc + (short)(code[pc + 1] << 8 | code[pc + 2]); break;
            }
            list.Add(new Insn(pc, op, name, len, o1, o2, target, cpIdx));
            pc += len;
        }
        return list;
    }

    public static bool IsBranch(int op) => op is >= 0x99 and <= 0xa7;
    public static bool IsConditional(int op) => op is >= 0x99 and <= 0xa6;
    public static bool IsReturn(int op) => op is >= 0xac and <= 0xb1;
    public static bool Halts(int op) => IsReturn(op) || op == 0xbf || op == 0xa7;
}
