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

// Cute welcome banner
PrintWelcome();
await PrintPrivilegeSummaryAsync();

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
	// no interactive sudo; just report privileges
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
// no interactive sudo; just report privileges
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
var tui = string.IsNullOrWhiteSpace(cfg.Sinks.Logs)
	? new ConsoleTui()
	: new ConsoleTui(logPath: (Path.IsPathRooted(cfg.Sinks.Logs!) ? WithTs(cfg.Sinks.Logs!) : WithTs(Path.Combine(AppContext.BaseDirectory, cfg.Sinks.Logs!))));
PinguChan.Core.Runtime.LoggerHelper.ExternalLogger = tui;
// Background task to surface findings
var findingsCts = new CancellationTokenSource();
var findingsTask = Task.Run(async () =>
{
	await foreach (var f in PinguChan.Core.Runtime.FindingsBus.ReadAllAsync(findingsCts.Token))
	{
		tui.LogWarn($"{f.Timestamp:HH:mm:ss} [{f.Severity}] {f.RuleId}: {f.Message}");
	}
});
var renderer = Task.Run(async () =>
{
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
	findingsCts.Cancel();
	await host.StopAsync();
};
await host.RunAsync(duration, once);
cts.Cancel();
findingsCts.Cancel();
await renderer;
findingsCts.Cancel();
try { await findingsTask; } catch { }


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

static void PrintWelcome()
{
	// Keep it short and cute.
	Console.WriteLine("====================");
	Console.WriteLine(" Pingu-chan 🐧💢");
	Console.WriteLine(" Network watcher & tsundere assistant");
	Console.WriteLine("====================\n");
}

static async Task PrintPrivilegeSummaryAsync()
{
	try
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		var caps = await PinguChan.Core.Capabilities.CapabilityScanner.ScanAsync(cts.Token);
		var icmp = caps.FirstOrDefault(c => c.Id == "icmp_raw");
		var elevated = IsElevated();
		Console.WriteLine($"Privileges: elevated {(elevated ? "yes" : "no")}" + (OperatingSystem.IsLinux() ? " (uid)" : string.Empty));
		if (icmp is not null)
			Console.WriteLine($"Privileges: raw ping {(icmp.Available ? "available" : "not available")}" + (icmp.RequiresSudo ? " (sudo suggested)" : string.Empty));
	}
	catch { }
}

static bool IsElevated()
{
	try
	{
		if (OperatingSystem.IsWindows())
		{
			var id = System.Security.Principal.WindowsIdentity.GetCurrent();
			var principal = new System.Security.Principal.WindowsPrincipal(id);
			return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
		}
		// Best-effort on Unix
		return Environment.UserName == "root";
	}
	catch { return false; }
}

// interactive sudo disabled per request; we only display privilege status now
