namespace Example.EnergyAnalyticsMcp.Models;

public class RouteQuestionOutput
{
    public required string Intent { get; set; }
    public required string Grain { get; set; }
    public required List<string> Metrics { get; set; }
    public required string StartDate { get; set; }
    public required string EndDate { get; set; }
    public required string Reason { get; set; }
}
