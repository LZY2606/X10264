namespace JvmCore;

/// <summary>
/// Parses StackMapTable attributes (JVMS §4.7.4). Raw offsets are preserved;
/// whether a frame offset lands on an instruction boundary is judged later
/// by the verifier, never silently repaired here.
/// </summary>
public static class StackMapTable
{
    public static List<StackMapFrameRaw> Parse(byte[] attr, ConstantPool cp, List<string> diagnostics)
    {
        var frames = new List<StackMapFrameRaw>();
        var r = 0;
        ushort U2() { var v = BigEndian.UInt16(attr, r); r += 2; return v; }
        byte U1() => attr[r++];

        VType ReadVerificationType()
        {
            var tag = U1();
            return tag switch
            {
                0 => VType.Top,
                1 => VType.Int,
                2 => VType.Float,
                3 => VType.Double,
                4 => VType.Long,
                5 => VType.Null,
                6 => VType.UninitializedThis,
                7 => VType.RefOf(cp.ClassName(U2())),
                8 => VType.UninitializedAt(U2()),
                _ => throw new ArgumentException($"bad verification-type tag {tag}")
            };
        }

        var count = U2();
        var prevLocals = new List<VType>();
        var prevOffset = -1;
        for (var i = 0; i < count; i++)
        {
            var tag = U1();
            int delta;
            List<VType> locals;
            List<VType> stack;
            string kind;
            switch (tag)
            {
                case <= 63:
                    delta = tag; locals = new(prevLocals); stack = new(); kind = "same_frame"; break;
                case >= 64 and <= 127:
                    delta = tag - 64; locals = new(prevLocals); stack = new() { ReadVerificationType() }; kind = "same_locals_1_stack_item"; break;
                case 247:
                    delta = U2(); locals = new(prevLocals); stack = new() { ReadVerificationType() }; kind = "same_locals_1_stack_item_extended"; break;
                case >= 248 and <= 250:
                    delta = U2(); locals = prevLocals.Take(prevLocals.Count - (251 - tag)).ToList(); stack = new(); kind = "chop_frame"; break;
                case 251:
                    delta = U2(); locals = new(prevLocals); stack = new(); kind = "same_frame_extended"; break;
                case >= 252 and <= 254:
                    delta = U2(); locals = new(prevLocals); stack = new();
                    for (var k = 0; k < tag - 251; k++) locals.Add(ReadVerificationType());
                    kind = "append_frame"; break;
                case 255:
                    delta = U2(); locals = new(); stack = new(); kind = "full_frame";
                    var nl = U2();
                    for (var k = 0; k < nl; k++) locals.Add(ReadVerificationType());
                    var ns = U2();
                    for (var k = 0; k < ns; k++) stack.Add(ReadVerificationType());
                    break;
                default:
                    diagnostics.Add($"stack map frame {i}: reserved tag {tag}; frame skipped, raw bytes kept");
                    return frames;
            }
            var offset = i == 0 ? delta : prevOffset + delta + 1;
            prevOffset = offset;
            prevLocals = locals;
            frames.Add(new StackMapFrameRaw(Quantity.ByteOffset(offset), kind,
                new FrameState(locals.ToArray(), stack.ToArray()), tag));
        }
        return frames;
    }

    /// <summary>Serialize frames back to attribute bytes (used by the fixture builder).</summary>
    public static byte[] Write(List<(int offset, VType[] locals, VType[] stack)> frames, Func<string, int> classIndex)
    {
        var bytes = new List<byte>();
        void U2(int v) { bytes.Add((byte)(v >> 8)); bytes.Add((byte)v); }
        void Vti(VType t)
        {
            switch (t.Kind)
            {
                case VKind.Top: bytes.Add(0); break;
                case VKind.Int: bytes.Add(1); break;
                case VKind.Float: bytes.Add(2); break;
                case VKind.Double: bytes.Add(3); break;
                case VKind.Long: bytes.Add(4); break;
                case VKind.Null: bytes.Add(5); break;
                case VKind.UninitializedThis: bytes.Add(6); break;
                case VKind.Ref: bytes.Add(7); U2(classIndex(t.ClassName!)); break;
                case VKind.Uninitialized: bytes.Add(8); U2(t.NewOffset ?? 0); break;
                default: throw new InvalidOperationException($"cannot encode {t}");
            }
        }
        U2(frames.Count);
        var prev = -1;
        foreach (var (offset, locals, stack) in frames)
        {
            var delta = prev < 0 ? offset : offset - prev - 1;
            prev = offset;
            // always emit full_frame for simplicity and exactness
            bytes.Add(255);
            U2(delta);
            U2(locals.Length);
            foreach (var l in locals) Vti(l);
            U2(stack.Length);
            foreach (var s in stack) Vti(s);
        }
        return bytes.ToArray();
    }
}
