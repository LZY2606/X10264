namespace BytecodeVerifier.Core.Classfile;

/// <summary>
/// 256 条 opcode 的元数据。FixedLength 为指令固定字节数（含 opcode）；
/// 0 表示变长（tableswitch/lookupswitch/wide），由解码器特判。
/// CpKind 标注该指令引用的常量池类型（用于导入期一致性诊断）。
/// </summary>
public static class Opcodes
{
    public sealed record Meta(string Name, int FixedLength, CpKind Cp = CpKind.None, bool Branch = false);

    public enum CpKind { None, Any, Field, Method, InterfaceMethod, Class, Literal, Dynamic, MethodType }

    private static readonly Meta Bad = new("UNKNOWN", 1);

    public static readonly Meta[] Table = Build();

    public static Meta At(int opcode) => opcode is >= 0 and < 256 ? Table[opcode] : Bad;

    private static Meta[] Build()
    {
        var t = Enumerable.Range(0, 256).Select(_ => Bad).ToArray();
        void Set(int op, string name, int len, CpKind cp = CpKind.None, bool branch = false)
            => t[op] = new Meta(name, len, cp, branch);

        Set(0x00, "nop", 1); Set(0x01, "aconst_null", 1);
        Set(0x02, "iconst_m1", 1);
        for (var op = 0x03; op <= 0x08; op++) Set(op, $"iconst_{op - 3}", 1);
        Set(0x09, "lconst_0", 1); Set(0x0a, "lconst_1", 1);
        Set(0x0b, "fconst_0", 1); Set(0x0c, "fconst_1", 1); Set(0x0d, "fconst_2", 1);
        Set(0x0e, "dconst_0", 1); Set(0x0f, "dconst_1", 1);
        Set(0x10, "bipush", 2); Set(0x11, "sipush", 3);
        Set(0x12, "ldc", 2, CpKind.Literal); Set(0x13, "ldc_w", 3, CpKind.Literal); Set(0x14, "ldc2_w", 3, CpKind.Literal);
        Set(0x15, "iload", 2); Set(0x16, "lload", 2); Set(0x17, "fload", 2); Set(0x18, "dload", 2); Set(0x19, "aload", 2);
        for (var op = 0x1a; op <= 0x1d; op++) Set(op, $"iload_{op - 0x1a}", 1);
        for (var op = 0x1e; op <= 0x21; op++) Set(op, $"lload_{op - 0x1e}", 1);
        for (var op = 0x22; op <= 0x25; op++) Set(op, $"fload_{op - 0x22}", 1);
        for (var op = 0x26; op <= 0x29; op++) Set(op, $"dload_{op - 0x26}", 1);
        for (var op = 0x2a; op <= 0x2d; op++) Set(op, $"aload_{op - 0x2a}", 1);
        Set(0x2e, "iaload", 1); Set(0x2f, "laload", 1); Set(0x30, "faload", 1); Set(0x31, "daload", 1);
        Set(0x32, "aaload", 1); Set(0x33, "baload", 1); Set(0x34, "caload", 1); Set(0x35, "saload", 1);
        Set(0x36, "istore", 2); Set(0x37, "lstore", 2); Set(0x38, "fstore", 2); Set(0x39, "dstore", 2); Set(0x3a, "astore", 2);
        for (var op = 0x3b; op <= 0x3e; op++) Set(op, $"istore_{op - 0x3b}", 1);
        for (var op = 0x3f; op <= 0x42; op++) Set(op, $"lstore_{op - 0x3f}", 1);
        for (var op = 0x43; op <= 0x46; op++) Set(op, $"fstore_{op - 0x43}", 1);
        for (var op = 0x47; op <= 0x4a; op++) Set(op, $"dstore_{op - 0x46}", 1);
        for (var op = 0x4b; op <= 0x4e; op++) Set(op, $"astore_{op - 0x4b}", 1);
        Set(0x4f, "iastore", 1); Set(0x50, "lastore", 1); Set(0x51, "fastore", 1); Set(0x52, "dastore", 1);
        Set(0x53, "aastore", 1); Set(0x54, "bastore", 1); Set(0x55, "castore", 1); Set(0x56, "sastore", 1);
        Set(0x57, "pop", 1); Set(0x58, "pop2", 1); Set(0x59, "dup", 1); Set(0x5a, "dup_x1", 1);
        Set(0x5b, "dup_x2", 1); Set(0x5c, "dup2", 1); Set(0x5d, "dup2_x1", 1); Set(0x5e, "dup2_x2", 1);
        Set(0x5f, "swap", 1);
        Set(0x60, "iadd", 1); Set(0x61, "ladd", 1); Set(0x62, "fadd", 1); Set(0x63, "dadd", 1);
        Set(0x64, "isub", 1); Set(0x65, "lsub", 1); Set(0x66, "fsub", 1); Set(0x67, "dsub", 1);
        Set(0x68, "imul", 1); Set(0x69, "lmul", 1); Set(0x6a, "fmul", 1); Set(0x6b, "dmul", 1);
        Set(0x6c, "idiv", 1); Set(0x6d, "ldiv", 1); Set(0x6e, "fdiv", 1); Set(0x6f, "ddiv", 1);
        Set(0x70, "irem", 1); Set(0x71, "lrem", 1); Set(0x72, "frem", 1); Set(0x73, "drem", 1);
        Set(0x74, "ineg", 1); Set(0x75, "lneg", 1); Set(0x76, "fneg", 1); Set(0x77, "dneg", 1);
        Set(0x78, "ishl", 1); Set(0x79, "lshl", 1); Set(0x7a, "ishr", 1); Set(0x7b, "lshr", 1);
        Set(0x7c, "iushr", 1); Set(0x7d, "lushr", 1); Set(0x7e, "iand", 1); Set(0x7f, "land", 1);
        Set(0x80, "ior", 1); Set(0x81, "lor", 1); Set(0x82, "ixor", 1); Set(0x83, "lxor", 1);
        Set(0x84, "iinc", 3);
        Set(0x85, "i2l", 1); Set(0x86, "i2f", 1); Set(0x87, "i2d", 1); Set(0x88, "l2i", 1);
        Set(0x89, "l2f", 1); Set(0x8a, "l2d", 1); Set(0x8b, "f2i", 1); Set(0x8c, "f2l", 1);
        Set(0x8d, "f2d", 1); Set(0x8e, "d2i", 1); Set(0x8f, "d2l", 1); Set(0x90, "d2f", 1);
        Set(0x91, "i2b", 1); Set(0x92, "i2c", 1); Set(0x93, "i2s", 1);
        Set(0x94, "lcmp", 1); Set(0x95, "fcmpl", 1); Set(0x96, "fcmpg", 1); Set(0x97, "dcmpl", 1); Set(0x98, "dcmpg", 1);
        Set(0x99, "ifeq", 3, None, true); Set(0x9a, "ifne", 3, None, true);
        Set(0x9b, "iflt", 3, None, true); Set(0x9c, "ifge", 3, None, true);
        Set(0x9d, "ifgt", 3, None, true); Set(0x9e, "ifle", 3, None, true);
        Set(0x9f, "if_icmpeq", 3, None, true); Set(0xa0, "if_icmpne", 3, None, true);
        Set(0xa1, "if_icmplt", 3, None, true); Set(0xa2, "if_icmpge", 3, None, true);
        Set(0xa3, "if_icmpgt", 3, None, true); Set(0xa4, "if_icmple", 3, None, true);
        Set(0xa5, "if_acmpeq", 3, None, true); Set(0xa6, "if_acmpne", 3, None, true);
        Set(0xa7, "goto", 3, None, true); Set(0xa8, "jsr", 3, None, true); Set(0xa9, "ret", 2);
        Set(0xaa, "tableswitch", 0); Set(0xab, "lookupswitch", 0);
        Set(0xac, "ireturn", 1); Set(0xad, "lreturn", 1); Set(0xae, "freturn", 1);
        Set(0xaf, "dreturn", 1); Set(0xb0, "areturn", 1); Set(0xb1, "return", 1);
        Set(0xb2, "getstatic", 3, CpKind.Field); Set(0xb3, "putstatic", 3, CpKind.Field);
        Set(0xb4, "getfield", 3, CpKind.Field); Set(0xb5, "putfield", 3, CpKind.Field);
        Set(0xb6, "invokevirtual", 3, CpKind.Method); Set(0xb7, "invokespecial", 3, CpKind.Method);
        Set(0xb8, "invokestatic", 3, CpKind.Method); Set(0xb9, "invokeinterface", 5, CpKind.InterfaceMethod);
        Set(0xba, "invokedynamic", 5, CpKind.Dynamic);
        Set(0xbb, "new", 3, CpKind.Class); Set(0xbc, "newarray", 2); Set(0xbd, "anewarray", 3, CpKind.Class);
        Set(0xbe, "arraylength", 1); Set(0xbf, "athrow", 1);
        Set(0xc0, "checkcast", 3, CpKind.Class); Set(0xc1, "instanceof", 3, CpKind.Class);
        Set(0xc2, "monitorenter", 1); Set(0xc3, "monitorexit", 1);
        Set(0xc4, "wide", 0); Set(0xc5, "multianewarray", 4, CpKind.Class);
        Set(0xc6, "ifnull", 3, None, true); Set(0xc7, "ifnonnull", 3, None, true);
        Set(0xc8, "goto_w", 5, None, true); Set(0xc9, "jsr_w", 5, None, true);
        return t;
    }
}
