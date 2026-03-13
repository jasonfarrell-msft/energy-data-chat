using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Example.ChatApi.Models;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace Example.ChatApi.Controllers;

[ApiController]
[Route("[controller]")]
public class ChatController : ControllerBase
{
    private readonly AIProjectClient _projectClient;
    private readonly string _agentName;
    private readonly string _agentVersion;

    public ChatController(AIProjectClient projectClient, IConfiguration configuration)
    {
        _projectClient = projectClient;
        _agentName = configuration["AzureAI:AgentName"] ?? "chat-agent";
        _agentVersion = configuration["AzureAI:AgentVersion"] ?? "3";
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequestModel request)
    {
        var responseClient = _projectClient.OpenAI.GetProjectResponsesClient();

        var options = new CreateResponseOptions();
        CreateResponseOptionsExtensions.set_Agent(options, new AgentReference(_agentName, _agentVersion));
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.ChatMessage));

        if (!string.IsNullOrEmpty(request.ConversationId))
            CreateResponseOptionsExtensions.set_AgentConversationId(options, request.ConversationId);

        var result = await responseClient.CreateResponseAsync(options);

        var conversationId = ResponseResultExtensions.get_AgentConversationId(result.Value)
            ?? result.Value.Id;

        return Ok(new ChatResponseModel
        {
            Response = result.Value.GetOutputText(),
            ConversationId = conversationId
        });
    }
}
