using System.Text;

namespace JvmVerifier.Core.ClassFile;

/// <summary>Structural problems found while importing. Values that cannot be decoded stay
/// in place; the reader never fabricates data to "make the picture draw".</summary>
public sealed record ImportDiagnostic(string Code, string Message, int? Offset = null);

public sealed class ClassFileParseResult
{
    public required ClassFileModel Model { get; init; }
    public required IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; }
    public bool Ok => Diagnostics.Count == 0;
}

public static class ClassFileReader
{
    public static ClassFileParseResult Parse(byte[] bytes)
    {
        var diags = new List<ImportDiagnostic>();
        var r = new ClassReader(bytes);
        try
        {
            uint magic = r.U4();
            if (magic != 0xCAFEBABE)
                diags.Add(new("bad-magic", $"Magic is 0x{magic:X8}, expected 0xCAFEBABE"));
            ushort minor = r.U2(), major = r.U2();
            var cp = ReadConstantPool(r, diags);
            ushort flags = r.U2();
            int thisClass = r.U2(), superClass = r.U2();
            var interfaces = new int[r.U2()];
            for (int i = 0; i < interfaces.Length; i++) interfaces[i] = r.U2();

            int fieldCount = r.U2();
            for (int i = 0; i < fieldCount; i++) SkipMember(r);

            var methods = new List<MethodInfo>();
            int methodCount = r.U2();
            for (int i = 0; i < methodCount; i++)
                methods.Add(ReadMethod(r, cp, i, diags));

            return new ClassFileParseResult
            {
                Model = new ClassFileModel
                {
                    MinorVersion = minor, MajorVersion = major, ConstantPool = cp,
                    AccessFlags = flags, ThisClass = thisClass, SuperClass = superClass,
                    Interfaces = interfaces, Methods = methods, RawBytes = bytes
                },
                Diagnostics = diags
            };
        }
        catch (Exception ex)
        {
            diags.Add(new("truncated", ex.Message, r.Position));
            return new ClassFileParseResult
            {
                Model = new ClassFileModel
                {
                    MinorVersion = 0, MajorVersion = 0, ConstantPool = new ConstantPool(),
                    AccessFlags = 0, ThisClass = 0, SuperClass = 0,
                    Interfaces = Array.Empty<int>(), Methods = Array.Empty<MethodInfo>(),
                    RawBytes = bytes
                },
                Diagnostics = diags
            };
        }
    }

    private static ConstantPool ReadConstantPool(ClassReader r, List<ImportDiagnostic> diags)
    {
        var cp = new ConstantPool();
        int count = r.U2();
        int i = 1;
        while (i < count)
        {
            int tagStart = r.Position;
            byte tag = r.U1();
            int payload = tag switch
            {
                3 or 4 or 9 or 10 or 11 or 12 or 17 or 18 => 4,
                5 or 6 => 8,
                7 or 8 or 16 or 19 or 20 => 2,
                15 => 3,
                _ => 0
            };
            if (tag != 1 && payload == 0)
            {
                diags.Add(new("bad-cp-tag", $"Unknown constant pool tag {tag} at index {i}", tagStart));
                cp.LoadSlot(new CpInfo { Tag = tag, Raw = Array.Empty<byte>() });
                i++;
                continue;
            }
            if (tag == 1)
            {
                int len = r.U2();
                byte[] raw = r.Bytes(len);
                string text;
                try { text = DecodeModifiedUtf8(raw); }
                catch (Exception ex)
                {
                    text = string.Empty;
                    diags.Add(new("bad-utf8", ex.Message, tagStart));
                }
                cp.LoadSlot(new CpInfo { Tag = tag, Raw = raw, Utf8Text = text });
                i++;
                continue;
            }
            byte[] body = r.Bytes(payload);
            var info = new CpInfo { Tag = tag, Raw = body };
            cp.LoadSlot(info);
            if (tag is 5 or 6) { cp.LoadSlot(null); i += 2; }
            else i++;
        }
        return cp;
    }

    private static string DecodeModifiedUtf8(byte[] raw)
    {
        var sb = new StringBuilder(raw.Length);
        int i = 0;
        while (i < raw.Length)
        {
            byte b = raw[i++];
            if (b < 0x80) sb.Append((char)b);
            else if ((b & 0xE0) == 0xC0)
            {
                int c = ((b & 0x1F) << 6) | (raw[i++] & 0x3F);
                sb.Append((char)c);
            }
            else if ((b & 0xF0) == 0xE0)
            {
                int c = ((b & 0x0F) << 12) | ((raw[i++] & 0x3F) << 6) | (raw[i++] & 0x3F);
                if (c == 0 && b == 0xE0) { /* NUL encoded form */ }
                sb.Append((char)c);
            }
            else throw new FormatException($"Invalid modified-UTF8 lead byte 0x{b:X2}");
        }
        return sb.ToString();
    }

    private static AttributeInfo ReadAttribute(ClassReader r)
    {
        int name = r.U2();
        int len = (int)r.U4();
        byte[] data = r.Bytes(len);
        return new AttributeInfo { NameIndex = name, Data = data };
    }

    private static void SkipMember(ClassReader r)
    {
        r.U2(); r.U2(); r.U2(); // access, name, desc
        int ac = r.U2();
        for (int i = 0; i < ac; i++) { r.U2(); int len = (int)r.U4(); r.Bytes(len); }
    }

    private static MethodInfo ReadMethod(ClassReader r, ConstantPool cp, int ordinal, List<ImportDiagnostic> diags)
    {
        ushort flags = r.U2();
        int name = r.U2(), desc = r.U2();
        var attrs = new List<AttributeInfo>();
        int ac = r.U2();
        CodeAttribute? code = null;
        for (int i = 0; i < ac; i++)
        {
            var a = ReadAttribute(r);
            attrs.Add(a);
            if (cp.Utf8(a.NameIndex) == "Code")
                code = ParseCode(a.Data, cp, diags);
        }
        return new MethodInfo
        {
            AccessFlags = flags, NameIndex = name, DescriptorIndex = desc,
            Attributes = attrs, Code = code, Ordinal = ordinal
        };
    }

    private static CodeAttribute ParseCode(byte[] data, ConstantPool cp, List<ImportDiagnostic> diags)
    {
        var cr = new ClassReader(data);
        int maxStack = cr.U2(), maxLocals = cr.U2();
        int codeLen = (int)cr.U4();
        byte[] code = cr.Bytes(codeLen);
        var table = new List<ExceptionTableEntry>();
        int exCount = cr.U2();
        for (int i = 0; i < exCount; i++)
            table.Add(new ExceptionTableEntry
            {
                StartPc = cr.U2(), EndPc = cr.U2(), HandlerPc = cr.U2(),
                CatchType = cr.U2(), Ordinal = i
            });
        var attrs = new List<AttributeInfo>();
        int ac = cr.U2();
        StackMapTable? smt = null;
        for (int i = 0; i < ac; i++)
        {
            var a = ReadAttribute(cr);
            attrs.Add(a);
            if (cp.Utf8(a.NameIndex) is "StackMapTable" or "StackMap")
                smt = ParseStackMapTable(a.Data, diags);
        }
        return new CodeAttribute
        {
            MaxStack = maxStack, MaxLocals = maxLocals, Code = code,
            ExceptionTable = table, Attributes = attrs, StackMapTable = smt
        };
    }

    private static StackMapTable ParseStackMapTable(byte[] data, List<ImportDiagnostic> diags)
    {
        var cr = new ClassReader(data);
        var frames = new List<StackMapFrame>();
        int count = cr.U2();
        int previous = -1;
        try
        {
            for (int i = 0; i < count; i++)
            {
                int rawStart = cr.Position - 2;
                int frameStart = cr.Position;
                int type = cr.U1();
                int delta, offset;
                var locals = Array.Empty<VerificationType>();
                var stack = Array.Empty<VerificationType>();

                if (type <= 63)
                {
                    delta = type;
                }
                else if (type <= 127)
                {
                    delta = type - 64;
                    stack = new[] { ReadVerificationType(cr, 0) };
                }
                else if (type == 247)
                {
                    delta = cr.U2();
                    stack = new[] { ReadVerificationType(cr, 0) };
                }
                else if (type >= 248 && type <= 250)
                {
                    delta = cr.U2();
                }
                else if (type == 251)
                {
                    delta = cr.U2();
                    locals = ReadVarList(cr);
                }
                else if (type >= 252 && type <= 254)
                {
                    delta = cr.U2();
                    int n = type - 251;
                    locals = new VerificationType[n];
                    for (int k = 0; k < n; k++) locals[k] = ReadVerificationType(cr, k);
                }
                else
                {
                    delta = cr.U2();
                    int nLocals = cr.U2();
                    locals = new VerificationType[nLocals];
                    for (int k = 0; k < nLocals; k++) locals[k] = ReadVerificationType(cr, k);
                    int nStack = cr.U2();
                    stack = new VerificationType[nStack];
                    for (int k = 0; k < nStack; k++) stack[k] = ReadVerificationType(cr, k);
                }
                offset = previous + 1 + delta;
                previous = offset;
                frames.Add(new StackMapFrame
                {
                    Ordinal = i, FrameType = type, Offset = offset, OffsetDelta = delta,
                    Locals = locals, Stack = stack,
                    RawLength = cr.Position - frameStart, RawStart = rawStart
                });
            }
            return new StackMapTable { Frames = frames, DecodeError = false };
        }
        catch (Exception ex)
        {
            diags.Add(new("bad-stackmap", ex.Message, cr.Position));
            return new StackMapTable
            {
                Frames = frames, DecodeError = true, DecodeErrorMessage = ex.Message
            };
        }
    }

    private static VerificationType[] ReadVarList(ClassReader cr)
    {
        int n = cr.U2();
        var arr = new VerificationType[n];
        for (int k = 0; k < n; k++) arr[k] = ReadVerificationType(cr, k);
        return arr;
    }

    private static VerificationType ReadVerificationType(ClassReader cr, int ordinal)
    {
        byte tag = cr.U1();
        int operand = tag switch
        {
            VerificationType.Object => cr.U2(),
            VerificationType.Uninitialized => cr.U2(),
            _ => 0
        };
        return new VerificationType { Tag = tag, Operand = operand, Ordinal = ordinal };
    }
}
