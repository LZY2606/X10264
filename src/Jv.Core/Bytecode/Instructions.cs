namespace Jv.Core.Bytecode;

public enum Mnemonic
{
    Nop, AconstNull, IconstM1, Iconst0, Iconst1, Iconst2, Iconst3, Iconst4, Iconst5,
    Lconst0, Lconst1, Fconst0, Fconst1, Fconst2, Dconst0, Dconst1,
    Bipush, Sipush, Ldc, LdcW, Ldc2W,
    Iload, Lload, Fload, Dload, Aload,
    Iload0, Iload1, Iload2, Iload3, Lload0, Lload1, Lload2, Lload3,
    Fload0, Fload1, Fload2, Fload3, Dload0, Dload1, Dload2, Dload3,
    Aload0, Aload1, Aload2, Aload3,
    Iaload, Laload, Faload, Daload, Aaload, Baload, Caload, Saload,
    Istore, Lstore, Fstore, Dstore, Astore,
    Istore0, Istore1, Istore2, Istore3, Lstore0, Lstore1, Lstore2, Lstore3,
    Fstore0, Fstore1, Fstore2, Fstore3, Dstore0, Dstore1, Dstore2, Dstore3,
    Astore0, Astore1, Astore2, Astore3,
    Iastore, Lastore, Fastore, Dastore, Aastore, Bastore, Castore, Sastore,
    Pop, Pop2, Dup, DupX1, DupX2, Dup2, Dup2X1, Dup2X2, Swap,
    Iadd, Ladd, Fadd, Dadd, Isub, Lsub, Fsub, Dsub,
    Imul, Lmul, Fmul, Dmul, Idiv, Ldiv, Fdiv, Ddiv,
    Ineg, Lneg, Fneg, Dneg, Irem, Lrem, Frem, Drem,
    Iand, Land, Ior, Lor, Ixor, Lxor, Ishl, Lshl, Ishr, Lshr, Iushr, Lushr,
    I2l, I2f, I2d, L2i, L2f, L2d, F2i, F2l, F2d, D2i, D2l, D2f, I2b, I2c, I2s,
    Lcmp, Fcmpl, Fcmpg, Dcmpl, Dcmpg,
    Ifeq, Ifne, Iflt, Ifge, Ifgt, Ifle,
    IfIcmpeq, IfIcmpne, IfIcmplt, IfIcmpge, IfIcmpgt, IfIcmple,
    IfAcmpeq, IfAcmpne, Goto, Jsr, Ret,
    Tableswitch, Lookupswitch,
    Ireturn, Lreturn, Freturn, Dreturn, Areturn, Return,
    Getstatic, Putstatic, Getfield, Putfield,
    Invokevirtual, Invokespecial, Invokestatic, Invokeinterface, Invokedynamic,
    New, Newarray, Anewarray, Arraylength, Athrow,
    Checkcast, Instanceof, Monitorenter, Monitorexit, Wide, Multianewarray,
    Ifnull, Ifnonnull, GotoW, JsrW,
    Unknown
}

// 解码后的指令：保留原字节偏移 Pc、原始 opcode、原始操作数字节。
public sealed record Instruction(
    int Pc, int Length, int Opcode, Mnemonic Mnemonic,
    IReadOnlyList<int> Operands,
    byte[] RawOperandBytes)
{
    public int BranchTarget => Mnemonic.IsBranch() ? Pc + (short)(Operands.Count > 0 ? Operands[0] : 0) : -1;
    public bool IsUnconditional => Mnemonic is Mnemonic.Goto or Mnemonic.GotoW or Mnemonic.Jsr or Mnemonic.JsrW;
    public bool IsReturn => Mnemonic is Mnemonic.Ireturn or Mnemonic.Lreturn or Mnemonic.Freturn
        or Mnemonic.Dreturn or Mnemonic.Areturn or Mnemonic.Return;
    public bool IsThrow => Mnemonic == Mnemonic.Athrow;
}

public static class MnemonicExtensions
{
    public static bool IsBranch(this Mnemonic m) => m is Mnemonic.Ifeq or Mnemonic.Ifne or
        Mnemonic.Iflt or Mnemonic.Ifge or Mnemonic.Ifgt or Mnemonic.Ifle or
        Mnemonic.IfIcmpeq or Mnemonic.IfIcmpne or Mnemonic.IfIcmplt or Mnemonic.IfIcmpge or
        Mnemonic.IfIcmpgt or Mnemonic.IfIcmple or Mnemonic.IfAcmpeq or Mnemonic.IfAcmpne or
        Mnemonic.Ifnull or Mnemonic.Ifnonnull or Mnemonic.Goto or Mnemonic.GotoW;
}
