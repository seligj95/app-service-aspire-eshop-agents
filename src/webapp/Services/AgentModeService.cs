using System;

namespace dotnetfashionassistant.Services
{
    /// <summary>
    /// Service to maintain the multi-agent conversation state across the application.
    /// </summary>
    public class AgentModeService
    {
        private string? _currentThreadId;
        
        /// <summary>
        /// Gets or sets the current multi-agent thread ID for maintaining conversation state.
        /// </summary>
        public string? CurrentThreadId 
        { 
            get 
            {
                return _currentThreadId;
            }
            set 
            {
                _currentThreadId = value;
                ThreadIdChanged?.Invoke(value);
            }
        }
        
        /// <summary>
        /// Event raised when the thread ID changes.
        /// </summary>
        public event Action<string?>? ThreadIdChanged;
        
        /// <summary>
        /// Clear the current thread ID to start a new conversation.
        /// </summary>
        public void ClearCurrentThread()
        {
            CurrentThreadId = null;
        }
    }
}
