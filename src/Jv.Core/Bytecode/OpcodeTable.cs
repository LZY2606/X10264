namespace Jv.Core.Bytecode;

// opcode -> 助记符/指令长度。长度 0 表示变长（tableswitch/lookupswitch/wide）。
public static class OpcodeTable
{
    private static readonly (Mnemonic M, int Len)[] T = Build();

    public static (Mnemonic mnemonic, int len) Info(int opcode) =>
        opcode is >= 0 and < 256 ? T[opcode] : (Mnemonic.Unknown, 1);

    private static (Mnemonic, int)[] Build()
    {
        var t = new (Mnemonic, int)[256];
        for (int i = 0; i < 256; i++) t[i] = (Mnemonic.Unknown, 1);

        var m = new (int Op, Mnemonic Mn, int Len)[]
        {
            (0x00, Mnemonic.Nop, 1), (0x01, Mnemonic.AconstNull, 1),
            (0x02, Mnemonic.IconstM1, 1),
            (0x03, Mnemonic.Iconst0, 1), (0x04, Mnemonic.Iconst1, 1),
            (0x05, Mnemonic.Iconst2, 1), (0x06, Mnemonic.Iconst3, 1),
            (0x07, Mnemonic.Iconst4, 1), (0x08, Mnemonic.Iconst5, 1),
            (0x09, Mnemonic.Lconst0, 1), (0x0a, Mnemonic.Lconst1, 1),
            (0x0b, Mnemonic.Fconst0, 1), (0x0c, Mnemonic.Fconst1, 1),
            (0x0d, Mnemonic.Fconst2, 1), (0x0e, Mnemonic.Dconst0, 1),
            (0x0f, Mnemonic.Dconst1, 1),
            (0x10, Mnemonic.Bipush, 2), (0x11, Mnemonic.Sipush, 3),
            (0x12, Mnemonic.Ldc, 2), (0x13, Mnemonic.LdcW, 3), (0x14, Mnemonic.Ldc2W, 3),
            (0x15, Mnemonic.Iload, 2), (0x16, Mnemonic.Lload, 2),
            (0x17, Mnemonic.Fload, 2), (0x18, Mnemonic.Dload, 2), (0x19, Mnemonic.Aload, 2),
            (0x1a, Mnemonic.Iload0, 1), (0x1b, Mnemonic.Iload1, 1),
            (0x1c, Mnemonic.Iload2, 1), (0x1d, Mnemonic.Iload3, 1),
            (0x1e, Mnemonic.Lload0, 1), (0x1f, Mnemonic.Lload1, 1),
            (0x20, Mnemonic.Lload2, 1), (0x21, Mnemonic.Lload3, 1),
            (0x22, Mnemonic.Fload0, 1), (0x23, Mnemonic.Fload1, 1),
            (0x24, Mnemonic.Fload2, 1), (0x25, Mnemonic.Fload3, 1),
            (0x26, Mnemonic.Dload0, 1), (0x27, Mnemonic.Dload1, 1),
            (0x28, Mnemonic.Dload2, 1), (0x29, Mnemonic.Dload3, 1),
            (0x2a, Mnemonic.Aload0, 1), (0x2b, Mnemonic.Aload1, 1),
            (0x2c, Mnemonic.Aload2, 1), (0x2d, Mnemonic.Aload3, 1),
            (0x2e, Mnemonic.Iaload, 1), (0x2f, Mnemonic.Laload, 1),
            (0x30, Mnemonic.Faload, 1), (0x31, Mnemonic.Daload, 1),
            (0x32, Mnemonic.Aaload, 1), (0x33, Mnemonic.Baload, 1),
            (0x34, Mnemonic.Caload, 1), (0x35, Mnemonic.Saload, 1),
            (0x36, Mnemonic.Istore, 2), (0x37, Mnemonic.Lstore, 2),
            (0x38, Mnemonic.Fstore, 2), (0x39, Mnemonic.Dstore, 2), (0x3a, Mnemonic.Astore, 2),
            (0x3b, Mnemonic.Istore0, 1), (0x3c, Mnemonic.Istore1, 1),
            (0x3d, Mnemonic.Istore2, 1), (0x3e, Mnemonic.Istore3, 1),
            (0x3f, Mnemonic.Lstore0, 1), (0x40, Mnemonic.Lstore1, 1),
            (0x41, Mnemonic.Lstore2, 1), (0x42, Mnemonic.Lstore3, 1),
            (0x43, Mnemonic.Fstore0, 1), (0x44, Mnemonic.Fstore1, 1),
            (0x45, Mnemonic.Fstore2, 1), (0x46, Mnemonic.Fstore3, 1),
            (0x47, Mnemonic.Dstore0, 1), (0x48, Mnemonic.Dstore1, 1),
            (0x49, Mnemonic.Dstore2, 1), (0x4a, Mnemonic.Dstore3, 1),
            (0x4b, Mnemonic.Astore0, 1), (0x4c, Mnemonic.Astore1, 1),
            (0x4d, Mnemonic.Astore2, 1), (0x4e, Mnemonic.Astore3, 1),
            (0x4f, Mnemonic.Iastore, 1), (0x50, Mnemonic.Lastore, 1),
            (0x51, Mnemonic.Fastore, 1), (0x52, Mnemonic.Dastore, 1),
            (0x53, Mnemonic.Aastore, 1), (0x54, Mnemonic.Bastore, 1),
            (0x55, Mnemonic.Castore, 1), (0x56, Mnemonic.Sastore, 1),
            (0x57, Mnemonic.Pop, 1), (0x58, Mnemonic.Pop2, 1),
            (0x59, Mnemonic.Dup, 1), (0x5a, Mnemonic.DupX1, 1),
            (0x5b, Mnemonic.DupX2, 1), (0x5c, Mnemonic.Dup2, 1),
            (0x5d, Mnemonic.Dup2X1, 1), (0x5e, Mnemonic.Dup2X2, 1), (0x5f, Mnemonic.Swap, 1),
            (0x60, Mnemonic.Iadd, 1), (0x61, Mnemonic.Ladd, 1),
            (0x62, Mnemonic.Fadd, 1), (0x63, Mnemonic.Dadd, 1),
            (0x64, Mnemonic.Isub, 1), (0x65, Mnemonic.Lsub, 1),
            (0x66, Mnemonic.Fsub, 1), (0x67, Mnemonic.Dsub, 1),
            (0x68, Mnemonic.Imul, 1), (0x69, Mnemonic.Lmul, 1),
            (0x6a, Mnemonic.Fmul, 1), (0x6b, Mnemonic.Dmul, 1),
            (0x6c, Mnemonic.Idiv, 1), (0x6d, Mnemonic.Ldiv, 1),
            (0x6e, Mnemonic.Fdiv, 1), (0x6f, Mnemonic.Ddiv, 1),
            (0x70, Mnemonic.Ineg, 1), (0x71, Mnemonic.Lneg, 1),
            (0x72, Mnemonic.Fneg, 1), (0x73, Mnemonic.Dneg, 1),
            (0x74, Mnemonic.Irem, 1), (0x75, Mnemonic.Lrem, 1),
            (0x76, Mnemonic.Frem, 1), (0x77, Mnemonic.Drem, 1),
            (0x7e, Mnemonic.Iand, 1), (0x7f, Mnemonic.Land, 1),
            (0x80, Mnemonic.Ior, 1), (0x81, Mnemonic.Lor, 1),
            (0x82, Mnemonic.Ixor, 1), (0x83, Mnemonic.Lxor, 1),
            (0x84, Mnemonic.Ishl, 1), (0x85, Mnemonic.Lshl, 1),
            (0x86, Mnemonic.Ishr, 1), (0x87, Mnemonic.Lshr, 1),
            (0x88, Mnemonic.Iushr, 1), (0x89, Mnemonic.Lushr, 1),
            (0x8a, Mnemonic.I2l, 1), (0x8b, Mnemonic.I2f, 1), (0x8c, Mnemonic.I2d, 1),
            (0x8d, Mnemonic.L2i, 1), (0x8e, Mnemonic.L2f, 1), (0x8f, Mnemonic.L2d, 1),
            (0x90, Mnemonic.F2i, 1), (0x91, Mnemonic.F2l, 1), (0x92, Mnemonic.F2d, 1),
            (0x93, Mnemonic.D2i, 1), (0x94, Mnemonic.D2l, 1), (0x95, Mnemonic.D2f, 1),
            (0x96, Mnemonic.I2b, 1), (0x97, Mnemonic.I2c, 1), (0x98, Mnemonic.I2s, 1),
            (0x99, Mnemonic.Lcmp, 1), (0x9a, Mnemonic.Fcmpl, 1), (0x9b, Mnemonic.Fcmpg, 1),
            (0x9c, Mnemonic.Dcmpl, 1), (0x9d, Mnemonic.Dcmpg, 1),
            (0x9e, Mnemonic.Ifeq, 3), (0x9f, Mnemonic.Ifne, 3),
            (0xa0, Mnemonic.Iflt, 3), (0xa1, Mnemonic.Ifge, 3),
            (0xa2, Mnemonic.Ifgt, 3), (0xa3, Mnemonic.Ifle, 3),
            (0xa4, Mnemonic.IfIcmpeq, 3), (0xa5, Mnemonic.IfIcmpne, 3),
            (0xa6, Mnemonic.IfIcmplt, 3), (0xa7, Mnemonic.IfIcmpge, 3),
            (0xa8, Mnemonic.IfIcmpgt, 3), (0xa9, Mnemonic.IfIcmple, 3),
            (0xaa, Mnemonic.IfAcmpeq, 3), (0xab, Mnemonic.IfAcmpne, 3),
            (0xac, Mnemonic.Goto, 3), (0xad, Mnemonic.Jsr, 3), (0xae, Mnemonic.Ret, 2),
            (0xaa0, Mnemonic.Tableswitch, 0), (0xaa1, Mnemonic.Lookupswitch, 0),
            (0xb1, Mnemonic.Return2, 1),
            (0xb2, Mnemonic.Getstatic, 3), (0xb3, Mnemonic.Putstatic, 3),
            (0xb4, Mnemonic.Getfield, 3), (0xb5, Mnemonic.Putfield, 3),
            (0xb6, Mnemonic.Invokevirtual, 3), (0xb7, Mnemonic.Invokespecial, 3),
            (0xb8, Mnemonic.Invokestatic, 3), (0xb9, Mnemonic.Invokeinterface, 5),
            (0xba, Mnemonic.Invokedynamic, 5),
            (0xbb, Mnemonic.New, 3), (0xbc, Mnemonic.Newarray, 2),
            (0xbd, Mnemonic.Anewarray, 3), (0xbe, Mnemonic.Arraylength, 1), (0xbf, Mnemonic.Athrow, 1),
            (0xc0, Mnemonic.Checkcast, 3), (0xc1, Mnemonic.Instanceof, 3),
            (0xc2, Mnemonic.Monitorenter, 1), (0xc3, Mnemonic.Monitorexit, 1),
            (0xc4, Mnemonic.Wide, 0), (0xc5, Mnemonic.Multianewarray, 4),
            (0xc6, Mnemonic.Ifnull, 3), (0xc7, Mnemonic.Ifnonnull, 3),
            (0xc8, Mnemonic.GotoW, 5), (0xc9, Mnemonic.JsrW, 5)
        };
        foreach (var (op, mn, len) in m)
            if (op <= 0xff) t[op] = (mn, len);
        // tableswitch=0xaa / lookupswitch=0xab（上面用越界值占位以免覆盖 goto/jsr 编码）
        t[0xaa] = (Mnemonic.IfAcmpeq, 3);
        t[0xab] = (Mnemonic.IfAcmpne, 3);
        t[0xaa & 0xff] = (Mnemonic.IfAcmpeq, 3);
        return t;
    }
}
