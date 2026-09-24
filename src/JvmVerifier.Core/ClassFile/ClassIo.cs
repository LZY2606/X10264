using System.Text;

namespace JvmVerifier.Core.ClassFile;

internal sealed class ClassReader(byte[] data)
{
    private int _pos;
    public int Position => _pos;
    public int Length => data.Length;
    public bool HasMore => _pos < data.Length;

    public byte U1() => data[_pos++];
    public ushort U2() { int v = (data[_pos] << 8) | data[_pos + 1]; _pos += 2; return (ushort)v; }
    public uint U4() { uint v = ((uint)data[_pos] << 24) | ((uint)data[_pos + 1] << 16) | ((uint)data[_pos + 2] << 8) | data[_pos + 3]; _pos += 4; return v; }
    public byte[] Bytes(int n) { var b = data.AsSpan(_pos, n).ToArray(); _pos += n; return b; }
    public void Seek(int p) => _pos = p;
}

internal sealed class ClassWriter
{
    private readonly MemoryStream _ms = new();

    public void U1(byte v) => _ms.WriteByte(v);
    public void U2(int v) { _ms.WriteByte((byte)(v >> 8)); _ms.WriteByte((byte)v); }
    public void U4(long v)
    {
        _ms.WriteByte((byte)(v >> 24)); _ms.WriteByte((byte)(v >> 16));
        _ms.WriteByte((byte)(v >> 8)); _ms.WriteByte((byte)v);
    }
    public void Raw(byte[] b) => _ms.Write(b, 0, b.Length);
    public byte[] ToArray() => _ms.ToArray();
    public int Length => (int)_ms.Length;
}
