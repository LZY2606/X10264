namespace JvmVerifier.Parsing;

internal sealed class BeReader(byte[] bytes, int start, int length)
{
    private int _position = start;
    public int Position => _position;
    public int Remaining => start + length - _position;

    public byte U1()
    {
        if (Remaining < 1) throw new EndOfStreamException("truncated classfile");
        return bytes[_position++];
    }
    public ushort U2()
    {
        if (Remaining < 2) throw new EndOfStreamException("truncated classfile");
        var value = (ushort)((bytes[_position] << 8) | bytes[_position + 1]);
        _position += 2;
        return value;
    }
    public uint U4()
    {
        if (Remaining < 4) throw new EndOfStreamException("truncated classfile");
        var value = ((uint)bytes[_position] << 24) | ((uint)bytes[_position + 1] << 16) | ((uint)bytes[_position + 2] << 8) | bytes[_position + 3];
        _position += 4;
        return value;
    }
    public byte[] Bytes(int count)
    {
        if (Remaining < count) throw new EndOfStreamException("truncated classfile");
        var result = bytes.Skip(_position).Take(count).ToArray();
        _position += count;
        return result;
    }
    public void Skip(int count)
    {
        if (Remaining < count) throw new EndOfStreamException("truncated classfile");
        _position += count;
    }
}
