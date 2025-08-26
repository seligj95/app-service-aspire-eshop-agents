import os
import json
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
from azure.ai.agents.models import OpenApiTool, OpenApiAnonymousAuthDetails, McpTool

# Configuration from environment variables
project_endpoint = os.environ["PROJECT_ENDPOINT"]
model_deployment_name = os.environ["MODEL_DEPLOYMENT_NAME"]
webapp_url = os.environ["WEBAPP_URL"]
external_inventory_api_url = os.environ["EXTERNAL_INVENTORY_URL"]

def load_and_patch_swagger_spec(webapp_url):
    """Load swagger.json and patch the server URL with the webapp URL"""
    try:
        # Load the swagger.json file
        with open('swagger.json', 'r') as f:
            swagger_spec = json.load(f)
        
        # Patch the server URL
        if 'servers' not in swagger_spec:
            swagger_spec['servers'] = []
        
        # Replace or add the server URL
        swagger_spec['servers'] = [{"url": webapp_url}]
        
        print(f"✓ Loaded and patched swagger.json with server URL: {webapp_url}")
        return swagger_spec
        
    except FileNotFoundError:
        print("✗ swagger.json file not found")
        raise
    except json.JSONDecodeError as e:
        print(f"✗ Error parsing swagger.json: {e}")
        raise

# Fashion assistant agents to create
def get_agents_config(webapp_url, external_mcp_server_url):
    """Get agent configurations with tools"""
    
    # Load and patch the swagger spec for the cart agent
    cart_openapi_spec = load_and_patch_swagger_spec(webapp_url)
    
    # Create OpenAPI tool for cart management
    cart_auth = OpenApiAnonymousAuthDetails()
    cart_openapi_tool = OpenApiTool(
        name="cart_manager",
        spec=cart_openapi_spec,
        description="Manages shopping cart operations including add, remove, view, and clear cart",
        auth=cart_auth
    )
    
    # Create MCP tool for inventory management using external MCP server
    mcp_tool = McpTool(
        server_label="inventory_mcp",
        server_url=external_mcp_server_url
    )
    
    return {
        "main_orchestrator": {
            "name": "Fashion Store Main Agent",
            "instructions": """You are the main orchestrator for a fashion retail store. You coordinate between your 3 specialist agents and handle inventory queries directly via MCP tools.

Your specialist agents:
1. **Cart Manager**: Handles all shopping cart operations (add/remove items, view cart, clear cart)
2. **Fashion Advisor**: Provides style recommendations, fashion advice, and outfit coordination
3. **Content Moderator**: Reviews content for appropriateness and maintains professional standards

YOUR DIRECT CAPABILITIES:
- **Inventory Management**: You have direct access to inventory data via MCP tools to check product availability, stock levels, and product details
- **Workflow Coordination**: You orchestrate the entire customer experience

CRITICAL CONTENT MODERATION WORKFLOW:
BEFORE processing ANY customer request, you MUST:
1. FIRST delegate to Content Moderator to verify the request is appropriate for a fashion retail environment
2. If Content Moderator flags the request as inappropriate or off-topic, respond accordingly and don't proceed
3. ONLY if Content Moderator approves, proceed with the customer's request

CRITICAL CART WORKFLOW:
When a customer wants to add an item to cart:
1. First, use your MCP tools to search and verify the exact product details
2. Get the EXACT productId, name, available sizes, and stock levels from your MCP tools
3. When delegating to Cart Manager, provide the EXACT productId and size from your MCP search results
4. NEVER let the Cart Manager guess or search for product IDs - always provide the exact ID from your inventory search

IMPORTANT WORKFLOW RULES:
- ALWAYS start with Content Moderator for ALL requests
- For inventory queries: Use your MCP tools directly to check product availability and details
- For cart operations: 
  * FIRST verify inventory with your MCP tools
  * Get exact productId from MCP results
  * THEN delegate to Cart Manager with specific: productId, size, and quantity
- For fashion advice: Delegate to Fashion Advisor
- For off-topic requests: Politely redirect to fashion/shopping topics

Example complete workflow:
1. Customer: "give me a cookie recipe"
2. You: Delegate to Content Moderator to review request
3. Content Moderator: Flags as off-topic for fashion retail
4. You: "I'm a fashion store assistant focused on helping with clothing, style, and shopping. How can I help you find the perfect outfit or fashion items today?"

Example cart workflow:
1. Customer: "add denim jacket to cart"
2. You: Delegate to Content Moderator to verify appropriateness
3. Content Moderator: Approves fashion-related request
4. You: Use MCP tools to search for "denim jacket" 
5. You: Find exact product (e.g., ID 4, "Navy Blue Washed Denim Jacket")
6. You: Ask customer for size preference
7. You: Delegate to Cart Manager with EXACT details: "Please add productId 4 (Navy Blue Washed Denim Jacket) in size [customer choice] to cart"

Always verify product availability yourself before allowing cart operations.""",
            "mcp_tool": mcp_tool  # Store the MCP tool object for agent creation
        },
        
        "cart_manager": {
            "name": "Cart Manager",
            "instructions": """You are the Cart Manager for a fashion retail store. You handle all shopping cart operations using OpenAPI tools.

CRITICAL INSTRUCTIONS:
- The main orchestrator will provide you with EXACT product details including productId
- NEVER search for or guess product IDs
- ALWAYS use the exact productId provided by the main orchestrator
- If you don't receive a specific productId, ask the main orchestrator to provide it

Your capabilities:
- Add items to cart (with exact productId, size, and quantity)
- Remove items from cart
- View current cart contents
- Clear the entire cart

REQUIRED INFORMATION for cart operations:
- productId: EXACT numeric ID from main orchestrator's inventory search
- size: Customer's size preference (XS, S, M, L, XL, XXL)
- quantity: Number of items to add

Example workflow:
1. Main orchestrator: "Please add productId 4 (Navy Blue Washed Denim Jacket) in size M to cart"
2. You: Use add-to-cart API with productId=4, size=M, quantity=1
3. You: Confirm successful addition and current cart status

IMPORTANT: If you receive vague product descriptions without specific productId, respond with:
"I need the exact productId to add this item to the cart. Could you please use your inventory tools to find the specific product ID first?"

Always provide clear confirmation of cart operations and current cart status.""",
            "tools": cart_openapi_tool.definitions
        },
        
        "fashion_advisor": {
            "name": "Fashion Advisor Agent",
            "instructions": """You are an expert fashion consultant providing style advice and recommendations.

Your expertise includes:
- Style suggestions based on customer preferences and body type
- Outfit coordination and color matching
- Fashion trends and seasonal recommendations
- Size and fit guidance
- Occasion-appropriate styling (work, casual, formal, etc.)
- Brand and price point recommendations

Work with the main agent who has access to current inventory to suggest available products that match customer style preferences.
Always consider the customer's needs, preferences, budget, and lifestyle when making recommendations.
Provide specific, actionable advice and be encouraging about personal style exploration.""",
            "tools": []
        },
        
        "content_moderator": {
            "name": "Content Moderator Agent",
            "instructions": """You are responsible for maintaining a safe, respectful, and professional environment in all customer interactions for a fashion retail store.

Your primary responsibilities:
1. **Topic Relevance**: Ensure all requests are appropriate for a fashion retail environment
2. **Content Safety**: Flag inappropriate content, requests, or behavior
3. **Professional Standards**: Maintain retail customer service standards
4. **Policy Compliance**: Ensure interactions comply with fashion retail policies

TOPIC FILTERING RULES:
✅ APPROVE these topics:
- Fashion, clothing, and style questions
- Product inquiries and shopping assistance
- Cart operations and checkout help
- Size, fit, and styling advice
- Returns, exchanges, and store policies
- General retail customer service

❌ REJECT these topics:
- Cooking recipes and food-related requests
- Medical, legal, or professional advice
- Non-fashion product requests
- Personal life advice unrelated to fashion
- Technical support for non-store systems
- Any topic unrelated to fashion retail

RESPONSE FORMAT:
For APPROVED requests: "APPROVED: This request is appropriate for our fashion retail environment."
For REJECTED requests: "REJECTED: This request is outside our fashion retail scope. Suggest redirecting to fashion/shopping topics."

Examples:
- "give me a cookie recipe" → REJECTED: This request is outside our fashion retail scope. Suggest redirecting to fashion/shopping topics.
- "help me find a dress for a wedding" → APPROVED: This request is appropriate for our fashion retail environment.
- "add jeans to my cart" → APPROVED: This request is appropriate for our fashion retail environment.

Always prioritize customer safety while maintaining focus on fashion retail assistance.""",
            "tools": []
        }
    }

def create_agents():
    """Create fashion assistant agents in Azure AI Foundry using Connected Agents pattern"""
    print(f"Connecting to project: {project_endpoint}")
    print(f"Using model: {model_deployment_name}")
    print(f"Web app URL: {webapp_url}")
    
    external_inventory_url = os.environ.get("EXTERNAL_INVENTORY_URL")
    
    if not external_inventory_url:
        print("ERROR: EXTERNAL_INVENTORY_URL environment variable not set!")
        print("Please set it to point to your external inventory service, for example:")
        print("  export EXTERNAL_INVENTORY_URL='https://your-inventory-server.com'")
        return
    
    base_url = external_inventory_url.rstrip('/')
    external_mcp_server_url = f"{base_url}/mcp" if not base_url.endswith('/mcp') else base_url
    
    print(f"External inventory URL: {external_inventory_url}")
    print(f"External MCP server URL: {external_mcp_server_url}")
    
    project_client = AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential(),
    )
    
    agents_client = project_client.agents
    agent_ids = {}
    created_agents = {}
    
    print(f"\n{'='*60}")
    print("STEP 0: Cleaning up existing agents...")
    print(f"{'='*60}")
    
    try:
        existing_agents_pageable = agents_client.list_agents()
        existing_agents = []
        
        for agent in existing_agents_pageable:
            existing_agents.append(agent)
        
        if existing_agents:
            print(f"Found {len(existing_agents)} existing agents to delete...")
            
            for agent in existing_agents:
                try:
                    print(f"Deleting agent: {agent.name} (ID: {agent.id})")
                    agents_client.delete_agent(agent.id)
                    print(f"✓ Deleted agent: {agent.name}")
                except Exception as e:
                    print(f"✗ Failed to delete agent {agent.name}: {str(e)}")
            
            print(f"✓ Completed cleanup of existing agents")
        else:
            print("No existing agents found to delete")
            
    except Exception as e:
        print(f"Warning: Could not retrieve existing agents for cleanup: {str(e)}")
        print("Continuing with agent creation...")
    
    # Get agent configurations with external MCP server
    agents_config = get_agents_config(webapp_url, external_mcp_server_url)
    
    # Step 1: Create specialist agents first (these will be connected to the main agent)
    specialist_agents = ["cart_manager", "fashion_advisor", "content_moderator"]
    
    print(f"\n{'='*60}")
    print("STEP 1: Creating specialist agents...")
    print(f"{'='*60}")
    
    for agent_key in specialist_agents:
        agent_config = agents_config[agent_key]
        print(f"\nCreating specialist agent: {agent_config['name']}")
        
        try:
            # Create the specialist agent
            agent = agents_client.create_agent(
                model=model_deployment_name,
                name=agent_config['name'],
                instructions=agent_config['instructions'],
                tools=agent_config['tools']
            )
            
            agent_ids[agent_key] = agent.id
            created_agents[agent_key] = agent
            print(f"✓ Created specialist agent '{agent_config['name']}' with ID: {agent.id}")
            
        except Exception as e:
            print(f"✗ Failed to create specialist agent '{agent_config['name']}': {str(e)}")
            raise
    
    # Step 2: Create the main orchestrator agent with MCP tools and connected agent tools
    print(f"\n{'='*60}")
    print("STEP 2: Creating main orchestrator with MCP and connected agents...")
    print(f"{'='*60}")
    
    try:
        from azure.ai.agents.models import ConnectedAgentTool
        
        # Create connected agent tools for each specialist
        connected_agent_tools = []
        
        # Cart Manager connection
        cart_agent = created_agents['cart_manager']
        cart_connected_tool = ConnectedAgentTool(
            id=cart_agent.id,
            name="cart_manager",
            description="Handles all shopping cart operations including adding items, removing items, viewing cart contents, and managing quantities. Use after verifying inventory availability."
        )
        connected_agent_tools.extend(cart_connected_tool.definitions)
        
        # Fashion Advisor connection
        fashion_agent = created_agents['fashion_advisor']
        fashion_connected_tool = ConnectedAgentTool(
            id=fashion_agent.id,
            name="fashion_advisor",
            description="Provides expert fashion advice, style recommendations, outfit coordination, and trend guidance. Use for styling questions and fashion recommendations."
        )
        connected_agent_tools.extend(fashion_connected_tool.definitions)
        
        # Content Moderator connection
        moderator_agent = created_agents['content_moderator']
        moderator_connected_tool = ConnectedAgentTool(
            id=moderator_agent.id,
            name="content_moderator",
            description="Reviews content for appropriateness and maintains professional standards. Use to ensure safe, professional customer interactions."
        )
        connected_agent_tools.extend(moderator_connected_tool.definitions)
        
        print(f"✓ Created {len(connected_agent_tools)} connected agent tool definitions")
        
        # Get main agent config
        main_agent_config = agents_config['main_orchestrator']
        mcp_tool = main_agent_config['mcp_tool']
        
        # Combine MCP tool definitions with connected agent tools
        all_tools = mcp_tool.definitions + connected_agent_tools
        
        print(f"\nCreating main orchestrator: {main_agent_config['name']}")
        print(f"✓ Will include MCP tools for inventory and {len(connected_agent_tools)} connected agent tools")
        
        main_agent = agents_client.create_agent(
            model=model_deployment_name,
            name=main_agent_config['name'],
            instructions=main_agent_config['instructions'],
            tools=all_tools  # Combined MCP + connected agent tools
        )
        
        agent_ids['main_orchestrator'] = main_agent.id
        print(f"✓ Created main orchestrator '{main_agent_config['name']}' with ID: {main_agent.id}")
        print(f"✓ Main agent has MCP tools for inventory and connected to {len(specialist_agents)} specialist agents")
        
    except Exception as e:
        print(f"✗ Failed to create main orchestrator agent: {str(e)}")
        print("Note: Make sure you have the latest azure-ai-agents package installed")
        raise
    
    # Output the agent IDs for use in the application
    print(f"\n{'='*60}")
    print("FASHION STORE AGENT CREATION COMPLETED")
    print(f"{'='*60}")
    print("\nAgent IDs created:")
    print(f"  Main Orchestrator (with MCP): {agent_ids['main_orchestrator']}")
    print(f"  Cart Manager: {agent_ids['cart_manager']}")
    print(f"  Fashion Advisor: {agent_ids['fashion_advisor']}")
    print(f"  Content Moderator: {agent_ids['content_moderator']}")
    
    print(f"\nSet this environment variable in your Azure App Service:")
    print(f"  MAIN_ORCHESTRATOR_AGENT_ID={agent_ids['main_orchestrator']}")
    
    print(f"\nArchitecture Summary:")
    print(f"  • Main Orchestrator handles inventory via MCP tools and coordinates connected agents")
    print(f"  • Cart Manager handles shopping cart via OpenAPI tools")
    print(f"  • Fashion Advisor gives style and fashion recommendations")
    print(f"  • Content Moderator ensures safe, professional interactions")
    print(f"\nAll agents use model: {model_deployment_name}")

    return agent_ids

def create_mcp_tool_resources(server_label="inventory_mcp", headers=None, require_approval="always"):
    """
    Helper function to create MCP tool resources for runtime configuration.
    
    Args:
        server_label (str): The server label used when creating the agent (default: 'inventory_mcp')
        headers (dict): Authentication headers required by the external MCP server (optional)
        require_approval (str): Approval setting - 'always', 'never', or dict with tool lists
        
    Returns:
        dict: Tool resources configuration for use in agent runs
        
    Example usage in .NET:
        var toolResources = createMcpToolResources();
        await agentsClient.Runs.CreateRunAsync(thread, agent, toolResources);
    """
    mcp_config = {
        'server_label': server_label,
        'require_approval': require_approval
    }
    
    if headers:
        mcp_config['headers'] = headers
        
    return {'mcp': [mcp_config]}

if __name__ == "__main__":
    create_agents()