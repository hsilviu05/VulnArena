namespace VulnArena.Models;

public class LogEntry
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? ChallengeId { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public LogLevel Level { get; set; } = LogLevel.Information;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error,
    Critical
} 