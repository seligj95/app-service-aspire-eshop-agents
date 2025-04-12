using System;

namespace dotnetfashionassistant.Services
{
    /// <summary>
    /// Service to maintain the state of AI Agent mode across the application.
    /// </summary>
    public class AgentModeService
    {
        /// <summary>
        /// Gets or sets a value indicating whether the application is using Azure AI Agent mode.
        /// </summary>
        public bool UseAzureAIAgent { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the current AI Agent thread ID for maintaining conversation state.
        /// </summary>
        public string? CurrentThreadId { get; set; }
        
        /// <summary>
        /// Event that is raised when the agent mode changes.
        /// </summary>
        public event Action? OnAgentModeChanged;
        
        /// <summary>
        /// Changes the agent mode and notifies subscribers.
        /// </summary>
        /// <param name="useAzureAIAgent">Whether to use Azure AI Agent mode.</param>
        public void SetAgentMode(bool useAzureAIAgent)
        {
            UseAzureAIAgent = useAzureAIAgent;
            OnAgentModeChanged?.Invoke();
        }
    }
}
