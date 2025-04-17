using Azure;
using Azure.AI.Projects;
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
{    public class AzureAIAgentService
    {
        private readonly string? _connectionString;
        private AgentsClient? _client;
        private readonly string? _agentId;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureAIAgentService> _logger;
        private bool _isConfigured = false;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();
        private readonly int _maxCacheEntries = 100; // Configure maximum cache entries
        
        // Thread-safe caches for thread history to improve performance when navigating back to home
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _threadHistoryCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastCacheUpdateTime = new();public AzureAIAgentService(IConfiguration configuration, ILogger<AzureAIAgentService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
        // Get configuration values from environment variables (App Service configuration)
            _connectionString = Environment.GetEnvironmentVariable("AzureAIAgent__ConnectionString");
            _agentId = Environment.GetEnvironmentVariable("AzureAIAgent__AgentId");
            
            _isConfigured = !string.IsNullOrEmpty(_connectionString) && !string.IsNullOrEmpty(_agentId);
            
            if (!_isConfigured)
            {
                _logger.LogWarning("Azure AI Agent is not properly configured. Missing connection string or agent ID.");
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
                    _logger.LogInformation("Initializing AI Agent client");
                    
                    // Use ManagedIdentityCredential for Azure deployment
                    _client = new AgentsClient(_connectionString, new ManagedIdentityCredential());
                    _logger.LogDebug("Using ManagedIdentityCredential for authentication");
                    
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    // Log the exception but don't set _isConfigured to false - we might succeed next time
                    _logger.LogError(ex, "Error initializing AzureAIAgentService client");
                }
            }
        }        public async Task<string> CreateThreadAsync()
        {
            if (!_isConfigured)
            {
                _logger.LogWarning("Attempted to create thread with unconfigured AI Agent service");
                return "agent-not-configured";
            }
            
            // Initialize client on demand
            EnsureInitialized();
            
            if (_client == null)
            {
                _logger.LogWarning("Failed to initialize AI Agent client");
                return "agent-not-configured";
            }
            
            try
            {
                _logger.LogDebug("Creating new AI Agent thread");
                Response<AgentThread> threadResponse = await _client.CreateThreadAsync();
                AgentThread thread = threadResponse.Value;
                _logger.LogDebug("Successfully created thread with ID: {ThreadId}", thread.Id);
                return thread.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI Agent thread");
                return "agent-not-configured";
            }
        }        public async Task<string> SendMessageAsync(string threadId, string userMessage)
        {
            if (!_isConfigured || threadId == "agent-not-configured")
            {
                _logger.LogWarning("Attempted to send message with unconfigured AI Agent service");
                return "The AI agent is not properly configured. Please add the required environment variables (AzureAIAgent__ConnectionString and AzureAIAgent__AgentId) in your application settings.";
            }
            
            // Initialize client on demand
            EnsureInitialized();
            
            if (_client == null || _agentId == null)
            {
                _logger.LogWarning("Failed to initialize AI Agent client");
                return "The AI agent client could not be initialized. Please check the configuration and try again.";
            }
            
            try
            {
                _logger.LogDebug("Sending message to thread {ThreadId}", threadId);
                
                // Send user message to the thread
                _logger.LogDebug("Creating message in thread {ThreadId}: {Message}", threadId, userMessage);
                Response<ThreadMessage> messageResponse = await _client.CreateMessageAsync(
                    threadId,
                    MessageRole.User,
                    userMessage);

                // Create and run a request with the agent
                _logger.LogDebug("Creating run for thread {ThreadId} with agent {AgentId}", threadId, _agentId);
                Response<ThreadRun> runResponse = await _client.CreateRunAsync(
                    threadId,
                    _agentId);

                ThreadRun run = runResponse.Value;
                _logger.LogDebug("Run {RunId} created for thread {ThreadId}", run.Id, threadId);

                // Poll until the run reaches a terminal status
                int pollCount = 0;
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    runResponse = await _client.GetRunAsync(threadId, run.Id);
                    pollCount++;
                    
                    if (pollCount % 10 == 0)  // Log every 5 seconds
                    {
                        _logger.LogDebug("Waiting for run {RunId} to complete. Current status: {Status}", 
                            run.Id, runResponse.Value.Status);
                    }
                }
                while (runResponse.Value.Status == RunStatus.Queued
                    || runResponse.Value.Status == RunStatus.InProgress);

                _logger.LogDebug("Run {RunId} completed with status {Status}", run.Id, runResponse.Value.Status);

                // Get all messages in the thread
                _logger.LogDebug("Retrieving messages for thread {ThreadId}", threadId);
                Response<PageableList<ThreadMessage>> messagesResponse = await _client.GetMessagesAsync(threadId);
                IReadOnlyList<ThreadMessage> messages = messagesResponse.Value.Data;
                
                // Get the latest assistant message (using historical convention of "assistant" role)
                ThreadMessage? latestAssistantMessage = messages
                    .Where(m => m.Role.ToString().Equals("Assistant", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                if (latestAssistantMessage != null)
                {
                    _logger.LogDebug("Found assistant response in thread {ThreadId}, message ID: {MessageId}", 
                        threadId, latestAssistantMessage.Id);
                    
                    string responseText = "";
                    foreach (MessageContent contentItem in latestAssistantMessage.ContentItems)
                    {
                        if (contentItem is MessageTextContent textItem)
                        {
                            responseText += textItem.Text;
                        }
                    }
                    return responseText;
                }

                _logger.LogWarning("No assistant response found in thread {ThreadId}", threadId);
                return "No response from AI agent.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error communicating with AI agent for thread {ThreadId}", threadId);
                return $"Error communicating with AI agent: {ex.Message}";
            }
        }public async Task<List<ChatMessage>> GetThreadHistoryAsync(string threadId)
        {
            if (!_isConfigured || threadId == "agent-not-configured")
            {
                return new List<ChatMessage> {
                    new ChatMessage {
                        Content = "The AI agent is not properly configured. Please add the required environment variables (AzureAIAgent__ConnectionString and AzureAIAgent__AgentId) in your application settings.",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    }
                };
            }
            
            // Initialize client on demand
            EnsureInitialized();
            
            if (_client == null)
            {
                return new List<ChatMessage> {
                    new ChatMessage {
                        Content = "The AI agent client could not be initialized. Please check the configuration and try again.",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    }
                };
            }
            
            _logger.LogDebug("Getting thread history for thread {ThreadId}", threadId);
            
            // Check if we already have this thread history cached and it's recent (less than 1 minute old)
            if (_threadHistoryCache.TryGetValue(threadId, out var cachedHistory) && 
                _lastCacheUpdateTime.TryGetValue(threadId, out var lastUpdate) &&
                (DateTime.UtcNow - lastUpdate).TotalMinutes < 1)
            {
                _logger.LogDebug("Using cached thread history for {ThreadId}, last updated at {LastUpdate}", threadId, lastUpdate);
                // Return the cached history if it exists and is recent
                return new List<ChatMessage>(cachedHistory);
            }
            
            var chatHistory = new List<ChatMessage>();
            
            try
            {
                _logger.LogDebug("Fetching fresh thread history from AI Agent service for {ThreadId}", threadId);
                // Set a cancellation timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                Response<PageableList<ThreadMessage>> messagesResponse = await _client.GetMessagesAsync(threadId, cancellationToken: cts.Token);
                IReadOnlyList<ThreadMessage> messages = messagesResponse.Value.Data;
                _logger.LogDebug("Retrieved {MessageCount} messages for thread {ThreadId}", messages.Count, threadId);

                foreach (ThreadMessage message in messages.OrderBy(m => m.CreatedAt))
                {
                    string messageContent = "";
                    foreach (MessageContent contentItem in message.ContentItems)
                    {
                        if (contentItem is MessageTextContent textItem)
                        {
                            messageContent += textItem.Text ?? string.Empty;
                        }
                    }
                    
                    chatHistory.Add(new ChatMessage
                    {
                        Content = messageContent,
                        IsUser = message.Role == MessageRole.User,
                        Timestamp = message.CreatedAt.DateTime
                    });
                }
                
                // Implement cache eviction policy - if cache exceeds limit, remove oldest entries
                if (_threadHistoryCache.Count >= _maxCacheEntries)
                {
                    _logger.LogInformation("Cache limit reached ({CacheSize}), evicting oldest entries", _threadHistoryCache.Count);
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
                        _logger.LogDebug("Removed cache entry for thread {ThreadId}", entry.Key);
                    }
                }
                
                // Update the cache using thread-safe methods
                _threadHistoryCache.AddOrUpdate(threadId, new List<ChatMessage>(chatHistory), 
                    (key, oldValue) => new List<ChatMessage>(chatHistory));
                _lastCacheUpdateTime.AddOrUpdate(threadId, DateTime.UtcNow, 
                    (key, oldValue) => DateTime.UtcNow);            }
            catch (OperationCanceledException ex)
            {
                // If the operation timed out, return cached data if available or empty list
                _logger.LogWarning(ex, "Thread history operation timed out for thread {ThreadId}", threadId);
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
    }
}
