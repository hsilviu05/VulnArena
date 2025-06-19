namespace VulnArena.Models;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public UserRole Role { get; set; } = UserRole.User;
    public int TotalPoints { get; set; } = 0;
    public int SolvedChallenges { get; set; } = 0;
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public Dictionary<string, string> Preferences { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }
}

public enum UserRole
{
    User,
    Moderator,
    Admin
} 