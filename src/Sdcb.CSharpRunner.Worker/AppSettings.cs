namespace Sdcb.CSharpRunner.Worker;

public record AppSettings
{
    public int MaxRuns { get; init; }

    public bool Register { get; init; }

    public required string RegisterHostUrl { get; init; }

    public string? ExposedUrl { get; init; }

    public bool WarmUp { get; init; }

    public int MaxTimeout { get; init; }

    public static AppSettings Load(IConfiguration config) => new()
    {
        MaxRuns = config.GetValue("MaxRuns", 0),
        Register = config.GetValue("Register", false),
        RegisterHostUrl = config.GetValue("RegisterHostUrl", string.Empty)!,
        ExposedUrl = config.GetValue<string?>("ExposedUrl"),
        WarmUp = config.GetValue("WarmUp", false),
        MaxTimeout = config.GetValue("MaxTimeout", 30000)
    };
}
