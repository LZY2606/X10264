namespace JvmVerifier.Core.Classfile;

public enum InsnFlow
{
    Normal,
    ConditionalBranch,
    UnconditionalBranch,
    Switch,
    Return,
    Throw,
    Invoke
}

public sealed class OpcodeInfo
{
    public int Opcode { get; init; }
    public string Mnemonic { get; init; } = "";
    public int FixedOperandSize { get; init; }
    public InsnFlow Flow { get; init; }
    public bool CpRef { get; init; }
    public int StackPush { get; init; }
    public int StackPop { get; init; }
}

public static class Opcodes
{
    public const int
        AconstNull = 1, IconstM1 = 2, Iconst0 = 3,
        Lconst0 = 9, Lconst1 = 10, Fconst0 = 11, Fconst1 = 12, Fconst2 = 13, Dconst0 = 14, Dconst1 = 15,
        Bipush = 16, Sipush = 17, Ldc1 = 18, Ldc2 = 19, Ldc2w = 20,
        Iload = 21, Lload = 22, Fload = 23, Dload = 24, Aload = 25,
        Iload0 = 26, Lload0 = 30, Fload0 = 34, Dload0 = 38, Aload0 = 42,
        Istore = 54, Lstore = 55, Fstore = 56, Dstore = 57, Astore = 58,
        Istore0 = 59, Lstore0 = 63, Fstore0 = 67, Dstore0 = 71, Astore0 = 75,
        Iadd = 96, Ladd = 97, Fadd = 98, Dadd = 99,
        Ifeq = 153, Ifne = 154, Iflt = 155, Ifge = 156, Ifgt = 157, Ifle = 158,
        IfIcmpeq = 159, IfIcmpne = 160, IfIcmplt = 161, IfIcmpge = 162, IfIcmpgt = 163, IfIcmple = 164,
        IfAcmpeq = 165, IfAcmpne = 166, Goto = 167, Jsr = 168, Ret = 169,
        Tableswitch = 170, Lookupswitch = 171,
        Ireturn = 172, Lreturn = 173, Freturn = 174, Dreturn = 175, Areturn = 176, Return = 177,
        Getstatic = 178, Putstatic = 179, Getfield = 180, Putfield = 181,
        Invokevirtual = 182, Invokespecial = 183, Invokestatic = 184, Invokeinterface = 185, Invokedynamic = 186,
        New = 187, Newarray = 188, Anewarray = 189, Arraylength = 190, Athrow = 191,
        Checkcast = 192, Instanceof = 193, Monitorenter = 194, Monitorexit = 195,
        Wide = 196, Multianewarray = 197, IfNull = 198, IfNonNull = 199, GotoW = 200, JsrW = 201;

    private static readonly Dictionary<int, OpcodeInfo> Map = Build();
    public static OpcodeInfo? Get(int opcode) => Map.TryGetValue(opcode, out var i) ? i : null;

    private static OpcodeInfo Mk(int op, string m, int osize = 0, InsnFlow f = InsnFlow.Normal,
        bool cp = false, int push = 0, int pop = 0) =>
        new() { Opcode = op, Mnemonic = m, FixedOperandSize = osize, Flow = f, CpRef = cp, StackPush = push, StackPop = pop };

    private static Dictionary<int, OpcodeInfo> Build()
    {
        var d = new Dictionary<int, OpcodeInfo>();
        void Put(OpcodeInfo i) => d[i.Opcode] = i;

        Put(Mk(0, "nop"));
        Put(Mk(AconstNull, "aconst_null", push: 1));
        Put(Mk(IconstM1, "iconst_m1", push: 1));
        for (int i = 0; i < 6; i++) Put(Mk(Iconst0 + i, $"iconst_{i - 1}".Replace("_-1", "_m1"), push: 1));
        Put(Mk(Lconst0, "lconst_0", push: 2));
        Put(Mk(Lconst1, "lconst_1", push: 2));
        Put(Mk(Fconst0, "fconst_0", push: 1));
        Put(Mk(Fconst1, "fconst_1", push: 1));
        Put(Mk(Fconst2, "fconst_2", push: 1));
        Put(Mk(Dconst0, "dconst_0", push: 2));
        Put(Mk(Dconst1, "dconst_1", push: 2));
        Put(Mk(Bipush, "bipush", 1, push: 1));
        Put(Mk(Sipush, "sipush", 2, push: 1));
        Put(Mk(Ldc1, "ldc", 1, cp: true, push: 1));
        Put(Mk(Ldc2, "ldc", 2, cp: true, push: 1));
        Put(Mk(Ldc2w, "ldc2_w", 2, cp: true, push: 2));

        Put(Mk(Iload, "iload", 1, push: 1));
        Put(Mk(Lload, "lload", 1, push: 2));
        Put(Mk(Fload, "fload", 1, push: 1));
        Put(Mk(Dload, "dload", 1, push: 2));
        Put(Mk(Aload, "aload", 1, push: 1));
        for (int s = 0; s < 4; s++)
        {
            Put(Mk(Iload0 + s, $"iload_{s}", push: 1));
            Put(Mk(Lload0 + s, $"lload_{s}", push: 2));
            Put(Mk(Fload0 + s, $"fload_{s}", push: 1));
            Put(Mk(Dload0 + s, $"dload_{s}", push: 2));
            Put(Mk(Aload0 + s, $"aload_{s}", push: 1));
        }
        Put(Mk(Istore, "istore", 1, pop: 1));
        Put(Mk(Lstore, "lstore", 1, pop: 2));
        Put(Mk(Fstore, "fstore", 1, pop: 1));
        Put(Mk(Dstore, "dstore", 1, pop: 2));
        Put(Mk(Astore, "astore", 1, pop: 1));
        for (int s = 0; s < 4; s++)
        {
            Put(Mk(Istore0 + s, $"istore_{s}", pop: 1));
            Put(Mk(Lstore0 + s, $"lstore_{s}", pop: 2));
            Put(Mk(Fstore0 + s, $"fstore_{s}", pop: 1));
            Put(Mk(Dstore0 + s, $"dstore_{s}", pop: 2));
            Put(Mk(Astore0 + s, $"astore_{s}", pop: 1));
        }

        Put(Mk(Iadd, "iadd", pop: 2, push: 1));
        Put(Mk(Ladd, "ladd", pop: 4, push: 2));
        Put(Mk(Fadd, "fadd", pop: 2, push: 1));
        Put(Mk(Dadd, "dadd", pop: 4, push: 2));

        Put(Mk(Getstatic, "getstatic", 2, cp: true, push: 1));
        Put(Mk(Putstatic, "putstatic", 2, cp: true, pop: 1));
        Put(Mk(Getfield, "getfield", 2, cp: true, pop: 1, push: 1));
        Put(Mk(Putfield, "putfield", 2, cp: true, pop: 2));
        Put(Mk(Invokevirtual, "invokevirtual", 2, InsnFlow.Invoke, cp: true));
        Put(Mk(Invokespecial, "invokespecial", 2, InsnFlow.Invoke, cp: true));
        Put(Mk(Invokestatic, "invokestatic", 2, InsnFlow.Invoke, cp: true));
        Put(Mk(Invokeinterface, "invokeinterface", 4, InsnFlow.Invoke, cp: true));
        Put(Mk(Invokedynamic, "invokedynamic", 4, InsnFlow.Invoke, cp: true));

        Put(Mk(New, "new", 2, cp: true, push: 1));
        Put(Mk(Newarray, "newarray", 1, pop: 1, push: 1));
        Put(Mk(Anewarray, "anewarray", 2, cp: true, pop: 1, push: 1));
        Put(Mk(Arraylength, "arraylength", pop: 1, push: 1));
        Put(Mk(Athrow, "athrow", 0, InsnFlow.Throw, pop: 1));
        Put(Mk(Checkcast, "checkcast", 2, cp: true));
        Put(Mk(Instanceof, "instanceof", 2, cp: true, pop: 1, push: 1));
        Put(Mk(Monitorenter, "monitorenter", pop: 1));
        Put(Mk(Monitorexit, "monitorexit", pop: 1));
        Put(Mk(Multianewarray, "multianewarray", 3, cp: true));

        string[] cmp = { "eq", "ne", "lt", "ge", "gt", "le" };
        for (int i = 0; i < 6; i++)
        {
            Put(Mk(Ifeq + i, $"if{cmp[i]}", 2, InsnFlow.ConditionalBranch, pop: 1));
            Put(Mk(IfIcmpeq + i, $"if_icmp{cmp[i]}", 2, InsnFlow.ConditionalBranch, pop: 2));
        }
        Put(Mk(IfAcmpeq, "if_acmpeq", 2, InsnFlow.ConditionalBranch, pop: 2));
        Put(Mk(IfAcmpne, "if_acmpne", 2, InsnFlow.ConditionalBranch, pop: 2));
        Put(Mk(IfNull, "ifnull", 2, InsnFlow.ConditionalBranch, pop: 1));
        Put(Mk(IfNonNull, "ifnonnull", 2, InsnFlow.ConditionalBranch, pop: 1));
        Put(Mk(Goto, "goto", 2, InsnFlow.UnconditionalBranch));
        Put(Mk(GotoW, "goto_w", 4, InsnFlow.UnconditionalBranch));
        Put(Mk(Jsr, "jsr", 2, InsnFlow.UnconditionalBranch, push: 1));
        Put(Mk(JsrW, "jsr_w", 4, InsnFlow.UnconditionalBranch, push: 1));
        Put(Mk(Ret, "ret", 1));
        Put(Mk(Tableswitch, "tableswitch", 0, InsnFlow.Switch, pop: 1));
        Put(Mk(Lookupswitch, "lookupswitch", 0, InsnFlow.Switch, pop: 1));
        Put(Mk(Wide, "wide", 0));

        Put(Mk(Ireturn, "ireturn", 0, InsnFlow.Return, pop: 1));
        Put(Mk(Lreturn, "lreturn", 0, InsnFlow.Return, pop: 2));
        Put(Mk(Freturn, "freturn", 0, InsnFlow.Return, pop: 1));
        Put(Mk(Dreturn, "dreturn", 0, InsnFlow.Return, pop: 2));
        Put(Mk(Areturn, "areturn", 0, InsnFlow.Return, pop: 1));
        Put(Mk(Return, "return", 0, InsnFlow.Return));
        return d;
    }
}
