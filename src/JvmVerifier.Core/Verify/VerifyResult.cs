namespace JvmVerifier.Core.Verify;

public sealed record VerifyOptions(int MajorVersion)
{
    public bool RequiresStackMaps => MajorVersion >= 51;
    public bool SupportsStackMaps => MajorVersion >= 50;
    public string SemanticsLabel => MajorVersion >= 51 ? "java7+ (stack-maps-required)"
        : MajorVersion >= 50 ? "java6 (stack-maps-optional)"
        : "pre-50 (type-inference)";
}

public sealed record BlockModel(
    int StartOffset, int EndOffset, bool Reachable,
    string[] EntryLocals, string[] EntryStack,
    int[] InstructionOffsets, string[] Mnemonics);

public sealed record EdgeModel(int From, int To, string Kind, string[] Locals, string[] Stack);

public sealed record VerifyError(int Offset, string Code, string Message, int[] Path, string[] Candidates);

public sealed record VerifyDiagnostic(int? Offset, string Code, string Message);

public sealed record MethodVerifyResult(
    string Method, string Descriptor, string Semantics,
    List<BlockModel> Blocks, List<EdgeModel> Edges,
    List<VerifyError> Errors, List<VerifyDiagnostic> Diagnostics)
{
    public bool Passed => Errors.Count == 0;
}

public sealed record ClassVerifyResult(
    string ClassName, string ClassVersion, string Semantics,
    List<MethodVerifyResult> Methods)
{
    public bool Passed => Methods.All(m => m.Passed);
}
