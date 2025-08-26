# Azure AI Foundry Agent Setup

This document describes how to set up persistent Azure AI Foundry agents for the Fashion Store demo.

## Overview

The `setup_agents.py` script creates a comprehensive 5-agent architecture:
### Troubleshooting

#### Common Issues

1. **Import errors**: Ensure all dependencies are installed via `pip install -r requirements.txt`
2. **Authentication errors**: Ensure you're logged into Azure CLI: `az login`
3. **Model not found**: Verify MODEL_DEPLOYMENT_NAME matches the deployment in main.bicep
4. **swagger.json not found**: Ensure the file exists in the root directory
5. **MCP tool failures**: Check that tool_resources are provided with correct server_label when creating runs
6. **MCP authentication issues**: Verify headers are correctly passed in tool_resources at runtime

#### MCP-Specific Troubleshooting

- **"requires_action" status**: When MCP tools require approval, handle the approval workflow
- **Authentication failures**: Ensure proper headers are passed in tool_resources, not during agent creation
- **Tool not found**: Verify the server_label matches between agent creation and run tool_resources
- **Connection timeouts**: Check that the MCP server URL is accessible and respondingin Orchestrator** - Coordinates all customer interactions and delegates to specialist agents
2. **Cart Manager** - Manages shopping cart operations via OpenAPI tools
3. **Inventory Manager** - Handles product availability and details via MCP tools
4. **Fashion Advisor** - Provides fashion advice and style recommendations
5. **Content Moderator** - Ensures safe, professional interactions

## Architecture

```
┌─────────────────────┐
│  Main Orchestrator  │ ← Entry point for all customer interactions
└─────────┬───────────┘
          │ Coordinates with:
          ├─── Cart Manager (OpenAPI)
          ├─── Inventory Manager (MCP)
          ├─── Fashion Advisor
          └─── Content Moderator
```

### Workflow Example:
1. Customer: "I want to buy a red dress"
2. Main Orchestrator → Fashion Advisor: "Recommend red dresses"
3. Main Orchestrator → Inventory Manager: "Check availability of recommended items"
4. Main Orchestrator → Cart Manager: "Add available item to cart"
5. Main Orchestrator: Provides coordinated response to customer

## Key Features

- Uses **gpt-4.1** model (as defined in main.bicep)
- Cart agent has OpenAPI tool integration with live swagger.json
- Automatically patches swagger.json with correct webapp URL
- Outputs environment variables for Azure App Service configuration

## Prerequisites

1. **Azure AI Foundry project** deployed via bicep
2. **Azure App Service** deployed and running
3. **Python environment** with required packages

## Installation

Install the required Python packages:

```bash
pip install -r requirements.txt
```

## Environment Variables

Set these environment variables before running the script:

```bash
export PROJECT_ENDPOINT="https://your-ai-foundry-project.eastus2.api.azureml.ms"
export MODEL_DEPLOYMENT_NAME="gpt-4.1"  # Must match main.bicep
export WEBAPP_URL="https://your-fashion-store.azurewebsites.net"
export EXTERNAL_INVENTORY_API_URL="https://your-inventory-api.azurewebsites.net"
```

**Note**: The script automatically appends `/mcp` to `EXTERNAL_INVENTORY_API_URL` to create the MCP server endpoint.

## Usage

Run the agent setup script:

```bash
python setup_agents.py
```

## Output

The script will:

1. Load and patch `swagger.json` with the correct webapp URL
2. Create 5 agents using the gpt-4.1 model with specialized roles and tools
3. Output agent IDs for configuration
4. Provide Azure App Service environment variable commands

Example output:
```
✓ Loaded and patched swagger.json with server URL: https://your-app.azurewebsites.net
MCP server URL: https://your-inventory-api.azurewebsites.net/mcp

Creating agent: Cart Manager Agent
✓ Created agent 'Cart Manager Agent' with ID: asst_cart123

Creating agent: Inventory Manager Agent
✓ Created agent 'Inventory Manager Agent' with ID: asst_inv456

Creating agent: Fashion Advisor Agent
✓ Created agent 'Fashion Advisor Agent' with ID: asst_fashion789

Creating agent: Content Moderator Agent
✓ Created agent 'Content Moderator Agent' with ID: asst_mod012

Creating agent: Fashion Store Main Agent
✓ Created agent 'Fashion Store Main Agent' with ID: asst_main345

============================================================
FASHION STORE AGENT CREATION COMPLETED
============================================================

Agent IDs created:
  Main Orchestrator: asst_main345
  Cart Manager: asst_cart123
  Inventory Manager: asst_inv456
  Fashion Advisor: asst_fashion789
  Content Moderator: asst_mod012

Set these environment variables in your Azure App Service:
  MAIN_ORCHESTRATOR_AGENT_ID=asst_main345
  CART_MANAGER_AGENT_ID=asst_cart123
  INVENTORY_MANAGER_AGENT_ID=asst_inv456
  FASHION_ADVISOR_AGENT_ID=asst_fashion789
  CONTENT_MODERATOR_AGENT_ID=asst_mod012
```

## Configuration

After running the script, configure your Azure App Service with the generated environment variables:

```bash
# Using Azure CLI
az webapp config appsettings set \
  --resource-group your-rg \
  --name your-app-service \
  --settings \
    MAIN_ORCHESTRATOR_AGENT_ID=asst_main345 \
    CART_MANAGER_AGENT_ID=asst_cart123 \
    INVENTORY_MANAGER_AGENT_ID=asst_inv456 \
    FASHION_ADVISOR_AGENT_ID=asst_fashion789 \
    CONTENT_MODERATOR_AGENT_ID=asst_mod012
```

## Architecture Details

### Main Orchestrator Agent
- **Model**: gpt-4.1 (from main.bicep)
- **Tools**: None (coordinates other agents)
- **Purpose**: Entry point for all customer interactions, delegates to specialist agents
- **Role**: Orchestrates workflow between specialist agents for comprehensive responses

### Cart Manager Agent
- **Model**: gpt-4.1 (from main.bicep)  
- **Tools**: OpenAPI (patched swagger.json)
- **Purpose**: Shopping cart operations
- **Workflow**: Must check with Inventory Manager before adding items
- **Endpoints**: 
  - `GET /api/Cart` - View cart
  - `POST /api/Cart/add` - Add items
  - `PUT /api/Cart/{productId}/size/{size}` - Update quantity
  - `DELETE /api/Cart/{productId}/size/{size}` - Remove items
  - `DELETE /api/Cart` - Clear cart

### Inventory Manager Agent
- **Model**: gpt-4.1 (from main.bicep)
- **Tools**: MCP (connects to external inventory system)
- **Purpose**: Product availability, stock levels, product details
- **MCP Endpoint**: `{EXTERNAL_INVENTORY_API_URL}/mcp`
- **Role**: Provides real-time inventory data to other agents

#### ⚠️ **MCP Runtime Configuration Required**

The Inventory Manager agent uses MCP tools which require **runtime configuration** when creating runs. You MUST provide:

1. **Tool Resources** with server label: `inventory-mcp`
2. **Authentication headers** (if required by your MCP server)  
3. **Approval settings** for security

**Example runtime configuration:**
```python
# When creating runs with the Inventory Manager agent
tool_resources = {
    'mcp': [{
        'server_label': 'inventory-mcp',
        'headers': {'Authorization': 'Bearer YOUR_TOKEN'},  # If authentication needed
        'require_approval': 'never'  # or 'always' for security
    }]
}

run = agents_client.runs.create(
    thread_id=thread_id,
    agent_id=inventory_manager_agent_id,
    tool_resources=tool_resources
)
```

**Approval Options:**
- `'always'` - Developer approval required for every MCP tool call (default, most secure)
- `'never'` - No approval required (fastest, less secure)
- `{'never': ['tool1', 'tool2']}` - Specific tools don't require approval
- `{'always': ['tool1', 'tool2']}` - Specific tools require approval

**Helper Function Available:**
The setup script includes a helper function `create_mcp_tool_resources()` for easy configuration.

### Fashion Advisor Agent
- **Model**: gpt-4.1 (from main.bicep)
- **Tools**: None (uses LLM knowledge)
- **Purpose**: Style recommendations, fashion advice, outfit coordination
- **Expertise**: Trends, styling, color coordination, occasion-appropriate recommendations

### Content Moderator Agent
- **Model**: gpt-4.1 (from main.bicep)
- **Tools**: None (uses LLM knowledge)
- **Purpose**: Content review, safety, professional standards
- **Role**: Ensures all interactions are appropriate and maintain retail standards

## Files

- `setup_agents.py` - Main setup script
- `swagger.json` - OpenAPI specification (auto-patched)
- `requirements.txt` - Python dependencies
- `AGENT_SETUP.md` - This documentation

## Troubleshooting

### Common Issues

1. **Import errors**: Ensure all dependencies are installed via `pip install -r requirements.txt`
2. **Authentication errors**: Ensure you're logged into Azure CLI: `az login`
3. **Model not found**: Verify MODEL_DEPLOYMENT_NAME matches the deployment in main.bicep
4. **swagger.json not found**: Ensure the file exists in the root directory

### Verification

Test agent creation by checking the Azure AI Foundry portal:
1. Navigate to your AI Foundry project
2. Go to "Agents" section
3. Verify both agents are listed with correct models and tools

## Next Steps

After successful agent setup:

1. Deploy your application: `azd up`
2. Verify agents work via the web interface
3. Monitor agent performance in Azure AI Foundry portal