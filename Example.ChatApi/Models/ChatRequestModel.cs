namespace Example.ChatApi.Models;

public class ChatRequestModel
{
    public required string ChatMessage { get; set; }
    public string? ConversationId { get; set; }
}
