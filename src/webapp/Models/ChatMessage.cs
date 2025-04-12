using System;

namespace dotnetfashionassistant.Models
{
    /// <summary>
    /// Represents a message in the chat interface.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets a value indicating whether the message is from the user (true) or the AI (false).
        /// </summary>
        public bool IsUser { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
