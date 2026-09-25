namespace JvmVerifier.Core.Binary;

public enum OpcodeFlow
{
    Normal,
    ConditionalBranch,
    UnconditionalBranch,
    Switch,
    Return,
    Throw,
    Invoke,
}

public sealed record OpcodeInfo(int Code, string Mnemonic, int OperandLength, OpcodeFlow Flow = OpcodeFlow.Normal)
{
    public int TotalLength => OperandLength + 1;
}

/// <summary>
/// JVMS 6.5 opcode table for the opcodes the verifier models. Unknown opcodes
/// are preserved (with a diagnostic) rather than crashing the import.
/// </summary>
public static class OpcodeTable
{
    private static readonly Dictionary<int, OpcodeInfo> Map = Build();

    public static OpcodeInfo? Get(int opcode) => Map.GetValueOrDefault(opcode);
    public static string Mnemonic(int opcode) => Map.GetValueOrDefault(opcode)?.Mnemonic ?? $"unknown_{opcode}";

    private static Dictionary<int, OpcodeInfo> Build()
    {
        var d = new Dictionary<int, OpcodeInfo>();
        void Add(int code, string name, int len, OpcodeFlow flow = OpcodeFlow.Normal)
            => d[code] = new OpcodeInfo(code, name, len, flow);

        // Constants
        Add(0x01, "aconst_null", 0); Add(0x02, "iconst_m1", 0);
        for (int i = 3; i <= 8; i++) Add(i, $"iconst_{i - 3}", 0);
        Add(0x09, "lconst_0", 0); Add(0x0a, "lconst_1", 0);
        Add(0x0b, "fconst_0", 0); Add(0x0c, "fconst_1", 0); Add(0x0d, "fconst_2", 0);
        Add(0x0e, "dconst_0", 0); Add(0x0f, "dconst_1", 0);
        Add(0x10, "bipush", 1); Add(0x11, "sipush", 2);
        Add(0x12, "ldc", 1); Add(0x13, "ldc_w", 2); Add(0x14, "ldc2_w", 2);

        // Loads
        for (int i = 0x1a; i <= 0x1d; i++) Add(i, $"iload_{i - 0x1a}", 0);
        for (int i = 0x1e; i <= 0x21; i++) Add(i, $"lload_{i - 0x1e}", 0);
        for (int i = 0x22; i <= 0x25; i++) Add(i, $"fload_{i - 0x22}", 0);
        for (int i = 0x26; i <= 0x29; i++) Add(i, $"dload_{i - 0x26}", 0);
        for (int i = 0x2a; i <= 0x2d; i++) Add(i, $"aload_{i - 0x2a}", 0);
        Add(0x15, "iload", 1); Add(0x16, "lload", 1); Add(0x17, "fload", 1);
        Add(0x18, "dload", 1); Add(0x19, "aload", 1);
        Add(0x2e, "iaload", 0); Add(0x2f, "laload", 0); Add(0x30, "faload", 0);
        Add(0x31, "daload", 0); Add(0x32, "aaload", 0); Add(0x33, "baload", 0);
        Add(0x34, "caload", 0); Add(0x35, "saload", 0);

        // Stores
        for (int i = 0x3b; i <= 0x3e; i++) Add(i, $"istore_{i - 0x3b}", 0);
        for (int i = 0x3f; i <= 0x42; i++) Add(i, $"lstore_{i - 0x3f}", 0);
        for (int i = 0x43; i <= 0x46; i++) Add(i, $"fstore_{i - 0x43}", 0);
        for (int i = 0x47; i <= 0x4a; i++) Add(i, $"dstore_{i - 0x47}", 0);
        for (int i = 0x4b; i <= 0x4e; i++) Add(i, $"astore_{i - 0x4b}", 0);
        Add(0x36, "istore", 1); Add(0x37, "lstore", 1); Add(0x38, "fstore", 1);
        Add(0x39, "dstore", 1); Add(0x3a, "astore", 1);
        Add(0x4f, "iastore", 0); Add(0x50, "lastore", 0); Add(0x51, "fastore", 0);
        Add(0x52, "dastore", 0); Add(0x53, "aastore", 0); Add(0x54, "bastore", 0);
        Add(0x55, "castore", 0); Add(0x56, "sastore", 0);

        // Stack / math
        Add(0x57, "pop", 0); Add(0x58, "pop2", 0); Add(0x59, "dup", 0); Add(0x5a, "dup_x1", 0);
        Add(0x5b, "dup_x2", 0); Add(0x5c, "dup2", 0); Add(0x5d, "dup2_x1", 0); Add(0x5e, "dup2_x2", 0);
        Add(0x5f, "swap", 0);
        for (int i = 0x60; i <= 0x83; i++)
        {
            string baseName = i switch
            {
                >= 0x60 and <= 0x63 => new[] { "iadd", "ladd", "fadd", "dadd" }[i - 0x60],
                >= 0x64 and <= 0x67 => new[] { "isub", "lsub", "fsub", "dsub" }[i - 0x64],
                >= 0x68 and <= 0x6b => new[] { "imul", "lmul", "fmul", "dmul" }[i - 0x68],
                >= 0x6c and <= 0x6f => new[] { "idiv", "ldiv", "fdiv", "ddiv" }[i - 0x6c],
                >= 0x70 and <= 0x73 => new[] { "irem", "lrem", "frem", "drem" }[i - 0x70],
                >= 0x74 and <= 0x77 => new[] { "ineg", "lneg", "fneg", "dneg" }[i - 0x74],
                >= 0x78 and <= 0x7b => new[] { "ishl", "lshl", "ishr", "lshr" }[i - 0x78],
                >= 0x7c and <= 0x7f => new[] { "iushr", "lushr", "iand", "land" }[i - 0x7c],
                _ => new[] { "ior", "lor", "ixor", "lxor" }[i - 0x80],
            };
            Add(i, baseName, 0);
        }
        Add(0x84, "iinc", 2);
        Add(0x85, "i2l", 0); Add(0x86, "i2f", 0); Add(0x87, "i2d", 0);
        Add(0x88, "l2i", 0); Add(0x89, "l2f", 0); Add(0x8a, "l2d", 0);
        Add(0x8b, "f2i", 0); Add(0x8c, "f2l", 0); Add(0x8d, "f2d", 0);
        Add(0x8e, "d2i", 0); Add(0x8f, "d2l", 0); Add(0x90, "d2f", 0);
        Add(0x91, "i2b", 0); Add(0x92, "i2c", 0); Add(0x93, "i2s", 0);
        Add(0x94, "lcmp", 0); Add(0x95, "fcmpl", 0); Add(0x96, "fcmpg", 0);
        Add(0x97, "dcmpl", 0); Add(0x98, "dcmpg", 0);

        // Branches
        Add(0x99, "ifeq", 2, OpcodeFlow.ConditionalBranch);
        Add(0x9a, "ifne", 2, OpcodeFlow.ConditionalBranch);
        Add(0x9b, "iflt", 2, OpcodeFlow.ConditionalBranch);
        Add(0x9c, "ifge", 2, OpcodeFlow.ConditionalBranch);
        Add(0x9d, "ifgt", 2, OpcodeFlow.ConditionalBranch);
        Add(0x9e, "ifle", 2, OpcodeFlow.ConditionalBranch);
        Add(0x9f, "if_icmpeq", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa0, "if_icmpne", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa1, "if_icmplt", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa2, "if_icmpge", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa3, "if_icmpgt", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa4, "if_icmple", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa5, "if_acmpeq", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa6, "if_acmpne", 2, OpcodeFlow.ConditionalBranch);
        Add(0xa7, "goto", 2, OpcodeFlow.UnconditionalBranch);
        Add(0xa8, "jsr", 2, OpcodeFlow.UnconditionalBranch);
        Add(0xa9, "ret", 1);
        Add(0xaa, "tableswitch", 0, OpcodeFlow.Switch);
        Add(0xab, "lookupswitch", 0, OpcodeFlow.Switch);
        Add(0xac, "ireturn", 0, OpcodeFlow.Return);
        Add(0xad, "lreturn", 0, OpcodeFlow.Return);
        Add(0xae, "freturn", 0, OpcodeFlow.Return);
        Add(0xaf, "dreturn", 0, OpcodeFlow.Return);
        Add(0xb0, "areturn", 0, OpcodeFlow.Return);
        Add(0xb1, "return", 0, OpcodeFlow.Return);

        Add(0xb2, "getstatic", 2); Add(0xb3, "putstatic", 2);
        Add(0xb4, "getfield", 2); Add(0xb5, "putfield", 2);
        Add(0xb6, "invokevirtual", 2, OpcodeFlow.Invoke);
        Add(0xb7, "invokespecial", 2, OpcodeFlow.Invoke);
        Add(0xb8, "invokestatic", 2, OpcodeFlow.Invoke);
        Add(0xb9, "invokeinterface", 4, OpcodeFlow.Invoke);
        Add(0xba, "invokedynamic", 4, OpcodeFlow.Invoke);
        Add(0xbb, "new", 2);
        Add(0xbc, "newarray", 1);
        Add(0xbd, "anewarray", 2);
        Add(0xbe, "arraylength", 0);
        Add(0xbf, "athrow", 0, OpcodeFlow.Throw);
        Add(0xc0, "checkcast", 2); Add(0xc1, "instanceof", 2);
        Add(0xc2, "monitorenter", 0); Add(0xc3, "monitorexit", 0);
        Add(0xc4, "wide", 0); Add(0xc5, "multianewarray", 3);
        Add(0xc6, "ifnull", 2, OpcodeFlow.ConditionalBranch);
        Add(0xc7, "ifnonnull", 2, OpcodeFlow.ConditionalBranch);
        Add(0xc8, "goto_w", 4, OpcodeFlow.UnconditionalBranch);
        Add(0xc9, "jsr_w", 4, OpcodeFlow.UnconditionalBranch);
        Add(0xca, "breakpoint", 0);
        Add(0xfe, "impdep1", 0); Add(0xff, "impdep2", 0);

        return d;
    }
}
