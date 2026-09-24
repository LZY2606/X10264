namespace Jbcv.Core.ClassFile;

/// <summary>Opcode metadata: mnemonic, operand length, control-flow category.
/// Padded opcodes (wide/table/lookup) are handled specially in the decoder.</summary>
public static class InstructionSet
{
    public enum FlowKind
    {
        Normal,
        ConditionalBranch,
        UnconditionalJump,
        Return,
        Switch,
        Invoke,
        Other
    }

    private sealed record Meta(byte Op, string Name, int FixedOperandBytes, FlowKind Flow);

    private static readonly Meta?[] s_table = new Meta[256];

    private static void M(byte op, string name, int ops, FlowKind flow = FlowKind.Other)
        => s_table[op] = new Meta(op, name, ops, flow);

    static InstructionSet()
    {
        M(0x00, "nop", 0); M(0x01, "aconst_null", 0);
        M(0x02, "iconst_m1", 0);
        for (byte i = 0x03; i <= 0x08; i++) M(i, $"iconst_{i - 3}", 0);
        M(0x09, "lconst_0", 0); M(0x0a, "lconst_1", 0);
        M(0x0b, "fconst_0", 0); M(0x0c, "fconst_1", 0); M(0x0d, "fconst_2", 0);
        M(0x0e, "dconst_0", 0); M(0x0f, "dconst_1", 0);
        M(0x10, "bipush", 1); M(0x11, "sipush", 2);
        M(0x12, "ldc", 1); M(0x13, "ldc_w", 2); M(0x14, "ldc2_w", 2);
        for (byte i = 0x15; i <= 0x19; i++) M(i, $"iload_{i - 0x15}", 0);
        for (byte i = 0x1a; i <= 0x1e; i++) M(i, $"lload_{i - 0x1a}", 0);
        for (byte i = 0x1f; i <= 0x23; i++) M(i, $"fload_{i - 0x1f}", 0);
        for (byte i = 0x24; i <= 0x28; i++) M(i, $"dload_{i - 0x24}", 0);
        for (byte i = 0x29; i <= 0x2d; i++) M(i, $"aload_{i - 0x29}", 0);
        M(0x2e, "iaload", 0); M(0x2f, "laload", 0); M(0x30, "faload", 0);
        M(0x31, "daload", 0); M(0x32, "aaload", 0); M(0x33, "baload", 0);
        M(0x34, "caload", 0); M(0x35, "saload", 0);
        for (byte i = 0x36; i <= 0x3a; i++) M(i, $"istore_{i - 0x36}", 0);
        for (byte i = 0x3b; i <= 0x3f; i++) M(i, $"lstore_{i - 0x3b}", 0);
        for (byte i = 0x40; i <= 0x44; i++) M(i, $"fstore_{i - 0x40}", 0);
        for (byte i = 0x45; i <= 0x49; i++) M(i, $"dstore_{i - 0x45}", 0);
        for (byte i = 0x4a; i <= 0x4e; i++) M(i, $"astore_{i - 0x4a}", 0);
        M(0x4f, "iastore", 0); M(0x50, "lastore", 0); M(0x51, "fastore", 0);
        M(0x52, "dastore", 0); M(0x53, "aastore", 0); M(0x54, "bastore", 0);
        M(0x55, "castore", 0); M(0x56, "sastore", 0);
        M(0x57, "pop", 0); M(0x58, "pop2", 0); M(0x59, "dup", 0); M(0x5a, "dup_x1", 0);
        M(0x5b, "dup_x2", 0); M(0x5c, "dup2", 0); M(0x5d, "dup2_x1", 0); M(0x5e, "dup2_x2", 0);
        M(0x5f, "swap", 0);
        M(0x60, "iadd", 0); M(0x61, "ladd", 0); M(0x62, "fadd", 0); M(0x63, "dadd", 0);
        M(0x64, "isub", 0); M(0x65, "lsub", 0); M(0x66, "fsub", 0); M(0x67, "dsub", 0);
        M(0x68, "imul", 0); M(0x69, "lmul", 0); M(0x6a, "fmul", 0); M(0x6b, "dmul", 0);
        M(0x6c, "idiv", 0); M(0x6d, "ldiv", 0); M(0x6e, "fdiv", 0); M(0x6f, "ddiv", 0);
        M(0x70, "irem", 0); M(0x71, "lrem", 0); M(0x72, "frem", 0); M(0x73, "drem", 0);
        M(0x74, "ineg", 0); M(0x75, "lneg", 0); M(0x76, "fneg", 0); M(0x77, "dneg", 0);
        M(0x78, "ishl", 0); M(0x79, "lshl", 0); M(0x7a, "ishr", 0); M(0x7b, "lshr", 0);
        M(0x7c, "iushr", 0); M(0x7d, "lushr", 0); M(0x7e, "iand", 0); M(0x7f, "land", 0);
        M(0x80, "ior", 0); M(0x81, "lor", 0); M(0x82, "ixor", 0); M(0x83, "lxor", 0);
        M(0x84, "iinc", 2);
        M(0x85, "i2l", 0); M(0x86, "i2f", 0); M(0x87, "i2d", 0);
        M(0x88, "l2i", 0); M(0x89, "l2f", 0); M(0x8a, "l2d", 0);
        M(0x8b, "f2i", 0); M(0x8c, "f2l", 0); M(0x8d, "f2d", 0);
        M(0x8e, "d2i", 0); M(0x8f, "d2l", 0); M(0x90, "d2f", 0);
        M(0x91, "i2b", 0); M(0x92, "i2c", 0); M(0x93, "i2s", 0);
        M(0x94, "lcmp", 0); M(0x95, "fcmpl", 0); M(0x96, "fcmpg", 0);
        M(0x97, "dcmpl", 0); M(0x98, "dcmpg", 0);
        M(0x99, "ifeq", 2, FlowKind.ConditionalBranch);
        M(0x9a, "ifne", 2, FlowKind.ConditionalBranch);
        M(0x9b, "iflt", 2, FlowKind.ConditionalBranch);
        M(0x9c, "ifge", 2, FlowKind.ConditionalBranch);
        M(0x9d, "ifgt", 2, FlowKind.ConditionalBranch);
        M(0x9e, "ifle", 2, FlowKind.ConditionalBranch);
        M(0x9f, "if_icmpeq", 2, FlowKind.ConditionalBranch);
        M(0xa0, "if_icmpne", 2, FlowKind.ConditionalBranch);
        M(0xa1, "if_icmplt", 2, FlowKind.ConditionalBranch);
        M(0xa2, "if_icmpge", 2, FlowKind.ConditionalBranch);
        M(0xa3, "if_icmpgt", 2, FlowKind.ConditionalBranch);
        M(0xa4, "if_icmple", 2, FlowKind.ConditionalBranch);
        M(0xa5, "if_acmpeq", 2, FlowKind.ConditionalBranch);
        M(0xa6, "if_acmpne", 2, FlowKind.ConditionalBranch);
        M(0xa7, "goto", 2, FlowKind.UnconditionalJump);
        M(0xa8, "jsr", 2); M(0xa9, "ret", 1);
        M(0xaa, "tableswitch", 0, FlowKind.Switch);
        M(0xab, "lookupswitch", 0, FlowKind.Switch);
        M(0xac, "ireturn", 0, FlowKind.Return);
        M(0xad, "lreturn", 0, FlowKind.Return);
        M(0xae, "freturn", 0, FlowKind.Return);
        M(0xaf, "dreturn", 0, FlowKind.Return);
        M(0xb0, "areturn", 0, FlowKind.Return);
        M(0xb1, "return", 0, FlowKind.Return);
        M(0xb2, "getstatic", 2); M(0xb3, "putstatic", 2);
        M(0xb4, "getfield", 2); M(0xb5, "putfield", 2);
        M(0xb6, "invokevirtual", 2, FlowKind.Invoke);
        M(0xb7, "invokespecial", 2, FlowKind.Invoke);
        M(0xb8, "invokestatic", 2, FlowKind.Invoke);
        M(0xb9, "invokeinterface", 4, FlowKind.Invoke);
        M(0xba, "invokedynamic", 4, FlowKind.Invoke);
        M(0xbb, "new", 2);
        M(0xbc, "newarray", 1);
        M(0xbd, "anewarray", 2);
        M(0xbe, "arraylength", 0);
        M(0xbf, "athrow", 0, FlowKind.Return);
        M(0xc0, "checkcast", 2); M(0xc1, "instanceof", 2);
        M(0xc2, "monitorenter", 0); M(0xc3, "monitorexit", 0);
        M(0xc4, "wide", 0); M(0xc5, "multianewarray", 3);
        M(0xc6, "ifnull", 2, FlowKind.ConditionalBranch);
        M(0xc7, "ifnonnull", 2, FlowKind.ConditionalBranch);
        M(0xc8, "goto_w", 4, FlowKind.UnconditionalJump);
        M(0xc9, "jsr_w", 4);
    }

    public static string Mnemonic(byte opcode) => s_table[opcode]?.Name ?? $"unknown_0x{opcode:X2}";
    public static FlowKind Flow(byte opcode) => s_table[opcode]?.Flow ?? FlowKind.Other;
    public static bool Known(byte opcode) => s_table[opcode] is not null;
    public static int FixedOperandBytes(byte opcode) => s_table[opcode]?.FixedOperandBytes ?? 0;
}
