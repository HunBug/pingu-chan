namespace PinguChan.Core.Capabilities;

public sealed record CapabilityReport(
    string Id,
    string Description,
    bool ImplementedOnOs,
    bool Available,
    bool RequiresSudo,
    bool ExternalDepsOk,
    string? Hint
);
