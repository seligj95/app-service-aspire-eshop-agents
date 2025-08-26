# MCP Runtime Configuration Implementation

## Overview

Updated the .NET application to properly handle Model Context Protocol (MCP) runtime configuration as required by Azure AI Foundry agents with MCP tools.

## Changes Made

### 1. Configuration Updates

**New Environment Variables:**
- `MAIN_ORCHESTRATOR_AGENT_ID` - Main agent that coordinates all interactions
- `INVENTORY_MANAGER_AGENT_ID` - Agent with MCP tools for inventory operations  
- `EXTERNAL_INVENTORY_API_URL` - Base URL for the external inventory API

### 2. MCP Tool Resources Configuration

**Added Methods:**
- `CreateMcpToolResources()` - Creates runtime MCP configuration
- `RequiresMcpToolResources()` - Determines if agent needs MCP tools

**Runtime Configuration:**
```csharp
var mcpToolResource = new MCPToolResource("inventory-mcp");
// Headers can be added for authentication:
// mcpToolResource.UpdateHeader("Authorization", "Bearer " + token);
var toolResources = mcpToolResource.ToToolResources();
```

### 3. Enhanced Tool Approval Handling

**Automatic Approval System:**
- Auto-approves MCP tool calls for demo purposes
- Handles both MCP and function tool calls
- Provides detailed logging for troubleshooting

**Tool Call Processing:**
```csharp
if (toolCall is RequiredMcpToolCall mcpToolCall)
{
    // Auto-approve MCP calls
    toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
}
```

### 4. Updated Agent Architecture

**5-Agent System:**
1. **Main Orchestrator** - Entry point, coordinates all interactions
2. **Cart Manager** - OpenAPI tools for cart operations
3. **Inventory Manager** - MCP tools for product data (requires runtime config)
4. **Fashion Advisor** - Style recommendations
5. **Content Moderator** - Content safety

## Technical Implementation

### MCP Runtime Flow

1. **Agent Creation** (Python script):
   ```python
   "tools": [
       {
           "type": "mcp",
           "server_label": "inventory-mcp", 
           "server_url": mcp_server_url,
           "allowed_tools": []
       }
   ]
   ```

2. **Runtime Configuration** (.NET app):
   ```csharp
   // Check if agent needs MCP tools
   if (RequiresMcpToolResources(_mainAgentId))
   {
       toolResources = CreateMcpToolResources();
   }
   
   // Create run with tool resources
   var run = await _agentsClient.Runs.CreateRunAsync(threadId, _mainAgentId);
   ```

3. **Tool Approval** (Automatic):
   ```csharp
   // Handle MCP tool approval requests
   if (run.Status == RunStatus.RequiresAction)
   {
       // Auto-approve for demo, validate in production
       toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
   }
   ```

## Configuration Requirements

### Environment Variables Needed

**Required for Basic Operation:**
- `AI_PROJECT_ENDPOINT` - Azure AI Foundry project endpoint
- `MAIN_ORCHESTRATOR_AGENT_ID` - From Python setup script output

**Required for MCP Tools:**
- `INVENTORY_MANAGER_AGENT_ID` - From Python setup script output
- `EXTERNAL_INVENTORY_API_URL` - Your inventory MCP server base URL

**Additional Agent IDs:**
- `CART_MANAGER_AGENT_ID`
- `FASHION_ADVISOR_AGENT_ID` 
- `CONTENT_MODERATOR_AGENT_ID`

### MCP Server Configuration

**Server Label:** `inventory-mcp` (must match between agent creation and runtime)
**Server URL:** `{EXTERNAL_INVENTORY_API_URL}/mcp`
**Approval Mode:** Automatic approval for demo (configurable)

## Production Considerations

### Security
- **Authentication**: Add proper headers via `mcpToolResource.UpdateHeader()`
- **Approval**: Consider manual approval for sensitive operations
- **Validation**: Add business logic validation before approving tool calls

### Error Handling
- **Connection failures**: Graceful degradation when MCP server unavailable
- **Timeout handling**: Appropriate timeouts for MCP operations
- **Retry logic**: Implement retry for transient failures

### Monitoring
- **Tool call logging**: Track MCP tool usage and performance
- **Approval audit**: Log all approval decisions for compliance
- **Performance metrics**: Monitor MCP server response times

## Testing

### Verification Steps
1. **Environment Variables**: Ensure all required variables are set
2. **Agent Creation**: Run Python script to create agents with MCP tools
3. **MCP Connectivity**: Test that inventory API `/mcp` endpoint is accessible
4. **Tool Approval**: Verify automatic approval workflow functions
5. **End-to-End**: Test inventory queries through the main agent

### Debug Information
The `GetMultiAgentSystemInfo()` method provides comprehensive configuration details for troubleshooting:
- Agent IDs and configuration status
- MCP server settings and connectivity
- Required environment variables checklist

## Next Steps

1. **Deploy Updated Code**: Deploy the .NET application with MCP runtime support
2. **Configure Environment**: Set all required environment variables
3. **Test MCP Integration**: Verify inventory queries work through agents
4. **Monitor Performance**: Watch for MCP tool call patterns and performance
5. **Production Hardening**: Add authentication and validation as needed