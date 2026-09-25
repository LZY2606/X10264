namespace BytecodeSeal.Core;

public enum OperandFormat { None, U1, I1, U2, I2, Branch2, Branch4, Cp1, Cp2, Cp2U1Zero, Cp2ZeroZero, Iinc, Unsupported }

public sealed record OpInfo(string Mnemonic, OperandFormat Format);

/// <summary>Opcode metadata for the instruction subset the verifier interprets.
/// Opcodes outside the supported set decode with format Unsupported so the
/// verifier can report them honestly instead of guessing stack effects.</summary>
public static class Opcodes
{
    public static readonly OpInfo?[] Table = new OpInfo?[256];

    static Opcodes()
    {
        void R(int op, string m, OperandFormat f = OperandFormat.None) => Table[op] = new OpInfo(m, f);
        R(0x00, "nop");
        R(0x01, "aconst_null");
        R(0x02, "iconst_m1"); R(0x03, "iconst_0"); R(0x04, "iconst_1"); R(0x05, "iconst_2");
        R(0x06, "iconst_3"); R(0x07, "iconst_4"); R(0x08, "iconst_5");
        R(0x09, "lconst_0"); R(0x0a, "lconst_1");
        R(0x0b, "fconst_0"); R(0x0c, "fconst_1"); R(0x0d, "fconst_2");
        R(0x0e, "dconst_0"); R(0x0f, "dconst_1");
        R(0x10, "bipush", OperandFormat.I1);
        R(0x11, "sipush", OperandFormat.I2);
        R(0x12, "ldc", OperandFormat.Cp1);
        R(0x13, "ldc_w", OperandFormat.Cp2);
        R(0x14, "ldc2_w", OperandFormat.Cp2);
        R(0x15, "iload", OperandFormat.U1); R(0x16, "lload", OperandFormat.U1);
        R(0x17, "fload", OperandFormat.U1); R(0x18, "dload", OperandFormat.U1);
        R(0x19, "aload", OperandFormat.U1);
        R(0x1a, "iload_0"); R(0x1b, "iload_1"); R(0x1c, "iload_2"); R(0x1d, "iload_3");
        R(0x1e, "lload_0"); R(0x1f, "lload_1"); R(0x20, "lload_2"); R(0x21, "lload_3");
        R(0x22, "fload_0"); R(0x23, "fload_1"); R(0x24, "fload_2"); R(0x25, "fload_3");
        R(0x26, "dload_0"); R(0x27, "dload_1"); R(0x28, "dload_2"); R(0x29, "dload_3");
        R(0x2a, "aload_0"); R(0x2b, "aload_1"); R(0x2c, "aload_2"); R(0x2d, "aload_3");
        R(0x36, "istore", OperandFormat.U1); R(0x37, "lstore", OperandFormat.U1);
        R(0x38, "fstore", OperandFormat.U1); R(0x39, "dstore", OperandFormat.U1);
        R(0x3a, "astore", OperandFormat.U1);
        R(0x3b, "istore_0"); R(0x3c, "istore_1"); R(0x3d, "istore_2"); R(0x3e, "istore_3");
        R(0x3f, "lstore_0"); R(0x40, "lstore_1"); R(0x41, "lstore_2"); R(0x42, "lstore_3");
        R(0x43, "fstore_0"); R(0x44, "fstore_1"); R(0x45, "fstore_2"); R(0x46, "fstore_3");
        R(0x47, "dstore_0"); R(0x48, "dstore_1"); R(0x49, "dstore_2"); R(0x4a, "dstore_3");
        R(0x4b, "astore_0"); R(0x4c, "astore_1"); R(0x4d, "astore_2"); R(0x4e, "astore_3");
        R(0x57, "pop"); R(0x58, "pop2");
        R(0x59, "dup"); R(0x5a, "dup_x1"); R(0x5b, "dup_x2"); R(0x5c, "dup2");
        R(0x5f, "swap");
        R(0x60, "iadd"); R(0x61, "ladd"); R(0x62, "fadd"); R(0x63, "dadd");
        R(0x64, "isub"); R(0x65, "lsub"); R(0x66, "fsub"); R(0x67, "dsub");
        R(0x68, "imul"); R(0x69, "lmul"); R(0x6a, "fmul"); R(0x6b, "dmul");
        R(0x6c, "idiv"); R(0x6d, "ldiv"); R(0x6e, "fdiv"); R(0x6f, "ddiv");
        R(0x70, "irem"); R(0x71, "lrem"); R(0x72, "frem"); R(0x73, "drem");
        R(0x74, "ineg"); R(0x75, "lneg"); R(0x76, "fneg"); R(0x77, "dneg");
        R(0x78, "ishl"); R(0x79, "lshl"); R(0x7a, "ishr"); R(0x7b, "lshr");
        R(0x7c, "iushr"); R(0x7d, "lushr");
        R(0x7e, "iand"); R(0x7f, "land"); R(0x80, "ior"); R(0x81, "lor");
        R(0x82, "ixor"); R(0x83, "lxor");
        R(0x84, "iinc", OperandFormat.Iinc);
        R(0x85, "i2l"); R(0x86, "i2f"); R(0x87, "i2d");
        R(0x88, "l2i"); R(0x89, "l2f"); R(0x8a, "l2d");
        R(0x8b, "f2i"); R(0x8c, "f2l"); R(0x8d, "f2d");
        R(0x8e, "d2i"); R(0x8f, "d2l"); R(0x90, "d2f");
        R(0x91, "i2b"); R(0x92, "i2c"); R(0x93, "i2s");
        R(0x94, "lcmp"); R(0x95, "fcmpl"); R(0x96, "fcmpg");
        R(0x97, "dcmpl"); R(0x98, "dcmpg");
        R(0x99, "ifeq", OperandFormat.Branch2); R(0x9a, "ifne", OperandFormat.Branch2);
        R(0x9b, "iflt", OperandFormat.Branch2); R(0x9c, "ifge", OperandFormat.Branch2);
        R(0x9d, "ifgt", OperandFormat.Branch2); R(0x9e, "ifle", OperandFormat.Branch2);
        R(0x9f, "if_icmpeq", OperandFormat.Branch2); R(0xa0, "if_icmpne", OperandFormat.Branch2);
        R(0xa1, "if_icmplt", OperandFormat.Branch2); R(0xa2, "if_icmpge", OperandFormat.Branch2);
        R(0xa3, "if_icmpgt", OperandFormat.Branch2); R(0xa4, "if_icmple", OperandFormat.Branch2);
        R(0xa5, "if_acmpeq", OperandFormat.Branch2); R(0xa6, "if_acmpne", OperandFormat.Branch2);
        R(0xa7, "goto", OperandFormat.Branch2);
        R(0xa8, "jsr", OperandFormat.Branch2); // reported as unsupported by verifier
        R(0xac, "ireturn"); R(0xad, "lreturn"); R(0xae, "freturn");
        R(0xaf, "dreturn"); R(0xb0, "areturn"); R(0xb1, "return");
        R(0xb2, "getstatic", OperandFormat.Cp2); R(0xb3, "putstatic", OperandFormat.Cp2);
        R(0xb4, "getfield", OperandFormat.Cp2); R(0xb5, "putfield", OperandFormat.Cp2);
        R(0xb6, "invokevirtual", OperandFormat.Cp2);
        R(0xb7, "invokespecial", OperandFormat.Cp2);
        R(0xb8, "invokestatic", OperandFormat.Cp2);
        R(0xb9, "invokeinterface", OperandFormat.Cp2U1Zero);
        R(0xba, "invokedynamic", OperandFormat.Cp2ZeroZero);
        R(0xbb, "new", OperandFormat.Cp2);
        R(0xbd, "anewarray", OperandFormat.Cp2);
        R(0xbf, "athrow");
        R(0xc0, "checkcast", OperandFormat.Cp2); R(0xc1, "instanceof", OperandFormat.Cp2);
        R(0xc2, "monitorenter"); R(0xc3, "monitorexit");
        R(0xc6, "ifnull", OperandFormat.Branch2); R(0xc7, "ifnonnull", OperandFormat.Branch2);
        R(0xc8, "goto_w", OperandFormat.Branch4);
    }
}
