namespace PinguChan.Core.Models;

public enum SampleKind { Ping, Dns, Http, Upnp, Gateway, Mtu, Collector }
public enum Severity { Info, Warning, Critical }

public sealed record NetSample(
    DateTimeOffset Timestamp,
    SampleKind Kind,
    string Target,
    bool Ok,
    double? Milliseconds,
    string? Extra // JSON blob (resolver, status code, iface, rssi, etc.)
);

public sealed record RuleFinding(
    DateTimeOffset Timestamp,
    string RuleId,
    Severity Severity,
    string Message,
    IReadOnlyDictionary<string, object>? Context = null
);
