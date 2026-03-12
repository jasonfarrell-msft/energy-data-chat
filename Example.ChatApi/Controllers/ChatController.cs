using Example.ChatApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Example.ChatApi.Controllers;

[ApiController]
[Route("[controller]")]
public class ChatController : ControllerBase
{
    [HttpPost]
    public IActionResult Chat([FromBody] ChatRequestModel request)
    {
        return Ok(request.ChatMessage);
    }
}
