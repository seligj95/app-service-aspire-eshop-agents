# Fashion Store Agent Setup

This directory contains the Python setup script for creating persistent Azure AI Foundry agents for the fashion store demo.

## Prerequisites

1. **Python 3.8+** installed
2. **Azure CLI** installed and authenticated
3. **Azure AI Foundry project** created
4. **Fashion store app** deployed to Azure App Service
5. **Inventory API** deployed with MCP endpoint

## Installation

1. Install Python dependencies:
```bash
pip install -r requirements.txt
```

2. Authenticate with Azure:
```bash
az login
```

## Usage

Run the setup script after deploying both the fashion store and inventory apps:

```bash
python setup_agents.py \
  --main-app-url "https://your-fashion-store.azurewebsites.net" \
  --inventory-mcp-url "https://your-inventory-api.azurewebsites.net/mcp" \
  --subscription-id "your-azure-subscription-id" \
  --resource-group "your-resource-group-name" \
  --app-service-name "your-app-service-name" \
  --ai-foundry-project "your-ai-foundry-project-name"
```

### Parameters

- `--main-app-url`: The URL of your deployed fashion store app
- `--inventory-mcp-url`: The MCP endpoint URL of your inventory API  
- `--subscription-id`: Your Azure subscription ID
- `--resource-group`: The resource group containing your App Service
- `--app-service-name`: The name of your fashion store App Service
- `--ai-foundry-project`: The name of your Azure AI Foundry project

## What the Script Does

1. **Creates Main Agent**: A fashion store orchestrator with 4 connected agents
2. **Creates Connected Agents**:
   - **Cart Manager**: Uses OpenAPI tool to manage shopping cart
   - **Inventory Manager**: Uses MCP tool to query inventory
   - **Fashion Advisor**: Provides style recommendations  
   - **Content Moderator**: Reviews user content
3. **Updates App Settings**: Adds agent IDs to your App Service configuration
4. **Validates Setup**: Tests agent creation and connectivity

## Agent Architecture

```
Main Fashion Store Agent
├── Cart Manager (OpenAPI: /swagger.json)
├── Inventory Manager (MCP: /mcp endpoint)
├── Fashion Advisor (no tools)
└── Content Moderator (no tools)
```

## Expected App Settings

After successful setup, these settings will be added to your App Service:

- `AZURE_AI_FOUNDRY_MAIN_AGENT_ID`: The main orchestrator agent ID
- `AZURE_AI_FOUNDRY_PROJECT_NAME`: Your AI Foundry project name

## Post-Setup

1. **Restart App Service**: Ensure new settings are picked up
2. **Test the App**: Verify cart and inventory operations work
3. **Monitor Logs**: Check for any agent communication issues

## Troubleshooting

- **Authentication Issues**: Ensure `az login` is working and you have proper permissions
- **Agent Creation Fails**: Verify AI Foundry project exists and is accessible
- **MCP Connection Issues**: Confirm inventory MCP endpoint is accessible
- **App Settings Update Fails**: Check App Service permissions and resource group access