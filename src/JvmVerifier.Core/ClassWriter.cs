using System.Text;

namespace JvmVerifier.Core;

/// <summary>
/// Minimal classfile emitter used to build the in-repo fixtures and the
/// offline case pack. Deterministic: same spec produces identical bytes.
/// </summary>
public sealed class ClassWriter
{
    private readonly List<CpEntry> _cp = [new CpEntry.Unknown(0, [])];
    private readonly Dictionary<string, int> _cache = new();
    private readonly List<MethodSpec> _methods = new();

    public string Name { get; }
    public int Major { get; set; } = 52;
    public int Minor { get; set; } = 0;

    public ClassWriter(string name) => Name = name;

    private int Add(string key, Func<int> make)
    {
        if (_cache.TryGetValue(key, out int idx)) return idx;
        idx = make();
        _cache[key] = idx;
        return idx;
    }

    private int Push(CpEntry e) { _cp.Add(e); return _cp.Count - 1; }

    public int Utf8(string s) => Add("u:" + s, () => Push(new CpEntry.Utf8(s)));
    public int Class(string name) => Add("c:" + name, () => Push(new CpEntry.Class(Utf8(name))));
    public int NameAndType(string n, string d) => Add("nt:" + n + d, () => Push(new CpEntry.NameAndType(Utf8(n), Utf8(d))));
    public int Methodref(string cls, string n, string d) => Add("m:" + cls + n + d, () => Push(new CpEntry.Methodref(Class(cls), NameAndType(n, d))));
    public int Fieldref(string cls, string n, string d) => Add("f:" + cls + n + d, () => Push(new CpEntry.Fieldref(Class(cls), NameAndType(n, d))));
    public int StringConst(string s) => Add("s:" + s, () => Push(new CpEntry.StringInfo(Utf8(s))));
    public int LongConst(long v) => Add("l:" + v, () => { int i = Push(new CpEntry.LongInfo(v)); Push(new CpEntry.WidePadding()); return i; });
    public int DoubleConst(double v) => Add("d:" + v, () => { int i = Push(new CpEntry.DoubleInfo(v)); Push(new CpEntry.WidePadding()); return i; });

    public void AddMethod(string name, string desc, int access, CodeSpec code) =>
        _methods.Add(new MethodSpec(name, desc, access, code));

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        void U1(int v) => ms.WriteByte((byte)v);
        void U2(int v) { ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }
        void U4(uint v) { ms.WriteByte((byte)(v >> 24)); ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }

        U4(0xCAFEBABE); U2(Minor); U2(Major);
        U2(_cp.Count);
        foreach (var e in _cp.Skip(1))
        {
            switch (e)
            {
                case CpEntry.Utf8 u: U1(1); var b = Encoding.UTF8.GetBytes(u.Value); U2(b.Length); ms.Write(b); break;
                case CpEntry.Class c: U1(7); U2(c.NameIndex); break;
                case CpEntry.StringInfo s: U1(8); U2(s.StringIndex); break;
                case CpEntry.Fieldref f: U1(9); U2(f.ClassIndex); U2(f.NameAndTypeIndex); break;
                case CpEntry.Methodref m: U1(10); U2(m.ClassIndex); U2(m.NameAndTypeIndex); break;
                case CpEntry.NameAndType nt: U1(12); U2(nt.NameIndex); U2(nt.DescriptorIndex); break;
                case CpEntry.LongInfo l: U1(5); U4((uint)(l.Value >> 32)); U4((uint)l.Value); break;
                case CpEntry.DoubleInfo d: U1(6); U4((uint)(BitConverter.DoubleToInt64Bits(d.Value) >> 32)); U4((uint)BitConverter.DoubleToInt64Bits(d.Value)); break;
                case CpEntry.WidePadding: break;
                default: throw new InvalidOperationException("unsupported cp entry " + e);
            }
        }
        U2(0x0021); // public super
        U2(Class(Name)); U2(Class("java/lang/Object"));
        U2(0); // interfaces
        U2(0); // fields
        U2(_methods.Count);
        foreach (var m in _methods)
        {
            U2(m.Access); U2(Utf8(m.Name)); U2(Utf8(m.Descriptor));
            U2(1); U2(Utf8("Code"));
            using var body = new MemoryStream();
            void B2(int v) { body.WriteByte((byte)(v >> 8)); body.WriteByte((byte)v); }
            void B4(uint v) { body.WriteByte((byte)(v >> 24)); body.WriteByte((byte)(v >> 16)); body.WriteByte((byte)(v >> 8)); body.WriteByte((byte)v); }
            B2(m.Code.MaxStack); B2(m.Code.MaxLocals);
            B4((uint)m.Code.Bytes.Length); body.Write(m.Code.Bytes);
            B2(m.Code.Handlers.Count);
            foreach (var h in m.Code.Handlers) { B2(h.StartPc); B2(h.EndPc); B2(h.HandlerPc); B2(h.CatchTypeIndex); }
            if (m.Code.StackMap.Count > 0)
            {
                B2(1); B2(Utf8("StackMapTable"));
                using var sm = new MemoryStream();
                void S1(int v) => sm.WriteByte((byte)v);
                void S2(int v) { sm.WriteByte((byte)(v >> 8)); sm.WriteByte((byte)v); }
                S2(m.Code.StackMap.Count);
                foreach (var f in m.Code.StackMap) WriteFrame(sm, S1, S2, f);
                B4((uint)sm.Length); body.Write(sm.ToArray());
            }
            else B2(0);
            B4((uint)body.Length); body.WriteTo(ms);
        }
        U2(0); // class attributes
        return ms.ToArray();
    }

    private static void WriteFrame(Stream sm, Action<int> s1, Action<int> s2, StackMapFrame f)
    {
        switch (f.Kind)
        {
            case StackMapFrameKind.Same when f.OffsetDelta <= 63:
                s1(f.OffsetDelta); break;
            case StackMapFrameKind.Same:
                s1(251); s2(f.OffsetDelta); break;
            case StackMapFrameKind.SameLocals1StackItem when f.OffsetDelta <= 63:
                s1(64 + f.OffsetDelta); WriteVTI(sm, s1, s2, f.Stack[0]); break;
            case StackMapFrameKind.SameLocals1StackItem:
                s1(247); s2(f.OffsetDelta); WriteVTI(sm, s1, s2, f.Stack[0]); break;
            case StackMapFrameKind.Chop:
                s1(251 - f.Locals.Count); s2(f.OffsetDelta); break;
            case StackMapFrameKind.Append:
                s1(251 + f.Locals.Count); s2(f.OffsetDelta);
                foreach (var l in f.Locals) WriteVTI(sm, s1, s2, l);
                break;
            case StackMapFrameKind.Full:
                s1(255); s2(f.OffsetDelta); s2(f.Locals.Count);
                foreach (var l in f.Locals) WriteVTI(sm, s1, s2, l);
                s2(f.Stack.Count);
                foreach (var s in f.Stack) WriteVTI(sm, s1, s2, s);
                break;
        }
    }

    private static void WriteVTI(Stream sm, Action<int> s1, Action<int> s2, VerificationTypeInfo t)
    {
        switch (t)
        {
            case VerificationTypeInfo.Top: s1(0); break;
            case VerificationTypeInfo.Int: s1(1); break;
            case VerificationTypeInfo.Float: s1(2); break;
            case VerificationTypeInfo.Double: s1(3); break;
            case VerificationTypeInfo.Long: s1(4); break;
            case VerificationTypeInfo.Null: s1(5); break;
            case VerificationTypeInfo.UninitializedThis: s1(6); break;
            case VerificationTypeInfo.Object o: s1(7); s2(o.CpIndex); break;
            case VerificationTypeInfo.Uninitialized u: s1(8); s2(u.Offset); break;
        }
    }

    public sealed record MethodSpec(string Name, string Descriptor, int Access, CodeSpec Code);
    public sealed record CodeSpec(int MaxStack, int MaxLocals, byte[] Bytes,
        List<ExceptionHandler> Handlers, List<StackMapFrame> StackMap);
}
