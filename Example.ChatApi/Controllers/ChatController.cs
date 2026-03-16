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
    private const int MaxApprovalLoops = 5;

    private readonly AIProjectClient _projectClient;
    private readonly ILogger<ChatController> _logger;
    private readonly string _agentName;

    public ChatController(AIProjectClient projectClient, IConfiguration configuration, ILogger<ChatController> logger)
    {
        _projectClient = projectClient;
        _logger = logger;
        _agentName = configuration["AzureAI:AgentName"] ?? "chat-agent";
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequestModel request)
    {
        _logger.LogInformation("Chat request received. Agent={Agent}, ConversationId={ConvId}, Message={Msg}",
            _agentName, request.ConversationId ?? "(new)", request.ChatMessage);

        var responseClient = _projectClient.OpenAI.GetProjectResponsesClient();

        var options = new CreateResponseOptions();
        CreateResponseOptionsExtensions.set_Agent(options, new AgentReference(_agentName));
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.ChatMessage));

        // Conversation continuity: conv_ IDs use AgentConversationId, resp_ IDs use PreviousResponseId
        if (!string.IsNullOrEmpty(request.ConversationId))
        {
            if (request.ConversationId.StartsWith("conv", StringComparison.OrdinalIgnoreCase))
                CreateResponseOptionsExtensions.set_AgentConversationId(options, request.ConversationId);
            else
                options.PreviousResponseId = request.ConversationId;
        }

        // Approval loop: auto-approve MCP tool calls that require consent
        ResponseResult response;
        for (int i = 0; ; i++)
        {
            var result = await responseClient.CreateResponseAsync(options);
            response = result.Value;

            _logger.LogInformation("Response iteration {Iter}: Status={Status}, OutputItems={Count}, Types=[{Types}]",
                i, response.Status, response.OutputItems.Count,
                string.Join(", ", response.OutputItems.Select(o => o.GetType().Name)));

            var approvalRequests = response.OutputItems
                .OfType<McpToolCallApprovalRequestItem>()
                .ToList();

            if (approvalRequests.Count > 0)
                _logger.LogInformation("Found {N} MCP approval requests: [{Details}]",
                    approvalRequests.Count,
                    string.Join(", ", approvalRequests.Select(a => $"{a.ToolName}@{a.ServerLabel}")));

            if (approvalRequests.Count == 0 || i >= MaxApprovalLoops)
                break;

            // Build a follow-up request that references the previous response
            // and auto-approves every pending MCP tool call.
            options = new CreateResponseOptions();
            CreateResponseOptionsExtensions.set_Agent(options, new AgentReference(_agentName));
            options.PreviousResponseId = response.Id;

            foreach (var approvalRequest in approvalRequests)
                options.InputItems.Add(
                    ResponseItem.CreateMcpApprovalResponseItem(approvalRequest.Id, true));
        }

        // Prefer the agent conversation ID; fall back to response ID for PreviousResponseId-based continuity
        var conversationId = ResponseResultExtensions.get_AgentConversationId(response)
            ?? response.Id;

        var outputText = response.GetOutputText();
        _logger.LogInformation("Final response: Status={Status}, ConvId={ConvId}, TextLength={Len}",
            response.Status, conversationId, outputText?.Length ?? 0);

        return Ok(new ChatResponseModel
        {
            Response = outputText,
            ConversationId = conversationId
        });
    }
}
