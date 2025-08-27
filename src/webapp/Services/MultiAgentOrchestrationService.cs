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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dotnetfashionassistant.Services
{
    /// <summary>
    /// Connected Agents Orchestration Service using Azure AI Foundry.
    /// Uses one main agent with connected specialist agents for seamless collaboration.
    /// The main agent automatically delegates to connected agents without custom orchestration logic.
    /// </summary>
    public class MultiAgentOrchestrationService
    {
        private readonly string? _projectEndpoint;
        private readonly string? _mainAgentId;
        private readonly string? _externalMcpServerUrl;
        private PersistentAgentsClient? _agentsClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MultiAgentOrchestrationService> _logger;
        private bool _isConfigured = false;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();
        private readonly int _maxCacheEntries = 100;
        
        // Thread-safe caches for conversation history and user threads
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _threadHistoryCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastCacheUpdateTime = new();
        private readonly ConcurrentDictionary<string, string> _userThreads = new(); // UserId -> ThreadId

        public MultiAgentOrchestrationService(
            IConfiguration configuration, 
            ILogger<MultiAgentOrchestrationService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Get configuration values for persistent agents
            var projectEndpoint = _configuration["AI_PROJECT_ENDPOINT"] ?? 
                                Environment.GetEnvironmentVariable("AI_PROJECT_ENDPOINT") ??
                                _configuration.GetConnectionString("AI_PROJECT_ENDPOINT");
            
            var mainAgentId = _configuration["MAIN_ORCHESTRATOR_AGENT_ID"] ?? 
                             Environment.GetEnvironmentVariable("MAIN_ORCHESTRATOR_AGENT_ID");
                                         
            var externalMcpServerUrl = _configuration["EXTERNAL_MCP_SERVER_URL"] ?? 
                                      Environment.GetEnvironmentVariable("EXTERNAL_MCP_SERVER_URL");
            
            if (!string.IsNullOrEmpty(projectEndpoint) && !string.IsNullOrEmpty(mainAgentId))
            {
                _projectEndpoint = projectEndpoint;
                _mainAgentId = mainAgentId;
                _externalMcpServerUrl = externalMcpServerUrl;
                _isConfigured = true;
                _logger.LogInformation("Connected Agents service configured successfully with endpoint: {Endpoint}, Main Agent ID: {AgentId}", 
                    _projectEndpoint, _mainAgentId);
                    
                if (!string.IsNullOrEmpty(_externalMcpServerUrl))
                    _logger.LogInformation("External MCP Server URL configured: {McpServerUrl}", _externalMcpServerUrl);
            }
            else
            {
                _isConfigured = false;
                _logger.LogError("Connected Agents configuration failed - missing required settings:");
                if (string.IsNullOrEmpty(projectEndpoint)) _logger.LogError("  - AI_PROJECT_ENDPOINT is missing");
                if (string.IsNullOrEmpty(mainAgentId)) _logger.LogError("  - MAIN_ORCHESTRATOR_AGENT_ID is missing");
            }
        }
        
        /// <summary>
        /// Creates MCP tool resources for the main agent that uses MCP tools for inventory.
        /// This configures runtime authentication and approval settings for the external MCP server.
        /// Based on Azure documentation: https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/model-context-protocol-samples
        /// </summary>
        private ToolResources? CreateMcpToolResources()
        {
            if (string.IsNullOrEmpty(_externalMcpServerUrl))
            {
                _logger.LogDebug("No external MCP server URL configured, skipping MCP tool resources");
                return null;
            }

            try
            {
                // Create MCP tool resource configuration for the external inventory MCP server
                // The server label must match what was configured when creating the agent
                var mcpToolResource = new MCPToolResource("inventory_mcp");
                
                // Add any required headers for authentication to the external MCP server
                // In a production environment, you might want to add:
                // mcpToolResource.UpdateHeader("Authorization", "Bearer " + apiToken);
                // mcpToolResource.UpdateHeader("X-API-Key", apiKey);
                
                // Set approval mode for demo purposes - in production you might want "always"
                // mcpToolResource.SetApprovalMode("never"); // Uncomment to disable approval requirement
                
                // Convert to ToolResources format required by the SDK
                var toolResources = mcpToolResource.ToToolResources();
                
                _logger.LogInformation("Created MCP tool resources for external inventory MCP server with label 'inventory_mcp' pointing to: {McpServerUrl}", _externalMcpServerUrl);
                return toolResources;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create MCP tool resources, continuing without them");
                return null;
            }
        }

        /// <summary>
        /// Determines if an agent requires MCP tool resources.
        /// The main agent uses MCP tools for inventory functionality.
        /// </summary>
        private bool RequiresMcpToolResources(string agentId)
        {
            // The main agent uses MCP tools for inventory functionality
            return agentId == _mainAgentId;
        }
        
        /// <summary>
        /// Lazy initialization of the persistent agents client.
        /// No agent factories needed since we use pre-created persistent agents.
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
                        _isInitialized = true;
                        _logger.LogInformation("Persistent Agent service initialized successfully");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing Persistent Agent service client");
                }
            }
        }

        /// <summary>
        /// Creates a new thread for conversation or reuses existing thread for user.
        /// With persistent agents, threads can be reused across conversations.
        /// </summary>
        public async Task<string> CreateThreadAsync()
        {
            return await CreateThreadForUserAsync("default_user");
        }

        /// <summary>
        /// Creates or gets existing thread for a specific user.
        /// This enables per-user conversation persistence.
        /// </summary>
        public async Task<string> CreateThreadForUserAsync(string userId)
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

            // Check if user already has a thread
            if (_userThreads.TryGetValue(userId, out string? existingThreadId))
            {
                _logger.LogInformation("Reusing existing thread {ThreadId} for user {UserId}", existingThreadId, userId);
                return existingThreadId;
            }
            
            try
            {
                var threadResponse = await _agentsClient.Threads.CreateThreadAsync();
                var newThreadId = threadResponse.Value.Id;
                _userThreads.TryAdd(userId, newThreadId);
                _logger.LogInformation("Created new thread {ThreadId} for user {UserId}", newThreadId, userId);
                return newThreadId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation thread for user {UserId}", userId);
                return "agent-not-configured";
            }
        }

        /// <summary>
        /// Sends a message using the main agent with connected agents.
        /// The connected agents pattern handles delegation automatically.
        /// 
        /// DEMO FLOW with Connected Agents:
        /// 1. Send user message to main agent 
        /// 2. Main agent automatically delegates to connected specialist agents as needed
        /// 3. Connected agents coordinate and execute specialized tasks
        /// 4. Main agent compiles and returns coordinated response
        /// </summary>
        public async Task<string> SendMessageAsync(string threadId, string userMessage)
        {
            if (!_isConfigured || threadId == "agent-not-configured")
            {
                return "The AI agent system is not properly configured. Please add the required environment variable (MAIN_ORCHESTRATOR_AGENT_ID) in your application settings.";
            }
            
            EnsureInitialized();
            
            if (_agentsClient == null || string.IsNullOrEmpty(_mainAgentId))
            {
                return "The AI agent system could not be initialized. Please check the configuration and try again.";
            }
            
            try
            {
                _logger.LogInformation("Starting persistent agent conversation for message: {Message}", userMessage);
                
                // Invalidate cache for this thread since we're adding a new message
                _threadHistoryCache.TryRemove(threadId, out _);
                _lastCacheUpdateTime.TryRemove(threadId, out _);
                
                // Add the user message to the thread
                _logger.LogInformation("Adding message to thread {ThreadId}...", threadId);
                
                await _agentsClient.Messages.CreateMessageAsync(
                    threadId: threadId,
                    role: MessageRole.User,
                    content: userMessage);
                
                _logger.LogInformation("User message added to thread. Creating run with main agent {AgentId}...", _mainAgentId);
                
                // Create MCP tool resources if needed for the main agent
                ToolResources? toolResources = null;
                if (RequiresMcpToolResources(_mainAgentId))
                {
                    toolResources = CreateMcpToolResources();
                    if (toolResources != null)
                    {
                        _logger.LogInformation("Using MCP tool resources for run with main agent");
                    }
                }
                
                // Create a run with the main agent (and MCP tool resources if available)
                // Based on Azure SDK documentation, we need to get the thread and agent objects first
                var threadResponse = await _agentsClient.Threads.GetThreadAsync(threadId);
                var agentResponse = await _agentsClient.Administration.GetAgentAsync(_mainAgentId);
                
                var run = toolResources != null
                    ? await _agentsClient.Runs.CreateRunAsync(threadResponse.Value, agentResponse.Value, toolResources)
                    : await _agentsClient.Runs.CreateRunAsync(threadId, _mainAgentId);
                    
                _logger.LogInformation("Started main agent run: {RunId} on thread: {ThreadId} with MCP tools: {HasMcpTools}", 
                    run.Value.Id, threadId, toolResources != null);
                
                // Wait for the main agent to complete (including delegation to connected agents)
                var completedRun = await WaitForRunCompletionAsync(threadId, run.Value.Id);
                
                if (completedRun.Status != RunStatus.Completed)
                {
                    var errorMessage = $"The conversation failed to complete successfully. Status: {completedRun.Status}";
                    if (completedRun.LastError != null)
                    {
                        errorMessage += $" Error: {completedRun.LastError.Message}";
                    }
                    _logger.LogWarning("Persistent agent conversation failed: {Error}", errorMessage);
                    return errorMessage;
                }

                // Get the response from the main agent
                var messagesPageable = _agentsClient.Messages.GetMessagesAsync(threadId);
                var messagesList = new List<PersistentThreadMessage>();
                await foreach (var message in messagesPageable)
                {
                    messagesList.Add(message);
                }
                
                var assistantMessage = messagesList.FirstOrDefault(m => m.Role == MessageRole.Agent);
                
                if (assistantMessage?.ContentItems?.FirstOrDefault() is MessageTextContent textContent)
                {
                    _logger.LogInformation("Persistent agent conversation completed successfully");
                    
                    // Clean up any debugging artifacts that might appear in responses
                    var cleanedResponse = CleanAgentResponse(textContent.Text);
                    return cleanedResponse;
                }
                
                return "I apologize, but I couldn't generate a response through the fashion store agent system.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in persistent agent conversation for thread {ThreadId}", threadId);
                return $"Error in agent conversation: {ex.Message}. Please try again.";
            }
        }

        /// <summary>
        /// Cleans up debugging artifacts and metadata that might appear in agent responses.
        /// </summary>
        private static string CleanAgentResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            // Remove debugging artifacts with square brackets containing colons or asterisks
            // Examples: 【message_idx:search_idx*source】
            var cleanedResponse = System.Text.RegularExpressions.Regex.Replace(
                response, 
                @"【[^】]*[:*][^】]*】", 
                "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove any extra whitespace that might be left behind
            cleanedResponse = System.Text.RegularExpressions.Regex.Replace(
                cleanedResponse, 
                @"\s+", 
                " ");

            return cleanedResponse.Trim();
        }

        /// <summary>
        /// Waits for a run to complete, with appropriate logging and timeout.
        /// Handles tool approval for connected agents that use MCP or other tools.
        /// </summary>
        private async Task<ThreadRun> WaitForRunCompletionAsync(string threadId, string runId)
        {
            ThreadRun run;
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(120); // 2 minute timeout
            
            do
            {
                await Task.Delay(1000); // Wait 1 second between checks
                run = (await _agentsClient!.Runs.GetRunAsync(threadId, runId)).Value;
                
                // Log progress
                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogDebug("Run {RunId} status: {Status} (elapsed: {Elapsed}s)", 
                    runId, run.Status, elapsed.TotalSeconds);
                
                // Handle RequiresAction state for tool calls (MCP/OpenAPI)
                if (run.Status == RunStatus.RequiresAction)
                {
                    _logger.LogInformation("Run {RunId} requires action - handling tool calls", runId);
                    
                    try
                    {
                        var requiredAction = run.RequiredAction;
                        if (requiredAction is SubmitToolApprovalAction toolApprovalAction)
                        {
                            _logger.LogInformation("Processing tool approval action for run {RunId}", runId);
                            
                            var toolApprovals = new List<ToolApproval>();
                            foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                            {
                                if (toolCall is RequiredMcpToolCall mcpToolCall)
                                {
                                    _logger.LogInformation("Approving MCP tool call: {ToolName}, Arguments: {Arguments}", 
                                        mcpToolCall.Name, mcpToolCall.Arguments);
                                    
                                    // Auto-approve MCP tool calls for demo purposes
                                    // In production, you might want to add validation logic here
                                    toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                                }
                                else if (toolCall is RequiredFunctionToolCall functionToolCall)
                                {
                                    _logger.LogInformation("Approving function tool call: {ToolName}, Arguments: {Arguments}", 
                                        functionToolCall.Name, functionToolCall.Arguments);
                                    
                                    // Auto-approve function tool calls for demo purposes
                                    toolApprovals.Add(new ToolApproval(functionToolCall.Id, approve: true));
                                }
                            }

                            if (toolApprovals.Count > 0)
                            {
                                _logger.LogInformation("Submitting {Count} tool approvals for run {RunId}", toolApprovals.Count, runId);
                                await _agentsClient.Runs.SubmitToolOutputsToRunAsync(threadId, runId, toolApprovals: toolApprovals);
                            }
                        }
                        else if (requiredAction != null)
                        {
                            _logger.LogInformation("Processing other required action for run {RunId}: {ActionType}", 
                                runId, requiredAction.GetType().Name);
                            
                            // Handle other types of required actions
                            await Task.Delay(2000); // Give tools time to execute
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Issue handling RequiresAction for run {RunId}, continuing...", runId);
                    }
                }
                
                // Check for timeout
                if (elapsed > timeout)
                {
                    _logger.LogError("Run {RunId} timed out after {Timeout}s", runId, timeout.TotalSeconds);
                    
                    try
                    {
                        await _agentsClient.Runs.CancelRunAsync(threadId, runId);
                        _logger.LogInformation("Cancelled timed out run {RunId}", runId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cancel timed out run {RunId}", runId);
                    }
                    
                    break;
                }
                    
            } while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction);

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
                        Content = "The AI agent system is not properly configured. Please add the required environment variable (MAIN_ORCHESTRATOR_AGENT_ID) in your application settings.",
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
                
                _logger.LogInformation("Fetching messages for thread {ThreadId}", threadId);
                
                var messagesResponse = _agentsClient.Messages.GetMessagesAsync(
                    threadId: threadId,
                    limit: 20,
                    order: ListSortOrder.Descending
                );
                
                var messagesList = new List<PersistentThreadMessage>();
                await foreach (var message in messagesResponse)
                {
                    messagesList.Add(message);
                    _logger.LogDebug("Retrieved message: Role={Role}, CreatedAt={CreatedAt}, ContentItems={ContentCount}", 
                        message.Role, message.CreatedAt, message.ContentItems?.Count ?? 0);
                }
                
                _logger.LogInformation("Retrieved {MessageCount} raw messages from thread {ThreadId}", messagesList.Count, threadId);
                
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
                    
                    _logger.LogDebug("Processing message: Role={Role}, Content={ContentPreview}", 
                        message.Role, messageContent.Length > 50 ? messageContent.Substring(0, 50) + "..." : messageContent);
                    
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
                
                _logger.LogInformation("Processed {ChatHistoryCount} messages into chat history for thread {ThreadId}", chatHistory.Count, threadId);
                
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
                
            // Filter out error messages first
            if (content.Contains("[message_idx:") || content.Contains("*source]"))
            {
                content = System.Text.RegularExpressions.Regex.Replace(
                    content, 
                    @"\s*\[message_idx:[^\]]*\]", 
                    "");
            }
                
            // Handle markdown-style bold text (convert **text** to <strong>text</strong>)
            content = System.Text.RegularExpressions.Regex.Replace(
                content, 
                @"\*\*([^*]+)\*\*", 
                "<strong>$1</strong>");
                
            // Handle line breaks and bullet points
            return content
                .Replace("\n\n", "<br><br>")
                .Replace("\n", "<br>")
                .Replace("•", "<br>•");
        }

        /// <summary>
        /// Gets a summary of the connected agents configuration.
        /// </summary>
        public Dictionary<string, object> GetMultiAgentSystemInfo()
        {
            return new Dictionary<string, object>
            {
                ["IsConfigured"] = _isConfigured,
                ["IsInitialized"] = _isInitialized,
                ["ProjectEndpoint"] = _projectEndpoint ?? "Not configured",
                ["MainAgentId"] = _mainAgentId ?? "Not configured",
                ["ExternalMcpServerUrl"] = _externalMcpServerUrl ?? "Not configured",
                ["Architecture"] = "Connected Agents - Main Orchestrator with 4 Connected Specialist Agents",
                ["AgentDetails"] = new List<object>
                {
                    new { Name = "Main Orchestrator", Purpose = "Coordinates all customer interactions and delegates to connected agents", Tools = "Connected agent tools" },
                    new { Name = "Cart Manager (Connected)", Purpose = "Shopping cart operations", Tools = "OpenAPI (swagger.json)" },
                    new { Name = "Inventory Manager (Connected)", Purpose = "Product availability and details", Tools = "MCP (external inventory system)" },
                    new { Name = "Fashion Advisor (Connected)", Purpose = "Style recommendations", Tools = "None (LLM knowledge)" },
                    new { Name = "Content Moderator (Connected)", Purpose = "Content safety", Tools = "None (LLM knowledge)" }
                },
                ["ConnectedAgentsFlow"] = new
                {
                    UserMessage = "Sent to Main Orchestrator",
                    Delegation = "Main agent intelligently delegates to appropriate connected agents",
                    Coordination = "Connected agents work together automatically",
                    Response = "Main agent compiles and returns coordinated response"
                },
                ["ThreadManagement"] = "Per-user persistent threads",
                ["AgentType"] = "Connected Agents (main + 4 connected specialists created via Python script)",
                ["RequiredEnvironmentVariables"] = new List<string>
                {
                    "AI_PROJECT_ENDPOINT",
                    "MAIN_ORCHESTRATOR_AGENT_ID", 
                    "EXTERNAL_MCP_SERVER_URL"
                }
            };
        }

        /// <summary>
        /// Enhanced send message method that returns agent tracking information.
        /// Maintains compatibility with the UI's expected interface.
        /// </summary>
        public async Task<AgentResponse> SendMessageWithAgentTrackingAsync(string threadId, string userMessage)
        {
            var response = await SendMessageAsync(threadId, userMessage);
            
            return new AgentResponse
            {
                Content = response
            };
        }

        /// <summary>
        /// Cleanup method for thread management.
        /// With persistent agents, we can optionally clear user thread mappings.
        /// </summary>
        public async Task CleanupConversationAsync(string threadId)
        {
            // With persistent agents, we could optionally:
            // 1. Remove the thread from user mappings
            // 2. Delete the thread if desired
            // 3. Clear cached history
            
            _threadHistoryCache.TryRemove(threadId, out _);
            _lastCacheUpdateTime.TryRemove(threadId, out _);
            
            // Remove from user thread mappings
            var userToRemove = _userThreads.FirstOrDefault(kvp => kvp.Value == threadId).Key;
            if (!string.IsNullOrEmpty(userToRemove))
            {
                _userThreads.TryRemove(userToRemove, out _);
                _logger.LogDebug("Removed thread {ThreadId} mapping for user {UserId}", threadId, userToRemove);
            }
            
            await Task.CompletedTask;
            _logger.LogDebug("Cleaned up conversation data for thread {ThreadId}", threadId);
        }
    }
}