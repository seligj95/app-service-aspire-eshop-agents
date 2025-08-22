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
    public class AzureAIAgentService
    {
        private readonly string? _projectEndpoint;
        private PersistentAgentsClient? _agentsClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureAIAgentService> _logger;
        private bool _isConfigured = false;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();
        private readonly int _maxCacheEntries = 100; // Configure maximum cache entries
        
        // Thread-safe caches for thread history to improve performance when navigating back to home
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _threadHistoryCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastCacheUpdateTime = new();

        public AzureAIAgentService(IConfiguration configuration, ILogger<AzureAIAgentService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Get configuration values from the new AI Foundry environment variables
            var projectEndpoint = _configuration["AI_PROJECT_ENDPOINT"] ?? 
                                Environment.GetEnvironmentVariable("AI_PROJECT_ENDPOINT") ??
                                _configuration.GetConnectionString("AI_PROJECT_ENDPOINT");
            
            // For Azure.AI.Agents.Persistent v1.1.0+, we use the endpoint format
            if (!string.IsNullOrEmpty(projectEndpoint))
            {
                _projectEndpoint = projectEndpoint;
            }
            
            _isConfigured = !string.IsNullOrEmpty(_projectEndpoint);
            
            if (!_isConfigured)
            {
                _logger.LogError("Azure AI Agent configuration failed: AI_PROJECT_ENDPOINT is missing");
            }
        }
        
        // Lazy initialization of the client only when actually needed
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
                    // For Azure.AI.Agents.Persistent v1.1.0+, use the endpoint as string
                    if (!string.IsNullOrEmpty(_projectEndpoint))
                    {
                        _agentsClient = new PersistentAgentsClient(_projectEndpoint, new DefaultAzureCredential());
                        _isInitialized = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing AzureAIAgentService client");
                }
            }
        }

        public async Task<string> CreateThreadAsync()
        {
            if (!_isConfigured)
            {
                return "agent-not-configured";
            }
            
            // Initialize client on demand
            EnsureInitialized();
            
            if (_agentsClient == null)
            {
                return "agent-initialization-failed";
            }
            
            try
            {
                var threadResponse = await _agentsClient.Threads.CreateThreadAsync();
                return threadResponse.Value.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI Agent thread");
                return "agent-not-configured";
            }
        }

        public async Task<string> SendMessageAsync(string threadId, string userMessage)
        {
            if (!_isConfigured || threadId == "agent-not-configured")
            {
                return "The AI agent is not properly configured. Please add the required environment variable (AI_PROJECT_ENDPOINT) in your application settings.";
            }
            
            // Initialize client on demand
            EnsureInitialized();
            
            if (_agentsClient == null)
            {
                return "The AI agent client could not be initialized. Please check the configuration and try again.";
            }
            
            PersistentAgent? agent = null;
            
            try
            {
                // Create agent dynamically for this request
                agent = await CreateAgentAsync();
                if (agent == null)
                {
                    return "Failed to create AI agent for this request.";
                }

                // Create a user message
                var userMessageOptions = new ThreadMessageOptions(MessageRole.User, userMessage);
                
                // Create and run in one step with the user message
                var threadOptions = new PersistentAgentThreadCreationOptions();
                threadOptions.Messages.Add(userMessageOptions);
                
                var threadAndRunOptions = new ThreadAndRunOptions
                {
                    ThreadOptions = threadOptions
                };
                
                var run = await _agentsClient.CreateThreadAndRunAsync(agent.Id, threadAndRunOptions);
                
                // Wait for completion
                var completedRun = await WaitForRunCompletionAsync(run.Value.ThreadId, run.Value.Id);
                
                // Get the messages from the thread
                var messagesPageable = _agentsClient.Messages.GetMessagesAsync(run.Value.ThreadId);
                var messagesList = new List<PersistentThreadMessage>();
                await foreach (var message in messagesPageable)
                {
                    messagesList.Add(message);
                }
                var assistantMessage = messagesList.FirstOrDefault(m => m.Role == MessageRole.Agent);
                
                if (assistantMessage?.ContentItems?.FirstOrDefault() is MessageTextContent textContent)
                {
                    return textContent.Text;
                }
                
                return "I apologize, but I couldn't generate a response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error communicating with AI agent for thread {ThreadId}", threadId);
                return $"Error communicating with AI agent: {ex.Message}";
            }
            finally
            {
                // Clean up - delete the agent
                if (agent != null)
                {
                    try
                    {
                        await _agentsClient!.Administration.DeleteAgentAsync(agent.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting agent {AgentId}", agent.Id);
                    }
                }
            }
        }

        private async Task<PersistentAgent?> CreateAgentAsync()
        {
            try
            {
                // Get the model deployment name from environment
                var modelDeployment = _configuration["AI_MODEL_DEPLOYMENT_NAME"] ?? Environment.GetEnvironmentVariable("AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o";
                
                // Create agent with basic instructions for a fashion assistant
                var agentResponse = await _agentsClient!.Administration.CreateAgentAsync(
                    model: modelDeployment,
                    name: "fashion-assistant",
                    description: "A helpful fashion assistant for recommendations and shopping advice.",
                    instructions: @"You are a helpful fashion assistant. You can help users with:
1. Finding products in the inventory
2. Managing their shopping cart
3. Providing fashion advice and recommendations
4. Answering questions about available items

Be friendly, helpful, and knowledgeable about fashion. If you need to access inventory or cart information, let the user know what you found or if you need more details to help them better.");

                return agentResponse.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating agent");
                return null;
            }
        }

        private async Task<ThreadRun> WaitForRunCompletionAsync(string threadId, string runId)
        {
            ThreadRun run;
            do
            {
                await Task.Delay(1000); // Wait 1 second between checks
                run = (await _agentsClient!.Runs.GetRunAsync(threadId, runId)).Value;
            } while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

            return run;
        }

        public async Task<List<ChatMessage>> GetThreadHistoryAsync(string threadId)
        {
            if (!_isConfigured || threadId == "agent-not-configured")
            {
                return new List<ChatMessage> {
                    new ChatMessage {
                        Content = "The AI agent is not properly configured. Please add the required environment variable (AI_PROJECT_ENDPOINT) in your application settings.",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    }
                };
            }
            
            // Initialize client on demand
            EnsureInitialized();
            
            if (_agentsClient == null)
            {
                return new List<ChatMessage> {
                    new ChatMessage {
                        Content = "The AI agent client could not be initialized. Please check the configuration and try again.",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    }
                };
            }

            // Check if we already have this thread history cached and is recent (less than 30 seconds old)
            if (_threadHistoryCache.TryGetValue(threadId, out var cachedHistory) && 
                _lastCacheUpdateTime.TryGetValue(threadId, out var lastUpdate) &&
                (DateTime.UtcNow - lastUpdate).TotalSeconds < 30)
            {
                return new List<ChatMessage>(cachedHistory);
            }
            
            var chatHistory = new List<ChatMessage>();
            
            try
            {
                // Set a cancellation timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                
                // Get all messages
                var messagesResponse = _agentsClient.Messages.GetMessagesAsync(
                    threadId: threadId,
                    limit: 20,
                    order: ListSortOrder.Descending
                );
                
                // Process messages in chronological order (oldest to newest)
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
                    
                    // For AI messages, format the content for proper HTML display
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
                
                // Implement cache eviction policy - if cache exceeds limit, remove oldest entries
                if (_threadHistoryCache.Count >= _maxCacheEntries)
                {
                    // Get oldest entries based on last update time
                    var oldestEntries = _lastCacheUpdateTime
                        .OrderBy(x => x.Value)
                        .Take(_threadHistoryCache.Count - _maxCacheEntries + 1)
                        .ToList();
                    
                    // Remove oldest entries
                    foreach (var entry in oldestEntries)
                    {
                        _threadHistoryCache.TryRemove(entry.Key, out _);
                        _lastCacheUpdateTime.TryRemove(entry.Key, out _);
                    }
                }
                
                // Update the cache using thread-safe methods
                _threadHistoryCache.AddOrUpdate(threadId, new List<ChatMessage>(chatHistory), 
                    (key, oldValue) => new List<ChatMessage>(chatHistory));
                _lastCacheUpdateTime.AddOrUpdate(threadId, DateTime.UtcNow, 
                    (key, oldValue) => DateTime.UtcNow);
            }
            catch (OperationCanceledException)
            {
                // If the operation timed out, return cached data if available or empty list
                return cachedHistory ?? new List<ChatMessage>();
            }
            catch (Exception ex)
            {
                // If there's an error, return cached data if available or empty list
                _logger.LogError(ex, "Error retrieving thread history for thread {ThreadId}", threadId);
                return cachedHistory ?? new List<ChatMessage>();
            }

            return chatHistory;
        }
        
        // Helper method to format message content with HTML tags
        private string FormatMessageContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;
                
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
    }
}
