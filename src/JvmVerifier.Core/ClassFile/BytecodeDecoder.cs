namespace JvmVerifier.Core.ClassFile;

/// <summary>Decodes the code array into instructions with original byte offsets and branch targets.</summary>
public static class BytecodeDecoder
{
    // opcode -> (mnemonic, operand length, isBranchOperand)
    private static readonly Dictionary<int, (string Name, int Len, bool Branch, bool Cp)> Table = new()
    {
        [0x00] = ("nop", 0, false, false),
        [0x01] = ("aconst_null", 0, false, false),
        [0x02] = ("iconst_m1", 0, false, false),
        [0x03] = ("iconst_0", 0, false, false),
        [0x04] = ("iconst_1", 0, false, false),
        [0x05] = ("iconst_2", 0, false, false),
        [0x06] = ("iconst_3", 0, false, false),
        [0x07] = ("iconst_4", 0, false, false),
        [0x08] = ("iconst_5", 0, false, false),
        [0x09] = ("lconst_0", 0, false, false),
        [0x0A] = ("lconst_1", 0, false, false),
        [0x0E] = ("dconst_0", 0, false, false),
        [0x0F] = ("dconst_1", 0, false, false),
        [0x10] = ("bipush", 1, false, false),
        [0x12] = ("ldc", 1, false, true),
        [0x14] = ("ldc2_w", 2, false, true),
        [0x15] = ("iload", 1, false, false),
        [0x16] = ("lload", 1, false, false),
        [0x17] = ("fload", 1, false, false),
        [0x18] = ("dload", 1, false, false),
        [0x19] = ("aload", 1, false, false),
        [0x1A] = ("iload_0", 0, false, false),
        [0x1B] = ("iload_1", 0, false, false),
        [0x1C] = ("iload_2", 0, false, false),
        [0x1D] = ("iload_3", 0, false, false),
        [0x1E] = ("lload_0", 0, false, false),
        [0x1F] = ("lload_1", 0, false, false),
        [0x20] = ("lload_2", 0, false, false),
        [0x21] = ("lload_3", 0, false, false),
        [0x2A] = ("aload_0", 0, false, false),
        [0x2B] = ("aload_1", 0, false, false),
        [0x2C] = ("aload_2", 0, false, false),
        [0x2D] = ("aload_3", 0, false, false),
        [0x36] = ("istore", 1, false, false),
        [0x37] = ("lstore", 1, false, false),
        [0x39] = ("dstore", 1, false, false),
        [0x3A] = ("astore", 1, false, false),
        [0x3B] = ("istore_0", 0, false, false),
        [0x3C] = ("istore_1", 0, false, false),
        [0x3D] = ("istore_2", 0, false, false),
        [0x3E] = ("istore_3", 0, false, false),
        [0x3F] = ("lstore_0", 0, false, false),
        [0x40] = ("lstore_1", 0, false, false),
        [0x41] = ("lstore_2", 0, false, false),
        [0x42] = ("lstore_3", 0, false, false),
        [0x4B] = ("astore_0", 0, false, false),
        [0x4C] = ("astore_1", 0, false, false),
        [0x4D] = ("astore_2", 0, false, false),
        [0x4E] = ("astore_3", 0, false, false),
        [0x57] = ("pop", 0, false, false),
        [0x59] = ("dup", 0, false, false),
        [0x5F] = ("swap", 0, false, false),
        [0x60] = ("iadd", 0, false, false),
        [0x61] = ("ladd", 0, false, false),
        [0x64] = ("isub", 0, false, false),
        [0x68] = ("imul", 0, false, false),
        [0x6B] = ("dmul", 0, false, false),
        [0x7E] = ("iand", 0, false, false),
        [0x84] = ("iinc", 2, false, false),
        [0x99] = ("ifeq", 2, true, false),
        [0x9A] = ("ifne", 2, true, false),
        [0x9F] = ("if_icmpeq", 2, true, false),
        [0xA0] = ("if_icmpne", 2, true, false),
        [0xA1] = ("if_icmplt", 2, true, false),
        [0xA2] = ("if_icmpge", 2, true, false),
        [0xA7] = ("goto", 2, true, false),
        [0xAC] = ("ireturn", 0, false, false),
        [0xAD] = ("lreturn", 0, false, false),
        [0xB0] = ("areturn", 0, false, false),
        [0xB1] = ("return", 0, false, false),
        [0xB2] = ("getstatic", 2, false, true),
        [0xB4] = ("getfield", 2, false, true),
        [0xB5] = ("putfield", 2, false, true),
        [0xB6] = ("invokevirtual", 2, false, true),
        [0xB7] = ("invokespecial", 2, false, true),
        [0xB8] = ("invokestatic", 2, false, true),
        [0xBB] = ("new", 2, false, true),
        [0xBD] = ("anewarray", 2, false, true),
        [0xBF] = ("athrow", 0, false, false),
        [0xC0] = ("checkcast", 2, false, true),
    };

    public static List<InstructionInfo> Decode(byte[] code, CpEntry?[] cp)
    {
        var list = new List<InstructionInfo>();
        int pc = 0;
        while (pc < code.Length)
        {
            int op = code[pc];
            if (!Table.TryGetValue(op, out var info))
            {
                list.Add(new InstructionInfo(pc, op, $"unknown_0x{op:X2}", Array.Empty<int>(), 1, null, null, null));
                pc += 1;
                continue;
            }
            var operands = new int[info.Len];
            for (int i = 0; i < info.Len; i++)
                operands[i] = pc + 1 + i < code.Length ? code[pc + 1 + i] : 0;
            int? branch = null;
            int? cpIndex = null;
            string? cpRef = null;
            if (info.Branch && info.Len == 2)
            {
                short rel = (short)((operands[0] << 8) | operands[1]);
                branch = pc + rel;
            }
            if (info.Cp)
            {
                cpIndex = info.Len == 1 ? operands[0] : (operands[0] << 8) | operands[1];
                if (cpIndex > 0 && cpIndex < cp.Length && cp[cpIndex] != null)
                    cpRef = $"{cp[cpIndex]!.TagName} {cp[cpIndex]!.Value}";
            }
            list.Add(new InstructionInfo(pc, op, info.Name, operands, 1 + info.Len, branch, cpIndex, cpRef));
            pc += 1 + info.Len;
        }
        return list;
    }
}
