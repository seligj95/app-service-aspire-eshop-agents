# Local Development Setup

## Prerequisites

1. .NET 9.0 SDK
2. Azure AI Foundry project with deployed GPT-4o model
3. External inventory API (or use the provided sample endpoint)

## Environment Configuration

### Step 1: Configure Environment Variables

The application requires the following environment variables. Copy the example file and fill in your values:

```bash
# Copy the example file
cp .env.example .env
```

Edit the `.env` file with your actual values:

```bash
# Azure AI Foundry Configuration
AI_PROJECT_ENDPOINT=https://your-project-name.eastus.api.azureml.ms
AI_MODEL_DEPLOYMENT_NAME=gpt-4o

# External Inventory API Configuration
EXTERNAL_INVENTORY_API_URL=https://your-inventory-api.azurewebsites.net
```

**Note**: The `.env` file is automatically loaded by the AppHost when running locally. No need for appsettings.json files!

### Step 2: Get Required Configuration Values

#### Azure AI Foundry Project Endpoint

1. Go to [Azure AI Foundry](https://ai.azure.com)
2. Open your project
3. Navigate to Project Settings → General
4. Copy the "Project Endpoint" value

#### Model Deployment Name

1. In your Azure AI Foundry project
2. Go to Deployments section
3. Use the deployment name of your GPT-4o model (typically "gpt-4o")

#### External Inventory API URL

This should point to your external inventory service that provides:
- `GET /api/inventory` - Returns inventory items
- `GET /mcp` - MCP server endpoint for the inventory agent

For testing, you can use a sample endpoint or set up your own inventory API.

## Running the Application

### Using .NET Aspire (Recommended)

```bash
# From the solution root
cd ai-agent-openai-web-app
dotnet run --project src/ai-agent-openai-web-app.AppHost/ai-agent-openai-web-app.AppHost.csproj
```

This will:
- Start the Aspire dashboard at https://localhost:17071
- Start the webapp at the URL shown in the dashboard
- Automatically load environment variables from the .env file

### Running the webapp directly

```bash
# From the webapp directory
cd ai-agent-openai-web-app/src/webapp
dotnet run
```

## Application Features

### Multi-Agent System

The application uses 4 specialized AI agents:

1. **Orchestrator Agent** - Coordinates all other agents
2. **Cart Manager Agent** - Handles shopping cart operations via OpenAPI
3. **Fashion Advisor Agent** - Provides fashion advice and recommendations
4. **Content Moderator Agent** - Validates content appropriateness
5. **MCP Inventory Agent** - Manages inventory using Model Context Protocol

### API Endpoints

- `/api/Cart` - Shopping cart operations
- `/api/docs` - Swagger UI documentation
- `/swagger/v1/swagger.json` - OpenAPI specification

## Troubleshooting

### Common Issues

1. **"Multi-Agent configuration failed: AI_PROJECT_ENDPOINT is missing"**
   - Ensure AI_PROJECT_ENDPOINT is set correctly
   - Check that the endpoint URL is valid and accessible

2. **"Error fetching external inventory"**
   - Verify EXTERNAL_INVENTORY_API_URL is correct
   - Ensure the external API is running and accessible
   - Check that the API returns the expected JSON format

3. **"MCP agent not working"**
   - Verify the external inventory API has a `/mcp` endpoint
   - Check that the MCP server is responding correctly

### Debug Logging

Enable debug logging by setting:

```json
{
  "Logging": {
    "LogLevel": {
      "dotnetfashionassistant": "Debug"
    }
  }
}
```

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Blazor UI     │    │  Multi-Agent    │    │  External APIs  │
│                 │    │  Orchestration  │    │                 │
│ • Shopping Cart │◄──►│                 │◄──►│ • Inventory API │
│ • Inventory     │    │ • Orchestrator  │    │ • MCP Server    │
│ • Chat          │    │ • Cart Manager  │    │                 │
│                 │    │ • Fashion Adv.  │    │                 │
└─────────────────┘    │ • Content Mod.  │    └─────────────────┘
                       │ • MCP Inventory │
                       └─────────────────┘
                               │
                       ┌─────────────────┐
                       │  Azure AI       │
                       │  Foundry        │
                       │                 │
                       │ • GPT-4o Model  │
                       │ • Agent Runtime │
                       └─────────────────┘
```