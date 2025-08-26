# Connected Agents Implementation Guide

## Overview

This fashion store demo uses Azure AI Foundry's **Connected Agents** pattern for seamless multi-agent collaboration without custom orchestration logic.

## Architecture: Connected Agents Pattern

### Main Components

1. **Main Orchestrator Agent** 
   - Entry point for all customer interactions
   - Uses `ConnectedAgentToolDefinition` to delegate to specialist agents
   - Automatically routes tasks using natural language understanding

2. **Connected Specialist Agents**
   - **Cart Manager**: Shopping cart operations via OpenAPI tools
   - **Inventory Manager**: Product data via MCP tools  
   - **Fashion Advisor**: Style recommendations using LLM knowledge
   - **Content Moderator**: Content safety and professional standards

### Key Benefits

✅ **No Custom Orchestration** - Main agent handles routing automatically  
✅ **Simplified Architecture** - Only interact with the main agent  
✅ **Automatic Coordination** - Connected agents work together seamlessly  
✅ **Easy Extensibility** - Add new connected agents without code changes  
✅ **Natural Language Routing** - No hardcoded delegation logic needed

## Implementation Details

### Python Setup (setup_agents.py)

The Python script creates the connected agents architecture:

```python
# Step 1: Create specialist agents with their tools
cart_agent = create_agent(name="Cart Manager", tools=[openapi_spec])
inventory_agent = create_agent(name="Inventory Manager", tools=[mcp_tools])
fashion_agent = create_agent(name="Fashion Advisor", tools=[])
moderator_agent = create_agent(name="Content Moderator", tools=[])

# Step 2: Create connected agent tools
from azure.ai.agents.models import ConnectedAgentTool

cart_connected_tool = ConnectedAgentTool(
    id=cart_agent.id,
    name="cart_manager", 
    description="Handles shopping cart operations..."
)

# Step 3: Create main agent with connected tools
main_agent = create_agent(
    name="Fashion Store Main Agent",
    tools=connected_agent_tools  # All connected agent definitions
)
```

### .NET Application (MultiAgentOrchestrationService.cs)

The .NET service is greatly simplified with connected agents:

```csharp
// Only need to interact with the main agent
var run = await _agentsClient.Runs.CreateRunAsync(threadId, _mainAgentId);

// Connected agents handle their own tools automatically
// No need for custom routing or tool resource management
```

## Configuration Requirements

### Environment Variables

**Minimal Required Configuration:**
```bash
AI_PROJECT_ENDPOINT=https://your-project.cognitiveservices.azure.com/
MAIN_ORCHESTRATOR_AGENT_ID=agent_xyz123  # From Python script output
EXTERNAL_INVENTORY_API_URL=https://your-inventory-api.com  # For MCP tools
```

**For Python Agent Setup:**
```bash
PROJECT_ENDPOINT=https://your-project.cognitiveservices.azure.com/
MODEL_DEPLOYMENT_NAME=gpt-4.1-deployment
WEBAPP_URL=https://your-webapp.azurewebsites.net
EXTERNAL_INVENTORY_API_URL=https://your-inventory-api.com
```

### Virtual Environment Setup

```bash
# Create and activate virtual environment
python3 -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install Azure AI packages
pip install azure-ai-projects azure-identity
```

## Deployment Workflow

### 1. Setup Connected Agents

```bash
# Set environment variables
export PROJECT_ENDPOINT="https://your-project.cognitiveservices.azure.com/"
export MODEL_DEPLOYMENT_NAME="gpt-4.1-deployment"
export WEBAPP_URL="https://your-webapp.azurewebsites.net"
export EXTERNAL_INVENTORY_API_URL="https://your-inventory-api.com"

# Run agent creation script
python setup_agents.py
```

### 2. Configure .NET Application

Update Azure App Service with environment variables from the Python script output:

```bash
MAIN_ORCHESTRATOR_AGENT_ID=agent_abc123  # Copy from script output
```

### 3. Deploy and Test

```bash
# Deploy the updated .NET application
azd up

# Test connected agents functionality through the main agent
```

## How Connected Agents Work

### User Interaction Flow

1. **User Message** → Main Orchestrator Agent
2. **Intelligent Routing** → Main agent determines which connected agents to involve
3. **Automatic Delegation** → Connected agents execute specialized tasks
4. **Coordination** → Connected agents can call each other if needed  
5. **Response Compilation** → Main agent provides unified response

### Example Scenarios

**"Add a red dress to my cart"**
```
Main Agent → Inventory Manager (check availability) 
          → Cart Manager (add to cart)
          → User receives: "Added red dress to cart, 3 items remaining in stock"
```

**"What's in my cart and any style suggestions?"**  
```
Main Agent → Cart Manager (get cart contents)
          → Fashion Advisor (provide styling suggestions)
          → User receives: Combined cart summary + style recommendations
```

## Tool Handling

### Connected Agent Tools

- **OpenAPI Tools**: Cart Manager handles REST API calls automatically
- **MCP Tools**: Inventory Manager handles MCP server communication automatically  
- **LLM Knowledge**: Fashion Advisor and Content Moderator use built-in capabilities

### Tool Approval

Connected agents handle tool approvals automatically. The main agent coordinates but doesn't need to manage individual tool resources.

## Monitoring and Debugging

### System Information

The `GetMultiAgentSystemInfo()` method provides:
- Connected agents configuration status
- Tool setup verification  
- Required environment variables checklist

### Logging

Key log events to monitor:
- Connected agent delegation decisions
- Tool execution by connected agents
- Coordination between connected agents
- Response compilation by main agent

## Production Considerations

### Security
- **Authentication**: Connected agents handle their own tool authentication
- **Approval**: Consider approval policies for sensitive operations
- **Access Control**: Ensure proper Azure RBAC for agent resources

### Performance  
- **Caching**: Connected agents can implement their own caching strategies
- **Timeout**: Set appropriate timeouts for connected agent operations
- **Scaling**: Connected agents scale independently based on usage

### Reliability
- **Error Handling**: Connected agents provide graceful error handling
- **Fallback**: Main agent can handle cases where connected agents are unavailable
- **Monitoring**: Track connected agent performance and availability

## Troubleshooting

### Common Issues

**"Connected agent not responding"**
- Check if specialist agent IDs are correct
- Verify connected agent tool definitions
- Ensure specialist agents are properly created

**"Tool calls not working"**  
- Verify MCP server accessibility for Inventory Manager
- Check OpenAPI spec validity for Cart Manager
- Confirm tool permissions and authentication

**"Delegation not happening"**
- Review main agent instructions for routing guidance
- Check connected agent descriptions for clarity
- Verify connected agent tool definitions are properly set

### Debug Steps

1. **Check Configuration**: Use `GetMultiAgentSystemInfo()` 
2. **Test Individual Agents**: Verify each connected agent works independently
3. **Check Logs**: Review delegation and coordination logs
4. **Validate Tools**: Ensure OpenAPI and MCP tools are accessible

## Migration from Legacy Multi-Agent

If migrating from the legacy multi-agent approach:

1. **Update Python Script**: Use `ConnectedAgentTool` instead of individual agent management
2. **Simplify .NET Code**: Remove custom orchestration logic  
3. **Update Configuration**: Only need main agent ID in .NET app
4. **Test Delegation**: Verify automatic routing works as expected

The connected agents pattern significantly simplifies the architecture while providing more robust multi-agent coordination.