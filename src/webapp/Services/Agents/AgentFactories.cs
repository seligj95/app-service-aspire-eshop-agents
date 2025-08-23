using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using dotnetfashionassistant.Config;

namespace dotnetfashionassistant.Services.Agents
{
    /// <summary>
    /// Base factory class for creating AI agents in the fashion store application.
    /// This provides common functionality and makes agent creation consistent.
    /// 
    /// HANDS-ON DEMO GUIDE:
    /// - All agent factories inherit from this base class
    /// - Common configuration and error handling is centralized here
    /// - Easy to extend for new agent types
    /// </summary>
    public abstract class BaseAgentFactory
    {
        protected readonly PersistentAgentsClient _agentsClient;
        protected readonly IConfiguration _configuration;
        protected readonly ILogger _logger;
        protected readonly string _modelDeploymentName;

        protected BaseAgentFactory(
            PersistentAgentsClient agentsClient,
            IConfiguration configuration,
            ILogger logger)
        {
            _agentsClient = agentsClient;
            _configuration = configuration;
            _logger = logger;
            _modelDeploymentName = _configuration["AI_MODEL_DEPLOYMENT_NAME"] ?? 
                                  Environment.GetEnvironmentVariable("AI_MODEL_DEPLOYMENT_NAME") ?? 
                                  "gpt-4o";
        }

        /// <summary>
        /// Abstract method that each agent factory must implement to create their specific agent type.
        /// </summary>
        public abstract Task<PersistentAgent?> CreateAgentAsync();

        /// <summary>
        /// Helper method to safely create any agent with error handling.
        /// </summary>
        protected async Task<PersistentAgent?> CreateAgentWithErrorHandlingAsync(
            string name, 
            string description, 
            string instructions, 
            IEnumerable<ToolDefinition>? tools = null)
        {
            try
            {
                var response = await _agentsClient.Administration.CreateAgentAsync(
                    model: _modelDeploymentName,
                    name: name,
                    description: description,
                    instructions: instructions,
                    tools: tools);

                _logger.LogInformation("Successfully created agent: {AgentName} (ID: {AgentId})", name, response.Value.Id);
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent: {AgentName}", name);
                return null;
            }
        }
    }

    /// <summary>
    /// Factory for creating the main orchestrator agent.
    /// The orchestrator coordinates all other agents but has no tools itself.
    /// 
    /// DEMO NOTE: This is the "brain" of the multi-agent system!
    /// </summary>
    public class OrchestratorAgentFactory : BaseAgentFactory
    {
        public OrchestratorAgentFactory(
            PersistentAgentsClient agentsClient,
            IConfiguration configuration,
            ILogger<OrchestratorAgentFactory> logger)
            : base(agentsClient, configuration, logger)
        {
        }

        /// <summary>
        /// Creates the orchestrator agent with connected agent tools.
        /// This agent will coordinate all specialist agents.
        /// </summary>
        public async Task<PersistentAgent?> CreateOrchestratorWithConnectedAgentsAsync(
            PersistentAgent cartManagerAgent,
            PersistentAgent fashionAdvisorAgent,
            PersistentAgent contentModeratorAgent)
        {
            try
            {
                // Create connected agent tool definitions
                var connectedAgentTools = new List<ToolDefinition>
                {
                    new ConnectedAgentToolDefinition(new ConnectedAgentDetails(
                        cartManagerAgent.Id,
                        "cart_manager",
                        AgentDefinitions.CartManager.ConnectedAgentDescription)),
                    
                    new ConnectedAgentToolDefinition(new ConnectedAgentDetails(
                        fashionAdvisorAgent.Id,
                        "fashion_advisor", 
                        AgentDefinitions.FashionAdvisor.ConnectedAgentDescription)),
                    
                    new ConnectedAgentToolDefinition(new ConnectedAgentDetails(
                        contentModeratorAgent.Id,
                        "content_moderator",
                        AgentDefinitions.ContentModerator.ConnectedAgentDescription))
                };

                var response = await _agentsClient.Administration.CreateAgentAsync(
                    model: _modelDeploymentName,
                    name: AgentDefinitions.Orchestrator.Name,
                    description: AgentDefinitions.Orchestrator.Description,
                    instructions: AgentDefinitions.Orchestrator.Instructions,
                    tools: connectedAgentTools);

                _logger.LogInformation("Successfully created orchestrator agent with {ConnectedAgentCount} connected agents", 
                    connectedAgentTools.Count);
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create orchestrator agent");
                return null;
            }
        }

        public override async Task<PersistentAgent?> CreateAgentAsync()
        {
            // This method creates the orchestrator without connected agents
            // Use CreateOrchestratorWithConnectedAgentsAsync for the full setup
            return await CreateAgentWithErrorHandlingAsync(
                AgentDefinitions.Orchestrator.Name,
                AgentDefinitions.Orchestrator.Description,
                AgentDefinitions.Orchestrator.Instructions);
        }
    }

    /// <summary>
    /// Factory for creating the cart manager agent.
    /// This agent handles shopping cart operations using OpenAPI tools.
    /// 
    /// DEMO NOTE: This agent shows how to integrate with external APIs!
    /// </summary>
    public class CartManagerAgentFactory : BaseAgentFactory
    {
        public CartManagerAgentFactory(
            PersistentAgentsClient agentsClient,
            IConfiguration configuration,
            ILogger<CartManagerAgentFactory> logger)
            : base(agentsClient, configuration, logger)
        {
        }

        public override async Task<PersistentAgent?> CreateAgentAsync()
        {
            try
            {
                // Create OpenAPI tool for cart operations
                var openApiTool = await CreateOpenApiToolAsync();
                var tools = openApiTool != null ? new[] { openApiTool } : null;

                return await CreateAgentWithErrorHandlingAsync(
                    AgentDefinitions.CartManager.Name,
                    AgentDefinitions.CartManager.Description,
                    AgentDefinitions.CartManager.Instructions,
                    tools);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create cart manager agent");
                return null;
            }
        }

        /// <summary>
        /// Creates the OpenAPI tool for cart operations.
        /// This dynamically configures the server URL based on the deployed app service.
        /// 
        /// DEMO NOTE: This is where we connect the agent to our actual API!
        /// </summary>
        private async Task<OpenApiToolDefinition?> CreateOpenApiToolAsync()
        {
            try
            {
                // Get the current app's base URL or use localhost for development
                var serverUrl = GetServerUrl();
                
                // Read and update the swagger.json specification
                var swaggerJson = await GetUpdatedSwaggerSpecificationAsync(serverUrl);
                
                if (string.IsNullOrEmpty(swaggerJson))
                {
                    _logger.LogWarning("Could not load swagger specification for cart manager");
                    return null;
                }

                return new OpenApiToolDefinition(
                    name: "fashion_store_api",
                    description: "API for managing fashion store inventory and shopping cart operations",
                    spec: BinaryData.FromString(swaggerJson),
                    openApiAuthentication: new OpenApiAnonymousAuthDetails());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create OpenAPI tool for cart manager");
                return null;
            }
        }

        /// <summary>
        /// Gets the server URL for the OpenAPI specification.
        /// Tries multiple sources to find the correct URL.
        /// </summary>
        private string GetServerUrl()
        {
            // Try to get from various configuration sources
            var serverUrl = _configuration["WEBSITE_HOSTNAME"] ?? 
                           Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ??
                           _configuration["ServerUrl"] ??
                           "localhost:5000"; // fallback for local development

            // Ensure proper format
            if (!serverUrl.StartsWith("http"))
            {
                serverUrl = serverUrl.Contains("localhost") ? $"http://{serverUrl}" : $"https://{serverUrl}";
            }

            _logger.LogInformation("Using server URL for OpenAPI tool: {ServerUrl}", serverUrl);
            return serverUrl;
        }

        /// <summary>
        /// Reads the swagger.json file and updates the server URL dynamically.
        /// This ensures the agent can call the correct API endpoint.
        /// </summary>
        private async Task<string?> GetUpdatedSwaggerSpecificationAsync(string serverUrl)
        {
            try
            {
                var swaggerPath = Path.Combine(AppContext.BaseDirectory, "swagger.json");
                if (!File.Exists(swaggerPath))
                {
                    _logger.LogWarning("Swagger.json file not found at {SwaggerPath}", swaggerPath);
                    return null;
                }

                var swaggerContent = await File.ReadAllTextAsync(swaggerPath);
                
                // Replace the placeholder server URL with the actual server URL
                swaggerContent = swaggerContent.Replace("<APP-SERVICE-URL>", serverUrl);
                
                _logger.LogInformation("Updated swagger specification with server URL: {ServerUrl}", serverUrl);
                return swaggerContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read or update swagger specification");
                return null;
            }
        }
    }

    /// <summary>
    /// Factory for creating the fashion advisor agent.
    /// This agent provides fashion advice and has no tools - knowledge only.
    /// 
    /// DEMO NOTE: This agent shows pure AI reasoning without external tools!
    /// </summary>
    public class FashionAdvisorAgentFactory : BaseAgentFactory
    {
        public FashionAdvisorAgentFactory(
            PersistentAgentsClient agentsClient,
            IConfiguration configuration,
            ILogger<FashionAdvisorAgentFactory> logger)
            : base(agentsClient, configuration, logger)
        {
        }

        public override async Task<PersistentAgent?> CreateAgentAsync()
        {
            return await CreateAgentWithErrorHandlingAsync(
                AgentDefinitions.FashionAdvisor.Name,
                AgentDefinitions.FashionAdvisor.Description,
                AgentDefinitions.FashionAdvisor.Instructions);
        }
    }

    /// <summary>
    /// Factory for creating the content moderator agent.
    /// This agent validates content appropriateness and relevance.
    /// 
    /// DEMO NOTE: This agent shows how to implement safety and content filtering!
    /// </summary>
    public class ContentModeratorAgentFactory : BaseAgentFactory
    {
        public ContentModeratorAgentFactory(
            PersistentAgentsClient agentsClient,
            IConfiguration configuration,
            ILogger<ContentModeratorAgentFactory> logger)
            : base(agentsClient, configuration, logger)
        {
        }

        public override async Task<PersistentAgent?> CreateAgentAsync()
        {
            return await CreateAgentWithErrorHandlingAsync(
                AgentDefinitions.ContentModerator.Name,
                AgentDefinitions.ContentModerator.Description,
                AgentDefinitions.ContentModerator.Instructions);
        }
    }
}