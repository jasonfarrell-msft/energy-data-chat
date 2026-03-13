namespace Example.EnergyAnalyticsMcp.Models;

public class ConversationState
{
    public string? LastGrain { get; set; }
    public DateRange? LastRange { get; set; }
}

public class DateRange
{
    public string? Start { get; set; }
    public string? End { get; set; }
}

public class RouteQuestionInput
{
    public required string Question { get; set; }
    public ConversationState? ConversationState { get; set; }
}
