namespace BytecodeSeal.Core;

public sealed record Instruction(
    int Offset, string Mnemonic, int Opcode, int Length,
    int? CpIndex = null, int? BranchTarget = null,
    IReadOnlyList<int>? SwitchTargets = null, int? LocalIndex = null, int? IntOperand = null)
{
    public bool IsBranch => BranchTarget.HasValue || SwitchTargets is { Count: > 0 };
    public IEnumerable<int> AllTargets()
    {
        if (BranchTarget is int t) yield return t;
        if (SwitchTargets is { } ts) foreach (var t in ts) yield return t;
    }
}

public static class BytecodeDecoder
{
    // local-indexed opcodes: loads/stores with implicit index
    private static readonly Dictionary<int, (string name, int len, int? local)> Fixed = new()
    {
        [0x00] = ("nop", 1, null),
        [0x01] = ("aconst_null", 1, null),
        [0x02] = ("iconst_m1", 1, null), [0x03] = ("iconst_0", 1, null), [0x04] = ("iconst_1", 1, null),
        [0x05] = ("iconst_2", 1, null), [0x06] = ("iconst_3", 1, null), [0x07] = ("iconst_4", 1, null),
        [0x08] = ("iconst_5", 1, null),
        [0x09] = ("lconst_0", 1, null), [0x0A] = ("lconst_1", 1, null),
        [0x0B] = ("fconst_0", 1, null),
        [0x1A] = ("iload_0", 1, 0), [0x1B] = ("iload_1", 1, 1), [0x1C] = ("iload_2", 1, 2), [0x1D] = ("iload_3", 1, 3),
        [0x1E] = ("lload_0", 1, 0), [0x1F] = ("lload_1", 1, 1), [0x20] = ("lload_2", 1, 2), [0x21] = ("lload_3", 1, 3),
        [0x2A] = ("aload_0", 1, 0), [0x2B] = ("aload_1", 1, 1), [0x2C] = ("aload_2", 1, 2), [0x2D] = ("aload_3", 1, 3),
        [0x3B] = ("istore_0", 1, 0), [0x3C] = ("istore_1", 1, 1), [0x3D] = ("istore_2", 1, 2), [0x3E] = ("istore_3", 1, 3),
        [0x3F] = ("lstore_0", 1, 0), [0x40] = ("lstore_1", 1, 1), [0x41] = ("lstore_2", 1, 2), [0x42] = ("lstore_3", 1, 3),
        [0x4B] = ("astore_0", 1, 0), [0x4C] = ("astore_1", 1, 1), [0x4D] = ("astore_2", 1, 2), [0x4E] = ("astore_3", 1, 3),
        [0x57] = ("pop", 1, null), [0x58] = ("pop2", 1, null),
        [0x59] = ("dup", 1, null), [0x5C] = ("dup2", 1, null),
        [0x60] = ("iadd", 1, null), [0x61] = ("ladd", 1, null), [0x64] = ("isub", 1, null),
        [0xAC] = ("ireturn", 1, null), [0xAD] = ("lreturn", 1, null),
        [0xAE] = ("freturn", 1, null), [0xB0] = ("areturn", 1, null), [0xB1] = ("return", 1, null),
        [0xBE] = ("arraylength", 1, null), [0xBF] = ("athrow", 1, null),
        [0xC2] = ("monitorenter", 1, null), [0xC3] = ("monitorexit", 1, null),
    };

    public static List<Instruction> Decode(byte[] code)
    {
        var list = new List<Instruction>();
        int p = 0;
        while (p < code.Length)
        {
            int start = p;
            int op = code[p++];
            if (Fixed.TryGetValue(op, out var f))
            {
                list.Add(new Instruction(start, f.name, op, 1, LocalIndex: f.local));
                continue;
            }
            switch (op)
            {
                case 0x10: list.Add(new(start, "bipush", op, 2, IntOperand: (sbyte)code[p])); p += 1; break;
                case 0x11: list.Add(new(start, "sipush", op, 3, IntOperand: (short)((code[p] << 8) | code[p+1]))); p += 2; break;
                case 0x12: list.Add(new(start, "ldc", op, 2, CpIndex: code[p])); p += 1; break;
                case 0x13: case 0x14: list.Add(new(start, op == 0x13 ? "ldc_w" : "ldc2_w", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0x15: list.Add(new(start, "iload", op, 2, LocalIndex: code[p])); p += 1; break;
                case 0x16: list.Add(new(start, "lload", op, 2, LocalIndex: code[p])); p += 1; break;
                case 0x19: list.Add(new(start, "aload", op, 2, LocalIndex: code[p])); p += 1; break;
                case 0x36: list.Add(new(start, "istore", op, 2, LocalIndex: code[p])); p += 1; break;
                case 0x37: list.Add(new(start, "lstore", op, 2, LocalIndex: code[p])); p += 1; break;
                case 0x3A: list.Add(new(start, "astore", op, 2, LocalIndex: code[p])); p += 1; break;
                case 0x84: list.Add(new(start, "iinc", op, 3, LocalIndex: code[p], IntOperand: (sbyte)code[p+1])); p += 2; break;
                case 0x99: case 0x9A: case 0x9B: case 0x9C: case 0x9D: case 0x9E:
                case 0x9F: case 0xA0: case 0xA1: case 0xA2: case 0xA3: case 0xA4:
                case 0xA5: case 0xA6: case 0xC6: case 0xC7:
                {
                    string name = op switch
                    {
                        0x99 => "ifeq", 0x9A => "ifne", 0x9B => "iflt", 0x9C => "ifge",
                        0x9D => "ifgt", 0x9E => "ifle", 0x9F => "if_icmpeq", 0xA0 => "if_icmpne",
                        0xA1 => "if_icmplt", 0xA2 => "if_icmpge", 0xA3 => "if_icmpgt", 0xA4 => "if_icmple",
                        0xA5 => "if_acmpeq", 0xA6 => "if_acmpne", 0xC6 => "ifnull", _ => "ifnonnull"
                    };
                    int t = start + (short)((code[p] << 8) | code[p+1]);
                    list.Add(new(start, name, op, 3, BranchTarget: t)); p += 2; break;
                }
                case 0xA7:
                {
                    int t = start + (short)((code[p] << 8) | code[p+1]);
                    list.Add(new(start, "goto", op, 3, BranchTarget: t)); p += 2; break;
                }
                case 0xAA: // tableswitch
                {
                    int pad = (4 - (p % 4)) % 4; p += pad;
                    int def = start + (int)U4(code, p); p += 4;
                    int low = (int)U4(code, p); p += 4;
                    int high = (int)U4(code, p); p += 4;
                    var targets = new List<int> { def };
                    for (int i = low; i <= high; i++) { targets.Add(start + (int)U4(code, p)); p += 4; }
                    list.Add(new(start, "tableswitch", op, p - start, SwitchTargets: targets));
                    break;
                }
                case 0xAB: // lookupswitch
                {
                    int pad = (4 - (p % 4)) % 4; p += pad;
                    int def = start + (int)U4(code, p); p += 4;
                    int npairs = (int)U4(code, p); p += 4;
                    var targets = new List<int> { def };
                    for (int i = 0; i < npairs; i++) { p += 4; targets.Add(start + (int)U4(code, p)); p += 4; }
                    list.Add(new(start, "lookupswitch", op, p - start, SwitchTargets: targets));
                    break;
                }
                case 0xB2: list.Add(new(start, "getstatic", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xB3: list.Add(new(start, "putstatic", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xB4: list.Add(new(start, "getfield", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xB5: list.Add(new(start, "putfield", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xB6: list.Add(new(start, "invokevirtual", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xB7: list.Add(new(start, "invokespecial", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xB8: list.Add(new(start, "invokestatic", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xBB: list.Add(new(start, "new", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xC0: list.Add(new(start, "checkcast", op, 3, CpIndex: U2(code, p))); p += 2; break;
                case 0xC1: list.Add(new(start, "instanceof", op, 3, CpIndex: U2(code, p))); p += 2; break;
                default:
                    list.Add(new(start, $"unsupported_0x{op:X2}", op, 1));
                    break;
            }
        }
        return list;
    }

    private static int U2(byte[] c, int p) => (c[p] << 8) | c[p + 1];
    private static uint U4(byte[] c, int p) =>
        ((uint)c[p] << 24) | ((uint)c[p+1] << 16) | ((uint)c[p+2] << 8) | c[p+3];
}
