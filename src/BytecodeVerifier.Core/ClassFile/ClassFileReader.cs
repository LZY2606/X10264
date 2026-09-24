using System.Security.Cryptography;
using System.Text;
using BytecodeVerifier.Core.Model;

namespace BytecodeVerifier.Core.ClassFile;

public sealed class ClassFileReadException : Exception
{
    public ClassFileReadException(string message) : base(message) { }
}

/// <summary>
/// Imports a class file preserving the original bytes, byte offsets, constant pool entries,
/// descriptors, branch targets, exception ranges, stack map frames and class file version.
/// Unconvertible values are retained in place with a diagnostic rather than dropped.
/// </summary>
public sealed class ClassFileReader
{
    private readonly byte[] _b;
    private int _p;
    private readonly List<string> _diagnostics = new();
    private List<RawCpInfo> _cp = new();

    private ClassFileReader(byte[] bytes) { _b = bytes; }

    public static RawClassFile Read(byte[] bytes)
    {
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ClassFileReader(bytes).Parse(sha);
    }

    private byte U1() => _b[_p++];
    private ushort U2() { var v = (ushort)(_b[_p] << 8 | _b[_p + 1]); _p += 2; return v; }
    private uint U4() { var v = (uint)(_b[_p] << 24 | _b[_p + 1] << 16 | _b[_p + 2] << 8 | _b[_p + 3]); _p += 4; return v; }
    private void Skip(int n) => _p += n;

    private RawClassFile Parse(string sha)
    {
        if (U4() != 0xCAFEBABE) throw new ClassFileReadException("bad magic (expected 0xCAFEBABE)");
        var minor = U2();
        var major = U2();
        var cpCount = U2();
        _cp = ReadConstantPool(cpCount);
        var access = U2();
        var thisClass = U2();
        var superClass = U2();
        var ifaceCount = U2();
        var ifaces = new List<ushort>();
        for (var i = 0; i < ifaceCount; i++) ifaces.Add(U2());

        SkipMembers(); // fields
        var methods = ReadMethods();

        return new RawClassFile
        {
            ClassId = StableId.ForClass(sha),
            RawSha256 = sha,
            RawBytes = (byte[])_b.Clone(),
            MinorVersion = minor,
            MajorVersion = major,
            ConstantPoolCount = cpCount,
            ConstantPool = _cp,
            AccessFlags = access,
            ThisClassCp = thisClass,
            SuperClassCp = superClass,
            InterfacesCp = ifaces,
            ThisClassName = ClassNameOf(thisClass),
            SuperClassName = superClass == 0 ? "" : ClassNameOf(superClass),
            InterfaceNames = ifaces.Select(ClassNameOf).ToList(),
            Methods = methods,
            ImportDiagnostics = _diagnostics.ToList()
        };
    }

    private RawCpInfo? Entry(ushort index) => _cp.FirstOrDefault(e => e.Index == index);

    private string Utf8(ushort index) => Entry(index)?.RawText ?? $"#{index}";

    private string ClassNameOf(ushort classCpIndex)
    {
        var entry = Entry(classCpIndex);
        return entry is null || entry.RefIndex1 is null ? $"#{classCpIndex}" : Utf8(entry.RefIndex1.Value);
    }

    private List<RawCpInfo> ReadConstantPool(ushort count)
    {
        var list = new List<RawCpInfo>();
        for (ushort i = 1; i < count; i++)
        {
            var tag = (ConstantPoolTag)U1();
            var start = _p;
            ushort? r1 = null, r2 = null;
            string text = "";
            switch (tag)
            {
                case ConstantPoolTag.Utf8:
                    var len = U2();
                    text = ReadModifiedUtf8(_p, len);
                    Skip(len);
                    break;
                case ConstantPoolTag.Integer:
                case ConstantPoolTag.Float:
                    Skip(4); break;
                case ConstantPoolTag.Long:
                case ConstantPoolTag.Double:
                    Skip(8); break;
                case ConstantPoolTag.Class:
                case ConstantPoolTag.String:
                case ConstantPoolTag.MethodType:
                case ConstantPoolTag.Module:
                case ConstantPoolTag.Package:
                    r1 = U2(); break;
                case ConstantPoolTag.Fieldref:
                case ConstantPoolTag.Methodref:
                case ConstantPoolTag.InterfaceMethodref:
                case ConstantPoolTag.NameAndType:
                case ConstantPoolTag.Dynamic:
                case ConstantPoolTag.InvokeDynamic:
                    r1 = U2(); r2 = U2(); break;
                case ConstantPoolTag.MethodHandle:
                    Skip(1); r1 = U2(); break;
                default:
                    throw new ClassFileReadException($"unknown constant pool tag {(byte)tag} at index {i}");
            }
            var payload = new byte[_p - start];
            Array.Copy(_b, start, payload, 0, payload.Length);
            list.Add(new RawCpInfo
            {
                Index = i, Tag = tag, RawPayload = payload, RawText = text,
                RefIndex1 = r1, RefIndex2 = r2,
                TakesTwoSlots = tag is ConstantPoolTag.Long or ConstantPoolTag.Double
            });
            if (tag is ConstantPoolTag.Long or ConstantPoolTag.Double) i++;
        }
        return list;
    }

    private string ReadModifiedUtf8(int offset, int length)
    {
        try
        {
            var end = offset + length;
            var sb = new StringBuilder();
            var i = offset;
            while (i < end)
            {
                var x = _b[i++];
                if ((x & 0x80) == 0) sb.Append((char)x);
                else if ((x & 0xE0) == 0xC0)
                {
                    var y = _b[i++];
                    sb.Append((char)(((x & 0x1F) << 6) | (y & 0x3F)));
                }
                else
                {
                    var y = _b[i++]; var z = _b[i++];
                    sb.Append((char)(((x & 0x0F) << 12) | ((y & 0x3F) << 6) | (z & 0x3F)));
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"modified-utf8 decode failure at bci {offset}: {ex.Message}");
            return $"<utf8@#{offset}>";
        }
    }

    private void SkipAttributes()
    {
        var count = U2();
        for (var i = 0; i < count; i++)
        {
            Skip(2);
            var len = (int)U4();
            Skip(len);
        }
    }

    private void SkipMembers()
    {
        var count = U2();
        for (var i = 0; i < count; i++) { Skip(6); SkipAttributes(); }
    }

    private List<RawMethod> ReadMethods()
    {
        var count = U2();
        var methods = new List<RawMethod>();
        for (ushort m = 0; m < count; m++)
        {
            var access = U2();
            var nameIdx = U2();
            var descIdx = U2();
            var attrCount = U2();
            byte[] code = Array.Empty<byte>();
            int maxStack = 0, maxLocals = 0;
            var handlers = new List<RawExceptionHandler>();
            var frames = new List<RawStackMapFrame>();
            var hasStackMapTable = false;
            for (var a = 0; a < attrCount; a++)
            {
                var attrNameIdx = U2();
                var attrLen = (int)U4();
                var attrStart = _p;
                if (Utf8(attrNameIdx) == "Code")
                {
                    maxStack = U2();
                    maxLocals = U2();
                    var codeLen = (int)U4();
                    code = new byte[codeLen];
                    Array.Copy(_b, _p, code, 0, codeLen);
                    Skip(codeLen);
                    handlers = ReadExceptionTable(code);
                    var codeAttrCount = U2();
                    for (var ca = 0; ca < codeAttrCount; ca++)
                    {
                        var canIdx = U2();
                        var caLen = (int)U4();
                        var caStart = _p;
                        if (Utf8(canIdx) == "StackMapTable")
                        {
                            hasStackMapTable = true;
                            frames = ReadStackMapTable();
                        }
                        _p = caStart + caLen;
                    }
                }
                _p = attrStart + attrLen;
            }
            var instructions = code.Length == 0
                ? Array.Empty<RawInstruction>()
                : Disassembler.Disassemble(code, out var disDiag);
            foreach (var d in disDiag) _diagnostics.Add($"method {m} ({Utf8(nameIdx)}): {d}");
            methods.Add(new RawMethod
            {
                MethodIndex = m,
                AccessFlags = access,
                NameCpIndex = nameIdx,
                DescriptorCpIndex = descIdx,
                Name = Utf8(nameIdx),
                Descriptor = Utf8(descIdx),
                MaxStack = maxStack,
                MaxLocals = maxLocals,
                CodeBytes = code,
                Instructions = instructions,
                ExceptionTable = handlers,
                StackMapFrames = frames,
                HasStackMapTableAttribute = hasStackMapTable
            });
        }
        return methods;
    }

    private List<RawExceptionHandler> ReadExceptionTable(byte[] code)
    {
        var n = U2();
        var list = new List<RawExceptionHandler>();
        for (var i = 0; i < n; i++)
        {
            var sp = U2(); var ep = U2(); var hp = U2(); var ct = U2();
            var h = new RawExceptionHandler
            {
                HandlerIndex = i, StartPc = sp, EndPc = ep, HandlerPc = hp,
                CatchTypeCpIndex = ct, CatchTypeRawName = ct == 0 ? "<any>" : ClassNameOf(ct)
            };
            if (sp >= ep) h.Diagnostic = $"exception range start_pc {sp} >= end_pc {ep}";
            if (hp >= code.Length) h.Diagnostic = $"handler_pc {hp} exceeds code length {code.Length}";
            list.Add(h);
        }
        return list;
    }

    private List<RawStackMapFrame> ReadStackMapTable()
    {
        var n = U2();
        var frames = new List<RawStackMapFrame>();
        int? previousOffset = null;
        for (var i = 0; i < n; i++)
        {
            var rawType = U1();
            StackMapFrameKind kind;
            int delta;
            var locals = new List<VerificationType>();
            var stack = new List<VerificationType>();
            if (rawType <= 63) { kind = StackMapFrameKind.Same; delta = rawType; }
            else if (rawType <= 127) { kind = StackMapFrameKind.SameLocals1StackItem; delta = rawType - 64; stack.Add(ReadVerificationType()); }
            else if (rawType == 247) { kind = StackMapFrameKind.SameLocals1StackItemExtended; delta = U2(); stack.Add(ReadVerificationType()); }
            else if (rawType is >= 248 and <= 250) { kind = StackMapFrameKind.Chop; delta = U2(); }
            else if (rawType == 251) { kind = StackMapFrameKind.SameExtended; delta = U2(); }
            else if (rawType is >= 252 and <= 254)
            {
                kind = StackMapFrameKind.Append; delta = U2();
                var count = rawType - 251;
                for (var k = 0; k < count; k++) locals.Add(ReadVerificationType());
            }
            else
            {
                kind = StackMapFrameKind.Full; delta = U2();
                var nl = U2();
                for (var k = 0; k < nl; k++) locals.Add(ReadVerificationType());
                var ns = U2();
                for (var k = 0; k < ns; k++) stack.Add(ReadVerificationType());
            }
            var absolute = (previousOffset ?? -1) + 1 + delta;
            frames.Add(new RawStackMapFrame
            {
                FrameIndex = i, Kind = kind, RawType = rawType, ByteOffsetDelta = delta,
                AbsoluteByteOffset = absolute, Locals = locals, Stack = stack,
                PreviousFrameOffset = previousOffset
            });
            previousOffset = absolute;
        }
        return frames;
    }

    private VerificationType ReadVerificationType()
    {
        var tag = (VerificationTypeTag)U1();
        switch (tag)
        {
            case VerificationTypeTag.Object:
            {
                var classIdx = U2();
                return new VerificationType { Tag = tag, CpIndex = classIdx, RawLabel = ClassNameOf(classIdx) };
            }
            case VerificationTypeTag.Uninitialized:
            {
                var newBci = U2();
                return new VerificationType { Tag = tag, NewOffset = newBci, RawLabel = $"uninitialized@{newBci}" };
            }
            default:
                return new VerificationType { Tag = tag, RawLabel = tag.ToString().ToLowerInvariant() };
        }
    }
}
