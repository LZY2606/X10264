namespace JvmCore;

/// <summary>Per-instruction abstract semantics, shared by both verifier regimes.</summary>
internal static class Stepper
{
    public static List<(int? target, FrameState state, string kind)> Step(
        ClassFile cls, MethodModel method, Instruction ins, FrameState f, int maxStack,
        IVerificationSemantics sem, VerificationResult result, int version)
    {
        var cp = cls.ConstantPool;
        var list = new List<(int?, FrameState, string)>();
        var fall = ins.NextOffset;

        void Error(string code, string msg)
        {
            var path = result.PathFor?.Invoke(ins.Offset) ?? new List<int> { ins.Offset };
            if (!result.Errors.Any(e => e.Offset == ins.Offset && e.Code == code))
                result.Errors.Add(new VerifyError(ins.Offset, code, msg, path));
        }

        VType? Pop()
        {
            if (f.Stack.Length == 0) { Error("stack-underflow", $"stack underflow at {ins.Mnemonic}"); return VType.Top; }
            var t = f.Stack[^1];
            f.Stack = f.Stack[..^1];
            return t;
        }

        bool Push(VType t)
        {
            if (f.Stack.Length + (t.IsCategory2 ? 2 : 1) > maxStack)
            { Error("stack-overflow", $"operand stack would exceed max_stack {maxStack}"); return false; }
            var s = new List<VType>(f.Stack) { t };
            if (t.IsCategory2) s.Add(VType.SecondSlot);
            f.Stack = s.ToArray();
            return true;
        }

        bool Expect(VType actual, VType expected, string what)
        {
            if (actual.Kind == VKind.Top) return false; // best-effort continuation, no extra error
            if (expected.Kind == VKind.Int && actual.Kind == VKind.Int) return true;
            if (expected.Kind == VKind.Long && actual.Kind == VKind.Long) return true;
            if (expected.Kind == VKind.Double && actual.Kind == VKind.Double) return true;
            if (expected.Kind == VKind.Float && actual.Kind == VKind.Float) return true;
            if (expected.Kind == VKind.Ref)
            {
                if (actual.Kind == VKind.Null) return true;
                if (actual.Kind == VKind.Ref) return true;
            }
            Error("type-mismatch", $"{what}: expected {expected}, actual {actual}");
            return false;
        }

        int LocalIndex(int implicitIdx) => implicitIdx;

        VType LoadLocal(int n, VType expected, string what)
        {
            if (n < 0 || n >= f.Locals.Length) { Error("local-out-of-range", $"local {n} out of range"); return VType.Top; }
            var t = f.Locals[n];
            if (t.Kind == VKind.SecondSlot)
            {
                Error("category-2-split", $"local {n} is the second slot of a category-2 value");
                result.Judgments.Add(Judgment.Make("category-2-stack-value", ins.Offset,
                    "JVMS §2.6.2 category-2 values occupy two local/stack slots", version,
                    "rejected-half-slot-read", new { local = n }));
                return expected;
            }
            if (expected.Kind == VKind.Long || expected.Kind == VKind.Double)
            {
                if (n + 1 >= f.Locals.Length || f.Locals[n + 1].Kind != VKind.SecondSlot || t.Kind != expected.Kind)
                    Error("category-2-split", $"local {n} does not hold a complete {expected} pair");
                result.Judgments.Add(Judgment.Make("category-2-stack-value", ins.Offset,
                    "JVMS §2.6.2 category-2 values occupy two local/stack slots", version,
                    "paired-read", new { local = n, type = expected.Kind.ToString() }));
            }
            else if (t.Kind != expected.Kind && expected.Kind != VKind.Ref && t.Kind != VKind.Top)
            {
                Error("load-type-mismatch", $"{what}: local {n} is {t}, expected {expected}");
            }
            return t.Kind == VKind.Top ? expected : t;
        }

        void StoreLocal(int n, VType t)
        {
            if (n < 0 || n >= f.Locals.Length) { Error("local-out-of-range", $"local {n} out of range"); return; }
            if (t.IsCategory2 && n + 1 >= f.Locals.Length)
            { Error("category-2-split", $"category-2 store at local {n} exceeds max_locals"); return; }
            if (f.Locals[n].Kind == VKind.SecondSlot)
            { Error("category-2-split", $"storing into the second half of a category-2 local at {n}"); return; }
            if (n + 1 < f.Locals.Length && f.Locals[n + 1].Kind == VKind.SecondSlot && !t.IsCategory2)
            { Error("category-2-split", $"local {n + 1} is the second half of a category-2 value"); return; }
            f.Locals[n] = t;
            if (t.IsCategory2) { f.Locals[n + 1] = VType.SecondSlot; }
            if (t.IsCategory2)
                result.Judgments.Add(Judgment.Make("category-2-stack-value", ins.Offset,
                    "JVMS §2.6.2 category-2 values occupy two local/stack slots", version,
                    "paired-write", new { local = n, type = t.Kind.ToString() }));
        }

        var op = ins.Opcode;

        switch (op)
        {
            case 0: // nop
                list.Add((fall, f, "fallthrough")); break;
            case 1: Push(VType.Null); list.Add((fall, f, "fallthrough")); break;
            case >= 2 and <= 8: Push(VType.Int); list.Add((fall, f, "fallthrough")); break;
            case 9 or 10: Push(VType.Long); list.Add((fall, f, "fallthrough")); break;
            case >= 11 and <= 13: Push(VType.Float); list.Add((fall, f, "fallthrough")); break;
            case 16 or 17: Push(VType.Int); list.Add((fall, f, "fallthrough")); break;
            case 18 or 19:
                var ci = op == 18 ? ins.Operands[0] : BigEndian.UInt16(ins.Operands, 0);
                var tag = cp[ci]?.Tag ?? 0;
                Push(tag == 5 ? VType.Long : tag == 6 ? VType.Double : tag == 4 ? VType.Float
                    : tag == 1 ? VType.RefOf("java/lang/String") : VType.Int);
                list.Add((fall, f, "fallthrough")); break;
            case 20: Push(VType.Long); list.Add((fall, f, "fallthrough")); break;

            case 21: case 23: case 25:
                var ldIdx = ins.Operands[0];
                var ldExp = op == 21 ? VType.Int : op == 23 ? VType.Float : VType.RefOf("?");
                var ldVal = LoadLocal(ldIdx, ldExp, ins.Mnemonic);
                Push(ldVal); list.Add((fall, f, "fallthrough")); break;
            case 22:
                Push(LoadLocal(ins.Operands[0], VType.Long, "lload"));
                list.Add((fall, f, "fallthrough")); break;
            case >= 26 and <= 29: Push(LoadLocal(op - 26, VType.Int, ins.Mnemonic)); list.Add((fall, f, "fallthrough")); break;
            case >= 30 and <= 33:
                Push(LoadLocal(op - 30, VType.Long, ins.Mnemonic));
                list.Add((fall, f, "fallthrough")); break;
            case >= 34 and <= 37: Push(LoadLocal(op - 34, VType.Float, ins.Mnemonic)); list.Add((fall, f, "fallthrough")); break;
            case >= 42 and <= 45: Push(LoadLocal(op - 42, VType.RefOf("?"), ins.Mnemonic)); list.Add((fall, f, "fallthrough")); break;

            case 54: case 56: case 58:
                var stVal = Pop();
                StoreLocal(ins.Operands[0], stVal ?? VType.Top);
                list.Add((fall, f, "fallthrough")); break;
            case 55:
                var hi = Pop(); if (hi?.Kind != VKind.SecondSlot) Error("category-2-split", "lstore expects category-2 high slot on top");
                var lo = Pop(); if (lo?.Kind != VKind.Long) Error("category-2-split", "lstore expects long below the high slot");
                if (lo != null) StoreLocal(ins.Operands[0], lo);
                list.Add((fall, f, "fallthrough")); break;
            case >= 59 and <= 62: StoreLocal(op - 59, Pop() ?? VType.Top); list.Add((fall, f, "fallthrough")); break;
            case >= 63 and <= 66:
                var hi2 = Pop(); if (hi2?.Kind != VKind.SecondSlot) Error("category-2-split", "lstore expects category-2 high slot on top");
                var lo2 = Pop(); if (lo2?.Kind != VKind.Long) Error("category-2-split", "lstore expects long below the high slot");
                if (lo2 != null) StoreLocal(op - 63, lo2);
                list.Add((fall, f, "fallthrough")); break;
            case >= 67 and <= 70: StoreLocal(op - 67, Pop() ?? VType.Top); list.Add((fall, f, "fallthrough")); break;
            case >= 75 and <= 78: StoreLocal(op - 75, Pop() ?? VType.Top); list.Add((fall, f, "fallthrough")); break;

            case 87: // pop
                var popped = Pop();
                if (popped?.IsCategory2 == true) Error("category-2-split", "pop cannot discard a category-2 value");
                list.Add((fall, f, "fallthrough")); break;
            case 88: // pop2
                var a = Pop();
                if (a?.IsCategory2 != true && a?.Kind != VKind.SecondSlot) Pop();
                list.Add((fall, f, "fallthrough")); break;
            case 89: // dup (category-1 top only)
                var top = Pop();
                if (top is { IsCategory2: true } || top?.Kind == VKind.SecondSlot)
                    Error("category-2-split", "dup requires a category-1 value on top");
                else { Push(top); Push(top); }
                list.Add((fall, f, "fallthrough")); break;

            case 96: case 98:
                var i2 = Pop(); var i1 = Pop();
                if (i1?.Kind != VKind.Top) Expect(i1!, op == 96 ? VType.Int : VType.Float, ins.Mnemonic);
                if (i2?.Kind != VKind.Top) Expect(i2!, op == 96 ? VType.Int : VType.Float, ins.Mnemonic);
                Push(op == 96 ? VType.Int : VType.Float);
                list.Add((fall, f, "fallthrough")); break;
            case 97:
                var l2h = Pop(); var l2l = Pop(); var l1h = Pop(); var l1l = Pop();
                if (l2h?.Kind != VKind.SecondSlot || l2l?.Kind != VKind.Long || l1h?.Kind != VKind.SecondSlot || l1l?.Kind != VKind.Long)
                    Error("category-2-split", "ladd expects two long category-2 pairs");
                Push(VType.Long); Push(VType.SecondSlot);
                list.Add((fall, f, "fallthrough")); break;

            case >= 153 and <= 158: // ifeq..ifle: pop int
                var cond = Pop(); if (cond?.Kind != VKind.Top) Expect(cond!, VType.Int, ins.Mnemonic);
                list.Add((InstructionDecoder.ShortBranch(ins), f, "branch"));
                list.Add((fall, f, "fallthrough")); break;
            case 198 or 199: // ifnull / ifnonnull: pop reference
                var rcond = Pop();
                if (rcond is { Kind: not (VKind.Ref or VKind.Null or VKind.Top or VKind.UninitializedThis or VKind.Uninitialized) })
                    Error("type-mismatch", $"ifnull expects a reference, got {rcond}");
                list.Add((InstructionDecoder.ShortBranch(ins), f, "branch"));
                list.Add((fall, f, "fallthrough")); break;
            case >= 159 and <= 164:
                var ci2 = Pop(); var ci1 = Pop();
                if (ci1 != null && ci1.Kind != VKind.Top) Expect(ci1, VType.Int, ins.Mnemonic);
                if (ci2 != null && ci2.Kind != VKind.Top) Expect(ci2, VType.Int, ins.Mnemonic);
                list.Add((InstructionDecoder.ShortBranch(ins), f, "branch"));
                list.Add((fall, f, "fallthrough")); break;
            case 165 or 166:
                var cr2 = Pop(); var cr1 = Pop();
                if (cr1 is { Kind: not (VKind.Ref or VKind.Null or VKind.Top) }) Error("type-mismatch", $"{ins.Mnemonic} expects references");
                if (cr2 is { Kind: not (VKind.Ref or VKind.Null or VKind.Top) }) Error("type-mismatch", $"{ins.Mnemonic} expects references");
                list.Add((InstructionDecoder.ShortBranch(ins), f, "branch"));
                list.Add((fall, f, "fallthrough")); break;
            case 167: // goto
                list.Add((InstructionDecoder.ShortBranch(ins), f, "goto")); break;
            case 168: // jsr
                if (!sem.AllowsJsrRet)
                    Error("jsr-not-allowed", $"jsr forbidden by {sem.Name} (major version {version}); legacy inference is required");
                Push(new VType(VKind.ReturnAddr, null, fall));
                list.Add((InstructionDecoder.ShortBranch(ins), f, "subroutine"));
                break;
            case 169: // ret
                var retLocal = ins.Operands[0];
                var retVal = retLocal < f.Locals.Length ? f.Locals[retLocal] : VType.Top;
                if (retVal.Kind != VKind.ReturnAddr)
                    Error("ret-type-mismatch", $"ret: local {retLocal} holds {retVal}, expected returnAddress");
                else list.Add((retVal.NewOffset, f, "subroutine-return"));
                if (!sem.AllowsJsrRet) Error("jsr-not-allowed", "ret forbidden by stackmap semantics");
                break;

            case 187: // new
                var newClass = cp.ClassName(BigEndian.UInt16(ins.Operands, 0));
                Push(VType.UninitializedAt(ins.Offset));
                result.Judgments.Add(Judgment.Make("uninitialized-object", ins.Offset,
                    "JVMS §4.10.1.2: new yields an uninitialized reference", version,
                    "marked-uninitialized", new { className = newClass, newOffset = ins.Offset }));
                list.Add((fall, f, "fallthrough")); break;
            case 192:
                var cast = Pop();
                if (cast is { Kind: not (VKind.Ref or VKind.Null or VKind.Top) }) Error("type-mismatch", "checkcast expects a reference");
                Push(VType.RefOf(cp.ClassName(BigEndian.UInt16(ins.Operands, 0))));
                list.Add((fall, f, "fallthrough")); break;
            case 193:
                var io = Pop();
                if (io is { Kind: not (VKind.Ref or VKind.Null or VKind.Top) }) Error("type-mismatch", "instanceof expects a reference");
                Push(VType.Int); list.Add((fall, f, "fallthrough")); break;
            case 189:
                var al = Pop();
                if (al is { Kind: not (VKind.Ref or VKind.Null or VKind.Top) }) Error("type-mismatch", "arraylength expects a reference");
                Push(VType.Int); list.Add((fall, f, "fallthrough")); break;

            case 191: // athrow
                var thrown = Pop();
                if (thrown is not (null or { Kind: VKind.Ref or VKind.Null or VKind.Top }) ||
                    (thrown != null && thrown.Kind != VKind.Ref && thrown.Kind != VKind.Null && thrown.Kind != VKind.Top))
                    Error("athrow-non-throwable", $"athrow requires a Throwable reference, got {thrown}");
                if (thrown is { Kind: VKind.UninitializedThis or VKind.Uninitialized })
                    Error("uninitialized-this-use", "athrow of an uninitialized reference");
                list.Add((null, f, "throw")); break;

            case 182 or 183 or 184 or 185:
                EmitInvoke(cls, ins, f, op, version, Error, Push, Pop, result, list, method, fall);
                break;
            case 180:
                var (_, _, fdesc) = cp.Ref(BigEndian.UInt16(ins.Operands, 0));
                var fReceiver = Pop();
                RequireReceiver(fReceiver, ins, version, result, Error);
                PushTypeFromDescriptor(fdesc, Push);
                list.Add((fall, f, "fallthrough")); break;
            case 181:
                var (_, _, pdesc) = cp.Ref(BigEndian.UInt16(ins.Operands, 0));
                PopTypeFromDescriptor(pdesc, Pop, Error, ins.Mnemonic);
                var pReceiver = Pop();
                RequireReceiver(pReceiver, ins, version, result, Error);
                list.Add((fall, f, "fallthrough")); break;

            case 172:
                var iret = Pop();
                if (iret != null && iret.Kind != VKind.Top) Expect(iret, VType.Int, "ireturn");
                list.Add((null, f, "return")); break;
            case 173:
                var lrh = Pop(); var lrl = Pop();
                if (lrh?.Kind != VKind.SecondSlot || lrl?.Kind != VKind.Long) Error("type-mismatch", "lreturn expects long pair");
                list.Add((null, f, "return")); break;
            case 174:
                var fret = Pop();
                if (fret != null && fret.Kind != VKind.Top) Expect(fret, VType.Float, "freturn");
                list.Add((null, f, "return")); break;
            case 176:
                var aret = Pop();
                if (aret is { Kind: not (VKind.Ref or VKind.Null or VKind.Top) }) Error("type-mismatch", "areturn expects reference");
                list.Add((null, f, "return")); break;
            case 177:
                if (method.IsConstructor && f.Locals.Length > 0 && f.Locals[0].Kind == VKind.UninitializedThis)
                {
                    Error("return-before-init", "constructor returns before initializing this");
                    result.Judgments.Add(Judgment.Make("uninitialized-object", ins.Offset,
                        "JVMS §4.10.1.2: this must be initialized before <init> returns", version,
                        "rejected-return", new {}));
                }
                list.Add((null, f, "return")); break;

            default:
                Error("unsupported-opcode", $"opcode {op} ({ins.Mnemonic}) not modeled by the case package");
                list.Add((fall, f, "fallthrough")); break;
        }
        return list;
    }

    private static void EmitInvoke(ClassFile cls, Instruction ins, FrameState f, int op, int version,
        Action<string, string> error, Action<VType> pushUnused, Func<VType?> pop,
        VerificationResult result, List<(int?, FrameState, string)> list, MethodModel method, int fall)
    {
        var cp = cls.ConstantPool;
        var (owner, name, desc) = cp.Ref(BigEndian.UInt16(ins.Operands, 0));
        var paramTypes = DescriptorNormalizer.ParseParameterTypes(desc, true, owner, false);

        VType? Pop() => pop();
        void Error(string code, string msg) => error(code, msg);
        void Push(VType t)
        {
            var s = new List<VType>(f.Stack) { t };
            if (t.IsCategory2) s.Add(VType.SecondSlot);
            f.Stack = s.ToArray();
        }

        if (op == 185) Pop(); // invokeinterface pops count byte value (u1 operand already decoded)
        for (var pi = paramTypes.Count - 1; pi >= 0; pi--)
        {
            var expected = paramTypes[pi];
            if (expected.IsCategory2)
            {
                var hi = Pop(); var lo = Pop();
                if (hi?.Kind != VKind.SecondSlot || lo?.Kind != expected.Kind)
                    Error("type-mismatch", $"{name}: expected {expected} argument pair, got {lo}/{hi}");
            }
            else
            {
                var actual = Pop();
                if (actual == null) { Error("stack-underflow", $"{name}: missing argument {expected}"); continue; }
                if (expected.Kind == VKind.Ref)
                {
                    if (actual.Kind is not (VKind.Ref or VKind.Null or VKind.Top))
                        Error("type-mismatch", $"{name}: reference argument expected, got {actual}");
                }
                else if (actual.Kind != VKind.Top && actual.Kind != expected.Kind)
                    Error("type-mismatch", $"{name}: argument expected {expected}, got {actual}");
            }
        }

        VType? receiver = null;
        if (op != 184) receiver = Pop();

        if (op == 183 && name == "<init>")
        {
            if (receiver is { Kind: VKind.Ref })
                Error("illegal-init-call", "invokespecial <init> on an already initialized object");
            else if (receiver is { Kind: not (VKind.UninitializedThis or VKind.Uninitialized) } and not null)
                Error("uninitialized-this-use", $"invokespecial <init> requires uninitialized receiver, got {receiver}");
            else if (receiver != null)
            {
                var initialized = receiver.Kind == VKind.UninitializedThis
                    ? VType.RefOf(cls.ThisClass)
                    : VType.RefOf(owner);
                for (var i = 0; i < f.Locals.Length; i++)
                    if (f.Locals[i].Equals(receiver)) f.Locals[i] = initialized;
                for (var i = 0; i < f.Stack.Length; i++)
                    if (f.Stack[i].Equals(receiver)) f.Stack[i] = initialized;
                result.Judgments.Add(Judgment.Make("uninitialized-object", ins.Offset,
                    "JVMS §4.10.1.2: constructor call initializes every alias", version,
                    "initialized-all-copies",
                    new { owner, receiver = receiver.ToString(), replacedIn = "locals,stack" }));
            }
            list.Add((fall, f, "fallthrough"));
            return;
        }

        if (receiver != null && receiver.Kind is VKind.UninitializedThis or VKind.Uninitialized)
        {
            Error("uninitialized-this-use", $"calling {name} on {receiver} before <init>");
            result.Judgments.Add(Judgment.Make("uninitialized-object", ins.Offset,
                "JVMS §4.10.1.2: uninitialized reference may only be used by <init>", version,
                "rejected-method-call", new { callee = $"{owner}.{name}{desc}" }));
        }
        else if (op != 184 && receiver is { Kind: not (VKind.Ref or VKind.Null or VKind.Top) })
            Error("type-mismatch", $"call to {name}: receiver is {receiver}");

        var retType = DescriptorNormalizer.ParseReturnType(desc);
        if (retType.Kind != VKind.Top) Push(retType);
        list.Add((fall, f, "fallthrough"));
    }

    private static void RequireReceiver(VType? receiver, Instruction ins, int version,
        VerificationResult result, Action<string, string> error)
    {
        if (receiver == null) { error("stack-underflow", $"{ins.Mnemonic}: missing receiver"); return; }
        if (receiver.Kind is VKind.UninitializedThis or VKind.Uninitialized)
        {
            error("uninitialized-this-use", $"{ins.Mnemonic} on {receiver} before <init>");
            result.Judgments.Add(Judgment.Make("uninitialized-object", ins.Offset,
                "JVMS §4.10.1.2: uninitialized reference may not be dereferenced", version,
                "rejected-field-access", new {}));
        }
        else if (receiver.Kind is not (VKind.Ref or VKind.Null or VKind.Top))
            error("type-mismatch", $"{ins.Mnemonic}: receiver is {receiver}");
    }

    private static void PushTypeFromDescriptor(string fieldDesc, Action<VType> push)
    {
        var t = fieldDesc[0] switch
        {
            'Z' or 'B' or 'C' or 'S' or 'I' => VType.Int,
            'J' => VType.Long,
            'F' => VType.Float,
            'D' => VType.Double,
            _ => VType.RefOf(fieldDesc.TrimStart('[').TrimStart('L').TrimEnd(';'))
        };
        push(t);
    }

    private static void PopTypeFromDescriptor(string fieldDesc, Func<VType?> pop,
        Action<string, string> error, string mnemonic)
    {
        if (fieldDesc == "J" || fieldDesc == "D") { var hi = pop(); var lo = pop(); }
        else pop();
    }
}
