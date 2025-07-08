namespace Sdcb.CSharpRunner.Host;

public record Worker : IHaveMaxRuns
{
    public required string Url { get; init; }
    public required int MaxRuns { get; init; }
    public int CurrentRuns { get; set; }
}
