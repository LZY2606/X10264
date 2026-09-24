namespace Jv.Core.Classfile;

// 大端 classfile 读取器，带位置坐标（字节偏移），便于诊断保留原位置。
public sealed class BeReader
{
    private readonly byte[] _b;
    public int Position { get; private set; }
    public int Length => _b.Length;

    public BeReader(byte[] bytes, int start = 0) { _b = bytes; Position = start; }

    public int U1()
    {
        Ensure(1);
        return _b[Position++];
    }
    public int U2()
    {
        Ensure(2);
        int v = (_b[Position] << 8) | _b[Position + 1];
        Position += 2;
        return v;
    }
    public int S2()
    {
        int v = U2();
        return (short)v;
    }
    public long U4()
    {
        Ensure(4);
        long v = ((long)_b[Position] << 24) | ((long)_b[Position + 1] << 16) |
                 ((long)_b[Position + 2] << 8) | _b[Position + 3];
        Position += 4;
        return v;
    }
    public byte[] Bytes(int n)
    {
        Ensure(n);
        var r = new byte[n];
        Array.Copy(_b, Position, r, 0, n);
        Position += n;
        return r;
    }
    public byte[] RestFrom(int start, int len)
    {
        var r = new byte[len];
        Array.Copy(_b, start, r, 0, len);
        return r;
    }
    public void Seek(int p)
    {
        if (p < 0 || p > _b.Length) throw new ClassFormatException($"偏移越界 {p}");
        Position = p;
    }
    private void Ensure(int n)
    {
        if (Position + n > _b.Length)
            throw new ClassFormatException($"意外的 classfile 结尾：需要 {n} 字节于偏移 {Position}，总长 {_b.Length}");
    }
}

public sealed class ClassFormatException : Exception
{
    public ClassFormatException(string message, int? offset = null)
        : base(offset is int o ? $"{message}（字节偏移 {o}）" : message) { }
}
