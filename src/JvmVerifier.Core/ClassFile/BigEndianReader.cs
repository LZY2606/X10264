using System.Text;

namespace JvmVerifier.Core.ClassFile;

/// <summary>
/// Sequential big-endian reader over the raw classfile bytes. Every offset it
/// exposes is the original byte offset inside the imported classfile, which is
/// preserved verbatim as the coordinate basis for every later derivation.
/// </summary>
public sealed class BigEndianReader
{
    private readonly byte[] _data;
    public int Position { get; private set; }
    public int Length => _data.Length;

    public BigEndianReader(byte[] data, int position = 0)
    {
        _data = data;
        Position = position;
    }

    public bool HasMore => Position < _data.Length;

    public byte U1()
    {
        Ensure(1);
        return _data[Position++];
    }

    public ushort U2()
    {
        Ensure(2);
        ushort v = (ushort)((_data[Position] << 8) | _data[Position + 1]);
        Position += 2;
        return v;
    }

    public uint U4()
    {
        Ensure(4);
        uint v = ((uint)_data[Position] << 24) | ((uint)_data[Position + 1] << 16) |
                 ((uint)_data[Position + 2] << 8) | _data[Position + 3];
        Position += 4;
        return v;
    }

    public byte[] ReadBytes(int count)
    {
        Ensure(count);
        var slice = new byte[count];
        Array.Copy(_data, Position, slice, 0, count);
        Position += count;
        return slice;
    }

    public string ModifiedUtf8(int count)
    {
        Ensure(count);
        int start = Position;
        var sb = new StringBuilder();
        int end = Position + count;
        while (Position < end)
        {
            int b = _data[Position++];
            if (b == 0) throw new ClassFormatException("Malformed modified-UTF8: null byte", Position);
            if (b < 0x80)
            {
                sb.Append((char)b);
            }
            else if ((b & 0xE0) == 0xC0)
            {
                int b2 = U1();
                sb.Append((char)(((b & 0x1F) << 6) | (b2 & 0x3F)));
            }
            else if ((b & 0xF0) == 0xE0)
            {
                int b2 = U1();
                int b3 = U1();
                sb.Append((char)(((b & 0x0F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F)));
            }
            else
            {
                throw new ClassFormatException("Malformed modified-UTF8 sequence", Position);
            }
        }
        Position = start + count;
        return sb.ToString();
    }

    private void Ensure(int count)
    {
        if (Position + count > _data.Length)
            throw new ClassFormatException($"Unexpected end of classfile (need {count} bytes at {Position})", Position);
    }
}

public sealed class ClassFormatException : Exception
{
    public int Offset { get; }
    public ClassFormatException(string message, int offset = -1)
        : base(offset >= 0 ? $"{message} [byte offset 0x{offset:X4}]" : message) => Offset = offset;
}
