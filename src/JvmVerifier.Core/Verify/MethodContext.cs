using JvmVerifier.Core.ClassFile;

namespace JvmVerifier.Core.Verify;

public sealed class MethodContext
{
    public required ClassFileModel ClassFile { get; init; }
    public required MethodInfo Method { get; init; }
    public required CodeAttribute Code { get; init; }
    public required DecodeResult Decoded { get; init; }
    public required VerifyProfile Profile { get; init; }

    public string MethodName => Method.Name(ClassFile.ConstantPool) ?? "?";
    public string MethodDescriptor => Method.Descriptor(ClassFile.ConstantPool) ?? "()V";
    public bool IsConstructor => MethodName == "<init>";
    public bool IsStatic => (Method.AccessFlags & 0x0008) != 0;

    /// <summary>Stable identity coordinate for this method across revisions.</summary>
    public string MethodId => $"{ClassFile.ThisName}#{MethodName}{MethodDescriptor}";
}
