# Multi-Agent Fashion Store Demo Guide

## Overview
This demo showcases a sophisticated multi-agent system for a fashion e-commerce store using Azure AI Foundry, .NET Aspire, and Blazor. The system uses an orchestrator agent that intelligently delegates tasks to specialized agents.

## Architecture

### Agents
1. **Orchestrator Agent** - Analyzes user input and delegates to appropriate specialists
2. **Cart Manager Agent** - Handles shopping cart operations via OpenAPI tools
3. **Fashion Advisor Agent** - Provides personalized fashion recommendations
4. **Content Moderator Agent** - Ensures discussions stay fashion-focused

### Key Features
- Per-request agent creation for clean conversations
- Intelligent agent delegation based on user intent
- Error handling with clear explanations
- Conversation history maintenance
- OpenAPI tool integration for cart operations

## Demo Scenarios

### Scenario 1: Fashion Advice
**User Input:** "What should I wear to a business meeting?"

**Expected Flow:**
1. Orchestrator analyzes the request
2. Delegates to Fashion Advisor Agent
3. Receives personalized recommendations
4. Returns styling suggestions

### Scenario 2: Cart Management
**User Input:** "Add a blue shirt to my cart"

**Expected Flow:**
1. Orchestrator identifies cart operation
2. Delegates to Cart Manager Agent
3. Cart Manager uses OpenAPI tools to add item
4. Returns confirmation of addition

### Scenario 3: Multi-Agent Interaction
**User Input:** "I need a professional outfit for an interview and want to add items to my cart"

**Expected Flow:**
1. Orchestrator identifies both fashion advice and cart operations
2. First delegates to Fashion Advisor for recommendations
3. Then delegates to Cart Manager for adding suggested items
4. Returns complete response with advice and cart updates

### Scenario 4: Content Moderation
**User Input:** "Tell me about the weather today"

**Expected Flow:**
1. Orchestrator analyzes off-topic request
2. Delegates to Content Moderator Agent
3. Content Moderator redirects to fashion-related topics
4. Returns helpful redirection message

## Configuration & Customization

### Agent Instructions (src/webapp/Config/AgentDefinitions.cs)

You can modify agent behavior by updating their instructions:

```csharp
public static class AgentDefinitions
{
    // Modify orchestrator logic
    public const string OrchestratorInstructions = "...";
    
    // Customize fashion advice style
    public const string FashionAdvisorInstructions = "...";
    
    // Adjust cart management behavior
    public const string CartManagerInstructions = "...";
    
    // Configure content moderation rules
    public const string ContentModeratorInstructions = "...";
}
```

### Demo Customizations

#### 1. Adding New Agents
1. Add agent definition to `AgentDefinitions.cs`
2. Create new factory in `AgentFactories.cs`
3. Update orchestrator instructions to include new agent
4. Test delegation logic

#### 2. Modifying Agent Behavior
- Edit instructions in `AgentDefinitions.cs`
- Restart application to see changes
- Test with various user inputs

#### 3. Adding Tools to Agents
- Define tools in agent factory classes
- Update agent instructions to reference new tools
- Ensure proper error handling

### Switching Between Single and Multi-Agent Mode

In `Program.cs`, you can switch implementations:

```csharp
// Multi-agent mode (default)
builder.Services.AddScoped<IAgentService, MultiAgentOrchestrationService>();

// Single agent mode (for comparison)
// builder.Services.AddScoped<IAgentService, AzureAIAgentService>();
```

## Demo Flow

### 1. Application Setup
```bash
cd src/ai-agent-openai-web-app.AppHost
dotnet run
```

### 2. Opening the Web Application
- Navigate to the displayed URL (typically https://localhost:7080)
- Open the web application endpoint

### 3. Demo Walkthrough

#### Part 1: Basic Fashion Advice
1. "What colors work well with navy blue?"
2. "I need an outfit for a summer wedding"
3. "How do I style a blazer casually?"

#### Part 2: Cart Operations
1. "Add a white t-shirt to my cart"
2. "Show me what's in my shopping cart"
3. "Remove the first item from my cart"

#### Part 3: Complex Scenarios
1. "I'm going to a job interview at a tech company. What should I wear and can you add those items to my cart?"
2. "I need both business and casual outfits for a work trip"

#### Part 4: Content Moderation
1. "What's the weather like today?"
2. "Tell me about sports news"
3. "Help me with my homework"

### 4. Observing Agent Behavior

Watch for these indicators in responses:
- **Agent Identification**: Responses show which agent handled the request
- **Tool Usage**: Cart operations show OpenAPI tool integration
- **Delegation Logic**: Complex requests are broken down appropriately
- **Error Handling**: Off-topic requests are redirected gracefully

## Troubleshooting

### Common Issues

#### 1. Agents Not Delegating Properly
- Check orchestrator instructions in `AgentDefinitions.cs`
- Ensure agent descriptions are clear and distinct
- Verify user input matches expected patterns

#### 2. Cart Operations Failing
- Verify OpenAPI specification is accessible
- Check network connectivity to cart API
- Ensure proper tool registration in CartManagerAgentFactory

#### 3. Responses Too Generic
- Refine agent instructions for more specific behavior
- Add more context to agent descriptions
- Test with varied user inputs

### Debug Mode
Enable detailed logging by setting environment variable:
```bash
export ASPNETCORE_ENVIRONMENT=Development
```

## Advanced Customization

### Adding Custom Tools
1. Define tool specification in agent factory
2. Update agent instructions to reference the tool
3. Handle tool responses in agent logic

### Modifying Delegation Logic
Edit the orchestrator instructions to change how agents are selected:
- Add new decision criteria
- Modify agent priority
- Include confidence scoring

### Performance Optimization
- Implement agent caching for repeated requests
- Add request debouncing for rapid user input
- Consider agent pooling for high-traffic scenarios

## Code Structure

```
src/webapp/
├── Config/
│   └── AgentDefinitions.cs          # Central agent configuration
├── Services/
│   ├── IAgentService.cs             # Service interface
│   ├── AzureAIAgentService.cs       # Single agent implementation
│   ├── MultiAgentOrchestrationService.cs  # Multi-agent orchestrator
│   └── Agents/
│       └── AgentFactories.cs        # Agent creation logic
├── Components/Pages/
│   └── Home.razor                   # Main chat interface
└── Program.cs                       # Service registration
```

## Further Development

### Potential Enhancements
1. **Agent Analytics**: Track which agents are used most frequently
2. **Dynamic Agent Loading**: Add agents without recompiling
3. **Agent Memory**: Implement persistent agent memory across sessions
4. **Custom Agent UI**: Specialized interfaces for different agent types
5. **Agent Collaboration**: Enable agents to work together on complex tasks

### Integration Opportunities
- **Azure Cognitive Search**: Enhanced product search capabilities
- **Azure Cosmos DB**: Persistent conversation history
- **Azure Service Bus**: Asynchronous agent communication
- **Azure Monitor**: Agent performance monitoring

## Questions & Exploration

During the demo, encourage participants to:
1. Modify agent instructions and observe behavior changes
2. Create new agent types for different domains
3. Experiment with complex multi-step scenarios
4. Explore error handling and edge cases
5. Discuss real-world applications and scaling considerations

This multi-agent system demonstrates the power of specialized AI agents working together to create sophisticated, domain-specific applications.