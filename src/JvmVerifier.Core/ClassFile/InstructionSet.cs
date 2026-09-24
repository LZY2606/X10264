namespace JvmVerifier.Core.ClassFile;

public static class InstructionSet
{
    private static readonly InstructionSpec?[] Specs = new InstructionSpec?[256];

    private static void Add(byte op, string name, int size, OperandKind kind = OperandKind.None,
        bool branch = false, bool ret = false, bool thr = false, bool unconditional = false) =>
        Specs[op] = new InstructionSpec
        {
            Opcode = op, Mnemonic = name, OperandSize = size, Kind = kind,
            IsBranch = branch, IsReturn = ret, IsThrow = thr, IsUnconditional = unconditional
        };

    private static void Simple(params (byte Op, string Name)[] ops)
    {
        foreach (var (op, name) in ops) Add(op, name, 0);
    }

    static InstructionSet()
    {
        Add(0, "nop", 0);
        Add(1, "aconst_null", 0);
        Simple((2, "iconst_m1"), (3, "iconst_0"), (4, "iconst_1"), (5, "iconst_2"),
            (6, "iconst_3"), (7, "iconst_4"), (8, "iconst_5"),
            (9, "lconst_0"), (10, "lconst_1"), (11, "fconst_0"), (12, "fconst_1"),
            (13, "fconst_2"), (14, "dconst_0"), (15, "dconst_1"));
        Add(16, "bipush", 1); Add(17, "sipush", 2);
        Add(18, "ldc", 1, OperandKind.CpIndex);
        Add(19, "ldc_w", 2, OperandKind.CpIndexWide);
        Add(20, "ldc2_w", 2, OperandKind.CpIndexWide);

        Add(21, "iload", 1, OperandKind.LocalIndex); Add(22, "lload", 1, OperandKind.LocalIndex);
        Add(23, "fload", 1, OperandKind.LocalIndex); Add(24, "dload", 1, OperandKind.LocalIndex);
        Add(25, "aload", 1, OperandKind.LocalIndex);
        Simple((26, "iload_0"), (27, "iload_1"), (28, "iload_2"), (29, "iload_3"),
            (30, "lload_0"), (31, "lload_1"), (32, "lload_2"), (33, "lload_3"),
            (34, "fload_0"), (35, "fload_1"), (36, "fload_2"), (37, "fload_3"),
            (38, "dload_0"), (39, "dload_1"), (40, "dload_2"), (41, "dload_3"),
            (42, "aload_0"), (43, "aload_1"), (44, "aload_2"), (45, "aload_3"),
            (46, "iaload"), (47, "laload"), (48, "faload"), (49, "daload"),
            (50, "aaload"), (51, "baload"), (52, "caload"), (53, "saload"));

        Add(54, "istore", 1, OperandKind.LocalIndex); Add(55, "lstore", 1, OperandKind.LocalIndex);
        Add(56, "fstore", 1, OperandKind.LocalIndex); Add(57, "dstore", 1, OperandKind.LocalIndex);
        Add(58, "astore", 1, OperandKind.LocalIndex);
        Simple((59, "istore_0"), (60, "istore_1"), (61, "istore_2"), (62, "istore_3"),
            (63, "lstore_0"), (64, "lstore_1"), (65, "lstore_2"), (66, "lstore_3"),
            (67, "fstore_0"), (68, "fstore_1"), (69, "fstore_2"), (70, "fstore_3"),
            (71, "dstore_0"), (72, "dstore_1"), (73, "dstore_2"), (74, "dstore_3"),
            (75, "astore_0"), (76, "astore_1"), (77, "astore_2"), (78, "astore_3"),
            (79, "iastore"), (80, "lastore"), (81, "fastore"), (82, "dastore"),
            (83, "aastore"), (84, "bastore"), (85, "castore"), (86, "sastore"));

        Simple((87, "pop"), (88, "pop2"), (89, "dup"), (90, "dup_x1"), (91, "dup_x2"),
            (92, "dup2"), (93, "dup2_x1"), (94, "dup2_x2"), (95, "swap"));
        Simple(Enumerable.Range(96, 36).Select(i => ((byte)i, MathMnemonic(i))).ToArray());
        Add(132, "iinc", 2, OperandKind.Iinc);
        Simple(Enumerable.Range(133, 20).Select(i => ((byte)i, ConvMnemonic(i))).ToArray());

        string[] cond = { "ifeq", "ifne", "iflt", "ifge", "ifgt", "ifle",
            "if_icmpeq", "if_icmpne", "if_icmplt", "if_icmpge", "if_icmpgt", "if_icmple",
            "if_acmpeq", "if_acmpne" };
        for (int i = 0; i < cond.Length; i++)
            Add((byte)(153 + i), cond[i], 2, OperandKind.BranchOffset, branch: true);
        Add(167, "goto", 2, OperandKind.BranchOffset, branch: true, unconditional: true);
        Add(168, "jsr", 2, OperandKind.BranchOffset, branch: true);
        Add(169, "ret", 1, OperandKind.LocalIndex);
        Add(170, "tableswitch", -1);
        Add(171, "lookupswitch", -1);
        Add(172, "ireturn", 0, ret: true); Add(173, "lreturn", 0, ret: true);
        Add(174, "freturn", 0, ret: true); Add(175, "dreturn", 0, ret: true);
        Add(176, "areturn", 0, ret: true); Add(177, "return", 0, ret: true);
        Add(178, "getstatic", 2, OperandKind.CpIndexWide);
        Add(179, "putstatic", 2, OperandKind.CpIndexWide);
        Add(180, "getfield", 2, OperandKind.CpIndexWide);
        Add(181, "putfield", 2, OperandKind.CpIndexWide);
        Add(182, "invokevirtual", 2, OperandKind.CpIndexWide);
        Add(183, "invokespecial", 2, OperandKind.CpIndexWide);
        Add(184, "invokestatic", 2, OperandKind.CpIndexWide);
        Add(185, "invokeinterface", 4, OperandKind.InvokeInterface);
        Add(186, "invokedynamic", 4, OperandKind.InvokeInterface);
        Add(187, "new", 2, OperandKind.CpIndexWide);
        Add(188, "newarray", 1, OperandKind.NewArrayType);
        Add(189, "anewarray", 2, OperandKind.CpIndexWide);
        Add(190, "arraylength", 0);
        Add(191, "athrow", 0, thr: true);
        Add(192, "checkcast", 2, OperandKind.CpIndexWide);
        Add(193, "instanceof", 2, OperandKind.CpIndexWide);
        Add(194, "monitorenter", 0); Add(195, "monitorexit", 0);
        Add(196, "wide", -1);
        Add(197, "multianewarray", 3, OperandKind.MultiArray);
        Add(198, "ifnull", 2, OperandKind.BranchOffset, branch: true);
        Add(199, "ifnonnull", 2, OperandKind.BranchOffset, branch: true);
        Add(200, "goto_w", 4, OperandKind.BranchOffsetW, branch: true, unconditional: true);
        Add(201, "jsr_w", 4, OperandKind.BranchOffsetW, branch: true);
    }

    public static InstructionSpec? Of(byte opcode) => Specs[opcode];

    private static string MathMnemonic(int i)
    {
        string[] t = { "i", "l", "f", "d" }; string[] op = { "add", "sub", "mul", "div", "rem", "neg" };
        int baseN = i - 96;
        if (baseN < 24) return t[baseN / 6] + op[baseN % 6];
        return i switch
        {
            120 or 122 or 124 or 126 => (i == 120 ? "i" : i == 122 ? "l" : i == 124 ? "f" : "d") + "shl",
            121 or 123 or 125 or 127 => (i == 121 ? "i" : i == 123 ? "l" : i == 125 ? "f" : "d") + "shr",
            _ when i >= 128 && i <= 131 => (i % 2 == 0 ? "i" : "l") + (i is 128 or 129 ? "and" : i is 130 ? "or" : "xor"),
            _ => $"op{i}"
        };
    }

    private static string ConvMnemonic(int i) => i switch
    {
        133 => "i2l", 134 => "i2f", 135 => "i2d", 136 => "l2i", 137 => "l2f", 138 => "l2d",
        139 => "f2i", 140 => "f2l", 141 => "f2d", 142 => "d2i", 143 => "d2l", 144 => "d2f",
        145 => "i2b", 146 => "i2c", 147 => "i2s",
        148 => "lcmp", 149 => "fcmpl", 150 => "fcmpg", 151 => "dcmpl", 152 => "dcmpg",
        _ => $"op{i}"
    };
}
