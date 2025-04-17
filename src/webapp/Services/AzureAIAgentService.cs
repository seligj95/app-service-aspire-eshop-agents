using Azure;
using Azure.AI.Projects;
using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using dotnetfashionassistant.Models;
using Microsoft.Extensions.Configuration;

namespace dotnetfashionassistant.Services
{    public class AzureAIAgentService
    {
        private readonly string? _connectionString;
        private readonly AgentsClient? _client;
        private readonly string? _agentId;
        private readonly IConfiguration _configuration;
        private readonly bool _isConfigured = false;
        
        // Cache for thread history to improve performance when navigating back to home
        private readonly Dictionary<string, List<ChatMessage>> _threadHistoryCache = new();
        private readonly Dictionary<string, DateTime> _lastCacheUpdateTime = new();        public AzureAIAgentService(IConfiguration configuration)
        {
            _configuration = configuration;
            
            try
            {
                // First try to get values from environment variables
                // Then fall back to appsettings.json configuration
                _connectionString = Environment.GetEnvironmentVariable("AzureAIAgent__ConnectionString") ?? 
                                  _configuration["AzureAIAgent:ConnectionString"];
                               
                _agentId = Environment.GetEnvironmentVariable("AzureAIAgent__AgentId") ?? 
                         _configuration["AzureAIAgent:AgentId"];
                
                // Only initialize the client if both values are available
                if (!string.IsNullOrEmpty(_connectionString) && !string.IsNullOrEmpty(_agentId))
                {
                    // For local development vs. Azure deployment
                    var isRunningInAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
                    
                    if (isRunningInAzure)
                    {
                        // In Azure, use ManagedIdentityCredential directly to avoid the DefaultAzureCredential fallback chain
                        _client = new AgentsClient(_connectionString, new ManagedIdentityCredential());
                    }
                    else
                    {
                        // For local development, use DefaultAzureCredential (will try multiple methods)
                        _client = new AgentsClient(_connectionString, new DefaultAzureCredential());
                    }
                    
                    _isConfigured = true;
                }
            }
            catch (Exception)
            {
                // Silently fail initialization - service will be in unconfigured state
                _isConfigured = false;
            }
        }        public async Task<string> CreateThreadAsync()
        {
            if (!_isConfigured || _client == null)
            {
                return "agent-not-configured";
            }
            
            try
            {
                Response<AgentThread> threadResponse = await _client.CreateThreadAsync();
                AgentThread thread = threadResponse.Value;
                return thread.Id;
            }
            catch (Exception)
            {
                return "agent-not-configured";
            }
        }        public async Task<string> SendMessageAsync(string threadId, string userMessage)
        {
            if (!_isConfigured || _client == null || _agentId == null || threadId == "agent-not-configured")
            {
                return "The AI agent is not properly configured. Please add the required environment variables (AzureAIAgent__ConnectionString and AzureAIAgent__AgentId) in your application settings.";
            }
            
            try
            {
                // Send user message to the thread
                Response<ThreadMessage> messageResponse = await _client.CreateMessageAsync(
                    threadId,
                    MessageRole.User,
                    userMessage);

                // Create and run a request with the agent
                Response<ThreadRun> runResponse = await _client.CreateRunAsync(
                    threadId,
                    _agentId);

                ThreadRun run = runResponse.Value;

                // Poll until the run reaches a terminal status
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    runResponse = await _client.GetRunAsync(threadId, run.Id);
                }
                while (runResponse.Value.Status == RunStatus.Queued
                    || runResponse.Value.Status == RunStatus.InProgress);

                // Get all messages in the thread
                Response<PageableList<ThreadMessage>> messagesResponse = await _client.GetMessagesAsync(threadId);
                IReadOnlyList<ThreadMessage> messages = messagesResponse.Value.Data;
                
                // Get the latest assistant message (using historical convention of "assistant" role)
                ThreadMessage? latestAssistantMessage = messages
                    .Where(m => m.Role.ToString().Equals("Assistant", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                if (latestAssistantMessage != null)
                {
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

                return "No response from AI agent.";
            }
            catch (Exception ex)
            {
                return $"Error communicating with AI agent: {ex.Message}";
            }
        }        public async Task<List<ChatMessage>> GetThreadHistoryAsync(string threadId)
        {
            if (!_isConfigured || _client == null || threadId == "agent-not-configured")
            {
                return new List<ChatMessage> {
                    new ChatMessage {
                        Content = "The AI agent is not properly configured. Please add the required environment variables (AzureAIAgent__ConnectionString and AzureAIAgent__AgentId) in your application settings.",
                        IsUser = false,
                        Timestamp = DateTime.Now
                    }
                };
            }
            
            // Check if we already have this thread history cached and it's recent (less than 1 minute old)
            if (_threadHistoryCache.TryGetValue(threadId, out var cachedHistory) && 
                _lastCacheUpdateTime.TryGetValue(threadId, out var lastUpdate) &&
                (DateTime.UtcNow - lastUpdate).TotalMinutes < 1)
            {
                // Return the cached history if it exists and is recent
                return new List<ChatMessage>(cachedHistory);
            }
            
            var chatHistory = new List<ChatMessage>();
            
            try
            {
                // Set a cancellation timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                Response<PageableList<ThreadMessage>> messagesResponse = await _client.GetMessagesAsync(threadId, cancellationToken: cts.Token);
                IReadOnlyList<ThreadMessage> messages = messagesResponse.Value.Data;

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
                
                // Update the cache
                _threadHistoryCache[threadId] = new List<ChatMessage>(chatHistory);
                _lastCacheUpdateTime[threadId] = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                // If the operation timed out, return cached data if available or empty list
                return cachedHistory ?? new List<ChatMessage>();
            }
            catch (Exception)
            {
                // If there's an error, return cached data if available or empty list
                return cachedHistory ?? new List<ChatMessage>();
            }

            return chatHistory;
        }
    }
}
