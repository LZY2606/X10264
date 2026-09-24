using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public sealed partial class BytecodeVerifier
{
    private void LocalLoad(VFrame f, int idx, int op)
    {
        var v = f.GetLocal(idx);
        if (v is TopType)
            Emit(VerifyEventCode.TypeMismatch, Severity.Failure,
                $"Load from uninitialized local {idx} at {op:X2}", fragment: "code", rule: "local-load");
        f.Push(v);
    }

    private void LocalStore(VFrame f, int idx, int op)
    {
        var v = f.Pop();
        f.SetLocal(idx, v);
    }

    private void LoadArray(int op, VFrame f)
    {
        f.Pop(); // index (int)
        ExpectRef(f.Pop(), op, 0);
        f.Push(op switch
        {
            46 or 51 or 52 or 53 => (VType)VType.Int,
            47 => VType.Long, 48 => VType.Float, 49 => VType.Double,
            _ => VType.Ref("java/lang/Object")
        });
    }

    private void StoreArray(int op, VFrame f)
    {
        f.Pop(); // value
        f.Pop(); // index
        ExpectRef(f.Pop(), op, 0);
    }

    private VType LdcType(int cpIndex)
    {
        var e = _c.ClassFile.ConstantPool.At(cpIndex);
        if (e is null) return VType.Top;
        return e.Tag switch
        {
            3 => VType.Int, 4 => VType.Float, 8 => VType.Ref("java/lang/String"),
            7 => VType.Ref("java/lang/Class"),
            _ => VType.Top
        };
    }

    private VType Ldc2Type(int cpIndex)
    {
        var e = _c.ClassFile.ConstantPool.At(cpIndex);
        if (e is null) return VType.Top;
        return e.Tag == 5 ? VType.Long : e.Tag == 6 ? VType.Double : VType.Top;
    }

    private void PopN(VFrame f, int n) { for (int i = 0; i < n; i++) f.Pop(); }

    private void PopCat(VFrame f, int expectedCategory, int op, int bci)
    {
        if (f.LogicalDepth == 0) throw new StackUnderflowException();
        var top = f.Stack[f.Stack.Count - 1];
        if (top.IsCategory2)
            Emit(VerifyEventCode.WideStackMismatch, Severity.Failure,
                $"pop at bci {bci} removes 1 slot but top is category-2 {top.Token}; " +
                "use pop2 for wide values",
                bci, fragment: "code", rule: "category-2-stack");
        f.Pop();
    }

    private void PopCat2(VFrame f, int op, int bci)
    {
        if (f.LogicalDepth == 0) throw new StackUnderflowException();
        var top = f.Stack[f.Stack.Count - 1];
        // pop2 removes a category-2 value OR two category-1 values. A single cat-1
        // top with nothing below is the narrow-stack failure shape.
        if (!top.IsCategory2 && f.LogicalDepth < 2)
            Emit(VerifyEventCode.WideStackMismatch, Severity.Failure,
                $"pop2 at bci {bci} needs a category-2 value or two category-1 values, " +
                "but the stack has only one category-1 value",
                bci, fragment: "code", rule: "category-2-stack");
        if (top.IsCategory2) f.Pop();
        else { f.Pop(); f.Pop(); }
    }

    private void Dup(VFrame f, int valueSlots, int insertBelow)
    {
        if (f.Stack.Count == 0) throw new StackUnderflowException();
        var top = f.Stack[^1];
        if (valueSlots == 1 && top.IsCategory2)
            Emit(VerifyEventCode.WideStackMismatch, Severity.Failure,
                $"dup targets 1 slot but top is category-2 {top.Token}", rule: "category-2-stack");
        f.Stack.Add(top);
    }

    private void Dup2(VFrame f)
    {
        if (f.Stack.Count == 0) throw new StackUnderflowException();
        var top = f.Stack[^1];
        if (top.IsCategory2) f.Stack.Add(top);
        else if (f.Stack.Count >= 2) { f.Stack.Add(f.Stack[^2]); f.Stack.Add(top); }
        else throw new StackUnderflowException();
    }

    private void SwapDup(VFrame f)
    {
        if (f.Stack.Count < 2) throw new StackUnderflowException();
        (f.Stack[^1], f.Stack[^2]) = (f.Stack[^2], f.Stack[^1]);
        if (f.Stack[^1].IsCategory2 || f.Stack[^2].IsCategory2)
            Emit(VerifyEventCode.WideStackMismatch, Severity.Failure,
                "swap cannot operate on category-2 values", rule: "category-2-stack");
    }

    private void Arithmetic(int op, VFrame f, int bci)
    {
        if (op is >= 148 and <= 152) f.Pop(); // cmp: 2 operands but pop below
        int pops = op switch
        {
            >= 116 and <= 119 or >= 133 and <= 147 => 1, // neg / conversions
            _ => 2
        };
        for (int i = 0; i < pops; i++) f.Pop();
        VType result = op switch
        {
            >= 96 and <= 99 or >= 120 and <= 131 or >= 133 or 136 or 139 or 142
                or >= 145 and <= 152 or 148 => VType.Int,
            100 or 103 or 104 or 105 or 107 or 110 or 111 or 113 or 137 or 140 or 143 or 149 or 150
                => VType.Int,
            97 or 101 or 106 or 108 or 114 or 127 => VType.Long,
            98 or 102 or 109 or 112 or 115 => VType.Float,
            99 or 103 or 110 or 113 => VType.Double,
            _ => VType.Int
        };
        if (op is 104 or 105 or 109 or 110 or 137 or 138 or 140 or 141 or 143 or 144 or 151 or 152)
            result = op is 104 or 105 or 137 or 138 or 151 or 152 ? (op is 151 or 152 or 105 or 138 ? VType.Double : VType.Float) : result;
        // long/double comparisons pop category-2 operands twice physically: model logical once.
        f.Push(result);
    }

    private void ExpectReturn(int op, VFrame f, int bci)
    {
        var v = f.Pop();
        VType want = op switch { 172 => VType.Int, 173 => VType.Long, 174 => VType.Float, 175 => VType.Double, _ => VType.Ref("java/lang/Object") };
        if (op == 176 && !v.IsReference && v is not NullType)
            Emit(VerifyEventCode.TypeMismatch, Severity.Failure,
                $"areturn at bci {bci} gets non-reference {v.Token}", bci, fragment: "code", rule: "return-type");
        if (op != 176 && want is not TopType && v.GetType() != want.GetType())
            Emit(VerifyEventCode.TypeMismatch, Severity.Failure,
                $"Return at bci {bci} wants {want.Token} but stack has {v.Token}", bci, fragment: "code+descriptor", rule: "return-type");
    }

    private void ExpectRef(VType v, int op, int bci)
    {
        if (!v.IsReference && v is not NullType)
            Emit(VerifyEventCode.TypeMismatch, Severity.Failure,
                $"Instruction 0x{op:X2} at bci {bci} expects a reference but found {v.Token}",
                bci, fragment: "code", rule: "reference-expected");
    }

    private VType FieldType(int fieldrefIndex)
    {
        var (desc, _) = FieldrefDescriptor(fieldrefIndex);
        if (desc is null) return VType.Top;
        var rt = desc; // NameAndType descriptor already a field type
        return DescriptorUtil.KindOf(rt) switch
        {
            JvmKind.Int => VType.Int, JvmKind.Long => VType.Long,
            JvmKind.Float => VType.Float, JvmKind.Double => VType.Double,
            JvmKind.Ref => rt.StartsWith("L") ? VType.Ref(rt[1..^1]) : VType.Ref(rt),
            _ => VType.Top
        };
    }

    private void ExpectField(int fieldrefIndex, VFrame f, bool store, bool popRef = false)
    {
        if (store) ExpectRef(f.Pop(), 181, 0);
        var v = FieldType(fieldrefIndex);
        if (store) f.Pop(); else f.Push(v);
    }

    private (string? Descriptor, string? ClassName) FieldrefDescriptor(int refIndex)
    {
        var e = _c.ClassFile.ConstantPool.At(refIndex);
        if (e is null || e.Raw.Length < 4) return (null, null);
        int classIdx = (e.Raw[0] << 8) | e.Raw[1];
        int natIdx = (e.Raw[2] << 8) | e.Raw[3];
        var nat = _c.ClassFile.ConstantPool.At(natIdx);
        string? desc = null;
        if (nat is { Raw.Length: >= 4 })
        {
            int di = (nat.Raw[2] << 8) | nat.Raw[3];
            desc = _c.ClassFile.ConstantPool.Utf8(di);
        }
        return (desc, _c.ClassFile.ConstantPool.ClassName(classIdx));
    }

    private (string? ClassName, string? Name, string? Descriptor) MethodrefInfo(int refIndex)
    {
        var e = _c.ClassFile.ConstantPool.At(refIndex);
        if (e is null || e.Raw.Length < 4) return (null, null, null);
        int classIdx = (e.Raw[0] << 8) | e.Raw[1];
        int natIdx = (e.Raw[2] << 8) | e.Raw[3];
        var nat = _c.ClassFile.ConstantPool.At(natIdx);
        string? name = null, desc = null;
        if (nat is { Raw.Length: >= 4 })
        {
            int ni = (nat.Raw[0] << 8) | nat.Raw[1];
            int di = (nat.Raw[2] << 8) | nat.Raw[3];
            name = _c.ClassFile.ConstantPool.Utf8(ni);
            desc = _c.ClassFile.ConstantPool.Utf8(di);
        }
        return (_c.ClassFile.ConstantPool.ClassName(classIdx), name, desc);
    }
}
