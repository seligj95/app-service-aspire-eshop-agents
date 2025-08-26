# Fashion Assistant with .NET Aspire and Azure AI Foundry Multi-Agent Architecture

This sample demonstrates a modern cloud-native fashion e-commerce application built with [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) and Azure AI Foundry's multi-agent architecture. The application showcases how to create intelligent shopping experiences using connected agents, the MCP (Model Context Protocol) tool, and the OpenAPI specified tool.

## .NET Aspire on Azure App Service

This project leverages .NET Aspire for enhanced development experience and Azure App Service for production deployment. As outlined in the [.NET Aspire on App Service blog post](https://azure.github.io/AppService/2025/05/19/Aspire-on-App-Service.html), .NET Aspire on App Service is currently in **preview** with some limitations. This sample works within those constraints to provide a sample app with a multi-agent architecture.

## Prerequisites

Before you begin, ensure you have:
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Developer CLI (azd)](https://aka.ms/azd)
- [Python 3.8+](https://www.python.org/downloads/) for the agent setup script
- Azure subscription with access to Azure AI Foundry
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)

## Step 1: Deploy the Inventory Service

First, deploy the external inventory service that provides product data via MCP (Model Context Protocol). The clothing store inventory is hosted in a separate App Service. The main app will connect to the inventory to populate the counts of the clothing items in the main app. And the Foundry agent will connect to the inventory using MCP so that it can look up inventory and product availablity while you chat with it. You should put the inventory in it's own resource group, which will be set-up when you run `azd up`.

```bash
git clone https://github.com/seligj95/app-service-python-mcp-inventory.git
cd app-service-python-mcp-inventory
azd up
```

**📝 Important:** Note the App Service URL from the deployment (e.g., `https://app-web-xyz123.azurewebsites.net`). You'll need this URL in the following steps.

This inventory service provides:
- REST API endpoints for product data
- MCP server endpoint for dynamic agent access
- Sample fashion inventory with sizes, prices, and stock levels

## Step 2: Deploy the Main Application

Clone and deploy this fashion assistant application:

```bash
git clone https://github.com/seligj95/app-service-aspire-eshop-agents.git
cd app-service-aspire-eshop-agents
azd up
```

This will provision:
- Azure App Service for the web application
- Azure AI Foundry project and resources
- All necessary infrastructure via Bicep templates

## Step 3: Local Development (Optional)

You can run the application locally to see the interface and test basic functionality:

### Setup Environment Variables

Create a `.env` file in the root directory:

```env
EXTERNAL_INVENTORY_URL=https://your-inventory-app.azurewebsites.net
```

Replace with your actual inventory service URL from Step 1.

### Run Locally

```bash
cd src/ai-agent-openai-web-app.AppHost
dotnet run
```

This will:
- Launch the **Aspire Dashboard** (typically at `https://localhost:17071`)
- Start the web application with enhanced observability
- Provide real-time monitoring and logging
- Enable hot reload for development

**⚠️ Important Notes for Local Development:**
- The **chat functionality will NOT work locally** because your local machine doesn't have access to Azure AI Foundry like the deployed App Service has using managed identity
- Local development is primarily for:
  - Seeing the application UI and structure
  - Testing basic navigation and components
  - Viewing the Aspire dashboard and telemetry since the Aspire dashboard is not supported on App Service yet
  - Making development changes with hot reload

**The chat and AI agents will only work after deploying to Azure and completing the setup steps below.**

## Step 4: Configure Inventory URL in App Service

After deployment, configure the inventory service URL in your fashion store App Service:

1. Go to **Azure Portal** → Your App Service
2. Navigate to **Configuration** → **Application settings**
3. Add or update the setting:
   - **Name:** `EXTERNAL_INVENTORY_URL`
   - **Value:** `https://your-inventory-app.azurewebsites.net` (from Step 1)
4. Click **Save** and **Continue** when prompted
5. Restart the App Service

## Step 5: Create AI Agents with Python Script

Now create the multi-agent architecture using the included Python setup script:

### Why Use This Script?

We use a Python script to create **persistent agents** for demo purposes, allowing you to:
- See all agents in the Azure AI Foundry portal
- Understand the multi-agent architecture visually
- Modify agent instructions and tools easily
- Debug agent interactions in Foundry

We are using a Python script because the Python SDK is the most mature and the Azure Foundry portal does not support creating agents with the MCP tool yet. Once the portal supports MCP tool creation, we will provide guidance for portal setup as well.

**Note:** In production, you might prefer **dynamic agents** that are created on-demand, but persistent agents are perfect for demos and development.

### Agent Architecture

This script creates a multi-agent system with the following architecture:

```
┌─────────────────────────────────────────────────────────────┐
│                    Fashion Store Orchestrator               │
│                     (Main Coordinator)                     │
│              • Connected to specialist agents              │
│              • Has MCP Tool for inventory access           │
└─────────────────┬───────────────────────────────────────────┘
                  │
        ┌─────────┼─────────┐
        │         │         │
        ▼         ▼         ▼
┌─────────────┐ ┌──────────────┐ ┌─────────────────┐
│   Cart      │ │   Fashion    │ │   Content       │
│  Manager    │ │   Advisor    │ │  Moderator      │
│             │ │              │ │                 │
│ • Add items │ │ • Style tips │ │ • Safety checks │
│ • Remove    │ │ • Outfit     │ │ • Topic         │
│   items     │ │   advice     │ │   validation    │
│ • View cart │ │ • Trends     │ │                 │
└─────────────┘ └──────────────┘ └─────────────────┘
        │                                    
        │ OpenAPI Tool                       
        ▼                                    
┌─────────────────┐     ┌──────────────────┐
│   App Service   │◄────┤ Main Orchestrator│
│   Cart API      │     │    MCP Tool      │
│                 │     │                  │
│ • Shopping cart │     │ • Real-time      │
│   operations    │     │   inventory      │
│ • Product data  │     │ • Stock levels   │
└─────────────────┘     │ • Product search │
                        └──────────────────┘
                                │
                                │ MCP Tool
                                ▼
                        ┌──────────────────┐
                        │  Inventory API   │
                        │   (External)     │
                        │                  │
                        │ • Product data   │
                        │ • Stock levels   │
                        │ • Pricing info   │
                        └──────────────────┘
```

### Agent Types and Tools

**1. Main Orchestrator Agent:**
- **Purpose:** Coordinates all other agents and handles user interactions
- **Tools:** 
  - Connected Agent Tools (connects to all specialist agents)
  - [MCP Tool](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/model-context-protocol#setup) for direct inventory access
- **Actions:** Inventory queries, workflow coordination, agent delegation

**2. Cart Manager Agent:**
- **Purpose:** Handles shopping cart operations
- **Tools:** [OpenAPI Tool](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/openapi-spec) connecting to App Service cart endpoints
- **Actions:** Add/remove items, view cart, update quantities

**3. Fashion Advisor Agent:**
- **Purpose:** Provides styling advice and fashion recommendations
- **Tools:** None (uses AI knowledge only)
- **Actions:** Style tips, outfit coordination, fashion trends

**4. Content Moderator Agent:**
- **Purpose:** Ensures conversations stay appropriate and on-topic
- **Tools:** None (uses AI reasoning)
- **Actions:** Content filtering, topic validation

### Connected Agents Architecture

This sample uses [Connected Agents](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/connected-agents?pivots=csharp) to create a seamless multi-agent experience where the main orchestrator automatically delegates tasks to specialist agents.

**MCP Tool Implementation:**
The MCP tool is attached directly to the **main orchestrator agent** because:
- MCP tool requires [runtime parameters](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/model-context-protocol#setup) that can only be passed when initiating a run (sending a message to the agent). Because we're using persistent agents, the app is only connected to the main agent and not the connected agents. Therefore, we can't pass the runtime parameters to a connected agent. If we were instead using dynamic agents, which get created with each run, we could definitely pass those parameters to a connected agent. This would allow us to have a dedicated inventory manager agent. To simplify this demo, the MCP tool is given to the main agent.
- This architecture allows the main agent to search inventory and provide exact product IDs to the Cart Manager
- Connected agents work together seamlessly with the main agent's MCP capabilities

### Run the Setup Script

```bash
# Ensure you're logged into Azure CLI
az login

# Create and activate a Python virtual environment
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install Python dependencies
pip install -r requirements.txt

# Set your inventory URL (from Step 1)
export EXTERNAL_INVENTORY_URL='https://your-inventory-app.azurewebsites.net'

# Run the agent setup script
python setup_agents.py
```

The script will:
1. Delete any existing agents (for clean setup)
2. Create all 5 agents with proper configurations
3. Set up connected agent relationships
4. Configure MCP and OpenAPI tools
5. Output the Main Orchestrator Agent ID

**📝 Save the Main Orchestrator Agent ID** - you'll need it for the next step.

## Step 6: Configure Main Agent in App Service

Add the agent ID to your App Service configuration:

1. Go to **Azure Portal** → Your App Service
2. Navigate to **Configuration** → **Application settings**
3. Add the setting:
   - **Name:** `MAIN_ORCHESTRATOR_AGENT_ID`
   - **Value:** `agent_abcd1234` (the ID from the Python script output)
4. Click **Save** and **Continue** when prompted
5. Restart the App Service

## 🎉 Ready to Use!

Your multi-agent fashion assistant is now fully configured! Visit your App Service URL and try these interactions:

### Sample Conversations

**Inventory Queries:**
- "What denim jackets do you have in stock?"
- "Do you have any blazers in size medium?"
- "Show me all the shirts available"

**Shopping Cart:**
- "Add a small navy blazer to my cart"
- "What's in my cart?"
- "Remove the large shirt from my cart"

**Fashion Advice:**
- "What would go well with a black denim jacket?"
- "I need a business casual outfit suggestion"
- "What's trending in fashion right now?"

**Complex Multi-Agent Interactions:**
- "Find me a medium red shirt and add it to my cart" (uses both inventory and cart agents)
- "I'm looking for a complete outfit for a date night" (fashion advice + inventory checks)

## Architecture Benefits

This multi-agent architecture provides:

**🔄 Seamless Delegation:** The main agent automatically routes requests to the right specialist
**🛠️ Tool Integration:** OpenAPI and MCP tools provide real-time data access
**🔍 Transparency:** See all agent interactions in Azure AI Foundry
**📈 Scalability:** Easy to add new agents or modify existing ones
**🎯 Specialization:** Each agent excels at its specific domain

## Troubleshooting

**Chat Not Responding:**
- Verify the `MAIN_ORCHESTRATOR_AGENT_ID` app setting is correctly set
- Check that your Azure AI Foundry resources are properly provisioned
- Ensure the App Service managed identity has permissions to access AI Foundry

**Inventory Issues:**
- Confirm the `EXTERNAL_INVENTORY_URL` points to your deployed inventory service
- Test the inventory API directly: `https://your-inventory-app.azurewebsites.net/api/inventory`
- Check that the inventory service is running and accessible

**Agent Setup Issues:**
- Ensure you have the correct Azure credentials configured
- Verify the Python dependencies are installed
- Check that your Azure AI Foundry project is accessible

## Clean Up

When you're finished exploring the demo:

```bash
# Delete the main application
azd down

# Delete the inventory service
azd down
```

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Azure AI Foundry Connected Agents](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/connected-agents?pivots=csharp)
- [MCP Tools Setup](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/model-context-protocol#setup)
- [OpenAPI Tools](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/openapi-spec)
- [.NET Aspire on App Service](https://azure.github.io/AppService/2025/05/19/Aspire-on-App-Service.html)

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.