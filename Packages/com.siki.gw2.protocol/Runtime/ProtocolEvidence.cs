namespace Gw2.Protocol
{
    // These describe evidence, not a recovered GW2 field or wire value.
    public enum TrafficDirection { Unknown, ServerToClient, ClientToServer }
    public enum PayloadRepresentation { Unknown, NativeMemory, DecodedHandlerRecord, WireBytes }
    public enum EvidenceLevel { Unknown, StaticAnalysis, Observed, IndependentlyReproduced }
}
