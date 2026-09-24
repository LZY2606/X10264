using System;
using System.Collections.Generic;
using System.Text;

namespace JvmVerify.Core.ClassFile;

public sealed class ByteReader
{
    private readonly byte[] _b;
    public int Position { get; private set; }
    public int Length => _b.Length;
    public ByteReader(IReadOnlyList<byte> bytes) { _b = new byte[bytes.Count]; for (int i = 0; i < bytes.Count; i++) _b[i] = bytes[i]; }
    public ByteReader(byte[] bytes) { _b = bytes; }
    public bool HasMore => Position < _b.Length;
    public int Mark() => Position;
    public void Seek(int p) => Position = p;
    public byte U1() => _b[Position++];
    public sbyte S1() => unchecked((sbyte)U1());
    public ushort U2() { ushort v = (ushort)((_b[Position] << 8) | _b[Position + 1]); Position += 2; return v; }
    public short S2() => unchecked((short)U2());
    public uint U4() { uint v = (uint)((_b[Position] << 24) | (_b[Position + 1] << 16) | (_b[Position + 2] << 8) | _b[Position + 3]); Position += 4; return v; }
    public byte[] Take(int n) { var r = new byte[n]; Array.Copy(_b, Position, r, 0, n); Position += n; return r; }
}

public sealed class ByteWriter
{
    private readonly List<byte> _b = new();
    public int Position => _b.Count;
    public byte[] ToArray() => _b.ToArray();
    public void U1(int v) => _b.Add(unchecked((byte)v));
    public void S1(int v) => _b.Add(unchecked((byte)v));
    public void U2(int v) { _b.Add(unchecked((byte)(v >> 8))); _b.Add(unchecked((byte)v)); }
    public void S2(int v) => U2(v);
    public void U4(long v) { _b.Add(unchecked((byte)(v >> 24))); _b.Add(unchecked((byte)(v >> 16))); _b.Add(unchecked((byte)(v >> 8))); _b.Add(unchecked((byte)v)); }
    public void Bytes(IEnumerable<byte> bytes) => _b.AddRange(bytes);
    public void Utf8(string s)
    {
        var encoded = Encoding.UTF8.GetBytes(s);
        // JVM modified UTF-8 differs only for null/4-byte sequences; fixtures use ASCII names.
        U2(encoded.Length);
        _b.AddRange(encoded);
    }
}
