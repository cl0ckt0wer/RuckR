namespace RuckR.Shared.Models
{
    public class ClientLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string LogLevel { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }

    public class ClientLogBatch
    {
        public List<ClientLogEntry> Entries { get; set; } = new();
        public string SessionId { get; set; } = string.Empty;
    }
}
