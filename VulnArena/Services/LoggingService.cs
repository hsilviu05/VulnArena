using VulnArena.Models;
using System.Text.Json;

namespace VulnArena.Services;

public class LoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly DBService _dbService;
    private readonly IConfiguration _configuration;
    private readonly Queue<LogEntry> _logQueue;
    private readonly Timer _flushTimer;
    private readonly int _batchSize;
    private readonly object _queueLock = new object();
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LoggingService(
        ILogger<LoggingService> logger,
        DBService dbService,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _dbService = dbService;
        _configuration = configuration;
        _logQueue = new Queue<LogEntry>();
        _batchSize = int.Parse(_configuration["VulnArena:Logging:BatchSize"] ?? "100");

        // Flush logs every 30 seconds
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string eventType,
        string userId,
        string? challengeId = null,
        string? details = null,
        Models.LogLevel level = Models.LogLevel.Information,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            var logEntry = new LogEntry
            {
                Id = Guid.NewGuid().ToString(),
                EventType = eventType,
                UserId = userId,
                ChallengeId = challengeId,
                Details = details,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                Level = level,
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            await _dbService.RecordLogEntryAsync(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging event {EventType} for user {UserId}", eventType, userId);
        }
    }

    public async Task<IEnumerable<LogEntry>> GetLogsAsync(
        DateTime? from = null,
        DateTime? to = null,
        string? userId = null,
        string? eventType = null,
        Models.LogLevel? level = null,
        string? challengeId = null,
        int page = 1,
        int pageSize = 50)
    {
        try
        {
            var logs = await _dbService.GetLogsAsync(from, to, userId, eventType, pageSize);
            
            // Apply additional filters
            var filteredLogs = logs.AsEnumerable();
            
            if (level.HasValue)
            {
                filteredLogs = filteredLogs.Where(l => l.Level == level.Value);
            }
            
            if (!string.IsNullOrEmpty(challengeId))
            {
                filteredLogs = filteredLogs.Where(l => l.ChallengeId == challengeId);
            }
            
            // Apply pagination
            return filteredLogs.Skip((page - 1) * pageSize).Take(pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            return Enumerable.Empty<LogEntry>();
        }
    }

    public async Task<IEnumerable<LogEntry>> GetLogsByLevelAsync(
        Models.LogLevel level,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 1000)
    {
        try
        {
            return await _dbService.GetLogsByLevelAsync(level, from, to, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs by level {Level}", level);
            return Enumerable.Empty<LogEntry>();
        }
    }

    public async Task<IEnumerable<LogEntry>> GetLogsByChallengeAsync(
        string challengeId,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100)
    {
        try
        {
            return await _dbService.GetLogsByChallengeAsync(challengeId, from, to, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs for challenge {ChallengeId}", challengeId);
            return Enumerable.Empty<LogEntry>();
        }
    }

    public async Task<LogStatistics> GetLogStatisticsAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var logs = await _dbService.GetLogsAsync(from, to, null, null, 10000);
            var logList = logs.ToList();

            return new LogStatistics
            {
                TotalLogs = logList.Count,
                LogsByLevel = logList.GroupBy(l => l.Level)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LogsByEventType = logList.GroupBy(l => l.EventType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LogsByUser = logList.GroupBy(l => l.UserId)
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentActivity = logList.OrderByDescending(l => l.Timestamp).Take(10).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log statistics");
            return new LogStatistics();
        }
    }

    public async Task<int> CleanupOldLogsAsync(DateTime cutoffDate)
    {
        try
        {
            return await _dbService.DeleteLogsOlderThanAsync(cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old logs");
            return 0;
        }
    }

    private string GetClientIpAddress()
    {
        try
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "Unknown";

            // Check for forwarded headers
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string? GetUserAgent()
    {
        try
        {
            return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    // Convenience methods for common logging scenarios
    public async Task LogUserLoginAsync(string userId, bool success, string? errorMessage = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["Success"] = success.ToString(),
            ["Timestamp"] = DateTime.UtcNow.ToString("O")
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            metadata["Error"] = errorMessage;
        }

        await LogAsync(
            success ? "UserLogin" : "UserLoginFailed",
            userId,
            details: success ? "User logged in successfully" : $"Login failed: {errorMessage}",
            level: success ? Models.LogLevel.Information : Models.LogLevel.Warning,
            metadata: metadata
        );
    }

    public async Task LogChallengeStartAsync(string userId, string challengeId)
    {
        await LogAsync(
            "ChallengeStart",
            userId,
            challengeId,
            "User started challenge",
            Models.LogLevel.Information
        );
    }

    public async Task LogChallengeStopAsync(string userId, string challengeId)
    {
        await LogAsync(
            "ChallengeStop",
            userId,
            challengeId,
            "User stopped challenge",
            Models.LogLevel.Information
        );
    }

    public async Task LogFlagSubmissionAsync(string userId, string challengeId, bool correct, int? pointsAwarded = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["Correct"] = correct.ToString()
        };

        if (pointsAwarded.HasValue)
        {
            metadata["PointsAwarded"] = pointsAwarded.Value.ToString();
        }

        await LogAsync(
            correct ? "FlagSubmissionCorrect" : "FlagSubmissionIncorrect",
            userId,
            challengeId,
            correct ? "Correct flag submitted" : "Incorrect flag submitted",
            Models.LogLevel.Information,
            metadata
        );
    }

    public async Task LogFileDownloadAsync(string userId, string challengeId, string filename)
    {
        var metadata = new Dictionary<string, string>
        {
            ["Filename"] = filename
        };

        await LogAsync(
            "FileDownload",
            userId,
            challengeId,
            $"User downloaded file: {filename}",
            Models.LogLevel.Information,
            metadata
        );
    }

    public async Task LogSystemEventAsync(string eventType, string details, Models.LogLevel level = Models.LogLevel.Information)
    {
        await LogAsync(
            eventType,
            "System",
            details: details,
            level: level
        );
    }

    private void FlushLogs(object? state)
    {
        _ = FlushLogsAsync();
    }

    private async Task FlushLogsAsync()
    {
        List<LogEntry> logsToFlush;
        
        lock (_queueLock)
        {
            if (_logQueue.Count == 0)
                return;

            logsToFlush = _logQueue.ToList();
            _logQueue.Clear();
        }

        try
        {
            await _dbService.BulkInsertLogsAsync(logsToFlush);
            _logger.LogDebug("Flushed {Count} log entries to database", logsToFlush.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing logs to database");
            
            // Re-queue failed logs
            lock (_queueLock)
            {
                foreach (var log in logsToFlush)
                {
                    _logQueue.Enqueue(log);
                }
            }
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        _ = FlushLogsAsync().Wait(5000); // Final flush with timeout
    }
}

public class LogStatistics
{
    public int TotalLogs { get; set; }
    public Dictionary<Models.LogLevel, int> LogsByLevel { get; set; } = new();
    public Dictionary<string, int> LogsByEventType { get; set; } = new();
    public Dictionary<string, int> LogsByUser { get; set; } = new();
    public List<LogEntry> RecentActivity { get; set; } = new();
} 