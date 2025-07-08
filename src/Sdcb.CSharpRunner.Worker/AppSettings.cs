namespace Sdcb.CSharpRunner.Worker;

public record AppSettings
{
    public int MaxRuns { get; init; }

    public bool Register { get; init; }

    public required string RegisterHostUrl { get; init; }

    public int? ExposedPort { get; init; }

    public static AppSettings Load(IConfiguration config) => new AppSettings
    {
        MaxRuns = config.GetValue("MaxRuns", 0),
        Register = config.GetValue("Register", false),
        RegisterHostUrl = config.GetValue("RegisterHostUrl", string.Empty)!,
        ExposedPort = config.GetValue<int?>("ExposedPort")
    };
}
