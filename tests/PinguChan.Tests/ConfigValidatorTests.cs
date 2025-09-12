using PinguChan.Core.Config;

public class ConfigValidatorTests
{
    [Fact]
    public void Validator_Dedupes_Targets_And_Warns_On_Empty()
    {
        var cfg = new PinguConfig
        {
            Targets = new TargetsConfig
            {
                Ping = new() { "1.1.1.1", "1.1.1.1", " 8.8.8.8 " },
                Dns = new() { },
                Http = new() { "https://a", "https://a" }
            }
        };
        var v = new ConfigValidator();
        var res = v.ValidateAndNormalize(cfg);
        Assert.True(res.Ok);
        Assert.Contains("No DNS targets configured.", res.Warnings);
        Assert.Equal(2, cfg.Targets.Ping.Count);
        Assert.Equal(1, cfg.Targets.Http.Count);
    }

    [Fact]
    public void Validator_Rejects_Negative_Floors()
    {
        var cfg = new PinguConfig
        {
            Orchestration = new OrchestrationConfig
            {
                Scheduler = new SchedulerConfig
                {
                    GlobalFloorPing = "-1s"
                }
            }
        };
        var v = new ConfigValidator();
        var res = v.ValidateAndNormalize(cfg);
        Assert.False(res.Ok);
        Assert.Contains(res.Errors, e => e.Contains("Global floors"));
    }
}
