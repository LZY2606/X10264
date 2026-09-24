namespace JvmVerifier.Core;

/// <summary>A decoded instruction. Offset is the original bytecode offset.</summary>
public sealed record Instruction(
    int Offset, int Opcode, string Mnemonic, byte[] Operands,
    int? BranchTarget = null, int? CpIndex = null, int? LocalIndex = null,
    long? ConstValue = null)
{
    public int Length => 1 + Operands.Length;
    public int NextOffset => Offset + Length;
}

public static class Bytecode
{
    private static readonly Dictionary<int, (string Name, int OperandBytes)> Table = new()
    {
        [0x00] = ("nop", 0), [0x01] = ("aconst_null", 0),
        [0x02] = ("iconst_m1", 0), [0x03] = ("iconst_0", 0), [0x04] = ("iconst_1", 0),
        [0x05] = ("iconst_2", 0), [0x06] = ("iconst_3", 0), [0x07] = ("iconst_4", 0), [0x08] = ("iconst_5", 0),
        [0x09] = ("lconst_0", 0), [0x0A] = ("lconst_1", 0),
        [0x0B] = ("fconst_0", 0), [0x0E] = ("dconst_0", 0),
        [0x10] = ("bipush", 1), [0x11] = ("sipush", 2),
        [0x12] = ("ldc", 1), [0x13] = ("ldc_w", 2), [0x14] = ("ldc2_w", 2),
        [0x15] = ("iload", 1), [0x16] = ("lload", 1), [0x17] = ("fload", 1), [0x18] = ("dload", 1), [0x19] = ("aload", 1),
        [0x1A] = ("iload_0", 0), [0x1B] = ("iload_1", 0), [0x1C] = ("iload_2", 0), [0x1D] = ("iload_3", 0),
        [0x1E] = ("lload_0", 0), [0x1F] = ("lload_1", 0), [0x20] = ("lload_2", 0), [0x21] = ("lload_3", 0),
        [0x22] = ("fload_0", 0), [0x23] = ("fload_1", 0), [0x24] = ("fload_2", 0), [0x25] = ("fload_3", 0),
        [0x26] = ("dload_0", 0), [0x27] = ("dload_1", 0), [0x28] = ("dload_2", 0), [0x29] = ("dload_3", 0),
        [0x2A] = ("aload_0", 0), [0x2B] = ("aload_1", 0), [0x2C] = ("aload_2", 0), [0x2D] = ("aload_3", 0),
        [0x36] = ("istore", 1), [0x37] = ("lstore", 1), [0x38] = ("fstore", 1), [0x39] = ("dstore", 1), [0x3A] = ("astore", 1),
        [0x3B] = ("istore_0", 0), [0x3C] = ("istore_1", 0), [0x3D] = ("istore_2", 0), [0x3E] = ("istore_3", 0),
        [0x3F] = ("lstore_0", 0), [0x40] = ("lstore_1", 0), [0x41] = ("lstore_2", 0), [0x42] = ("lstore_3", 0),
        [0x43] = ("fstore_0", 0), [0x44] = ("fstore_1", 0), [0x45] = ("fstore_2", 0), [0x46] = ("fstore_3", 0),
        [0x47] = ("dstore_0", 0), [0x48] = ("dstore_1", 0), [0x49] = ("dstore_2", 0), [0x4A] = ("dstore_3", 0),
        [0x4B] = ("astore_0", 0), [0x4C] = ("astore_1", 0), [0x4D] = ("astore_2", 0), [0x4E] = ("astore_3", 0),
        [0x57] = ("pop", 0), [0x58] = ("pop2", 0), [0x59] = ("dup", 0), [0x5C] = ("dup2", 0),
        [0x60] = ("iadd", 0), [0x61] = ("ladd", 0), [0x62] = ("fadd", 0), [0x63] = ("dadd", 0),
        [0x64] = ("isub", 0), [0x65] = ("lsub", 0), [0x68] = ("imul", 0), [0x6C] = ("idiv", 0),
        [0x70] = ("irem", 0), [0x74] = ("ineg", 0),
        [0x78] = ("ishl", 0), [0x7A] = ("ishr", 0), [0x7E] = ("iand", 0), [0x80] = ("ior", 0),
        [0x84] = ("iinc", 2),
        [0x85] = ("i2l", 0), [0x86] = ("i2f", 0), [0x87] = ("i2d", 0),
        [0x88] = ("l2i", 0), [0x8B] = ("f2i", 0),
        [0x99] = ("ifeq", 2), [0x9A] = ("ifne", 2), [0x9B] = ("iflt", 2), [0x9C] = ("ifge", 2),
        [0x9D] = ("ifgt", 2), [0x9E] = ("ifle", 2),
        [0x9F] = ("if_icmpeq", 2), [0xA0] = ("if_icmpne", 2), [0xA1] = ("if_icmplt", 2),
        [0xA2] = ("if_icmpge", 2), [0xA3] = ("if_icmpgt", 2), [0xA4] = ("if_icmple", 2),
        [0xA5] = ("if_acmpeq", 2), [0xA6] = ("if_acmpne", 2),
        [0xA7] = ("goto", 2), [0xA8] = ("jsr", 2),
        [0xAC] = ("ireturn", 0), [0xAD] = ("lreturn", 0), [0xAE] = ("freturn", 0),
        [0xAF] = ("dreturn", 0), [0xB0] = ("areturn", 0), [0xB1] = ("return", 0),
        [0xB2] = ("getstatic", 2), [0xB4] = ("getfield", 2), [0xB5] = ("putfield", 2),
        [0xB6] = ("invokevirtual", 2), [0xB7] = ("invokespecial", 2),
        [0xB8] = ("invokestatic", 2), [0xB9] = ("invokeinterface", 4),
        [0xBB] = ("new", 2), [0xBC] = ("newarray", 1),
        [0xBD] = ("anewarray", 2), [0xBE] = ("arraylength", 0),
        [0xBF] = ("athrow", 0), [0xC0] = ("checkcast", 2), [0xC1] = ("instanceof", 2),
        [0xC5] = ("multianewarray", 3),
    };

    public static bool IsKnown(int opcode) => Table.ContainsKey(opcode);
    public static string NameOf(int opcode) => Table.TryGetValue(opcode, out var e) ? e.Name : $"opcode_{opcode:X2}";

    public static List<Instruction> Decode(byte[] code)
    {
        var result = new List<Instruction>();
        int pc = 0;
        while (pc < code.Length)
        {
            int op = code[pc];
            if (!Table.TryGetValue(op, out var meta))
                throw new VerifyException("corrupt", pc, $"unknown opcode 0x{op:X2} at offset {pc}");
            int len = meta.OperandBytes;
            if (pc + 1 + len > code.Length)
                throw new VerifyException("corrupt", pc, $"truncated instruction at offset {pc}");
            var operands = code[(pc + 1)..(pc + 1 + len)];
            var ins = DecodeOperands(pc, op, meta.Name, operands);
            result.Add(ins);
            pc += 1 + len;
        }
        return result;
    }

    private static int S1(byte[] o, int i) => (sbyte)o[i];
    private static int S2(byte[] o, int i) => (short)(o[i] << 8 | o[i + 1]);
    private static int U1(byte[] o, int i) => o[i];
    private static int U2(byte[] o, int i) => o[i] << 8 | o[i + 1];

    private static Instruction DecodeOperands(int pc, int op, string name, byte[] o)
    {
        switch (name)
        {
            case "bipush": return new(pc, op, name, o, ConstValue: S1(o, 0));
            case "sipush": return new(pc, op, name, o, ConstValue: S2(o, 0));
            case "ldc": return new(pc, op, name, o, CpIndex: U1(o, 0));
            case "ldc_w" or "ldc2_w": return new(pc, op, name, o, CpIndex: U2(o, 0));
            case "iload" or "lload" or "fload" or "dload" or "aload"
              or "istore" or "lstore" or "fstore" or "dstore" or "astore":
                return new(pc, op, name, o, LocalIndex: U1(o, 0));
            case "iload_0" or "iload_1" or "iload_2" or "iload_3":
                return new(pc, op, name, o, LocalIndex: name[^1] - '0');
            case "lload_0" or "lload_1" or "lload_2" or "lload_3":
                return new(pc, op, name, o, LocalIndex: name[^1] - '0');
            case var n when n.StartsWith("fload_"): return new(pc, op, name, o, LocalIndex: n[^1] - '0');
            case var n when n.StartsWith("dload_"): return new(pc, op, name, o, LocalIndex: n[^1] - '0');
            case "aload_0" or "aload_1" or "aload_2" or "aload_3":
                return new(pc, op, name, o, LocalIndex: name[^1] - '0');
            case "istore_0" or "istore_1" or "istore_2" or "istore_3":
                return new(pc, op, name, o, LocalIndex: name[^1] - '0');
            case "lstore_0" or "lstore_1" or "lstore_2" or "lstore_3":
                return new(pc, op, name, o, LocalIndex: name[^1] - '0');
            case var n when n.StartsWith("fstore_"): return new(pc, op, name, o, LocalIndex: n[^1] - '0');
            case var n when n.StartsWith("dstore_"): return new(pc, op, name, o, LocalIndex: n[^1] - '0');
            case "astore_0" or "astore_1" or "astore_2" or "astore_3":
                return new(pc, op, name, o, LocalIndex: name[^1] - '0');
            case "iinc": return new(pc, op, name, o, LocalIndex: U1(o, 0), ConstValue: S1(o, 1));
            case "ifeq" or "ifne" or "iflt" or "ifge" or "ifgt" or "ifle"
              or "if_icmpeq" or "if_icmpne" or "if_icmplt" or "if_icmpge" or "if_icmpgt" or "if_icmple"
              or "if_acmpeq" or "if_acmpne" or "goto" or "jsr":
                return new(pc, op, name, o, BranchTarget: pc + S2(o, 0));
            case "getstatic" or "getfield" or "putfield"
              or "invokevirtual" or "invokespecial" or "invokestatic" or "invokeinterface"
              or "new" or "anewarray" or "checkcast" or "instanceof":
                return new(pc, op, name, o, CpIndex: U2(o, 0));
            case "iconst_m1": return new(pc, op, name, o, ConstValue: -1);
            case var n when n.StartsWith("iconst_"): return new(pc, op, name, o, ConstValue: n[^1] - '0');
            case var n when n.StartsWith("lconst_"): return new(pc, op, name, o, ConstValue: n[^1] - '0');
            default: return new(pc, op, name, o);
        }
    }
}

public sealed class VerifyException(string kind, int offset, string message) : Exception(message)
{
    public string Kind { get; } = kind;
    public int Offset { get; } = offset;
}
