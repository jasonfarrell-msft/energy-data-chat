using Azure.AI.Projects;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var endpoint = builder.Configuration["AzureAI:Endpoint"]
    ?? "https://foundry-pseg-main-eus2-mx01.services.ai.azure.com/api/projects/energy-chat-project";

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(endpoint: new Uri(endpoint), tokenProvider: credential);

builder.Services.AddSingleton(projectClient);

var app = builder.Build();

app.MapControllers();

app.Run();
