using System.Net.Http.Headers;

namespace PinguChan.Core.Config;

public sealed class ConfigValidator
{
    public sealed record Result(bool Ok, List<string> Warnings, List<string> Errors)
    {
        public static Result Success(params string[] warnings) => new(true, warnings.ToList(), new());
        public static Result Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
            => new(false, warnings?.ToList() ?? new(), errors.ToList());
    }

    public Result ValidateAndNormalize(PinguConfig cfg)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Dedupe and normalize targets
    cfg.Targets.Ping = cfg.Targets.Ping
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        cfg.Targets.Dns = cfg.Targets.Dns
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        cfg.Targets.Http = cfg.Targets.Http
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Apply allow/deny filters if present
        if (cfg.Filters is not null && (cfg.Filters.Allow.Count > 0 || cfg.Filters.Deny.Count > 0))
        {
            bool Allowed(string s)
            {
                var allowOk = cfg.Filters.Allow.Count == 0 || cfg.Filters.Allow.Any(a => s.Contains(a, StringComparison.OrdinalIgnoreCase));
                var denyHit = cfg.Filters.Deny.Any(d => s.Contains(d, StringComparison.OrdinalIgnoreCase));
                return allowOk && !denyHit;
            }
            int before = cfg.Targets.Ping.Count + cfg.Targets.Dns.Count + cfg.Targets.Http.Count;
            cfg.Targets.Ping = cfg.Targets.Ping.Where(Allowed).ToList();
            cfg.Targets.Dns = cfg.Targets.Dns.Where(Allowed).ToList();
            cfg.Targets.Http = cfg.Targets.Http.Where(Allowed).ToList();
            int after = cfg.Targets.Ping.Count + cfg.Targets.Dns.Count + cfg.Targets.Http.Count;
            if (after < before) warnings.Add($"Filters removed {before - after} target(s) via allow/deny lists.");
        }

        if (cfg.Targets.Ping.Count == 0) warnings.Add("No ping targets configured.");
        if (cfg.Targets.Dns.Count == 0) warnings.Add("No DNS targets configured.");
        if (cfg.Targets.Http.Count == 0) warnings.Add("No HTTP targets configured.");

        // Floors sanity
        var sched = cfg.Orchestration.Scheduler;
        var fp = DurationParser.Parse(sched.GlobalFloorPing, TimeSpan.Zero);
        var fd = DurationParser.Parse(sched.GlobalFloorDns, TimeSpan.Zero);
        var fh = DurationParser.Parse(sched.GlobalFloorHttp, TimeSpan.Zero);
        if (fp < TimeSpan.Zero || fd < TimeSpan.Zero || fh < TimeSpan.Zero)
            errors.Add("Global floors must not be negative.");

        // User-Agent sanity
        var ua = cfg.Http.UserAgent;
        if (!string.IsNullOrWhiteSpace(ua))
        {
            try { _ = ProductInfoHeaderValue.Parse(ua); }
            catch { warnings.Add("http.userAgent is not a standard product token; servers may ignore it."); }
        }

        return errors.Count > 0 ? Result.Failure(errors, warnings) : Result.Success(warnings.ToArray());
    }
}
