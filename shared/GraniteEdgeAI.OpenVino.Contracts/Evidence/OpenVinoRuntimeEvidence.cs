namespace GraniteEdgeAI.OpenVino.Contracts;

/// <summary>Safe factual runtime evidence; it deliberately has no diagnostic or path field.</summary>
public sealed record OpenVinoRuntimeEvidence(string RequestedDevice, string ActualDevice, string ProtocolId)
{
    public void Validate()
    {
        OpenVinoProtocol.RequireText(RequestedDevice, nameof(RequestedDevice));
        OpenVinoProtocol.RequireText(ActualDevice, nameof(ActualDevice));
        OpenVinoProtocol.Require(
            ProtocolId is OpenVinoProtocol.OfficialProtocolId or OpenVinoProtocol.TurboQuantProtocolId,
            nameof(ProtocolId) + " must be an approved OpenVINO protocol identity.");
    }
}
