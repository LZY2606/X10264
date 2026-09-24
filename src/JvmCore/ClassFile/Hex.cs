using System.Text;

namespace JvmCore.ClassFile;

public static class Hex
{
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static byte[] Decode(string hex)
    {
        hex = hex.Trim();
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.AsSpan(i * 2, 2), 16);
        return bytes;
    }
}

internal ref struct BeReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> data = data;
    public int Pos;
    public bool U1(out byte v) { v = Pos < data.Length ? data[Pos++] : (byte)0; return Pos <= data.Length; }
    public bool U2(out ushort v)
    {
        v = 0; if (Pos + 2 > data.Length) return false;
        v = (ushort)((data[Pos] << 8) | data[Pos + 1]); Pos += 2; return true;
    }
    public bool U4(out uint v)
    {
        v = 0; if (Pos + 4 > data.Length) return false;
        v = (uint)((data[Pos] << 24) | (data[Pos + 1] << 16) | (data[Pos + 2] << 8) | data[Pos + 3]);
        Pos += 4; return true;
    }
    public ReadOnlySpan<byte> Take(int n)
    {
        var s = data.Slice(Pos, Math.Min(n, data.Length - Pos)); Pos += n; return s;
    }
}
