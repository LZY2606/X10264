using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public sealed partial class BytecodeVerifier
{
    private void Execute(Instruction ins, VFrame f, BasicBlock block, int bci, Queue<int> work)
    {
        byte op = ins.Spec.Opcode;
        var cp = _c.ClassFile.ConstantPool;

        switch (op)
        {
            case 1: f.Push(VType.Null); break;
            case >= 2 and <= 8: f.Push(VType.Int); break;
            case 9 or 10: f.Push(VType.Long); break;
            case >= 11 and <= 13: f.Push(VType.Float); break;
            case 14 or 15: f.Push(VType.Double); break;
            case 16 or 17: f.Push(VType.Int); break;
            case 18: case 19:
                f.Push(LdcType(ins.OperandU2 != 0 ? (op == 18 ? ins.OperandU1 : ins.OperandU2) : ins.OperandU1));
                break;
            case 20: f.Push(Ldc2Type(ins.OperandU2)); break;

            case >= 21 and <= 25: LocalLoad(f, ins.OperandU1, op); break;
            case >= 26 and <= 45: LocalLoad(f, op - 26, op); break;
            case >= 54 and <= 58: LocalStore(f, ins.OperandU1, op); break;
            case >= 59 and <= 78: LocalStore(f, op - 59, op); break;

            case >= 46 and <= 53: LoadArray(op, f); break;
            case >= 79 and <= 86: StoreArray(op, f); break;
            case 188: f.Pop(); f.Push(VType.Int); break; // newarray pops size
            case 189: f.Pop(); f.Push(VType.Ref(arrayName(ins.OperandU2) + "[]")); break;
            case 197: PopN(f, 1 + ins.OperandBytes[2]); f.Push(VType.Ref("array")); break;
            case 190: ExpectRef(f.Pop(), op, bci); f.Push(VType.Int); break;
            case 192: ExpectRef(f.Pop(), op, bci); f.Push(VType.Ref(cp.ClassName(ins.OperandU2) ?? "java/lang/Object")); break;
            case 193: ExpectRef(f.Pop(), op, bci); f.Push(VType.Int); break;

            case 87: PopCat(f, 1, op, bci); break;
            case 88: PopCat2(f, op, bci); break;
            case 89: Dup(f, 1, 0); break;
            case 90: Dup(f, 1, 1); break;
            case 91: Dup(f, 1, 2); break;
            case 92: Dup2(f); break;
            case 93: Dup(f, 2, 2); break;
            case 94: Dup(f, 2, 3); break;
            case 95: SwapDup(f); break;

            case 132: // iinc local, const
                CheckIinc(ins.OperandBytes[0], f, bci);
                break;
            case >= 96 and <= 152: Arithmetic(op, f, bci); break;

            case >= 153 and <= 158: PopN(f, 1); break;                 // ifeq..ifle
            case >= 159 and <= 166: PopN(f, 2); break;                // if_icmp*, if_acmp*
            case 198 or 199: ExpectRef(f.Pop(), op, bci); break;      // ifnull/ifnonnull
            case 167 or 200: break;                                   // goto
            case 168 or 201:
                if (!_c.Profile.JsrRetAllowed)
                    Emit(VerifyEventCode.JsrRetVersion, Severity.Failure,
                        $"{ins.Mnemonic} at {bci} is forbidden in class major {_c.Profile.MajorVersion} (>=51)",
                        bci, block.Index, fragment: "code+major_version", rule: "jsr-ret-version");
                f.Push(VType.Addr(ins.EndOffset));
                break;
            case 169:
                if (!_c.Profile.JsrRetAllowed)
                    Emit(VerifyEventCode.JsrRetVersion, Severity.Failure,
                        $"ret at {bci} is forbidden in class major {_c.Profile.MajorVersion} (>=51)",
                        bci, block.Index, fragment: "code+major_version", rule: "jsr-ret-version");
                break;
            case 170 or 171: PopN(f, 1); break; // switch on int

            case 172 or 173 or 174 or 175: ExpectReturn(op, f, bci); break;
            case 176: ExpectRef(f.Pop(), op, bci); break;
            case 177: // return (void): constructor must have initialized this
                if (_c.IsConstructor && f.Locals.Length > 0 && f.Locals[0] is UninitThisType)
                    Emit(VerifyEventCode.UninitializedUse, Severity.Failure,
                        $"Constructor {_c.MethodName} returns while local 0 is still uninitializedThis",
                        bci, block.Index, fragment: "code", rule: "constructor-return");
                break;
            case 191: // athrow
                ExpectRef(f.Pop(), op, bci);
                break;

            case 178: f.Push(FieldType(ins.OperandU2)); break;
            case 179: ExpectField(ins.OperandU2, f, store: true); break;
            case 180: ExpectRef(f.Pop(), op, bci); f.Push(FieldType(ins.OperandU2)); break;
            case 181: ExpectField(ins.OperandU2, f, store: true, popRef: true); break;

            case 182 or 184: Invoke(ins.OperandU2, f, bci, special: false, isInterface: false); break;
            case 183: Invoke(ins.OperandU2, f, bci, special: true, isInterface: false); break;
            case 185: Invoke(ins.OperandU2, f, bci, special: false, isInterface: true); break;
            case 187: f.Push(VType.Uninit(bci)); break; // new
            case 194: f.Pop(); break; // monitorenter
            case 195: f.Pop(); break; // monitorexit
            default:
                Emit(VerifyEventCode.InstructionDecode, Severity.Warning,
                    $"Opcode {ins.Mnemonic} (0x{op:X2}) not modeled; stack effects skipped",
                    bci, block.Index, fragment: "code", rule: "model-coverage");
                break;
        }
    }

    private string arrayName(int classIndex) =>
        _c.ClassFile.ConstantPool.ClassName(classIndex) ?? "?";
}
