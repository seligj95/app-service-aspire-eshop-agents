using System;

namespace dotnetfashionassistant.Services
{
    /// <summary>
    /// Service to maintain the state of AI Agent across the application.
    /// </summary>
    public class AgentModeService
    {
        /// <summary>
        /// Gets or sets the current AI Agent thread ID for maintaining conversation state.
        /// </summary>
        public string? CurrentThreadId { get; set; }
        
        // Always using Azure AI Agent Mode, so this is now always true
        public bool UseAzureAIAgent { get; } = true;
    }
}
