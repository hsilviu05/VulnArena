using System.Text.Json.Serialization;
using VulnArena.Core;

namespace VulnArena.Models;

public class Challenge
{
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
    
    public string Path { get; set; } = string.Empty;
    
    [JsonPropertyName("flag")]
    public string Flag { get; set; } = string.Empty;
    
    [JsonPropertyName("flagType")]
    public FlagType FlagType { get; set; } = FlagType.PlainText;
    
    [JsonPropertyName("difficulty")]
    public ChallengeDifficulty Difficulty { get; set; } = ChallengeDifficulty.Easy;
    
    [JsonPropertyName("points")]
    public int Points { get; set; } = 100;
    
    [JsonPropertyName("requiresContainer")]
    public bool RequiresContainer { get; set; } = false;
    
    [JsonPropertyName("containerImage")]
    public string? ContainerImage { get; set; }
    
    [JsonPropertyName("containerPort")]
    public int? ContainerPort { get; set; }
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
    
    [JsonPropertyName("author")]
    public string? Author { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
    
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    
    [JsonPropertyName("solveCount")]
    public int SolveCount { get; set; } = 0;
    
    [JsonPropertyName("hint")]
    public string? Hint { get; set; }
    
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
} 