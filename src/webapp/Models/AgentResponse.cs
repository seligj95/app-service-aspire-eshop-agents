namespace dotnetfashionassistant.Models
{
    /// <summary>
    /// Represents a response from an AI agent with tracking information.
    /// </summary>
    public class AgentResponse
    {
        /// <summary>
        /// The content/message returned by the agent.
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional agent identifier that handled the request.
        /// </summary>
        public string? AgentId { get; set; }
        
        /// <summary>
        /// Optional thread identifier for conversation tracking.
        /// </summary>
        public string? ThreadId { get; set; }
        
        /// <summary>
        /// Timestamp when the response was generated.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}