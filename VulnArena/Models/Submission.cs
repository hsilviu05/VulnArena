namespace VulnArena.Models;

public class Submission
{
    public string Id { get; set; } = string.Empty;
    public string ChallengeId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SubmittedFlag { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } = false;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public int? PointsAwarded { get; set; }
    public DateTime? PointsAwardedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
} 