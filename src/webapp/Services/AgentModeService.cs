using System;

namespace dotnetfashionassistant.Services
{
    /// <summary>
    /// Service to maintain the multi-agent conversation state across the application.
    /// HANDS-ON DEMO: This service persists the thread ID for multi-agent conversations.
    /// </summary>
    public class AgentModeService
    {
        /// <summary>
        /// Gets or sets the current multi-agent thread ID for maintaining conversation state.
        /// </summary>
        public string? CurrentThreadId { get; set; }
    }
}
