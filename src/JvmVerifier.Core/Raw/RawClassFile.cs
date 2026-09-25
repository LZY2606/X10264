namespace JvmVerifier.Core.Raw;

public sealed class RawMethod
{
    public required string Name { get; set; }
    public required string Descriptor { get; set; }
    public int AccessFlags { get; set; }
    public int MaxStack { get; set; }
    public int MaxLocals { get; set; }
    public required List<RawInstruction> Instructions { get; set; } = new();
    public required List<RawExceptionHandler> ExceptionTable { get; set; } = new();
    public required List<RawStackMapFrame> StackMapTable { get; set; } = new();

    /// <summary>Code attribute byte length and its offset inside attributes[].</summary>
    public int CodeLength { get; set; }
    public Coords? CodeAttributeOffset { get; set; }
    public required string Id { get; init; }

    public RawInstruction? AtPc(int pc) => Instructions.FirstOrDefault(i => i.Pc.Value == pc);
}

/// <summary>
/// Class hierarchy hint used by the derived verifier when resolving assignability.
/// SuperName/Interfaces use internal names (java/lang/Object). These are input
/// facts, never guessed by back-propagation.
/// </summary>
public sealed class RawClassFact
{
    public required string InternalName { get; set; }
    public string? SuperName { get; set; }
    public List<string> Interfaces { get; set; } = new();
    public bool IsInterface { get; set; }
}

/// <summary>
/// The immutable source artifact: every direct source representation the
/// verifier needs is preserved here (offsets, constant pool, descriptors,
/// branch targets, exception ranges, stack map frames, class file version).
/// <see cref="Origin"/> records binary file path or JSON source id.
/// </summary>
public sealed class RawClassFile
{
    public required string Origin { get; set; }
    public required Coords MajorVersion { get; set; }
    public required Coords MinorVersion { get; set; }
    public required ConstantPool ConstantPool { get; set; }
    public required List<RawMethod> Methods { get; set; } = new();
    public required List<RawClassFact> ClassFacts { get; set; } = new();

    public string ThisClassName { get; set; } = "";
    public string? SuperClassName { get; set; }
    public int AccessFlags { get; set; }
    public string? SourceMethodId { get; set; }

    public RawMethod? Method(string id) => Methods.FirstOrDefault(m => m.Id == id);
}

/// <summary>
/// A value that could not be converted stays at its original location together
/// with a diagnostic — it is never dropped just to make a diagram render.
/// </summary>
public sealed record RawDiagnostic(string Code, string Location, string Message, string? ValueId = null);

public sealed class RawImport
{
    public required RawClassFile ClassFile { get; set; }
    public List<RawDiagnostic> Diagnostics { get; } = new();
}
