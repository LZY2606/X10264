namespace BytecodeVerifier.Core.Model;

/// <summary>
/// Stable, content-addressed identities. Survive export/delete/reimport.
/// Identity is derived from immutable coordinates, never from database row ids.
/// </summary>
public static class StableId
{
    public static string ForClass(string rawSha256) => $"cls_{rawSha256[..16]}";
    public static string ForMethod(string classId, ushort methodIndex) => $"{classId}.m{methodIndex}";
    public static string ForCp(string methodId, ushort cpIndex) => $"{methodId}.cp{cpIndex}";
    public static string ForInstr(string methodId, int bci) => $"{methodId}.i{bci}";
    public static string ForFrame(string methodId, int frameIndex) => $"{methodId}.f{frameIndex}";
    public static string ForHandler(string methodId, int handlerIndex) => $"{methodId}.h{handlerIndex}";
    public static string ForBlock(string methodId, int leaderBci) => $"{methodId}.b{leaderBci}";
    public static string ForEdge(string srcBlockId, string edgeKind, int ordinal) => $"{srcBlockId}.e_{edgeKind}_{ordinal}";
    public static string ForEvent(string derivationId, int seq) => $"{derivationId}.ev{seq}";
    public static string ForCandidate(string derivationId, string nodeId, int ordinal) => $"{derivationId}.cand_{nodeId}_{ordinal}";
    public static string ForMutation(string dossierId, string mutationCode) => $"{dossierId}.mut_{mutationCode}";
}
