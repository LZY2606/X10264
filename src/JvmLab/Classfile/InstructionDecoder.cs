namespace JvmLab.Classfile;

/// <summary>Decodes the JVM instruction set, preserving byte offsets.</summary>
public static class InstructionDecoder
{
    // (mnemonic, operand bytes); -1 means variable-length encoding.
    private static readonly (string Name, int Operands)[] Table = new (string, int)[256];

    static InstructionDecoder()
    {
        for (var i = 0; i < 256; i++) Table[i] = ($"op_{i:x2}", 0);
        void Op(int code, string name, int operands = 0) => Table[code] = (name, operands);

        Op(0x00, "nop"); Op(0x01, "aconst_null");
        Op(0x02, "iconst_m1"); Op(0x03, "iconst_0"); Op(0x04, "iconst_1"); Op(0x05, "iconst_2");
        Op(0x06, "iconst_3"); Op(0x07, "iconst_4"); Op(0x08, "iconst_5");
        Op(0x09, "lconst_0"); Op(0x0A, "lconst_1");
        Op(0x0B, "fconst_0"); Op(0x0C, "fconst_1"); Op(0x0D, "fconst_2");
        Op(0x0E, "dconst_0"); Op(0x0F, "dconst_1");
        Op(0x10, "bipush", 1); Op(0x11, "sipush", 2);
        Op(0x12, "ldc", 1); Op(0x13, "ldc_w", 2); Op(0x14, "ldc2_w", 2);
        Op(0x15, "iload", 1); Op(0x16, "lload", 1); Op(0x17, "fload", 1); Op(0x18, "dload", 1); Op(0x19, "aload", 1);
        for (var i = 0; i < 4; i++)
        {
            Op(0x1A + i, $"iload_{i}"); Op(0x1E + i, $"lload_{i}"); Op(0x22 + i, $"fload_{i}");
            Op(0x26 + i, $"dload_{i}"); Op(0x2A + i, $"aload_{i}");
        }
        Op(0x2E, "iaload"); Op(0x2F, "laload"); Op(0x30, "faload"); Op(0x31, "daload");
        Op(0x32, "aaload"); Op(0x33, "baload"); Op(0x34, "caload"); Op(0x35, "saload");
        Op(0x36, "istore", 1); Op(0x37, "lstore", 1); Op(0x38, "fstore", 1); Op(0x39, "dstore", 1); Op(0x3A, "astore", 1);
        for (var i = 0; i < 4; i++)
        {
            Op(0x3B + i, $"istore_{i}"); Op(0x3F + i, $"lstore_{i}"); Op(0x43 + i, $"fstore_{i}");
            Op(0x47 + i, $"dstore_{i}"); Op(0x4B + i, $"astore_{i}");
        }
        Op(0x4F, "iastore"); Op(0x50, "lastore"); Op(0x51, "fastore"); Op(0x52, "dastore");
        Op(0x53, "aastore"); Op(0x54, "bastore"); Op(0x55, "castore"); Op(0x56, "sastore");
        Op(0x57, "pop"); Op(0x58, "pop2"); Op(0x59, "dup"); Op(0x5A, "dup_x1"); Op(0x5B, "dup_x2");
        Op(0x5C, "dup2"); Op(0x5D, "dup2_x1"); Op(0x5E, "dup2_x2"); Op(0x5F, "swap");
        string[] arith = { "iadd", "ladd", "fadd", "dadd", "isub", "lsub", "fsub", "dsub",
            "imul", "lmul", "fmul", "dmul", "idiv", "ldiv", "fdiv", "ddiv",
            "irem", "lrem", "frem", "drem" };
        for (var i = 0; i < arith.Length; i++) Op(0x60 + i, arith[i]);
        Op(0x74, "ineg"); Op(0x75, "lneg"); Op(0x76, "fneg"); Op(0x77, "dneg");
        Op(0x78, "ishl"); Op(0x79, "lshl"); Op(0x7A, "ishr"); Op(0x7B, "lshr");
        Op(0x7C, "iushr"); Op(0x7D, "lushr"); Op(0x7E, "iand"); Op(0x7F, "land");
        Op(0x80, "ior"); Op(0x81, "lor"); Op(0x82, "ixor"); Op(0x83, "lxor");
        Op(0x84, "iinc", 2);
        string[] conv = { "i2l", "i2f", "i2d", "l2i", "l2f", "l2d", "f2i", "f2l", "f2d",
            "d2i", "d2l", "d2f", "i2b", "i2c", "i2s" };
        for (var i = 0; i < conv.Length; i++) Op(0x85 + i, conv[i]);
        Op(0x94, "lcmp"); Op(0x95, "fcmpl"); Op(0x96, "fcmpg"); Op(0x97, "dcmpl"); Op(0x98, "dcmpg");
        Op(0x99, "ifeq", 2); Op(0x9A, "ifne", 2); Op(0x9B, "iflt", 2);
        Op(0x9C, "ifge", 2); Op(0x9D, "ifgt", 2); Op(0x9E, "ifle", 2);
        Op(0x9F, "if_icmpeq", 2); Op(0xA0, "if_icmpne", 2); Op(0xA1, "if_icmplt", 2);
        Op(0xA2, "if_icmpge", 2); Op(0xA3, "if_icmpgt", 2); Op(0xA4, "if_icmple", 2);
        Op(0xA5, "if_acmpeq", 2); Op(0xA6, "if_acmpne", 2);
        Op(0xA7, "goto", 2); Op(0xA8, "jsr", 2); Op(0xA9, "ret", 1);
        Op(0xAA, "tableswitch", -1); Op(0xAB, "lookupswitch", -1);
        Op(0xAC, "ireturn"); Op(0xAD, "lreturn"); Op(0xAE, "freturn");
        Op(0xAF, "dreturn"); Op(0xB0, "areturn"); Op(0xB1, "return");
        Op(0xB2, "getstatic", 2); Op(0xB3, "putstatic", 2);
        Op(0xB4, "getfield", 2); Op(0xB5, "putfield", 2);
        Op(0xB6, "invokevirtual", 2); Op(0xB7, "invokespecial", 2); Op(0xB8, "invokestatic", 2);
        Op(0xB9, "invokeinterface", 4); Op(0xBA, "invokedynamic", 4);
        Op(0xBB, "new", 2); Op(0xBC, "newarray", 1); Op(0xBD, "anewarray", 2);
        Op(0xBE, "arraylength"); Op(0xBF, "athrow");
        Op(0xC0, "checkcast", 2); Op(0xC1, "instanceof", 2);
        Op(0xC2, "monitorenter"); Op(0xC3, "monitorexit");
        Op(0xC4, "wide", -1); Op(0xC5, "multianewarray", 3);
        Op(0xC6, "ifnull", 2); Op(0xC7, "ifnonnull", 2);
        Op(0xC8, "goto_w", 4); Op(0xC9, "jsr_w", 4);
    }

    private static int S2(byte[] b, int i) => (short)((b[i] << 8) | b[i + 1]);
    private static int S4(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

    public static List<Instruction> Decode(byte[] code, long codeFileOffset)
    {
        var result = new List<Instruction>();
        var pc = 0;
        while (pc < code.Length)
        {
            var opcode = code[pc];
            var (name, operandCount) = Table[opcode];
            var start = pc;
            Instruction ins;
            if (operandCount >= 0)
            {
                var operands = code.Skip(pc + 1).Take(operandCount).ToArray();
                ins = new Instruction
                {
                    Offset = start,
                    FileOffset = Quantity.Bytes(codeFileOffset + start, "classFileStart",
                        Convert.ToHexString(code, start, 1 + operandCount)),
                    Opcode = opcode, Mnemonic = name, Operands = operands
                };
                pc += 1 + operandCount;
            }
            else if (opcode == 0xAA) // tableswitch
            {
                var pad = (4 - ((pc + 1) % 4)) % 4;
                var p = pc + 1 + pad;
                var def = S4(code, p); var low = S4(code, p + 4); var high = S4(code, p + 8);
                var count = Math.Max(0, high - low + 1);
                var operands = code.Skip(pc + 1).Take(pad + 12 + count * 4).ToArray();
                ins = new Instruction
                {
                    Offset = start,
                    FileOffset = Quantity.Bytes(codeFileOffset + start, "classFileStart",
                        Convert.ToHexString(code, start, 1 + operands.Length)),
                    Opcode = opcode, Mnemonic = name, Operands = operands
                };
                ins.SwitchTargets.Add(start + def);
                for (var i = 0; i < count; i++) ins.SwitchTargets.Add(start + S4(code, p + 12 + i * 4));
                pc = p + 12 + count * 4;
            }
            else if (opcode == 0xAB) // lookupswitch
            {
                var pad = (4 - ((pc + 1) % 4)) % 4;
                var p = pc + 1 + pad;
                var def = S4(code, p); var npairs = Math.Max(0, S4(code, p + 4));
                var operands = code.Skip(pc + 1).Take(pad + 8 + npairs * 8).ToArray();
                ins = new Instruction
                {
                    Offset = start,
                    FileOffset = Quantity.Bytes(codeFileOffset + start, "classFileStart",
                        Convert.ToHexString(code, start, 1 + operands.Length)),
                    Opcode = opcode, Mnemonic = name, Operands = operands
                };
                ins.SwitchTargets.Add(start + def);
                for (var i = 0; i < npairs; i++) ins.SwitchTargets.Add(start + S4(code, p + 8 + i * 8 + 4));
                pc = p + 8 + npairs * 8;
            }
            else // wide
            {
                var sub = code[pc + 1];
                var len = sub == 0x84 ? 5 : 3;
                var operands = code.Skip(pc + 1).Take(len).ToArray();
                ins = new Instruction
                {
                    Offset = start,
                    FileOffset = Quantity.Bytes(codeFileOffset + start, "classFileStart",
                        Convert.ToHexString(code, start, 1 + len)),
                    Opcode = opcode, Mnemonic = $"wide {Table[sub].Name}", Operands = operands
                };
                pc += 1 + len;
            }

            switch (opcode)
            {
                case >= 0x99 and <= 0xA8:
                case 0xC6 or 0xC7:
                    ins.BranchTarget = start + S2(code, start + 1);
                    break;
                case 0xC8 or 0xC9:
                    ins.BranchTarget = start + S4(code, start + 1);
                    break;
            }
            ins.CpIndex = opcode switch
            {
                0x12 => code[start + 1],
                0x13 or 0x14 or (>= 0xB2 and <= 0xBA) or 0xBB or 0xBD or 0xC0 or 0xC1
                    => (code[start + 1] << 8) | code[start + 2],
                _ => null
            };
            result.Add(ins);
        }
        return result;
    }
}
