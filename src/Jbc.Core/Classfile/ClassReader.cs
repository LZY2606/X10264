using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Jbc.Core.Classfile;

public sealed class ClassReadException(string message) : Exception(message);

public sealed class ClassReader
{
    private readonly BinaryReader _r;
    private readonly List<string> _diag = new();

    private ClassReader(byte[] bytes) => _r = new BinaryReader(new MemoryStream(bytes));

    public static RawClassFile Read(byte[] bytes) => new ClassReader(bytes).ReadFile();

    private RawClassFile ReadFile()
    {
        if (_r.ReadUInt32() != 0xCAFEBABE) throw new ClassReadException("bad magic (期望 CAFEBABE)");
        var minor = new Qty(_r.ReadUInt16(), "u2", "minor_version");
        var major = new Qty(_r.ReadUInt16(), "u2", "major_version");
        var cp = ReadConstantPool();
        _r.ReadUInt16(); // access_flags
        var thisIdx = _r.ReadUInt16();
        var superIdx = _r.ReadUInt16();
        var interfaces = new List<string>();
        var ic = _r.ReadUInt16();
        for (var i = 0; i < ic; i++)
            interfaces.Add(ClassNameAt(_r.ReadUInt16()));
        var fields = _r.ReadUInt16();
        for (var i = 0; i < fields; i++) SkipMember();
        var methods = new List<RawMethod>();
        var mc = _r.ReadUInt16();
        for (var i = 0; i < mc; i++) methods.Add(ReadMethod());
        var ac = _r.ReadUInt16();
        for (var i = 0; i < ac; i++) SkipAttribute();
        return new RawClassFile(minor, major, cp, ClassNameAt(thisIdx),
            superIdx == 0 ? null : ClassNameAt(superIdx), interfaces, methods,
            "java/classfile-v45+", _diag);
    }

    private List<RawCpEntry> ReadConstantPool()
    {
        var count = _r.ReadUInt16();
        var list = new List<RawCpEntry> { null! };
        for (var i = 1; i < count; i++)
        {
            var tag = (CpTag)_r.ReadByte();
            RawCpEntry e;
            switch (tag)
            {
                case CpTag.Utf8:
                {
                    var len = _r.ReadUInt16();
                    var bytes = _r.ReadBytes(len);
                    var text = Encoding.UTF8.GetString(bytes);
                    e = new RawCpEntry(i, tag, $"Utf8({len}b)", text);
                    break;
                }
                case CpTag.Integer: e = new(i, tag, "Integer", Numeric: _r.ReadInt32()); break;
                case CpTag.Float: e = new(i, tag, "Float", Numeric: BitConverter.SingleToInt32Bits(_r.ReadSingle())); break;
                case CpTag.Long: e = new(i, tag, "Long", Numeric: _r.ReadInt64()); i++; list.Add(null!); break;
                case CpTag.Double: e = new(i, tag, "Double", Numeric: BitConverter.DoubleToInt64Bits(_r.ReadDouble())); i++; list.Add(null!); break;
                case CpTag.Class: e = new(i, tag, "Class", Ref1: _r.ReadUInt16()); break;
                case CpTag.String: e = new(i, tag, "String", Ref1: _r.ReadUInt16()); break;
                case CpTag.Fieldref:
                case CpTag.Methodref:
                case CpTag.InterfaceMethodref:
                case CpTag.NameAndType:
                case CpTag.Dynamic:
                case CpTag.InvokeDynamic:
                {
                    var a = _r.ReadUInt16(); var b = _r.ReadUInt16();
                    e = new RawCpEntry(i, tag, tag.ToString(), Ref1: a, Ref2: b);
                    break;
                }
                case CpTag.MethodHandle:
                    e = new(i, tag, "MethodHandle", Ref1: _r.ReadByte(), Ref2: _r.ReadUInt16()); break;
                case CpTag.MethodType: e = new(i, tag, "MethodType", Ref1: _r.ReadUInt16()); break;
                default: throw new ClassReadException($"constant pool tag 不支持: {(int)tag} @#{i}");
            }
            list.Add(e);
        }
        return list;
    }

    private string Utf8At(int idx)
    {
        if (idx <= 0 || idx >= _cp.Count) throw new ClassReadException($"常量池索引越界 #{idx}");
        var e = _cp[idx] ?? throw new ClassReadException($"常量池 #{idx} 被 long/double 占用");
        if (e.Tag != CpTag.Utf8) throw new ClassReadException($"#{idx} 不是 Utf8（实际 {e.Tag}）");
        return e.Text!;
    }

    private string ClassNameAt(int idx)
    {
        var e = _cp[idx]!;
        if (e.Tag != CpTag.Class) throw new ClassReadException($"#{idx} 不是 Class 项");
        return Utf8At(e.Ref1!.Value).Replace('/', '.');
    }

    private List<RawCpEntry> _cp = new();

    private void SkipMember()
    {
        _r.ReadUInt16(); _r.ReadUInt16(); _r.ReadUInt16();
        var ac = _r.ReadUInt16();
        for (var i = 0; i < ac; i++) SkipAttribute();
    }

    private RawMethod ReadMethod()
    {
        var access = _r.ReadUInt16();
        var name = Utf8At(_r.ReadUInt16());
        var desc = Utf8At(_r.ReadUInt16());
        var ac = _r.ReadUInt16();
        RawCode? code = null;
        for (var i = 0; i < ac; i++)
        {
            var attrName = Utf8At(_r.ReadUInt16());
            var len = _r.ReadInt32();
            var start = _r.BaseStream.Position;
            if (attrName == "Code") code = ReadCode();
            if (_r.BaseStream.Position != start + len)
                _r.BaseStream.Seek(start + len, SeekOrigin.Begin);
        }
        if (code == null)
        {
            // 抽象/native 方法没有 Code：以空代码表示，仍进入模型。
            code = new RawCode(new Qty(0, "u2"), new Qty(0, "u2"),
                Array.Empty<RawInstruction>(), Array.Empty<RawException>(),
                Array.Empty<RawStackFrame>(), false);
        }
        return new RawMethod(name, desc, access, code);
    }

    private void SkipAttribute()
    {
        _r.ReadUInt16();
        var len = _r.ReadInt32();
        _r.BaseStream.Seek(len, SeekOrigin.Current);
    }

    private RawCode ReadCode()
    {
        var maxStack = new Qty(_r.ReadUInt16(), "u2", "max_stack");
        var maxLocals = new Qty(_r.ReadUInt16(), "u2", "max_locals");
        var codeLen = _r.ReadUInt32();
        var codeBytes = _r.ReadBytes((int)codeLen);
        var insns = InstructionDisassembler.Decode(codeBytes, _diag);
        var exCount = _r.ReadUInt16();
        var ex = new List<RawException>();
        for (var i = 0; i < exCount; i++)
        {
            var sp = _r.ReadUInt16(); var ep = _r.ReadUInt16(); var hp = _r.ReadUInt16();
            var ct = _r.ReadUInt16();
            string catchName;
            string? finallyName = null;
            if (ct == 0) { catchName = "*any"; finallyName = "finally"; }
            else catchName = ClassNameAt(ct);
            ex.Add(new RawException(new Qty(sp, "u2", "start_pc"), new Qty(ep, "u2", "end_pc"),
                new Qty(hp, "u2", "handler_pc"), ct, catchName, finallyName));
        }
        var ac = _r.ReadUInt16();
        var frames = Array.Empty<RawStackFrame>();
        var stackMapPresent = false;
        for (var i = 0; i < ac; i++)
        {
            var attrName = Utf8At(_r.ReadUInt16());
            var len = _r.ReadInt32();
            var start = _r.BaseStream.Position;
            if (attrName == "StackMapTable")
            {
                stackMapPresent = true;
                frames = ReadStackMap();
            }
            _r.BaseStream.Seek(start + len, SeekOrigin.Begin);
        }
        return new RawCode(maxStack, maxLocals, insns, ex, frames, stackMapPresent);
    }

    private RawStackFrame[] ReadStackMap()
    {
        var n = _r.ReadUInt16();
        var result = new List<RawStackFrame>();
        var prevOffset = -1;
        for (var i = 0; i < n; i++)
        {
            var tag = _r.ReadByte();
            int delta; StackFrameKind kind;
            var locals = new List<string>();
            var stack = new List<string>();
            string? corrupt = null;
            if (tag <= 63) { kind = StackFrameKind.Same; delta = tag; }
            else if (tag <= 127) { kind = StackFrameKind.SameLocals1StackItem; delta = tag - 64; stack.Add(ReadVerificationType()); }
            else if (tag == 247) { kind = StackFrameKind.SameLocals1StackItem; delta = _r.ReadUInt16(); stack.Add(ReadVerificationType()); }
            else if (tag >= 248 && tag <= 250) { kind = StackFrameKind.Chop; delta = _r.ReadUInt16(); }
            else if (tag >= 251 && tag <= 254) { kind = StackFrameKind.Append; delta = _r.ReadUInt16(); for (var k = 0; k < tag - 251 + 1; k++) locals.Add(ReadVerificationType()); }
            else if (tag == 255)
            {
                kind = StackFrameKind.Full; delta = _r.ReadUInt16();
                var nl = _r.ReadUInt16();
                for (var k = 0; k < nl; k++) locals.Add(ReadVerificationType());
                var ns = _r.ReadUInt16();
                for (var k = 0; k < ns; k++) stack.Add(ReadVerificationType());
            }
            else { kind = StackFrameKind.Reserved; delta = 0; corrupt = $"保留帧标签 {tag}"; }
            var byteOffset = prevOffset + 1 + delta;
            prevOffset = byteOffset;
            result.Add(new RawStackFrame(new Qty(byteOffset, "bytecode-pc", $"offset_delta={delta}"),
                kind, tag, locals, stack, corrupt, corrupt == null));
        }
        return result.ToArray();
    }

    private string ReadVerificationType()
    {
        var t = _r.ReadByte();
        return t switch
        {
            0 => "top",
            1 => "int",
            2 => "float",
            3 => "double",
            4 => "long",
            5 => "null",
            6 => "uninitializedThis",
            7 => $"class:{ClassNameAt(_r.ReadUInt16())}",
            8 => $"uninitialized@{_r.ReadUInt16()}",
            _ => throw new ClassReadException($"非法 verification_type_info tag {t}")
        };
    }
}
