namespace Verifier.Core;

public enum OperandKind { None, Byte, Short, Branch, Index, Index0, Const, Dynamic, TableSwitch, LookupSwitch, Wide }

public sealed record OpSpec(byte Opcode, string Mnemonic, OperandKind Operands = OperandKind.None, int Category = 0);

public static class Opcodes
{
    public static readonly OpSpec[] Table = Build();

    public static OpSpec? Get(int op) => op >= 0 && op < 256 ? Table[op] : null;

    private static OpSpec[] Build()
    {
        var t = new OpSpec[256];
        void Set(byte op, string m, OperandKind k = OperandKind.None) => t[op] = new OpSpec(op, m, k);
        Set(0x00, "nop"); Set(0x01, "aconst_null");
        Set(0x02, "iconst_m1"); Set(0x03, "iconst_0"); Set(0x04, "iconst_1"); Set(0x05, "iconst_2");
        Set(0x06, "iconst_3"); Set(0x07, "iconst_4"); Set(0x08, "iconst_5");
        Set(0x09, "lconst_0"); Set(0x0a, "lconst_1");
        Set(0x0b, "fconst_0"); Set(0x0c, "fconst_1"); Set(0x0d, "fconst_2");
        Set(0x0e, "dconst_0"); Set(0x0f, "dconst_1");
        Set(0x10, "bipush", OperandKind.Byte); Set(0x11, "sipush", OperandKind.Short);
        Set(0x12, "ldc", OperandKind.Index0); Set(0x13, "ldc_w", OperandKind.Index); Set(0x14, "ldc2_w", OperandKind.Index);
        Set(0x15, "iload", OperandKind.Index0); Set(0x16, "lload", OperandKind.Index0); Set(0x17, "fload", OperandKind.Index0);
        Set(0x18, "dload", OperandKind.Index0); Set(0x19, "aload", OperandKind.Index0);
        Set(0x1a, "iload_0"); Set(0x1b, "iload_1"); Set(0x1c, "iload_2"); Set(0x1d, "iload_3");
        Set(0x1e, "lload_0"); Set(0x1f, "lload_1"); Set(0x20, "lload_2"); Set(0x21, "lload_3");
        Set(0x22, "fload_0"); Set(0x23, "fload_1"); Set(0x24, "fload_2"); Set(0x25, "fload_3");
        Set(0x26, "dload_0"); Set(0x27, "dload_1"); Set(0x28, "dload_2"); Set(0x29, "dload_3");
        Set(0x2a, "aload_0"); Set(0x2b, "aload_1"); Set(0x2c, "aload_2"); Set(0x2d, "aload_3");
        Set(0x2e, "iaload"); Set(0x2f, "laload"); Set(0x30, "faload"); Set(0x31, "daload");
        Set(0x32, "aaload"); Set(0x33, "baload"); Set(0x34, "caload"); Set(0x35, "saload");
        Set(0x36, "istore", OperandKind.Index0); Set(0x37, "lstore", OperandKind.Index0); Set(0x38, "fstore", OperandKind.Index0);
        Set(0x39, "dstore", OperandKind.Index0); Set(0x3a, "astore", OperandKind.Index0);
        Set(0x3b, "istore_0"); Set(0x3c, "istore_1"); Set(0x3d, "istore_2"); Set(0x3e, "istore_3");
        Set(0x3f, "lstore_0"); Set(0x40, "lstore_1"); Set(0x41, "lstore_2"); Set(0x42, "lstore_3");
        Set(0x43, "fstore_0"); Set(0x44, "fstore_1"); Set(0x45, "fstore_2"); Set(0x46, "fstore_3");
        Set(0x47, "dstore_0"); Set(0x48, "dstore_1"); Set(0x49, "dstore_2"); Set(0x4a, "dstore_3");
        Set(0x4b, "astore_0"); Set(0x4c, "astore_1"); Set(0x4d, "astore_2"); Set(0x4e, "astore_3");
        Set(0x4f, "iastore"); Set(0x50, "lastore"); Set(0x51, "fastore"); Set(0x52, "dastore");
        Set(0x53, "aastore"); Set(0x54, "bastore"); Set(0x55, "castore"); Set(0x56, "sastore");
        Set(0x57, "pop"); Set(0x58, "pop2"); Set(0x59, "dup"); Set(0x5a, "dup_x1"); Set(0x5b, "dup_x2");
        Set(0x5c, "dup2"); Set(0x5d, "dup2_x1"); Set(0x5e, "dup2_x2"); Set(0x5f, "swap");
        Set(0x60, "iadd"); Set(0x61, "ladd"); Set(0x62, "fadd"); Set(0x63, "dadd");
        Set(0x64, "isub"); Set(0x65, "lsub"); Set(0x66, "fsub"); Set(0x67, "dsub");
        Set(0x68, "imul"); Set(0x69, "lmul"); Set(0x6a, "fmul"); Set(0x6b, "dmul");
        Set(0x6c, "idiv"); Set(0x6d, "ldiv"); Set(0x6e, "fdiv"); Set(0x6f, "ddiv");
        Set(0x70, "irem"); Set(0x71, "lrem"); Set(0x72, "frem"); Set(0x73, "drem");
        Set(0x74, "ineg"); Set(0x75, "lneg"); Set(0x76, "fneg"); Set(0x77, "dneg");
        Set(0x78, "ishl"); Set(0x79, "lshl"); Set(0x7a, "ishr"); Set(0x7b, "lshr");
        Set(0x7c, "iushr"); Set(0x7d, "lushr"); Set(0x7e, "iand"); Set(0x7f, "land");
        Set(0x80, "ior"); Set(0x81, "lor"); Set(0x82, "ixor"); Set(0x83, "lxor");
        Set(0x84, "iinc", OperandKind.Const);
        Set(0x85, "i2l"); Set(0x86, "i2f"); Set(0x87, "i2d"); Set(0x88, "l2i"); Set(0x89, "l2f"); Set(0x8a, "l2d");
        Set(0x8b, "f2i"); Set(0x8c, "f2l"); Set(0x8d, "f2d"); Set(0x8e, "d2i"); Set(0x8f, "d2l"); Set(0x90, "d2f");
        Set(0x91, "i2b"); Set(0x92, "i2c"); Set(0x93, "i2s");
        Set(0x94, "lcmp"); Set(0x95, "fcmpl"); Set(0x96, "fcmpg"); Set(0x97, "dcmpl"); Set(0x98, "dcmpg");
        Set(0x99, "ifeq", OperandKind.Branch); Set(0x9a, "ifne", OperandKind.Branch); Set(0x9b, "iflt", OperandKind.Branch);
        Set(0x9c, "ifge", OperandKind.Branch); Set(0x9d, "ifgt", OperandKind.Branch); Set(0x9e, "ifle", OperandKind.Branch);
        Set(0x9f, "if_icmpeq", OperandKind.Branch); Set(0xa0, "if_icmpne", OperandKind.Branch);
        Set(0xa1, "if_icmplt", OperandKind.Branch); Set(0xa2, "if_icmpge", OperandKind.Branch);
        Set(0xa3, "if_icmpgt", OperandKind.Branch); Set(0xa4, "if_icmple", OperandKind.Branch);
        Set(0xa5, "if_acmpeq", OperandKind.Branch); Set(0xa6, "if_acmpne", OperandKind.Branch);
        Set(0xa7, "goto", OperandKind.Branch); Set(0xa8, "jsr", OperandKind.Branch); Set(0xa9, "ret", OperandKind.Index0);
        Set(0xaa, "tableswitch", OperandKind.TableSwitch); Set(0xab, "lookupswitch", OperandKind.LookupSwitch);
        Set(0xac, "ireturn"); Set(0xad, "lreturn"); Set(0xae, "freturn"); Set(0xaf, "dreturn");
        Set(0xb0, "areturn"); Set(0xb1, "return");
        Set(0xb2, "getstatic", OperandKind.Index); Set(0xb3, "putstatic", OperandKind.Index);
        Set(0xb4, "getfield", OperandKind.Index); Set(0xb5, "putfield", OperandKind.Index);
        Set(0xb6, "invokevirtual", OperandKind.Index); Set(0xb7, "invokespecial", OperandKind.Index);
        Set(0xb8, "invokestatic", OperandKind.Index); Set(0xb9, "invokeinterface", OperandKind.Index);
        Set(0xba, "invokedynamic", OperandKind.Dynamic); Set(0xbb, "new", OperandKind.Index);
        Set(0xbc, "newarray", OperandKind.Byte); Set(0xbd, "anewarray", OperandKind.Index);
        Set(0xbe, "arraylength"); Set(0xbf, "athrow"); Set(0xc0, "checkcast", OperandKind.Index);
        Set(0xc1, "instanceof", OperandKind.Index); Set(0xc2, "monitorenter"); Set(0xc3, "monitorexit");
        Set(0xc4, "wide", OperandKind.Wide); Set(0xc5, "multianewarray", OperandKind.Index);
        Set(0xc6, "ifnull", OperandKind.Branch); Set(0xc7, "ifnonnull", OperandKind.Branch);
        Set(0xc8, "goto_w", OperandKind.Branch); Set(0xc9, "jsr_w", OperandKind.Branch);
        for (var i = 0; i < 256; i++) t[i] ??= new OpSpec((byte)i, $"unknown_0x{i:x2}");
        return t;
    }
}
