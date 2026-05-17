namespace RuckR.Shared.Models
{
    /// <summary>
    /// Represents a single client-side log event sent from the app.
    /// </summary>
    public class ClientLogEntry
    {
        /// <summary>Time when the log entry was generated.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Log severity level.</summary>
        public string LogLevel { get; set; } = string.Empty;

        /// <summary>Log category for routing or filtering.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Primary message payload.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Optional serialized exception details.</summary>
        public string? Exception { get; set; }

        /// <summary>Optional current page URL that generated the event.</summary>
        public string? Url { get; set; }

        /// <summary>Optional user-agent from the requesting client.</summary>
        public string? UserAgent { get; set; }
    }

    /// <summary>
    /// A batch payload for client log transport.
    /// </summary>
    public class ClientLogBatch
    {
        /// <summary>Log entries included in this batch.</summary>
        public List<ClientLogEntry> Entries { get; set; } = new();

        /// <summary>Client session identifier for traceability.</summary>
        public string SessionId { get; set; } = string.Empty;
    }
}
