using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public sealed partial class BytecodeVerifier
{
    private void Invoke(int refIndex, VFrame f, int bci, bool special, bool isInterface)
    {
        var (className, name, desc) = MethodrefInfo(refIndex);
        if (desc is null)
        {
            Emit(VerifyEventCode.ImportDiagnostic, Severity.Warning,
                $"Methodref #{refIndex} at bci {bci} has no resolvable descriptor",
                bci, fragment: $"constant_pool#{refIndex}", rule: "cp-resolution");
            return;
        }

        var args = DescriptorUtil.ParseParameters(desc);
        for (int i = args.Count - 1; i >= 0; i--) f.Pop();

        var receiver = f.Pop();
        bool isInit = name == "<init>";

        if (special && isInit)
        {
            // The only legal receivers for <init> are Uninit(newBci) or uninitializedThis.
            if (receiver is UninitType uninit)
            {
                if (className is null || !IsAssignableOwner(className))
                    Emit(VerifyEventCode.UninitializedUse, Severity.Failure,
                        $"<init> at bci {bci} initializes object from new@{uninit.NewBci} " +
                        $"but owner {className ?? "?"} is not the new class or a superclass",
                        bci, fragment: $"constant_pool#{refIndex}+code", rule: "constructor-owner");
                ReplaceUninitialized(f, uninit.NewBci, VType.Ref(className ?? "java/lang/Object"));
            }
            else if (receiver is UninitThisType)
            {
                // Calling super/this <init> initializes this to the current class.
                if (f.Locals.Length > 0) f.SetLocal(0, VType.Ref(_c.ClassFile.ThisName));
                ReplaceUninitialized(f, -1, VType.Ref(_c.ClassFile.ThisName), initThis: true);
            }
            else
            {
                Emit(VerifyEventCode.UninitializedUse, Severity.Failure,
                    $"invokespecial <init> at bci {bci} requires an uninitialized receiver " +
                    $"but found {receiver.Token}",
                    bci, fragment: "code", rule: "constructor-receiver");
            }
        }
        else if (special)
        {
            if (receiver is UninitType or UninitThisType)
                Emit(VerifyEventCode.UninitializedUse, Severity.Failure,
                    $"invokespecial {name} at bci {bci} on an uninitialized receiver {receiver.Token}",
                    bci, fragment: "code", rule: "uninitialized-method-target");
        }
        else
        {
            if (receiver is UninitType or UninitThisType)
                Emit(VerifyEventCode.UninitializedUse, Severity.Failure,
                    $"{(isInterface ? "invokeinterface" : "virtual/static-checked")} {name} at bci {bci} " +
                    $"called on uninitialized receiver {receiver.Token}",
                    bci, fragment: "code", rule: "uninitialized-method-target");
        }

        var ret = DescriptorUtil.ReturnType(desc);
        if (ret != "V")
            f.Push(DescriptorUtil.KindOf(ret) switch
            {
                JvmKind.Int => (VType)VType.Int, JvmKind.Long => VType.Long,
                JvmKind.Float => VType.Float, JvmKind.Double => VType.Double,
                JvmKind.Ref => ret.StartsWith("L") ? VType.Ref(ret[1..^1]) : VType.Ref(ret),
                _ => VType.Top
            });
    }

    private bool IsAssignableOwner(string owner) =>
        owner == _c.ClassFile.ThisName || owner == _c.ClassFile.SuperName;

    private void ReplaceUninitialized(VFrame f, int newBci, VType initialized, bool initThis = false)
    {
        for (int i = 0; i < f.Locals.Length; i++)
        {
            if (initThis && f.Locals[i] is UninitThisType) f.Locals[i] = initialized;
            if (!initThis && f.Locals[i] is UninitType u && u.NewBci == newBci) f.Locals[i] = initialized;
        }
        for (int i = 0; i < f.Stack.Count; i++)
        {
            if (f.Stack[i] is UninitType u2 && u2.NewBci == newBci) f.Stack[i] = initialized;
            if (initThis && f.Stack[i] is UninitThisType) f.Stack[i] = initialized;
        }
    }
}
