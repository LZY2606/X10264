// Minimal but real opcode metadata table used for boundary decoding and
// control-flow extraction. Constants are the real JVM opcodes.
using System.Collections.Generic;

namespace JvmVerify.Core.ClassFile;

public enum BranchKind { None, Branch2, Branch4, LookupSwitch, TableSwitch }

public sealed record OpcodeSpec(int Code, string Mnemonic, int OperandLength, BranchKind Branch)
{
    public bool IsVariableLength => Branch is BranchKind.LookupSwitch or BranchKind.TableSwitch;
}

public static class Opcodes
{
    public const int Nop=0x00, AconstNull=0x01, IconstM1=0x02, Iconst0=0x03, Iconst1=0x04, Iconst2=0x05,
        Iconst3=0x06, Iconst4=0x07, Iconst5=0x08, Lconst0=0x09, Lconst1=0x0A, Fconst0=0x0B, Fconst1=0x0C,
        Fconst2=0x0D, Dconst0=0x0E, Dconst1=0x0F, Bipush=0x10, Sipush=0x11, Ldc=0x12, LdcW=0x13, Ldc2W=0x14,
        Iload=0x15, Lload=0x16, Fload=0x17, Dload=0x18, Aload=0x19, Iload0=0x1A, Iload1=0x1B, Iload2=0x1C,
        Iload3=0x1D, Lload0=0x1E, Lload1=0x1F, Lload2=0x20, Lload3=0x21, Fload0=0x22, Fload1=0x23, Fload2=0x24,
        Fload3=0x25, Dload0=0x26, Dload1=0x27, Dload2=0x28, Dload3=0x29, Aload0=0x2A, Aload1=0x2B, Aload2=0x2C,
        Aload3=0x2D, Iaload=0x2E, Laload=0x2F, Faload=0x30, Daload=0x31, Aaload=0x32, Baload=0x33,
        Caload=0x34, Saload=0x35, Istore=0x36, Lstore=0x37, Fstore=0x38, Dstore=0x39, Astore=0x3A,
        Istore0=0x3B, Istore1=0x3C, Istore2=0x3D, Istore3=0x3E, Lstore0=0x3F, Lstore1=0x40, Lstore2=0x41,
        Lstore3=0x42, Fstore0=0x43, Fstore1=0x44, Fstore2=0x45, Fstore3=0x46, Dstore0=0x47, Dstore1=0x48,
        Dstore2=0x49, Dstore3=0x4A, Astore0=0x4B, Astore1=0x4C, Astore2=0x4D, Astore3=0x4E,
        Iastore=0x4F, Lastore=0x50, Fastore=0x51, Dastore=0x52, Aastore=0x53, Bastore=0x54,
        Castore=0x55, Sastore=0x56, Pop=0x57, Pop2=0x58, Dup=0x59, DupX1=0x5A, DupX2=0x5B, Dup2=0x5C,
        Dup2X1=0x5D, Dup2X2=0x5E, Swap=0x5F, Iadd=0x60, Ladd=0x61, Fadd=0x62, Dadd=0x63,
        Isub=0x64, Lsub=0x65, Fsub=0x66, Dsub=0x67, Imul=0x68, Lmul=0x69, Fmul=0x6A, Dmul=0x6B,
        Idiv=0x6C, Ldiv=0x6D, Fdiv=0x6E, Ddiv=0x6F, Irem=0x70, Lrem=0x71, Frem=0x72, Drem=0x73,
        Ineg=0x74, Lneg=0x75, Fneg=0x76, Dneg=0x77, Ishl=0x78, Lshl=0x79, Ishr=0x7A, Lshr=0x7B,
        Iushr=0x7C, Lushr=0x7D, Iand=0x7E, Land=0x7F, Ior=0x80, Lor=0x81, Ixor=0x82, Lxor=0x83,
        Iinc=0x84, I2l=0x85, I2f=0x86, I2d=0x87, L2i=0x88, L2f=0x89, L2d=0x8A, F2i=0x8B, F2l=0x8C,
        F2d=0x8D, D2i=0x8E, D2l=0x8F, D2f=0x90, I2b=0x91, I2c=0x92, I2s=0x93, Lcmp=0x94, Fcmpl=0x95,
        Fcmpg=0x96, Dcmpl=0x97, Dcmpg=0x98, Ifeq=0x99, Ifne=0x9A, Iflt=0x9B, Ifge=0x9C, Ifgt=0x9D,
        Ifle=0x9E, Ificmpeq=0x9F, Ificmpne=0xA0, Ificmplt=0xA1, Ificmpge=0xA2, Ificmpgt=0xA3,
        Ificmple=0xA4, Ifacmpeq=0xA5, Ifacmpne=0xA6, Goto=0xA7, Jsr=0xA8, Ret=0xA9,
        Tableswitch=0xAA, Lookupswitch=0xAB, Ireturn=0xAC, Lreturn=0xAD, Freturn=0xAE, Dreturn=0xAF,
        Areturn=0xB0, Return=0xB1, Getstatic=0xB2, Putstatic=0xB3, Getfield=0xB4, Putfield=0xB5,
        Invokevirtual=0xB6, Invokespecial=0xB7, Invokestatic=0xB8, Invokeinterface=0xB9,
        Invokedynamic=0xBA, New=0xBB, Newarray=0xBC, Anewarray=0xBD, Arraylength=0xBE, Athrow=0xBF,
        Checkcast=0xC0, Instanceof=0xC1, Monitorenter=0xC2, Monitorexit=0xC3, Wide=0xC4,
        Multianewarray=0xC5, Ifnull=0xC6, Ifnonnull=0xC7, GotoW=0xC8, JsrW=0xC9;

    private static readonly Dictionary<int, OpcodeSpec> Specs = new(Build());

    public static OpcodeSpec? Find(int code) => Specs.TryGetValue(code, out var s) ? s : null;

    private static IEnumerable<KeyValuePair<int, OpcodeSpec>> Build()
    {
        OpcodeSpec S(int c, string m, int op = 0, BranchKind b = BranchKind.None) => new(c, m, op, b);
        var list = new List<OpcodeSpec>
        {
            S(Nop,"nop"), S(AconstNull,"aconst_null"), S(IconstM1,"iconst_m1"),
            S(Iconst0,"iconst_0"), S(Iconst1,"iconst_1"), S(Iconst2,"iconst_2"), S(Iconst3,"iconst_3"),
            S(Iconst4,"iconst_4"), S(Iconst5,"iconst_5"), S(Lconst0,"lconst_0"), S(Lconst1,"lconst_1"),
            S(Fconst0,"fconst_0"), S(Fconst1,"fconst_1"), S(Fconst2,"fconst_2"),
            S(Dconst0,"dconst_0"), S(Dconst1,"dconst_1"),
            S(Bipush,"bipush",1), S(Sipush,"sipush",2),
            S(Ldc,"ldc",1), S(LdcW,"ldc_w",2), S(Ldc2W,"ldc2_w",2),
            S(Iload,"iload",1), S(Lload,"lload",1), S(Fload,"fload",1), S(Dload,"dload",1), S(Aload,"aload",1),
        };
        for (int c = Iload0; c <= Aload3; c++) list.Add(S(c, LocalName(c)));
        list.AddRange(new[]
        {
            S(Iaload,"iaload"), S(Laload,"laload"), S(Faload,"faload"), S(Daload,"daload"), S(Aaload,"aaload"),
            S(Baload,"baload"), S(Caload,"caload"), S(Saload,"saload"),
            S(Istore,"istore",1), S(Lstore,"lstore",1), S(Fstore,"fstore",1), S(Dstore,"dstore",1), S(Astore,"astore",1),
        });
        for (int c = Istore0; c <= Astore3; c++) list.Add(S(c, LocalName(c)));
        list.AddRange(new[]
        {
            S(Iastore,"iastore"), S(Lastore,"lastore"), S(Fastore,"fastore"), S(Dastore,"dastore"),
            S(Aastore,"aastore"), S(Bastore,"bastore"), S(Castore,"castore"), S(Sastore,"sastore"),
            S(Pop,"pop"), S(Pop2,"pop2"), S(Dup,"dup"), S(DupX1,"dup_x1"), S(DupX2,"dup_x2"),
            S(Dup2,"dup2"), S(Dup2X1,"dup2_x1"), S(Dup2X2,"dup2_x2"), S(Swap,"swap"),
        });
        for (int c = Iadd; c <= Lxor; c++) list.Add(S(c, ArithName(c)));
        list.Add(S(Iinc,"iinc",2));
        list.AddRange(new[]
        {
            S(I2l,"i2l"), S(I2f,"i2f"), S(I2d,"i2d"), S(L2i,"l2i"), S(L2f,"l2f"), S(L2d,"l2d"),
            S(F2i,"f2i"), S(F2l,"f2l"), S(F2d,"f2d"), S(D2i,"d2i"), S(D2l,"d2l"), S(D2f,"d2f"),
            S(I2b,"i2b"), S(I2c,"i2c"), S(I2s,"i2s"),
            S(Lcmp,"lcmp"), S(Fcmpl,"fcmpl"), S(Fcmpg,"fcmpg"), S(Dcmpl,"dcmpl"), S(Dcmpg,"dcmpg"),
        });
        for (int c = Ifeq; c <= Ifle; c++) list.Add(S(c, "if" + (c - Ifeq), 2, BranchKind.Branch2));
        list.AddRange(new[]
        {
            S(Ificmpeq,"if_icmpeq",2,BranchKind.Branch2), S(Ificmpne,"if_icmpne",2,BranchKind.Branch2),
            S(Ificmplt,"if_icmplt",2,BranchKind.Branch2), S(Ificmpge,"if_icmpge",2,BranchKind.Branch2),
            S(Ificmpgt,"if_icmpgt",2,BranchKind.Branch2), S(Ificmple,"if_icmple",2,BranchKind.Branch2),
            S(Ifacmpeq,"if_acmpeq",2,BranchKind.Branch2), S(Ifacmpne,"if_acmpne",2,BranchKind.Branch2),
            S(Goto,"goto",2,BranchKind.Branch2), S(Jsr,"jsr",2,BranchKind.Branch2), S(Ret,"ret",1),
            S(Tableswitch,"tableswitch",0,BranchKind.TableSwitch),
            S(Lookupswitch,"lookupswitch",0,BranchKind.LookupSwitch),
            S(Ireturn,"ireturn"), S(Lreturn,"lreturn"), S(Freturn,"freturn"), S(Dreturn,"dreturn"),
            S(Areturn,"areturn"), S(Return,"return"),
            S(Getstatic,"getstatic",2), S(Putstatic,"putstatic",2),
            S(Getfield,"getfield",2), S(Putfield,"putfield",2),
            S(Invokevirtual,"invokevirtual",2), S(Invokespecial,"invokespecial",2),
            S(Invokestatic,"invokestatic",2), S(Invokeinterface,"invokeinterface",4),
            S(Invokedynamic,"invokedynamic",4),
            S(New,"new",2), S(Newarray,"newarray",1), S(Anewarray,"anewarray",2),
            S(Arraylength,"arraylength"), S(Athrow,"athrow"),
            S(Checkcast,"checkcast",2), S(Instanceof,"instanceof",2),
            S(Monitorenter,"monitorenter"), S(Monitorexit,"monitorexit"),
            // wide is a prefix; decode handled specially (operand length 3/5).
            S(Wide,"wide",0),
            S(Multianewarray,"multianewarray",3),
            S(Ifnull,"ifnull",2,BranchKind.Branch2), S(Ifnonnull,"ifnonnull",2,BranchKind.Branch2),
            S(GotoW,"goto_w",4,BranchKind.Branch4), S(JsrW,"jsr_w",4,BranchKind.Branch4),
        });
        foreach (var s in list) yield return new KeyValuePair<int, OpcodeSpec>(s.Code, s);
    }

    private static string LocalName(int c)
    {
        string[] kinds = { "iload", "lload", "fload", "dload", "aload", "istore", "lstore", "fstore", "dstore", "astore" };
        int block = c - Iload0;
        return kinds[block / 4] + "_" + (block % 4);
    }
    private static string ArithName(int c)
    {
        string[] ops = { "add","sub","mul","div","rem","neg","shl","shr","ushr","and","or","xor" };
        string[] t = { "i","l","f","d" };
        int rel = c - Iadd;
        return t[rel / 12] + ops[rel % 12];
    }
}
