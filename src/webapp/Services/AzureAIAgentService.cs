using Azure;
using Azure.AI.Projects;
using Azure.Identity;
using System.Collections.Generic;
using System.Threading.Tasks;
using dotnetfashionassistant.Models;
using Microsoft.Extensions.Configuration;

namespace dotnetfashionassistant.Services
{    public class AzureAIAgentService
    {
        private readonly string _connectionString;
        private readonly AgentsClient _client;
        private readonly string _agentId;
        private readonly IConfiguration _configuration;
        
        // Cache for thread history to improve performance when navigating back to home
        private readonly Dictionary<string, List<ChatMessage>> _threadHistoryCache = new();
        private readonly Dictionary<string, DateTime> _lastCacheUpdateTime = new();        public AzureAIAgentService(IConfiguration configuration)
        {
            _configuration = configuration;
              // First try to get values from environment variables
            // Then fall back to appsettings.json configuration
            
            _connectionString = Environment.GetEnvironmentVariable("AzureAIAgent__ConnectionString") ?? 
                               _configuration["AzureAIAgent:ConnectionString"] ?? 
                               throw new InvalidOperationException("Azure AI Agent connection string is not configured. Please set the AzureAIAgent__ConnectionString environment variable or AzureAIAgent:ConnectionString in appsettings.json");
                               
            _agentId = Environment.GetEnvironmentVariable("AzureAIAgent__AgentId") ?? 
                      _configuration["AzureAIAgent:AgentId"] ?? 
                      throw new InvalidOperationException("Azure AI Agent ID is not configured. Please set the AzureAIAgent__AgentId environment variable or AzureAIAgent:AgentId in appsettings.json");
              // Initialize the AI Agent client with appropriate credentials
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
        }

        public async Task<string> CreateThreadAsync()
        {
            Response<AgentThread> threadResponse = await _client.CreateThreadAsync();
            AgentThread thread = threadResponse.Value;
            return thread.Id;
        }

        public async Task<string> SendMessageAsync(string threadId, string userMessage)
        {
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
                    runResponse = await _client.GetRunAsync(threadId, runResponse.Value.Id);
                }
                while (runResponse.Value.Status == RunStatus.Queued
                    || runResponse.Value.Status == RunStatus.InProgress);

                // Get all messages in the thread
                Response<PageableList<ThreadMessage>> messagesResponse = await _client.GetMessagesAsync(threadId);
                IReadOnlyList<ThreadMessage> messages = messagesResponse.Value.Data;                // Get the latest assistant message (using historical convention of "assistant" role)
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
                            messageContent += textItem.Text;
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
