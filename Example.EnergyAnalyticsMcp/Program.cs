using Azure.AI.Projects;
using Azure.Identity;
using Example.EnergyAnalyticsMcp.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EnergyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EnergyDb")));

var aiEndpoint = builder.Configuration["AzureAI:Endpoint"]
    ?? "https://foundry-pseg-main-eus2-mx01.services.ai.azure.com/api/projects/energy-chat-project";

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(endpoint: new Uri(aiEndpoint), tokenProvider: credential);

builder.Services.AddSingleton(projectClient);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp();

app.Run();
