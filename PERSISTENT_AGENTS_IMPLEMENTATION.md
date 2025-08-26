# Persistent Agent Architecture Implementation

## Summary

Successfully implemented a persistent agent architecture for the Fashion Store demo using Azure AI Foundry. This replaces the previous per-request agent creation pattern with a more stable, demo-friendly approach.

## Key Changes Made

### 1. Python Agent Setup Script (`setup_agents.py`)
- **Purpose**: Creates persistent agents once after deployment
- **Architecture**: 1 main agent with 4 connected agents
- **Connected Agents**:
  - **Cart Manager**: OpenAPI tool for cart operations (`swagger.json`)
  - **Inventory Manager**: MCP tool for inventory queries (external MCP endpoint)
  - **Fashion Advisor**: Style recommendations (no tools)
  - **Content Moderator**: Content review (no tools)

### 2. Simplified .NET Orchestration Service
- **Before**: Created agents per request, complex factory pattern
- **After**: Uses single persistent main agent ID, manages threads only
- **Key Benefits**:
  - No agent creation overhead
  - Stable agent references
  - Thread-based conversation persistence
  - Per-user thread management

### 3. Proper Tool Separation
- **Cart Operations**: Via OpenAPI tool (local swagger.json)
- **Inventory Queries**: Via MCP tool (external inventory API)
- **Delegation Logic**: Cart agent knows to check with inventory agent before adding items

### 4. Configuration Updates
- **No appsettings.json needed**: Configuration handled via Azure App Service settings only
- **App Settings Set by Python Script**:
  - `AZURE_AI_FOUNDRY_MAIN_AGENT_ID`: Set by Python script
  - `AZURE_AI_FOUNDRY_PROJECT_NAME`: AI Foundry project name
  - `AI_PROJECT_ENDPOINT`: Azure AI Foundry endpoint
  - `EXTERNAL_INVENTORY_API_URL`: Inventory API URL (for reference)
- **Configuration Pattern**: Environment variables → Azure App Service settings → No local files needed

## Deployment Workflow

### Step 1: Deploy Infrastructure
```bash
azd deploy
```

### Step 2: Run Agent Setup Script
```bash
# Install Python dependencies
pip install -r requirements.txt

# Authenticate with Azure
az login

# Create persistent agents
python setup_agents.py \
  --main-app-url "https://your-fashion-store.azurewebsites.net" \
  --inventory-mcp-url "https://your-inventory-api.azurewebsites.net/mcp" \
  --subscription-id "your-azure-subscription-id" \
  --resource-group "your-resource-group-name" \
  --app-service-name "your-app-service-name" \
  --ai-foundry-project "your-ai-foundry-project-name"
```

### Step 3: Restart App Service
After the Python script updates app settings, restart the App Service to pick up the new configuration.

## Agent Workflow

### User Interaction Flow
```
User: "Add a red dress to my cart"
    ↓
Main Agent receives request
    ↓
Delegates to Cart Manager: "Add red dress to cart"
    ↓
Cart Manager → Inventory Manager: "Check red dress availability"
    ↓
Inventory Manager → MCP Tool: Query external inventory API
    ↓
Inventory Manager → Cart Manager: "Red dress available, here are details"
    ↓
Cart Manager → OpenAPI Tool: Add item to cart via swagger.json
    ↓
Response to user: "Added red dress to your cart"
```

## Benefits of Persistent Agent Architecture

1. **Stability**: No real-time agent creation complexity
2. **Performance**: Agents are pre-created and optimized
3. **Scalability**: Multiple users share same persistent agents
4. **Demo-Ready**: Reliable and predictable behavior
5. **Debugging**: Easier to troubleshoot persistent agent issues
6. **Thread Management**: Per-user conversation persistence

## Troubleshooting

### Common Issues
1. **Agent Creation Fails**: Check AI Foundry project permissions and connectivity
2. **MCP Connection Issues**: Verify inventory MCP endpoint is accessible
3. **App Settings Not Updated**: Check Azure CLI authentication and App Service permissions
4. **Configuration Errors**: Ensure all required settings are present

### Validation Steps
1. Check app settings in Azure portal
2. Test cart operations (should work via OpenAPI)
3. Test inventory queries (should work via MCP)
4. Monitor application logs for agent communication

## Files Changed

### New Files
- `setup_agents.py` - Python agent creation script
- `requirements.txt` - Python dependencies
- `SETUP_AGENTS.md` - Setup documentation

### Modified Files
- `src/webapp/Services/MultiAgentOrchestrationService.cs` - Simplified for persistent agents
- `src/webapp/swagger.json` - Removed inventory endpoints, kept cart endpoints

### Removed Files
- `src/webapp/Controllers/InventoryController.cs` - Inventory now via MCP only
- `src/webapp/appsettings.json` - Not needed, using Azure App Service settings
- `src/webapp/appsettings.Development.json` - Not needed, using Azure App Service settings

## Next Steps

1. Test the complete workflow with actual inventory MCP endpoint
2. Validate agent delegation and tool usage
3. Monitor performance and stability
4. Consider additional connected agents for future features

This persistent agent architecture provides a much more stable foundation for the fashion store demo while maintaining the multi-agent capabilities and tool integration.