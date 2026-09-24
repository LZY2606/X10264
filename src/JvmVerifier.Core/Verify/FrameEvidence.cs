using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

internal static class FrameEvidence
{
    public static VType ToVType(VerificationType vt, ConstantPool cp) => vt.Tag switch
    {
        VerificationType.Top => VType.Top,
        VerificationType.Integer => VType.Int,
        VerificationType.FloatV => VType.Float,
        VerificationType.DoubleV => VType.Double,
        VerificationType.LongV => VType.Long,
        VerificationType.Null => VType.Null,
        VerificationType.UninitializedThis => VType.UninitThis,
        VerificationType.Object => VType.Ref(cp.ClassName(vt.Operand) ?? $"bad#{vt.Operand}"),
        VerificationType.Uninitialized => VType.Uninit(vt.Operand),
        _ => VType.Top
    };
}

internal sealed class DeclaredFrame
{
    public required StackMapFrame Frame { get; init; }
    public VFrame Incoming { get; init; } = null!;
    public FrameSnapshot Snapshot => FrameSnapshot.From(Incoming);
}

public static class DeclaredFrameExtensions
{
    public static VFrame ToIncoming(this StackMapFrame frame, MethodContext ctx)
    {
        var f = new VFrame(ctx.Code.MaxLocals);
        int slot = 0;
        foreach (var vt in frame.Locals)
        {
            var v = FrameEvidence.ToVType(vt, ctx.ClassFile.ConstantPool);
            if (slot < f.Locals.Length) f.SetLocal(slot, v);
            slot += v.Slots;
        }
        foreach (var vt in frame.Stack)
            f.Push(FrameEvidence.ToVType(vt, ctx.ClassFile.ConstantPool));
        return f;
    }
}
