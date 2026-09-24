namespace JvmVerify.Core.Bytecode;

public enum OpForm
{
    NoOperands,
    CpIndex2,
    CpIndex2Plus0,
    LocalIndex1,
    LocalIndex1Plus0,
    Branch2,
    Branch4,
    Byte1,
    Short2,
    Iinc,
    WideLocal,
    WideIinc,
    Newarray,
    TableSwitch,
    LookupSwitch,
    Multianewarray
}

public sealed record OpInfo(byte Opcode, string Mnemonic, OpForm Form)
{
    public OperandKind OperandKind => Form switch
    {
        OpForm.CpIndex2 or OpForm.CpIndex2Plus0 or OpForm.Newarray => OperandKind.CpIndex,
        OpForm.Branch2 or OpForm.Branch4 => OperandKind.BranchOffset,
        OpForm.LocalIndex1 or OpForm.LocalIndex1Plus0 or OpForm.WideLocal => OperandKind.LocalIndex,
        OpForm.Byte1 => OperandKind.Byte,
        OpForm.Short2 => OperandKind.Short,
        OpForm.Iinc or OpForm.WideIinc => OperandKind.IincIncr,
        OpForm.TableSwitch => OperandKind.TableSwitch,
        OpForm.LookupSwitch => OperandKind.LookupSwitch,
        OpForm.Multianewarray => OperandKind.Multianewarray,
        _ => OperandKind.None
    };
}

public static class Opcodes
{
    private static OpInfo OP(byte op, string m, OpForm f = OpForm.NoOperands) => new(op, m, f);

    public static readonly IReadOnlyDictionary<byte, OpInfo> Table = new[]
    {
        OP(0,"nop"), OP(1,"aconst_null"), OP(2,"iconst_m1"),
        OP(3,"iconst_0"), OP(4,"iconst_1"), OP(5,"iconst_2"), OP(6,"iconst_3"),
        OP(7,"iconst_4"), OP(8,"iconst_5"), OP(9,"lconst_0"), OP(10,"lconst_1"),
        OP(11,"fconst_0"), OP(12,"fconst_1"), OP(13,"fconst_2"),
        OP(14,"dconst_0"), OP(15,"dconst_1"), OP(16,"bipush",OpForm.Byte1), OP(17,"sipush",OpForm.Short2),
        OP(18,"ldc",OpForm.CpIndex2Plus0), OP(19,"ldc_w",OpForm.CpIndex2), OP(20,"ldc2_w",OpForm.CpIndex2),
        OP(21,"iload",OpForm.LocalIndex1), OP(22,"lload",OpForm.LocalIndex1), OP(23,"fload",OpForm.LocalIndex1),
        OP(24,"dload",OpForm.LocalIndex1), OP(25,"aload",OpForm.LocalIndex1),
        OP(26,"iload_0"),OP(27,"iload_1"),OP(28,"iload_2"),OP(29,"iload_3"),
        OP(30,"lload_0"),OP(31,"lload_1"),OP(32,"lload_2"),OP(33,"lload_3"),
        OP(34,"fload_0"),OP(35,"fload_1"),OP(36,"fload_2"),OP(37,"fload_3"),
        OP(38,"dload_0"),OP(39,"dload_1"),OP(40,"dload_2"),OP(41,"dload_3"),
        OP(42,"aload_0"),OP(43,"aload_1"),OP(44,"aload_2"),OP(45,"aload_3"),
        OP(46,"iaload"),OP(47,"laload"),OP(48,"faload"),OP(49,"daload"),
        OP(50,"aaload"),OP(51,"baload"),OP(52,"caload"),OP(53,"saload"),
        OP(54,"istore",OpForm.LocalIndex1), OP(55,"lstore",OpForm.LocalIndex1), OP(56,"fstore",OpForm.LocalIndex1),
        OP(57,"dstore",OpForm.LocalIndex1), OP(58,"astore",OpForm.LocalIndex1),
        OP(59,"istore_0"),OP(60,"istore_1"),OP(61,"istore_2"),OP(62,"istore_3"),
        OP(63,"lstore_0"),OP(64,"lstore_1"),OP(65,"lstore_2"),OP(66,"lstore_3"),
        OP(67,"fstore_0"),OP(68,"fstore_1"),OP(69,"fstore_2"),OP(70,"fstore_3"),
        OP(71,"dstore_0"),OP(72,"dstore_1"),OP(73,"dstore_2"),OP(74,"dstore_3"),
        OP(75,"astore_0"),OP(76,"astore_1"),OP(77,"astore_2"),OP(78,"astore_3"),
        OP(79,"iastore"),OP(80,"lastore"),OP(81,"fastore"),OP(82,"dastore"),
        OP(83,"aastore"),OP(84,"bastore"),OP(85,"castore"),OP(86,"sastore"),
        OP(87,"pop"),OP(88,"pop2"),OP(89,"dup"),OP(90,"dup_x1"),OP(91,"dup_x2"),
        OP(92,"dup2"),OP(93,"dup2_x1"),OP(94,"dup2_x2"),OP(95,"swap"),
        OP(96,"iadd"),OP(97,"ladd"),OP(98,"fadd"),OP(99,"dadd"),
        OP(100,"isub"),OP(101,"lsub"),OP(102,"fsub"),OP(103,"dsub"),
        OP(104,"imul"),OP(105,"lmul"),OP(106,"fmul"),OP(107,"dmul"),
        OP(108,"idiv"),OP(109,"ldiv"),OP(110,"fdiv"),OP(111,"ddiv"),
        OP(112,"irem"),OP(113,"lrem"),OP(114,"frem"),OP(115,"drem"),
        OP(116,"ineg"),OP(117,"lneg"),OP(118,"fneg"),OP(119,"dneg"),
        OP(120,"ishl"),OP(121,"lshl"),OP(122,"ishr"),OP(123,"lshr"),
        OP(124,"iushr"),OP(125,"lushr"),OP(126,"iand"),OP(127,"land"),
        OP(128,"ior"),OP(129,"lor"),OP(130,"ixor"),OP(131,"lxor"),
        OP(132,"iinc",OpForm.Iinc),
        OP(133,"i2l"),OP(134,"i2f"),OP(135,"i2d"),OP(136,"l2i"),OP(137,"l2f"),OP(138,"l2d"),
        OP(139,"f2i"),OP(140,"f2l"),OP(141,"f2d"),OP(142,"d2i"),OP(143,"d2l"),OP(144,"d2f"),
        OP(145,"i2b"),OP(146,"i2c"),OP(147,"i2s"),
        OP(148,"lcmp"),OP(149,"fcmpl"),OP(150,"fcmpg"),OP(151,"dcmpl"),OP(152,"dcmpg"),
        OP(153,"ifeg",OpForm.Branch2), OP(154,"ifne",OpForm.Branch2), OP(155,"iflt",OpForm.Branch2),
        OP(156,"ifge",OpForm.Branch2), OP(157,"ifgt",OpForm.Branch2), OP(158,"ifle",OpForm.Branch2),
        OP(159,"if_icmpeq",OpForm.Branch2), OP(160,"if_icmpne",OpForm.Branch2),
        OP(161,"if_icmplt",OpForm.Branch2), OP(162,"if_icmpge",OpForm.Branch2),
        OP(163,"if_icmpgt",OpForm.Branch2), OP(164,"if_icmple",OpForm.Branch2),
        OP(165,"ifacmpeq",OpForm.Branch2), OP(166,"ifacmpne",OpForm.Branch2),
        OP(167,"goto",OpForm.Branch2), OP(168,"jsr",OpForm.Branch2), OP(169,"ret",OpForm.LocalIndex1),
        OP(170,"tableswitch",OpForm.TableSwitch), OP(171,"lookupswitch",OpForm.LookupSwitch),
        OP(172,"ireturn"),OP(173,"lreturn"),OP(174,"freturn"),OP(175,"dreturn"),OP(176,"areturn"),OP(177,"return"),
        OP(178,"getstatic",OpForm.CpIndex2), OP(179,"putstatic",OpForm.CpIndex2),
        OP(180,"getfield",OpForm.CpIndex2), OP(181,"putfield",OpForm.CpIndex2),
        OP(182,"invokevirtual",OpForm.CpIndex2), OP(183,"invokespecial",OpForm.CpIndex2),
        OP(184,"invokestatic",OpForm.CpIndex2), OP(185,"invokeinterface",OpForm.CpIndex2Plus0),
        OP(186,"invokedynamic",OpForm.CpIndex2Plus0),
        OP(187,"new",OpForm.CpIndex2), OP(188,"newarray",OpForm.Newarray),
        OP(189,"anewarray",OpForm.CpIndex2), OP(190,"arraylength"),
        OP(191,"athrow"), OP(192,"checkcast",OpForm.CpIndex2), OP(193,"instanceof",OpForm.CpIndex2),
        OP(194,"monitorenter"),OP(195,"monitorexit"),OP(196,"wide",OpForm.WideLocal),
        OP(197,"multianewarray",OpForm.Multianewarray),
        OP(198,"ifnull",OpForm.Branch2), OP(199,"ifnonnull",OpForm.Branch2),
        OP(200,"goto_w",OpForm.Branch4), OP(201,"jsr_w",OpForm.Branch4)
    }.ToDictionary(o => o.Opcode);

    public static string NewarrayTypeName(byte at) => at switch
    {
        4 => "boolean", 5 => "char", 6 => "float", 7 => "double",
        8 => "byte", 9 => "short", 10 => "int", 11 => "long",
        _ => $"badtype({at})"
    };
}
