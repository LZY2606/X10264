using System.Text;

namespace JvmBytecodeVerifier.Core.Raw;

public sealed class ClassFormatException : Exception
{
    public string Coordinate { get; }
    public ClassFormatException(string message, string coordinate = "classfile") : base(message)
    {
        Coordinate = coordinate;
    }
}

public ref struct BigEndianReader
{
    private readonly byte[] _b;
    public int Position { get; set; }
    public int Length => _b.Length;
    public byte[] Underlying => _b;

    public BigEndianReader(byte[] bytes, int position = 0)
    {
        _b = bytes;
        Position = position;
    }

    public byte U1(string where = "u1")
    {
        if (Position + 1 > _b.Length)
            throw new ClassFormatException($"Unexpected end of class file while reading {where}", $"byte:{Position}");
        return _b[Position++];
    }

    public ushort U2(string where = "u2")
    {
        if (Position + 2 > _b.Length)
            throw new ClassFormatException($"Unexpected end of class file while reading {where}", $"byte:{Position}");
        var v = (ushort)((_b[Position] << 8) | _b[Position + 1]);
        Position += 2;
        return v;
    }

    public uint U4(string where = "u4")
    {
        if (Position + 4 > _b.Length)
            throw new ClassFormatException($"Unexpected end of class file while reading {where}", $"byte:{Position}");
        var v = ((uint)_b[Position] << 24) | ((uint)_b[Position + 1] << 16) |
                ((uint)_b[Position + 2] << 8) | _b[Position + 3];
        Position += 4;
        return v;
    }

    public ReadOnlySpan<byte> Bytes(int count)
    {
        if (Position + count > _b.Length)
            throw new ClassFormatException($"Unexpected end of class file while reading {count} bytes", $"byte:{Position}");
        var span = new ReadOnlySpan<byte>(_b, Position, count);
        Position += count;
        return span;
    }

    public string ModifiedUtf8(int count)
    {
        var span = Bytes(count);
        var sb = new StringBuilder(count);
        for (var i = 0; i < span.Length;)
        {
            var b0 = span[i++];
            if (b0 == 0) throw new ClassFormatException("Modified UTF-8 contains embedded null", $"byte:{Position - count + i - 1}");
            if (b0 < 0x80) sb.Append((char)b0);
            else if ((b0 & 0xE0) == 0xC0)
            {
                if (i >= span.Length) throw new ClassFormatException("Truncated two-byte modified UTF-8 sequence", $"byte:{Position}");
                var b1 = span[i++];
                if ((b1 & 0xC0) != 0x80) throw new ClassFormatException("Invalid modified UTF-8 continuation byte", $"byte:{Position}");
                sb.Append((char)(((b0 & 0x1F) << 6) | (b1 & 0x3F)));
            }
            else if ((b0 & 0xF0) == 0xE0)
            {
                if (i + 1 >= span.Length) throw new ClassFormatException("Truncated three-byte modified UTF-8 sequence", $"byte:{Position}");
                var b1 = span[i++];
                var b2 = span[i++];
                if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80)
                    throw new ClassFormatException("Invalid modified UTF-8 continuation byte", $"byte:{Position}");
                sb.Append((char)(((b0 & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (b2 & 0x3F)));
            }
            else throw new ClassFormatException("Invalid modified UTF-8 leading byte", $"byte:{Position - count + i - 1}");
        }
        return sb.ToString();
    }
}
