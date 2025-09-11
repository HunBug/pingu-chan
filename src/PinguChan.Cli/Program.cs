using PinguChan.Core;
using PinguChan.Core.Probes;
using PinguChan.Core.Runtime;
using PinguChan.Core.Sinks;
using PinguChan.Cli;
using PinguChan.Core.Config;
using PinguChan.Core.Models;
using PinguChan.Cli.Tui;
using PinguChan.Core.Collectors;
using PinguChan.Cli.Sudo;

// Minimal wiring without Host (no external packages required)
// TODO: integrate Microsoft.Extensions.* when NuGet is available.

// Load config (YAML or JSON). Defaults applied if file missing.
var configPath = args.SkipWhile(a => a != "--config").Skip(1).FirstOrDefault();
var cfg = ConfigLoader.LoadFromFile(configPath);

TimeSpan pingInt = DurationParser.Parse(cfg.Intervals.Ping, TimeSpan.FromSeconds(2));
TimeSpan dnsInt = DurationParser.Parse(cfg.Intervals.Dns, TimeSpan.FromSeconds(5));
TimeSpan httpInt = DurationParser.Parse(cfg.Intervals.Http, TimeSpan.FromSeconds(10));

var probes = new List<IProbe>();
foreach (var t in cfg.Targets.Ping) probes.Add(new PingProbe(t, pingInt));
foreach (var t in cfg.Targets.Dns) probes.Add(new DnsProbe(t, dnsInt));
foreach (var t in cfg.Targets.Http) probes.Add(new HttpProbe(t, httpInt));
// Gateway probe (implicit)
var gatewayProbe = new GatewayProbe(TimeSpan.FromSeconds(5));
if (!gatewayProbe.Id.EndsWith(":unknown")) probes.Add(gatewayProbe);
// MTU probe (best-effort, infrequent)
probes.Add(new MtuProbe("8.8.8.8", TimeSpan.FromMinutes(30)));

// Collectors (diagnostics)
var collectors = new List<ICollector>
{
	new NetIfCollector(TimeSpan.FromMinutes(5)),
	new RouteCollector(TimeSpan.FromMinutes(5)),
	new FirewallCollector(TimeSpan.FromMinutes(10)),
	new TracerouteCollector(TimeSpan.FromMinutes(30)),
	new InternetConnectivityCollector(TimeSpan.FromMinutes(1))
};

var sinks = new List<IResultSink>();
var baseDir = AppContext.BaseDirectory;
string WithTs(string path)
{
	if (!cfg.Sinks.AppendTimestamp) return path;
	var dir = Path.GetDirectoryName(path) ?? ".";
	var name = Path.GetFileNameWithoutExtension(path);
	var ext = Path.GetExtension(path);
	var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
	return Path.Combine(dir, $"{name}_{stamp}{ext}");
}
if (!string.IsNullOrWhiteSpace(cfg.Sinks.Csv))
{
	var p = Path.Combine(baseDir, cfg.Sinks.Csv!);
	sinks.Add(new CsvSink(WithTs(p)));
}
if (!string.IsNullOrWhiteSpace(cfg.Sinks.Jsonl))
{
	var p = Path.Combine(baseDir, cfg.Sinks.Jsonl!);
	sinks.Add(new JsonlSink(WithTs(p)));
}

var durationArg = args.SkipWhile(a => a != "--duration").Skip(1).FirstOrDefault();
var once = args.Contains("--once");
var diagnose = args.Contains("diagnose");
var askSudo = args.Contains("--sudo");
TimeSpan? duration = null;
if (TimeSpan.TryParse(durationArg, out var d)) duration = d;

if (diagnose)
{
	// quick one-shot: environment + capabilities + run each probe once
	PrintEnv();
	if (askSudo && SudoHelper.IsUnixLike)
	{
		Console.WriteLine("Requesting sudo for extended diagnostics (optional)...");
		var ok = SudoHelper.TryElevateInteractive();
		Console.WriteLine(ok ? "sudo cached." : "sudo not available or canceled.");
	}
	await PrintCapabilitiesAsync();
	await using var diagHost = new MonitorHost(probes, collectors, sinks);
	await diagHost.RunAsync(duration: null, once: true);
	return;
}

var stats = new StatsAggregator(TimeSpan.FromMinutes(1));
sinks.Add(new ForwardingSink(sample => stats.Add(sample)));

// Start a renderer task to show two static lines updated in place
var cts = new CancellationTokenSource();
var start = DateTimeOffset.UtcNow;
if (duration is null)
{
	Console.WriteLine("Starting continuous monitoring. Press Ctrl+C to stop.");
}
else
{
	Console.WriteLine($"Starting monitoring for duration: {duration.Value}.");
}
if (askSudo && SudoHelper.IsUnixLike)
{
	Console.WriteLine("Requesting sudo for extended monitoring (optional)...");
	var ok = SudoHelper.TryElevateInteractive();
	Console.WriteLine(ok ? "sudo cached." : "sudo not available or canceled.");
}
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
var tui = string.IsNullOrWhiteSpace(cfg.Sinks.Logs)
	? new ConsoleTui()
	: new ConsoleTui(logPath: (Path.IsPathRooted(cfg.Sinks.Logs!) ? WithTs(cfg.Sinks.Logs!) : WithTs(Path.Combine(AppContext.BaseDirectory, cfg.Sinks.Logs!))));
PinguChan.Core.Runtime.LoggerHelper.ExternalLogger = tui;
var renderer = Task.Run(async () =>
{
	var lastAlert = new Dictionary<string, DateTimeOffset>();
	while (!cts.IsCancellationRequested)
	{
		var (l1, l2) = stats.BuildSummary();
		if (duration is { } dur)
		{
			var elapsed = DateTimeOffset.UtcNow - start;
			var remaining = dur - elapsed;
			if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
			l2 = $"{l2}  | remaining: {remaining:hh\\:mm\\:ss}".Trim();
		}
		else
		{
			l2 = string.IsNullOrEmpty(l2) ? "Press Ctrl+C to stop" : $"{l2}  | Press Ctrl+C to stop";
		}
	// Draw both bottom status lines: l1 metrics and l2 hint/remaining
	tui.SetStatus(l1, l2);

		// Emit problem markers into logs when rolling stats show issues
		async void MaybeAlert(string key, string message, string ruleId)
		{
			var now = DateTimeOffset.UtcNow;
			if (lastAlert.TryGetValue(key, out var prev) && (now - prev) < TimeSpan.FromSeconds(10)) return;
			lastAlert[key] = now;
			tui.LogWarn(message);
			// Also persist to sinks as a RuleFinding so files capture the event
			var finding = new RuleFinding(now, ruleId, Severity.Warning, message);
			foreach (var s in sinks)
			{
				try { await s.WriteAsync(finding, CancellationToken.None); } catch { }
			}
		}

		// Ping loss warnings
		foreach (var p in stats.GetPingWindowStats())
		{
			if (p.count >= 5 && p.lossPct >= 5.0)
			{
				MaybeAlert($"ping:{p.target}", $"PING {p.target} window loss {p.lossPct:F1}% ({p.count}){(p.avgMs.HasValue ? $" avg={p.avgMs:F0}ms" : string.Empty)}", $"loss_1m:{p.target}");
			}
		}
		// DNS failure warnings
		var dns = stats.GetDnsWindowStats();
		if (dns.total >= 5 && dns.failPct >= 10.0)
		{
			MaybeAlert("dns", $"DNS window fail {dns.failPct:F1}% ({dns.total}){(dns.p95.HasValue ? $" p95={dns.p95:F0}ms" : string.Empty)}", "dns_fail_1m");
		}
		// HTTP failure warnings
		var http = stats.GetHttpWindowStats();
		if (http.ok + http.fail >= 5 && http.fail > 0)
		{
			var failPct = 100.0 * http.fail / Math.Max(1, http.ok + http.fail);
			if (failPct >= 10.0)
				MaybeAlert("http", $"HTTP window fail {failPct:F1}% (ok={http.ok} fail={http.fail}){(http.p95.HasValue ? $" p95={http.p95:F0}ms" : string.Empty)}", "http_fail_1m");
		}
		try { await Task.Delay(1000, cts.Token); } catch { }
	}
});

await using var host = new MonitorHost(probes, collectors, sinks) { SummaryInterval = TimeSpan.FromSeconds(30) };
var stopRequested = false;
Console.CancelKeyPress += async (_, e) =>
{
	if (stopRequested) return; // ignore repeated signals while stopping
	stopRequested = true;
	e.Cancel = true;
	cts.Cancel();
	await host.StopAsync();
};
await host.RunAsync(duration, once);
cts.Cancel();
await renderer;


static void PrintEnv()
{
	Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
	Console.WriteLine($"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
	Console.WriteLine($"Process: {(Environment.Is64BitProcess ? "x64" : "x86")}");
}

static async Task PrintCapabilitiesAsync()
{
	var caps = await PinguChan.Core.Capabilities.CapabilityScanner.ScanAsync(CancellationToken.None);
	foreach (var c in caps)
	{
		Console.WriteLine($"- {c.Id}: avail={(c.Available ? "yes" : "no")}, sudo={(c.RequiresSudo ? "yes" : "no")}{(string.IsNullOrEmpty(c.Hint) ? string.Empty : $" | hint: {c.Hint}")}");
	}
}

// color writing handled by ConsoleTui
