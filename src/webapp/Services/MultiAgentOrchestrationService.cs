using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using dotnetfashionassistant.Models;
using dotnetfashionassistant.Config;
using dotnetfashionassistant.Services.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dotnetfashionassistant.Services
{
    /// <summary>
    /// Multi-Agent Orchestration Service using Azure AI Foundry.
    /// Manages 4 specialized agents: Orchestrator, Cart Manager, Fashion Advisor, and Content Moderator.
    /// </summary>
    public class MultiAgentOrchestrationService
    {
        private readonly string? _projectEndpoint;
        private PersistentAgentsClient? _agentsClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MultiAgentOrchestrationService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private bool _isConfigured = false;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();
        private readonly int _maxCacheEntries = 100;
        
        // Thread-safe caches for conversation history
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _threadHistoryCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastCacheUpdateTime = new();

        // Agent factories - these make agent creation clear and reusable
        private OrchestratorAgentFactory? _orchestratorFactory;
        private CartManagerAgentFactory? _cartManagerFactory;
        private FashionAdvisorAgentFactory? _fashionAdvisorFactory;
        private ContentModeratorAgentFactory? _contentModeratorFactory;

        public MultiAgentOrchestrationService(
            IConfiguration configuration, 
            ILogger<MultiAgentOrchestrationService> logger,
            ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _loggerFactory = loggerFactory;

            // Get configuration values from the AI Foundry environment variables
            var projectEndpoint = _configuration["AI_PROJECT_ENDPOINT"] ?? 
                                Environment.GetEnvironmentVariable("AI_PROJECT_ENDPOINT") ??
                                _configuration.GetConnectionString("AI_PROJECT_ENDPOINT");
            
            if (!string.IsNullOrEmpty(projectEndpoint))
            {
                _projectEndpoint = projectEndpoint;
            }
            
            _isConfigured = !string.IsNullOrEmpty(_projectEndpoint);
            
            if (!_isConfigured)
            {
                _logger.LogError("Multi-Agent configuration failed: AI_PROJECT_ENDPOINT is missing");
            }
            else
            {
                _logger.LogInformation("Multi-Agent service configured successfully with endpoint: {Endpoint}", _projectEndpoint);
            }
        }
        
        /// <summary>
        /// Lazy initialization of the client and agent factories.
        /// This ensures everything is set up when actually needed.
        /// 
        /// DEMO NOTE: This is where we set up all the agent factories!
        /// </summary>
        private void EnsureInitialized()
        {
            if (_isInitialized || !_isConfigured)
                return;
                
            lock (_initLock)
            {
                if (_isInitialized)
                    return;
                    
                try
                {
                    if (!string.IsNullOrEmpty(_projectEndpoint))
                    {
                        _agentsClient = new PersistentAgentsClient(_projectEndpoint, new DefaultAzureCredential());
                        
                        // Initialize agent factories
                        _orchestratorFactory = new OrchestratorAgentFactory(_agentsClient, _configuration, 
                            _loggerFactory.CreateLogger<OrchestratorAgentFactory>());
                        _cartManagerFactory = new CartManagerAgentFactory(_agentsClient, _configuration, 
                            _loggerFactory.CreateLogger<CartManagerAgentFactory>());
                        _fashionAdvisorFactory = new FashionAdvisorAgentFactory(_agentsClient, _configuration, 
                            _loggerFactory.CreateLogger<FashionAdvisorAgentFactory>());
                        _contentModeratorFactory = new ContentModeratorAgentFactory(_agentsClient, _configuration, 
                            _loggerFactory.CreateLogger<ContentModeratorAgentFactory>());
                        
                        _isInitialized = true;
                        _logger.LogInformation("Multi-Agent service initialized successfully with {FactoryCount} agent factories", 4);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing Multi-Agent service client");
                }
            }
        }

        /// <summary>
        /// Creates a new thread for conversation.
        /// This maintains compatibility with the existing interface.
        /// </summary>
        public async Task<string> CreateThreadAsync()
        {
            if (!_isConfigured)
            {
                return "agent-not-configured";
            }
            
            EnsureInitialized();
            
            if (_agentsClient == null)
            {
                return "agent-initialization-failed";
            }
            
            try
            {
                var threadResponse = await _agentsClient.Threads.CreateThreadAsync();
                _logger.LogInformation("Created new conversation thread: {ThreadId}", threadResponse.Value.Id);
                return threadResponse.Value.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation thread");
                return "agent-not-configured";
            }
        }

        /// <summary>
        /// Sends a message using the multi-agent system.
        /// This is the main entry point that demonstrates the multi-agent pattern.
        /// 
        /// DEMO FLOW:
        /// 1. Create all specialist agents
        /// 2. Create orchestrator agent with connected agents
        /// 3. Send user message to orchestrator
        /// 4. Orchestrator delegates to appropriate specialist
        /// 5. Return coordinated response
        /// </summary>
        public async Task<string> SendMessageAsync(string threadId, string userMessage)
        {
            if (!_isConfigured || threadId == "agent-not-configured")
            {
                return "The AI agent system is not properly configured. Please add the required environment variable (AI_PROJECT_ENDPOINT) in your application settings.";
            }
            
            EnsureInitialized();
            
            if (_agentsClient == null)
            {
                return "The AI agent system could not be initialized. Please check the configuration and try again.";
            }

            // Track all agents for cleanup
            var agentsToCleanup = new List<PersistentAgent>();
            
            try
            {
                _logger.LogInformation("Starting multi-agent conversation for message: {Message}", userMessage);
                
                // STEP 1: Create all specialist agents
                // This demonstrates creating each type of agent with their specific purposes
                _logger.LogInformation("Creating specialist agents...");
                
                var cartManagerAgent = await _cartManagerFactory!.CreateAgentAsync();
                if (cartManagerAgent == null)
                {
                    return "Failed to create cart management specialist. The system may be experiencing issues.";
                }
                agentsToCleanup.Add(cartManagerAgent);
                _logger.LogInformation("✅ Created Cart Manager Agent: {AgentId}", cartManagerAgent.Id);

                var fashionAdvisorAgent = await _fashionAdvisorFactory!.CreateAgentAsync();
                if (fashionAdvisorAgent == null)
                {
                    return "Failed to create fashion advice specialist. The system may be experiencing issues.";
                }
                agentsToCleanup.Add(fashionAdvisorAgent);
                _logger.LogInformation("✅ Created Fashion Advisor Agent: {AgentId}", fashionAdvisorAgent.Id);

                var contentModeratorAgent = await _contentModeratorFactory!.CreateAgentAsync();
                if (contentModeratorAgent == null)
                {
                    return "Failed to create content moderation specialist. The system may be experiencing issues.";
                }
                agentsToCleanup.Add(contentModeratorAgent);
                _logger.LogInformation("✅ Created Content Moderator Agent: {AgentId}", contentModeratorAgent.Id);

                // STEP 2: Create the orchestrator agent with connected agents
                // This demonstrates the Connected Agents pattern
                _logger.LogInformation("Creating orchestrator agent with connected specialists...");
                
                var orchestratorAgent = await _orchestratorFactory!.CreateOrchestratorWithConnectedAgentsAsync(
                    cartManagerAgent, fashionAdvisorAgent, contentModeratorAgent);
                    
                if (orchestratorAgent == null)
                {
                    return "Failed to create the main coordinator agent. The system may be experiencing issues.";
                }
                agentsToCleanup.Add(orchestratorAgent);
                _logger.LogInformation("✅ Created Orchestrator Agent with {ConnectedAgentCount} connected agents: {AgentId}", 
                    3, orchestratorAgent.Id);

                // STEP 3: Process the conversation through the orchestrator
                // The orchestrator will automatically delegate to the appropriate specialist
                _logger.LogInformation("Processing message through orchestrator...");
                
                var userMessageOptions = new ThreadMessageOptions(MessageRole.User, userMessage);
                var threadOptions = new PersistentAgentThreadCreationOptions();
                threadOptions.Messages.Add(userMessageOptions);
                
                var threadAndRunOptions = new ThreadAndRunOptions
                {
                    ThreadOptions = threadOptions
                };
                
                var run = await _agentsClient.CreateThreadAndRunAsync(orchestratorAgent.Id, threadAndRunOptions);
                _logger.LogInformation("Started orchestrator run: {RunId}", run.Value.Id);
                
                // Wait for the orchestrator to complete (including any specialist delegations)
                var completedRun = await WaitForRunCompletionAsync(run.Value.ThreadId, run.Value.Id);
                
                if (completedRun.Status != RunStatus.Completed)
                {
                    var errorMessage = $"The conversation failed to complete successfully. Status: {completedRun.Status}";
                    if (completedRun.LastError != null)
                    {
                        errorMessage += $" Error: {completedRun.LastError.Message}";
                    }
                    _logger.LogWarning("Multi-agent conversation failed: {Error}", errorMessage);
                    return errorMessage;
                }

                // STEP 4: Get the coordinated response from the orchestrator
                var messagesPageable = _agentsClient.Messages.GetMessagesAsync(run.Value.ThreadId);
                var messagesList = new List<PersistentThreadMessage>();
                await foreach (var message in messagesPageable)
                {
                    messagesList.Add(message);
                }
                
                var assistantMessage = messagesList.FirstOrDefault(m => m.Role == MessageRole.Agent);
                
                if (assistantMessage?.ContentItems?.FirstOrDefault() is MessageTextContent textContent)
                {
                    _logger.LogInformation("Multi-agent conversation completed successfully");
                    return textContent.Text;
                }
                
                return "I apologize, but I couldn't generate a response through the specialist agents.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in multi-agent conversation for thread {ThreadId}", threadId);
                return $"Error in multi-agent conversation: {ex.Message}. Please try again.";
            }
            finally
            {
                // CLEANUP: Delete all created agents
                // This follows the per-request pattern as specified
                _logger.LogInformation("Cleaning up {AgentCount} agents...", agentsToCleanup.Count);
                
                foreach (var agent in agentsToCleanup)
                {
                    try
                    {
                        await _agentsClient!.Administration.DeleteAgentAsync(agent.Id);
                        _logger.LogDebug("Deleted agent: {AgentId}", agent.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting agent {AgentId}", agent.Id);
                    }
                }
                
                _logger.LogInformation("Agent cleanup completed");
            }
        }

        /// <summary>
        /// Waits for a run to complete, with appropriate logging for demo purposes.
        /// </summary>
        private async Task<ThreadRun> WaitForRunCompletionAsync(string threadId, string runId)
        {
            ThreadRun run;
            var startTime = DateTime.UtcNow;
            
            do
            {
                await Task.Delay(1000); // Wait 1 second between checks
                run = (await _agentsClient!.Runs.GetRunAsync(threadId, runId)).Value;
                
                // Log progress for demo purposes
                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogDebug("Run {RunId} status: {Status} (elapsed: {Elapsed}s)", 
                    runId, run.Status, elapsed.TotalSeconds);
                    
            } while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

            var totalTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Run {RunId} completed with status {Status} in {TotalTime}s", 
                runId, run.Status, totalTime.TotalSeconds);

            return run;
        }

        /// <summary>
        /// Gets conversation history with caching for performance.
        /// This maintains compatibility with the existing interface.
        /// </summary>
        public async Task<List<ChatMessage>> GetThreadHistoryAsync(string threadId)
        {
            if (!_isConfigured || threadId == "agent-not-configured")
            {
                return new List<ChatMessage> {
                    new ChatMessage {
                        Content = "The AI agent system is not properly configured. Please add the required environment variable (AI_PROJECT_ENDPOINT) in your application settings.",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    }
                };
            }
            
            EnsureInitialized();
            
            if (_agentsClient == null)
            {
                return new List<ChatMessage> {
                    new ChatMessage {
                        Content = "The AI agent system could not be initialized. Please check the configuration and try again.",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    }
                };
            }

            // Check cache first
            if (_threadHistoryCache.TryGetValue(threadId, out var cachedHistory) && 
                _lastCacheUpdateTime.TryGetValue(threadId, out var lastUpdate) &&
                (DateTime.UtcNow - lastUpdate).TotalSeconds < 30)
            {
                return new List<ChatMessage>(cachedHistory);
            }
            
            var chatHistory = new List<ChatMessage>();
            
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                
                var messagesResponse = _agentsClient.Messages.GetMessagesAsync(
                    threadId: threadId,
                    limit: 20,
                    order: ListSortOrder.Descending
                );
                
                var messagesList = new List<PersistentThreadMessage>();
                await foreach (var message in messagesResponse)
                {
                    messagesList.Add(message);
                }
                
                foreach (var message in messagesList.OrderBy(m => m.CreatedAt))
                {
                    string messageContent = "";
                    foreach (var contentItem in message.ContentItems)
                    {
                        if (contentItem is MessageTextContent textItem)
                        {
                            messageContent += textItem.Text ?? string.Empty;
                        }
                    }
                    
                    string formattedContent = message.Role != MessageRole.User ? 
                        FormatMessageContent(messageContent) : "";
                        
                    chatHistory.Add(new ChatMessage
                    {
                        Content = messageContent,
                        FormattedContent = formattedContent,
                        IsUser = message.Role == MessageRole.User,
                        Timestamp = message.CreatedAt.DateTime
                    });
                }
                
                // Cache management
                if (_threadHistoryCache.Count >= _maxCacheEntries)
                {
                    var oldestEntries = _lastCacheUpdateTime
                        .OrderBy(x => x.Value)
                        .Take(_threadHistoryCache.Count - _maxCacheEntries + 1)
                        .ToList();
                    
                    foreach (var entry in oldestEntries)
                    {
                        _threadHistoryCache.TryRemove(entry.Key, out _);
                        _lastCacheUpdateTime.TryRemove(entry.Key, out _);
                    }
                }
                
                _threadHistoryCache.AddOrUpdate(threadId, new List<ChatMessage>(chatHistory), 
                    (key, oldValue) => new List<ChatMessage>(chatHistory));
                _lastCacheUpdateTime.AddOrUpdate(threadId, DateTime.UtcNow, 
                    (key, oldValue) => DateTime.UtcNow);
            }
            catch (OperationCanceledException)
            {
                return cachedHistory ?? new List<ChatMessage>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving thread history for thread {ThreadId}", threadId);
                return cachedHistory ?? new List<ChatMessage>();
            }

            return chatHistory;
        }
        
        /// <summary>
        /// Helper method to format message content with HTML tags for display.
        /// </summary>
        private string FormatMessageContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;
                
            content = System.Text.RegularExpressions.Regex.Replace(
                content, 
                @"\*\*([^*]+)\*\*", 
                "<strong>$1</strong>");
                
            return content
                .Replace("\n\n", "<br><br>")
                .Replace("\n", "<br>")
                .Replace("•", "<br>•");
        }

        /// <summary>
        /// Gets a summary of the current multi-agent configuration.
        /// Perfect for demo purposes to show what agents are available.
        /// </summary>
        public Dictionary<string, object> GetMultiAgentSystemInfo()
        {
            var agentConfigs = AgentDefinitions.GetAllAgentConfigurations();
            var connectedAgentDescriptions = AgentDefinitions.GetConnectedAgentDescriptions();
            
            return new Dictionary<string, object>
            {
                ["IsConfigured"] = _isConfigured,
                ["IsInitialized"] = _isInitialized,
                ["ProjectEndpoint"] = _projectEndpoint ?? "Not configured",
                ["AvailableAgents"] = agentConfigs.Keys.ToList(),
                ["AgentDetails"] = agentConfigs,
                ["ConnectedAgentDescriptions"] = connectedAgentDescriptions,
                ["Architecture"] = "Orchestrator → Specialist Agents → Coordinated Response"
            };
        }
    }
}