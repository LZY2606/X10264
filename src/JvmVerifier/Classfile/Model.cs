namespace JvmVerifier.Classfile;

/// <summary>
/// Raw-preserving classfile model. Everything the importer saw is kept verbatim:
/// original byte offsets, constant pool entries (raw bytes + decoded), descriptors,
/// branch targets, exception ranges, stack map frames and the classfile version.
/// Normalization happens only in the derived (verification) layer.
/// </summary>
public sealed record CpEntry(int Index, int Tag, string TagName, string RawHex, string Decoded);

public sealed record InstructionInfo(
    int Offset, int Opcode, string Mnemonic, string OperandRawHex, string OperandText);

public sealed record ExceptionRange(
    int StartPc, int EndPc, int HandlerPc, int CatchTypeIndex, string CatchTypeName);

public sealed record StackMapFrameInfo(
    int FrameIndex, int OffsetDelta, int ComputedOffset, int FrameType,
    string RawHex, string Decoded);

public sealed record MethodModel(
    string Name, string Descriptor, int AccessFlags,
    int MaxStack, int MaxLocals,
    string CodeRawHex,
    List<InstructionInfo> Instructions,
    List<ExceptionRange> ExceptionTable,
    List<StackMapFrameInfo> StackMapFrames,
    string StackMapTableRawHex);

public sealed record ClassFileModel(
    string Name,
    int MinorVersion, int MajorVersion,
    List<CpEntry> ConstantPool,
    string ThisClass, string SuperClass,
    List<MethodModel> Methods)
{
    public string CpUtf8(int index) =>
        ConstantPool.First(e => e.Index == index && e.Tag == 1).Decoded;

    public string CpClassName(int index)
    {
        var e = ConstantPool.First(x => x.Index == index && x.Tag == 7);
        // decoded form: "Class #nameIndex"
        var nameIdx = int.Parse(e.Decoded.Split('#')[1]);
        return CpUtf8(nameIdx);
    }

    public (string ClassName, string Name, string Descriptor) CpMethodRef(int index)
    {
        var e = ConstantPool.First(x => x.Index == index && (x.Tag == 10 || x.Tag == 11));
        // decoded: "Methodref #class.#nameAndType"
        var parts = e.Decoded.Split(' ')[1].Split('.');
        var cls = CpClassName(int.Parse(parts[0].TrimStart('#')));
        var nt = ConstantPool.First(x => x.Index == int.Parse(parts[1].TrimStart('#')) && x.Tag == 12);
        var np = nt.Decoded.Split(' ')[1].Split('.');
        return (cls, CpUtf8(int.Parse(np[0].TrimStart('#'))), CpUtf8(int.Parse(np[1].TrimStart('#'))));
    }

    public string CpFieldRefClass(int index)
    {
        var e = ConstantPool.First(x => x.Index == index && x.Tag == 9);
        var parts = e.Decoded.Split(' ')[1].Split('.');
        return CpClassName(int.Parse(parts[0].TrimStart('#')));
    }
}
