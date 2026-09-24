namespace JvmCore;

public enum OperandForm { None, U1, U2, S2, NoneJsr }

public sealed record Instruction(int Offset, int Opcode, string Mnemonic, byte[] Operands)
{
    public int Length => 1 + Operands.Length;
    public int NextOffset => Offset + Length;
    public override string ToString()
    {
        var op = Operands.Length switch { 0 => "", 1 => $" {Operands[0]}", 2 => $" {BigEndian.UInt16(Operands, 0)}", _ => $" [{Convert.ToHexString(Operands)}]" };
        return $"{Offset,4}: {Mnemonic}{op}";
    }
}

public static class InstructionDecoder
{
    private static readonly string[] Names = new string[256];
    private static readonly int[] Ops = new int[256]; // 0=none,1=u1,2=u2,3=s2(branch)
    static InstructionDecoder()
    {
        void Set(int op, string name, int form = 0) { Names[op] = name; Ops[op] = form; }
        Set(0, "nop"); Set(1, "aconst_null");
        for (var i = 2; i <= 8; i++) Set(i, i == 2 ? "iconst_m1" : $"iconst_{i - 3}");
        Set(9, "lconst_0"); Set(10, "lconst_1"); Set(11, "fconst_0"); Set(12, "fconst_1"); Set(13, "fconst_2");
        Set(16, "bipush", 1); Set(17, "sipush", 2);
        Set(18, "ldc", 1); Set(19, "ldc_w", 2); Set(20, "ldc2_w", 2);
        Set(21, "iload", 1); Set(22, "lload", 1); Set(23, "fload", 1); Set(25, "aload", 1);
        for (var i = 0; i < 4; i++) { Set(26 + i, $"iload_{i}"); Set(30 + i, $"lload_{i}"); Set(34 + i, $"fload_{i}"); Set(42 + i, $"aload_{i}"); }
        Set(54, "istore", 1); Set(55, "lstore", 1); Set(56, "fstore", 1); Set(58, "astore", 1);
        for (var i = 0; i < 4; i++) { Set(59 + i, $"istore_{i}"); Set(63 + i, $"lstore_{i}"); Set(67 + i, $"fstore_{i}"); Set(75 + i, $"astore_{i}"); }
        Set(87, "pop"); Set(88, "pop2"); Set(89, "dup");
        Set(96, "iadd"); Set(97, "ladd"); Set(98, "fadd");
        for (var op = 153; op <= 166; op++) Set(op, op switch
        {
            153 => "ifeq", 154 => "ifne", 155 => "iflt", 156 => "ifge", 157 => "ifgt", 158 => "ifle",
            159 => "if_icmpeq", 160 => "if_icmpne", 161 => "if_icmplt", 162 => "if_icmpge", 163 => "if_icmpgt", 164 => "if_icmple",
            165 => "if_acmpeq", _ => "if_acmpne"
        }, 2);
        Set(167, "goto", 2); Set(168, "jsr", 2); Set(169, "ret", 1);
        Set(172, "ireturn"); Set(173, "lreturn"); Set(174, "freturn"); Set(176, "areturn"); Set(177, "return");
        Set(180, "getfield", 2); Set(181, "putfield", 2);
        Set(182, "invokevirtual", 2); Set(183, "invokespecial", 2); Set(184, "invokestatic", 2); Set(185, "invokeinterface", 4);
        Set(187, "new", 2); Set(189, "arraylength");
        Set(191, "athrow"); Set(192, "checkcast", 2); Set(193, "instanceof", 2);
        Set(198, "ifnull", 2); Set(199, "ifnonnull", 2);
    }

    public static bool IsKnown(int op) => Names[op] != null;
    public static bool IsBranch(int op) => op is 167 or 168 or (>= 153 and <= 166) or 198 or 199;
    public static bool IsReturn(int op) => op is 172 or 173 or 174 or 176 or 177;
    public static bool Terminates(int op) => IsReturn(op) || op is 191 or 169;

    public static List<Instruction> Decode(byte[] code)
    {
        var list = new List<Instruction>();
        var off = 0;
        while (off < code.Length)
        {
            var op = code[off];
            if (!IsKnown(op)) throw new ArgumentException($"unknown opcode {op} at byte offset {off}");
            var n = Ops[op] switch { 0 => 0, 1 => 1, 2 => 2, 4 => 4, _ => 0 };
            var operands = new byte[n];
            Array.Copy(code, off + 1, operands, 0, n);
            list.Add(new Instruction(off, op, Names[op], operands));
            off += 1 + n;
        }
        return list;
    }

    public static int ShortBranch(Instruction i)
    {
        if (i.Operands.Length != 2) return 0;
        var s = (short)((i.Operands[0] << 8) | i.Operands[1]);
        return i.NextOffset + s;
    }

    public static bool IsIntBranch(int op) => op is >= 153 and <= 158 or 198 or 199;
    public static bool IsIcmpBranch(int op) => op is >= 159 and <= 164;
    public static bool IsArefBranch(int op) => op is 165 or 166;
}
