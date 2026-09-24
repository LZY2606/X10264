namespace JvmCore.ClassFile;

// 操作数形状：None 无操作数；Branch 2字节有符号偏移；BranchWide 4字节；
// Cp1/Cp2 常量池索引(1/2字节)；Var1/Var2 局部变量槽；Iinc/IincWide；
// Imm1/Imm2 立即数；InvokeInterface 4字节；MultiArray 3字节；Switch/Variable 变长
public enum OperandShape
{
    None, Branch, BranchWide, Cp1, Cp2, Var1, Var2, Iinc, IincWide,
    Imm1, Imm2, InvokeInterface, InvokeDynamic, MultiArray, Switch, Variable
}

public readonly record struct OpSpec(string Mnemonic, OperandShape Shape);

public static class Opcodes
{
    public const byte
        Nop = 0x00, AconstNull = 0x01, IconstM1 = 0x02, Iconst0 = 0x03, Iconst5 = 0x08,
        Lconst0 = 0x09, Fconst0 = 0x0b, Dconst0 = 0x0e, Bipush = 0x10, Sipush = 0x11,
        Ldc = 0x12, LdcW = 0x13, Ldc2W = 0x14,
        Iload = 0x15, Lload = 0x16, Fload = 0x17, Dload = 0x18, Aload = 0x19,
        Iload0 = 0x1a, Lload0 = 0x1e, Fload0 = 0x22, Dload0 = 0x26, Aload0 = 0x2a,
        Iaload = 0x2e, Laload = 0x2f, Faload = 0x30, Daload = 0x31, Aaload = 0x32,
        Baload = 0x33, Caload = 0x34, Saload = 0x35,
        Istore = 0x36, Lstore = 0x37, Fstore = 0x38, Dstore = 0x39, Astore = 0x3a,
        Istore0 = 0x3b, Lstore0 = 0x3f, Fstore0 = 0x43, Dstore0 = 0x47, Astore0 = 0x4b,
        Iastore = 0x4f, Lastore = 0x50, Fastore = 0x51, Dastore = 0x52, Aastore = 0x53,
        Bastore = 0x54, Castore = 0x55, Sastore = 0x56,
        Pop = 0x57, Pop2 = 0x58, Dup = 0x59, DupX1 = 0x5a, DupX2 = 0x5b, Dup2 = 0x5c,
        Dup2X1 = 0x5d, Dup2X2 = 0x5e, Swap = 0x5f,
        Iadd = 0x60, Iinc = 0x84,
        I2l = 0x85, L2i = 0x88, I2b = 0x91, I2c = 0x92,
        Ifeq = 0x99, IfIcmpeq = 0x9f, IfAcmpEq = 0xa5, Goto = 0xa7, Jsr = 0xa8, Ret = 0xa9,
        Tableswitch = 0xaa, Lookupswitch = 0xab,
        Ifnull = 0xc6, Ifnonnull = 0xc7, GotoW = 0xc8,
        Ireturn = 0xac, Lreturn = 0xad, Freturn = 0xae, Dreturn = 0xaf, Areturn = 0xb0, Return = 0xb1,
        Getstatic = 0xb2, Putstatic = 0xb3, Getfield = 0xb4, Putfield = 0xb5,
        Invokevirtual = 0xb6, Invokespecial = 0xb7, Invokestatic = 0xb8, Invokeinterface = 0xb9,
        Invokedynamic = 0xba, New = 0xbb, Newarray = 0xbc, Anewarray = 0xbd, Arraylength = 0xbe,
        Athrow = 0xbf, Checkcast = 0xc0, Instanceof = 0xc1, Monitorenter = 0xc2, Monitorexit = 0xc3,
        Wide = 0xc4, Multianewarray = 0xc5;

    private static readonly Dictionary<int, OpSpec> Specs = Build();
    public static OpSpec? Get(int opcode) => Specs.TryGetValue(opcode, out var s) ? s : null;

    private static Dictionary<int, OpSpec> Build()
    {
        var d = new Dictionary<int, OpSpec>();
        void Put(int op, string m, OperandShape sh) => d[op] = new OpSpec(m, sh);

        Put(Nop, "nop", OperandShape.None); Put(AconstNull, "aconst_null", OperandShape.None);
        d[IconstM1] = new OpSpec("iconst_m1", OperandShape.None);
        for (int op = Iconst0; op <= Iconst5; op++) Put(op, "iconst_" + (op - Iconst0), OperandShape.None);
        Put(Lconst0, "lconst_0", OperandShape.None); Put(0x0a, "lconst_1", OperandShape.None);
        Put(Fconst0, "fconst_0", OperandShape.None); Put(0x0c, "fconst_1", OperandShape.None); Put(0x0d, "fconst_2", OperandShape.None);
        Put(Dconst0, "dconst_0", OperandShape.None); Put(0x0f, "dconst_1", OperandShape.None);
        Put(Bipush, "bipush", OperandShape.Imm1); Put(Sipush, "sipush", OperandShape.Imm2);
        Put(Ldc, "ldc", OperandShape.Cp1); Put(LdcW, "ldc_w", OperandShape.Cp2); Put(Ldc2W, "ldc2_w", OperandShape.Cp2);

        (int first, string name, OperandShape sh)[] varOps =
        {
            (Iload, "iload", OperandShape.Var1), (Lload, "lload", OperandShape.Var1),
            (Fload, "fload", OperandShape.Var1), (Dload, "dload", OperandShape.Var1),
            (Aload, "aload", OperandShape.Var1),
        };
        foreach (var (first, name, sh) in varOps)
        {
            Put(first, name, sh);
            for (int k = 0; k < 4; k++) Put(first + 0x05 + k, name + "_" + k, OperandShape.None);
        }
        string[] arrLoad = { "iaload", "laload", "faload", "daload", "aaload", "baload", "caload", "saload" };
        for (int k = 0; k < arrLoad.Length; k++) Put(Iaload + k, arrLoad[k], OperandShape.None);

        (int first2, string name2)[] varOps2 =
            { (Istore, "istore"), (Lstore, "lstore"), (Fstore, "fstore"), (Dstore, "dstore"), (Astore, "astore") };
        foreach (var (first, name) in varOps2)
        {
            Put(first, name, OperandShape.Var1);
            for (int k = 0; k < 4; k++) Put(first + 0x05 + k, name + "_" + k, OperandShape.None);
        }
        string[] arrStore = { "iastore", "lastore", "fastore", "dastore", "aastore", "bastore", "castore", "sastore" };
        for (int k = 0; k < arrStore.Length; k++) Put(Iastore + k, arrStore[k], OperandShape.None);

        Put(Pop, "pop", OperandShape.None); Put(Pop2, "pop2", OperandShape.None);
        Put(Dup, "dup", OperandShape.None); Put(DupX1, "dup_x1", OperandShape.None); Put(DupX2, "dup_x2", OperandShape.None);
        Put(Dup2, "dup2", OperandShape.None); Put(Dup2X1, "dup2_x1", OperandShape.None); Put(Dup2X2, "dup2_x2", OperandShape.None);
        Put(Swap, "swap", OperandShape.None);

        string[] bin = { "iadd","ladd","fadd","dadd","isub","lsub","fsub","dsub","imul","lmul","fmul","dmul",
                         "idiv","ldiv","fdiv","ddiv","irem","lrem","frem","drem" };
        for (int k = 0; k < bin.Length; k++) Put(Iadd + k, bin[k], OperandShape.None);
        Put(0x74, "ineg", OperandShape.None); Put(0x75, "lneg", OperandShape.None);
        Put(0x76, "fneg", OperandShape.None); Put(0x77, "dneg", OperandShape.None);
        Put(0x78, "ishl", OperandShape.None); Put(0x79, "lshl", OperandShape.None);
        Put(0x7a, "ishr", OperandShape.None); Put(0x7b, "lshr", OperandShape.None);
        Put(0x7c, "iushr", OperandShape.None); Put(0x7d, "lushr", OperandShape.None);
        Put(0x7e, "iand", OperandShape.None); Put(0x7f, "land", OperandShape.None);
        Put(0x80, "ior", OperandShape.None); Put(0x81, "lor", OperandShape.None);
        Put(0x82, "ixor", OperandShape.None); Put(0x83, "lxor", OperandShape.None);
        Put(Iinc, "iinc", OperandShape.Iinc);

        string[] conv = { "i2l","i2f","i2d","l2i","l2f","l2d","f2i","f2l","f2d","d2i","d2l","d2f","i2b","i2c","i2s" };
        for (int k = 0; k < conv.Length; k++) Put(I2l + k, conv[k], OperandShape.None);

        string[] cmp = { "ifeq","ifne","iflt","ifge","ifgt","ifle" };
        for (int k = 0; k < cmp.Length; k++) Put(Ifeq + k, cmp[k], OperandShape.Branch);
        Put(0x9f, "if_icmpeq", OperandShape.Branch); Put(0xa0, "if_icmpne", OperandShape.Branch);
        Put(0xa1, "if_icmplt", OperandShape.Branch); Put(0xa2, "if_icmpge", OperandShape.Branch);
        Put(0xa3, "if_icmpgt", OperandShape.Branch); Put(0xa4, "if_icmple", OperandShape.Branch);
        Put(IfAcmpEq, "if_acmpeq", OperandShape.Branch); Put(0xa6, "if_acmpne", OperandShape.Branch);
        Put(Goto, "goto", OperandShape.Branch); Put(Jsr, "jsr", OperandShape.Branch); Put(Ret, "ret", OperandShape.Var1);
        Put(Tableswitch, "tableswitch", OperandShape.Switch); Put(Lookupswitch, "lookupswitch", OperandShape.Switch);
        Put(Ifnull, "ifnull", OperandShape.Branch); Put(Ifnonnull, "ifnonnull", OperandShape.Branch);
        Put(GotoW, "goto_w", OperandShape.BranchWide); Put(0xc9, "jsr_w", OperandShape.BranchWide);

        Put(Ireturn, "ireturn", OperandShape.None); Put(Lreturn, "lreturn", OperandShape.None);
        Put(Freturn, "freturn", OperandShape.None); Put(Dreturn, "dreturn", OperandShape.None);
        Put(Areturn, "areturn", OperandShape.None); Put(Return, "return", OperandShape.None);

        Put(Getstatic, "getstatic", OperandShape.Cp2); Put(Putstatic, "putstatic", OperandShape.Cp2);
        Put(Getfield, "getfield", OperandShape.Cp2); Put(Putfield, "putfield", OperandShape.Cp2);
        Put(Invokevirtual, "invokevirtual", OperandShape.Cp2);
        Put(Invokespecial, "invokespecial", OperandShape.Cp2);
        Put(Invokestatic, "invokestatic", OperandShape.Cp2);
        Put(Invokeinterface, "invokeinterface", OperandShape.InvokeInterface);
        Put(Invokedynamic, "invokedynamic", OperandShape.InvokeDynamic);
        Put(New, "new", OperandShape.Cp2); Put(Newarray, "newarray", OperandShape.Imm1);
        Put(Anewarray, "anewarray", OperandShape.Cp2); Put(Arraylength, "arraylength", OperandShape.None);
        Put(Athrow, "athrow", OperandShape.None);
        Put(Checkcast, "checkcast", OperandShape.Cp2); Put(Instanceof, "instanceof", OperandShape.Cp2);
        Put(Monitorenter, "monitorenter", OperandShape.None); Put(Monitorexit, "monitorexit", OperandShape.None);
        Put(Wide, "wide", OperandShape.Variable); Put(Multianewarray, "multianewarray", OperandShape.MultiArray);
        Put(0xca, "breakpoint", OperandShape.None); Put(0xfe, "impdep1", OperandShape.None); Put(0xff, "impdep2", OperandShape.None);
        return d;
    }
}
