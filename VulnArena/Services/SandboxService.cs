using VulnArena.Models;
using VulnArena.Core;

namespace VulnArena.Services;

public class SandboxService
{
    private readonly ILogger<SandboxService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ContainerService _containerService;
    private readonly DBService _dbService;
    private readonly LoggingService _loggingService;
    private readonly Dictionary<string, SandboxInstance> _activeSandboxes;
    private readonly Timer _cleanupTimer;

    public SandboxService(
        ILogger<SandboxService> logger,
        IConfiguration configuration,
        ContainerService containerService,
        DBService dbService,
        LoggingService loggingService)
    {
        _logger = logger;
        _configuration = configuration;
        _containerService = containerService;
        _dbService = dbService;
        _loggingService = loggingService;
        _activeSandboxes = new Dictionary<string, SandboxInstance>();

        // Cleanup expired sandboxes every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredSandboxes, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<SandboxResult> StartSandboxAsync(string challengeId, string userId)
    {
        try
        {
            // Check if sandbox already exists
            var sandboxKey = $"{challengeId}_{userId}";
            if (_activeSandboxes.TryGetValue(sandboxKey, out var existingSandbox))
            {
                if (existingSandbox.ExpiresAt > DateTime.UtcNow)
                {
                    _logger.LogInformation("Sandbox already exists for challenge {ChallengeId} and user {UserId}", challengeId, userId);
                    return new SandboxResult
                    {
                        Success = true,
                        SandboxId = existingSandbox.Id,
                        ContainerId = existingSandbox.ContainerId,
                        Port = existingSandbox.Port,
                        ExpiresAt = existingSandbox.ExpiresAt
                    };
                }
                else
                {
                    // Remove expired sandbox
                    _activeSandboxes.Remove(sandboxKey);
                }
            }

            // Get challenge details
            var challenge = await _dbService.GetChallengeAsync(challengeId);
            if (challenge == null)
            {
                return new SandboxResult { Success = false, Message = "Challenge not found." };
            }

            if (!challenge.RequiresContainer)
            {
                return new SandboxResult { Success = false, Message = "Challenge does not require a sandbox." };
            }

            // Start container
            var containerId = await _containerService.StartContainerAsync(challengeId, userId);
            if (string.IsNullOrEmpty(containerId))
            {
                return new SandboxResult { Success = false, Message = "Failed to start container." };
            }

            // Create sandbox instance
            var sandbox = new SandboxInstance
            {
                Id = Guid.NewGuid().ToString(),
                ChallengeId = challengeId,
                UserId = userId,
                ContainerId = containerId,
                Port = challenge.ContainerPort ?? GetRandomPort(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(2), // 2 hour timeout
                Status = SandboxStatus.Running
            };

            _activeSandboxes[sandboxKey] = sandbox;

            // Log the sandbox creation
            await _loggingService.LogSystemEventAsync("SANDBOX_STARTED", $"Container: {containerId}, Port: {sandbox.Port}", Models.LogLevel.Information);

            _logger.LogInformation("Started sandbox {SandboxId} for challenge {ChallengeId} and user {UserId}", 
                sandbox.Id, challengeId, userId);

            return new SandboxResult
            {
                Success = true,
                SandboxId = sandbox.Id,
                ContainerId = containerId,
                Port = sandbox.Port,
                ExpiresAt = sandbox.ExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting sandbox for challenge {ChallengeId}", challengeId);
            return new SandboxResult { Success = false, Message = "Failed to start sandbox." };
        }
    }

    public async Task<bool> StopSandboxAsync(string challengeId, string userId)
    {
        try
        {
            var sandboxKey = $"{challengeId}_{userId}";
            if (!_activeSandboxes.TryGetValue(sandboxKey, out var sandbox))
            {
                _logger.LogInformation("No sandbox found for challenge {ChallengeId} and user {UserId}", challengeId, userId);
                return true;
            }

            // Stop container
            await _containerService.StopContainerAsync(challengeId, userId);

            // Update sandbox status
            sandbox.Status = SandboxStatus.Stopped;
            sandbox.StoppedAt = DateTime.UtcNow;

            // Remove from active sandboxes
            _activeSandboxes.Remove(sandboxKey);

            // Log the sandbox stop
            await _loggingService.LogSystemEventAsync("SANDBOX_STOPPED", $"Container: {sandbox.ContainerId}", Models.LogLevel.Information);

            _logger.LogInformation("Stopped sandbox {SandboxId} for challenge {ChallengeId} and user {UserId}", 
                sandbox.Id, challengeId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping sandbox for challenge {ChallengeId}", challengeId);
            return false;
        }
    }

    public async Task<SandboxInstance?> GetSandboxAsync(string challengeId, string userId)
    {
        var sandboxKey = $"{challengeId}_{userId}";
        return _activeSandboxes.TryGetValue(sandboxKey, out var sandbox) ? sandbox : null;
    }

    public async Task<IEnumerable<SandboxInstance>> GetActiveSandboxesAsync()
    {
        return _activeSandboxes.Values.Where(s => s.Status == SandboxStatus.Running && s.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<bool> ExtendSandboxAsync(string challengeId, string userId, TimeSpan extension)
    {
        try
        {
            var sandboxKey = $"{challengeId}_{userId}";
            if (!_activeSandboxes.TryGetValue(sandboxKey, out var sandbox))
            {
                return false;
            }

            sandbox.ExpiresAt = sandbox.ExpiresAt.Add(extension);
            sandbox.ExtensionCount++;

            await _loggingService.LogSystemEventAsync("SANDBOX_EXTENDED", $"Extended by {extension.TotalMinutes} minutes, new expiry: {sandbox.ExpiresAt}", Models.LogLevel.Information);

            _logger.LogInformation("Extended sandbox {SandboxId} by {Extension} minutes", sandbox.Id, extension.TotalMinutes);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending sandbox for challenge {ChallengeId}", challengeId);
            return false;
        }
    }

    public async Task<bool> IsSandboxHealthyAsync(string challengeId, string userId)
    {
        try
        {
            var sandbox = await GetSandboxAsync(challengeId, userId);
            if (sandbox == null)
            {
                return false;
            }

            if (sandbox.ExpiresAt < DateTime.UtcNow)
            {
                return false;
            }

            // Check container health
            return await _containerService.IsContainerHealthyAsync(sandbox.ContainerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking sandbox health for challenge {ChallengeId}", challengeId);
            return false;
        }
    }

    public async Task<SandboxStatistics> GetSandboxStatisticsAsync()
    {
        try
        {
            var activeSandboxes = await GetActiveSandboxesAsync();
            var totalSandboxes = _activeSandboxes.Count;
            var expiredSandboxes = _activeSandboxes.Values.Count(s => s.ExpiresAt < DateTime.UtcNow);

            var challengeBreakdown = activeSandboxes
                .GroupBy(s => s.ChallengeId)
                .ToDictionary(g => g.Key, g => g.Count());

            var userBreakdown = activeSandboxes
                .GroupBy(s => s.UserId)
                .ToDictionary(g => g.Key, g => g.Count());

            return new SandboxStatistics
            {
                ActiveSandboxes = activeSandboxes.Count(),
                TotalSandboxes = totalSandboxes,
                ExpiredSandboxes = expiredSandboxes,
                ChallengeBreakdown = challengeBreakdown,
                UserBreakdown = userBreakdown
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sandbox statistics");
            return new SandboxStatistics();
        }
    }

    private void CleanupExpiredSandboxes(object? state)
    {
        _ = CleanupExpiredSandboxesAsync();
    }

    private async Task CleanupExpiredSandboxesAsync()
    {
        try
        {
            var expiredSandboxes = _activeSandboxes
                .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow)
                .ToList();

            foreach (var kvp in expiredSandboxes)
            {
                var sandbox = kvp.Value;
                _logger.LogInformation("Cleaning up expired sandbox {SandboxId} for challenge {ChallengeId} and user {UserId}", 
                    sandbox.Id, sandbox.ChallengeId, sandbox.UserId);

                // Stop the container
                await _containerService.StopContainerAsync(sandbox.ChallengeId, sandbox.UserId);

                // Update sandbox status
                sandbox.Status = SandboxStatus.Expired;
                sandbox.StoppedAt = DateTime.UtcNow;

                // Log the cleanup
                await _loggingService.LogSystemEventAsync("SANDBOX_EXPIRED", $"Container: {sandbox.ContainerId}, Runtime: {DateTime.UtcNow - sandbox.CreatedAt}", Models.LogLevel.Information);

                // Remove from active sandboxes
                _activeSandboxes.Remove(kvp.Key);
            }

            if (expiredSandboxes.Any())
            {
                _logger.LogInformation("Cleaned up {Count} expired sandboxes", expiredSandboxes.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired sandboxes");
        }
    }

    private int GetRandomPort()
    {
        var random = new Random();
        return random.Next(10000, 65535);
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

public class SandboxResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SandboxId { get; set; }
    public string? ContainerId { get; set; }
    public int? Port { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class SandboxInstance
{
    public string Id { get; set; } = string.Empty;
    public string ChallengeId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public SandboxStatus Status { get; set; }
    public int ExtensionCount { get; set; } = 0;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum SandboxStatus
{
    Running,
    Stopped,
    Expired,
    Error
}

public class SandboxStatistics
{
    public int ActiveSandboxes { get; set; }
    public int TotalSandboxes { get; set; }
    public int ExpiredSandboxes { get; set; }
    public Dictionary<string, int> ChallengeBreakdown { get; set; } = new();
    public Dictionary<string, int> UserBreakdown { get; set; } = new();
} 