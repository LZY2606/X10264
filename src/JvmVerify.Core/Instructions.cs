namespace JvmVerify.Core;

public enum FlowKind { Next, Branch, Conditional, Switch, Return, Throw, Unsupported }

/// <summary>A decoded instruction. Original byte offset and size are preserved.</summary>
public sealed record Insn(
    int Offset,
    byte Opcode,
    string Mnemonic,
    int[] Operands,
    int Size,
    FlowKind Flow,
    int[] BranchTargets,
    ushort CpIndex = 0)
{
    public override string ToString()
    {
        var ops = Operands.Length == 0 ? "" : " " + string.Join(",", Operands);
        var cp = CpIndex == 0 ? "" : $"  // cp#{CpIndex}";
        return $"{Offset,4}: {Mnemonic}{ops}{cp}";
    }
}

public static class InstructionDecoder
{
    /// <summary>Decodes the full code array; unknown opcodes decode as Unsupported (size 1).</summary>
    public static List<Insn> Decode(byte[] code)
    {
        var list = new List<Insn>();
        int pc = 0;
        while (pc < code.Length)
        {
            var insn = DecodeOne(code, pc);
            list.Add(insn);
            pc += insn.Size;
        }
        return list;
    }

    private static int S1(byte[] c, int p) => (sbyte)c[p];
    private static int S2(byte[] c, int p) => (short)((c[p] << 8) | c[p + 1]);
    private static int U2(byte[] c, int p) => (c[p] << 8) | c[p + 1];
    private static int S4(byte[] c, int p) => (c[p] << 24) | (c[p + 1] << 16) | (c[p + 2] << 8) | c[p + 3];

    public static Insn DecodeOne(byte[] c, int pc)
    {
        byte op = c[pc];
        switch (op)
        {
            case 0x00: return N(pc, op, "nop");
            case 0x01: return N(pc, op, "aconst_null");
            case >= 0x02 and <= 0x08: return N(pc, op, $"iconst_{op - 0x03}");
            case 0x09: return N(pc, op, "lconst_0");
            case 0x0a: return N(pc, op, "lconst_1");
            case >= 0x0b and <= 0x0d: return N(pc, op, $"fconst_{op - 0x0b}");
            case >= 0x0e and <= 0x0f: return N(pc, op, $"dconst_{op - 0x0e}");
            case 0x10: return O(pc, op, "bipush", 2, S1(c, pc + 1));
            case 0x11: return O(pc, op, "sipush", 3, S2(c, pc + 1));
            case 0x12: return Cp(pc, op, "ldc", 2, c[pc + 1]);
            case 0x13: return Cp(pc, op, "ldc_w", 3, U2(c, pc + 1));
            case 0x14: return Cp(pc, op, "ldc2_w", 3, U2(c, pc + 1));
            case 0x15: return O(pc, op, "iload", 2, c[pc + 1]);
            case 0x16: return O(pc, op, "lload", 2, c[pc + 1]);
            case 0x17: return O(pc, op, "fload", 2, c[pc + 1]);
            case 0x18: return O(pc, op, "dload", 2, c[pc + 1]);
            case 0x19: return O(pc, op, "aload", 2, c[pc + 1]);
            case >= 0x1a and <= 0x2d:
                {
                    string[] names = { "iload", "lload", "fload", "dload", "aload" };
                    int idx = (op - 0x1a) % 4;
                    return N(pc, op, $"{names[(op - 0x1a) / 4]}_{idx}", 1, idx);
                }
            case >= 0x2e and <= 0x35:
                {
                    string[] names = { "iaload", "laload", "faload", "daload", "aaload", "baload", "caload", "saload" };
                    return N(pc, op, names[op - 0x2e]);
                }
            case 0x36: return O(pc, op, "istore", 2, c[pc + 1]);
            case 0x37: return O(pc, op, "lstore", 2, c[pc + 1]);
            case 0x38: return O(pc, op, "fstore", 2, c[pc + 1]);
            case 0x39: return O(pc, op, "dstore", 2, c[pc + 1]);
            case 0x3a: return O(pc, op, "astore", 2, c[pc + 1]);
            case >= 0x3b and <= 0x4e:
                {
                    string[] names = { "istore", "lstore", "fstore", "dstore", "astore" };
                    int idx = (op - 0x3b) % 4;
                    return N(pc, op, $"{names[(op - 0x3b) / 4]}_{idx}", 1, idx);
                }
            case >= 0x4f and <= 0x56:
                {
                    string[] names = { "iastore", "lastore", "fastore", "dastore", "aastore", "bastore", "castore", "sastore" };
                    return N(pc, op, names[op - 0x4f]);
                }
            case 0x57: return N(pc, op, "pop");
            case 0x58: return N(pc, op, "pop2");
            case 0x59: return N(pc, op, "dup");
            case 0x5a: return N(pc, op, "dup_x1");
            case 0x5b: return N(pc, op, "dup_x2");
            case 0x5c: return N(pc, op, "dup2");
            case 0x5d: return N(pc, op, "dup2_x1");
            case 0x5e: return N(pc, op, "dup2_x2");
            case 0x5f: return N(pc, op, "swap");
            case >= 0x60 and <= 0x83:
                {
                    string[] names = {
                        "iadd","ladd","fadd","dadd","isub","lsub","fsub","dsub",
                        "imul","lmul","fmul","dmul","idiv","ldiv","fdiv","ddiv",
                        "irem","lrem","frem","drem","ineg","lneg","fneg","dneg",
                        "ishl","lshl","ishr","lshr","iushr","lushr","iand","land",
                        "ior","lor","ixor","lxor" };
                    return N(pc, op, names[op - 0x60]);
                }
            case 0x84: return O(pc, op, "iinc", 3, c[pc + 1], S1(c, pc + 2));
            case >= 0x85 and <= 0x93:
                {
                    string[] names = {
                        "i2l","i2f","i2d","l2i","l2f","l2d","f2i","f2l","f2d",
                        "d2i","d2l","d2f","i2b","i2c","i2s" };
                    return N(pc, op, names[op - 0x85]);
                }
            case >= 0x94 and <= 0x98:
                {
                    string[] names = { "lcmp", "fcmpl", "fcmpg", "dcmpl", "dcmpg" };
                    return N(pc, op, names[op - 0x94]);
                }
            case >= 0x99 and <= 0xa6:
                {
                    string[] names = {
                        "ifeq","ifne","iflt","ifge","ifgt","ifle",
                        "if_icmpeq","if_icmpne","if_icmplt","if_icmpge","if_icmpgt","if_icmple",
                        "if_acmpeq","if_acmpne" };
                    int target = pc + S2(c, pc + 1);
                    return new Insn(pc, op, names[op - 0x99], new[] { target }, 3,
                        FlowKind.Conditional, new[] { target });
                }
            case 0xa7:
                {
                    int target = pc + S2(c, pc + 1);
                    return new Insn(pc, op, "goto", new[] { target }, 3, FlowKind.Branch, new[] { target });
                }
            case 0xa8:
                {
                    int target = pc + S2(c, pc + 1);
                    return new Insn(pc, op, "jsr", new[] { target }, 3, FlowKind.Unsupported, new[] { target });
                }
            case 0xaa: // tableswitch
                {
                    int p = pc + 1;
                    p += (4 - (p % 4)) % 4;
                    int def = pc + S4(c, p); p += 4;
                    int low = S4(c, p); p += 4;
                    int high = S4(c, p); p += 4;
                    int n = high - low + 1;
                    if (n < 0 || n > 65536) n = 0;
                    var targets = new List<int> { def };
                    var ops = new List<int> { def, low, high };
                    for (int i = 0; i < n; i++) { int t = pc + S4(c, p); p += 4; targets.Add(t); ops.Add(t); }
                    return new Insn(pc, op, "tableswitch", ops.ToArray(), p - pc, FlowKind.Switch, targets.ToArray());
                }
            case 0xab: // lookupswitch
                {
                    int p = pc + 1;
                    p += (4 - (p % 4)) % 4;
                    int def = pc + S4(c, p); p += 4;
                    int npairs = S4(c, p); p += 4;
                    if (npairs < 0 || npairs > 65536) npairs = 0;
                    var targets = new List<int> { def };
                    var ops = new List<int> { def, npairs };
                    for (int i = 0; i < npairs; i++)
                    {
                        int match = S4(c, p); int t = pc + S4(c, p + 4); p += 8;
                        targets.Add(t); ops.Add(match); ops.Add(t);
                    }
                    return new Insn(pc, op, "lookupswitch", ops.ToArray(), p - pc, FlowKind.Switch, targets.ToArray());
                }
            case >= 0xac and <= 0xb1:
                {
                    string[] names = { "ireturn", "lreturn", "freturn", "dreturn", "areturn", "return" };
                    return new Insn(pc, op, names[op - 0xac], Array.Empty<int>(), 1, FlowKind.Return, Array.Empty<int>());
                }
            case 0xb2: return Cp(pc, op, "getstatic", 3, U2(c, pc + 1));
            case 0xb3: return Cp(pc, op, "putstatic", 3, U2(c, pc + 1));
            case 0xb4: return Cp(pc, op, "getfield", 3, U2(c, pc + 1));
            case 0xb5: return Cp(pc, op, "putfield", 3, U2(c, pc + 1));
            case 0xb6: return Cp(pc, op, "invokevirtual", 3, U2(c, pc + 1));
            case 0xb7: return Cp(pc, op, "invokespecial", 3, U2(c, pc + 1));
            case 0xb8: return Cp(pc, op, "invokestatic", 3, U2(c, pc + 1));
            case 0xb9: return Cp(pc, op, "invokeinterface", 5, U2(c, pc + 1));
            case 0xba: return Cp(pc, op, "invokedynamic", 5, U2(c, pc + 1));
            case 0xbb: return Cp(pc, op, "new", 3, U2(c, pc + 1));
            case 0xbc: return O(pc, op, "newarray", 2, c[pc + 1]);
            case 0xbd: return Cp(pc, op, "anewarray", 3, U2(c, pc + 1));
            case 0xbe: return N(pc, op, "arraylength");
            case 0xbf: return new Insn(pc, op, "athrow", Array.Empty<int>(), 1, FlowKind.Throw, Array.Empty<int>());
            case 0xc0: return Cp(pc, op, "checkcast", 3, U2(c, pc + 1));
            case 0xc1: return Cp(pc, op, "instanceof", 3, U2(c, pc + 1));
            case 0xc2: return N(pc, op, "monitorenter");
            case 0xc3: return N(pc, op, "monitorexit");
            case 0xc4: // wide
                {
                    byte sub = c[pc + 1];
                    if (sub == 0x84)
                        return O(pc, op, "wide iinc", 6, U2(c, pc + 2), S2(c, pc + 3));
                    return O(pc, op, $"wide {sub:x2}", 4, U2(c, pc + 2));
                }
            case 0xc5: return Cp(pc, op, "multianewarray", 4, U2(c, pc + 1));
            case 0xc6:
                {
                    int target = pc + S2(c, pc + 1);
                    return new Insn(pc, op, "ifnull", new[] { target }, 3, FlowKind.Conditional, new[] { target });
                }
            case 0xc7:
                {
                    int target = pc + S2(c, pc + 1);
                    return new Insn(pc, op, "ifnonnull", new[] { target }, 3, FlowKind.Conditional, new[] { target });
                }
            case 0xc8:
                {
                    int target = pc + S4(c, pc + 1);
                    return new Insn(pc, op, "goto_w", new[] { target }, 5, FlowKind.Branch, new[] { target });
                }
            case 0xc9:
                {
                    int target = pc + S4(c, pc + 1);
                    return new Insn(pc, op, "jsr_w", new[] { target }, 5, FlowKind.Unsupported, new[] { target });
                }
            case 0xca: return N(pc, op, "breakpoint");
            default:
                return new Insn(pc, op, $"opcode_0x{op:x2}", Array.Empty<int>(), 1, FlowKind.Unsupported, Array.Empty<int>());
        }
    }

    private static Insn N(int pc, byte op, string m, int size = 1, params int[] ops)
        => new(pc, op, m, ops, size, FlowKind.Next, Array.Empty<int>());

    private static Insn O(int pc, byte op, string m, int size, params int[] ops)
        => new(pc, op, m, ops, size, FlowKind.Next, Array.Empty<int>());

    private static Insn Cp(int pc, byte op, string m, int size, int cpIndex)
        => new(pc, op, m, new[] { cpIndex }, size, FlowKind.Next, Array.Empty<int>(), (ushort)cpIndex);
}
