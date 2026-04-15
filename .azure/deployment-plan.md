# PSEG Energy Chat - Deployment Plan

**Status:** Ready for Validation

## Overview

FusionSun (PSEG Energy Analytics Chat) deployment to Azure using Azure Developer CLI (azd).

## Architecture

| Component | Azure Service | Description |
|-----------|---------------|-------------|
| Chat API | Container App | .NET 8 API with Azure AI Foundry integration |
| MCP Server | Container App | .NET 8 MCP server with EF Core + SQL Server |
| Frontend | App Service | React + Vite SPA |
| Database | SQL Server | Energy data storage |
| AI | AI Foundry | GPT model for natural language queries |
| Observability | Application Insights | Logging and telemetry |
| Registry | Container Registry | Docker image storage |

## Services Mapping

```
azure.yaml services → Bicep modules:
├── chat-api      → containerApp.bicep
├── mcp-server    → containerAppMcp.bicep  
└── frontend      → appService.bicep
```

## Infrastructure Files

- `infra/main.bicep` - Main orchestration
- `infra/main.bicepparam` - Parameters (requires secrets)
- `infra/modules/` - Individual resource modules

## Required Parameters

| Parameter | Description | Source |
|-----------|-------------|--------|
| `suffix` | 4-char resource suffix | Default: `mx01` |
| `sqlAdminLogin` | SQL admin username | User input |
| `sqlAdminPassword` | SQL admin password | User input (secure) |
| `containerImageName` | Chat API image tag | azd build output |
| `mcpContainerImageName` | MCP server image tag | azd build output |
| `foundryProjectName` | AI Foundry project | User input |

## Deployment Commands

```bash
# Initialize environment (first time)
azd init

# Provision infrastructure + deploy
azd up

# Deploy code only (after infra exists)
azd deploy

# View deployed resources
azd show
```

## Prerequisites

1. Azure CLI authenticated (`az login`)
2. Azure Developer CLI installed (`azd version`)
3. Docker running (for container builds)
4. SQL admin credentials ready
5. AI Foundry project name decided

## Validation Checklist

- [x] Bicep modules exist and are complete
- [x] Dockerfiles exist for both .NET services
- [x] azure.yaml created with service mappings
- [ ] Environment variables configured in `.azure/`
- [ ] `azd up` tested
