using VulnArena.Models;

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

    public LoggingService(
        ILogger<LoggingService> logger,
        DBService dbService,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbService = dbService;
        _configuration = configuration;
        _logQueue = new Queue<LogEntry>();
        _batchSize = int.Parse(_configuration["VulnArena:Logging:BatchSize"] ?? "100");

        // Flush logs every 30 seconds
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task LogEventAsync(string eventType, string userId, string? challengeId, string details, Models.LogLevel level = Models.LogLevel.Information)
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
                Level = level,
                IpAddress = "127.0.0.1", // TODO: Get actual IP from context
                UserAgent = "Unknown" // TODO: Get actual user agent from context
            };

            lock (_queueLock)
            {
                _logQueue.Enqueue(logEntry);
            }

            // Flush immediately if queue is getting large
            if (_logQueue.Count >= _batchSize)
            {
                await FlushLogsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging event {EventType}", eventType);
        }
    }

    public async Task LogSecurityEventAsync(string eventType, string userId, string? challengeId, string details)
    {
        await LogEventAsync(eventType, userId, challengeId, details, Models.LogLevel.Warning);
    }

    public async Task LogErrorAsync(string eventType, string userId, string? challengeId, string details)
    {
        await LogEventAsync(eventType, userId, challengeId, details, Models.LogLevel.Error);
    }

    public async Task<IEnumerable<LogEntry>> GetLogsAsync(DateTime? from = null, DateTime? to = null, string? userId = null, string? eventType = null, int limit = 1000)
    {
        try
        {
            return await _dbService.GetLogsAsync(from, to, userId, eventType, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            return Enumerable.Empty<LogEntry>();
        }
    }

    public async Task<IEnumerable<LogEntry>> GetSecurityLogsAsync(DateTime? from = null, DateTime? to = null, int limit = 1000)
    {
        try
        {
            return await _dbService.GetLogsByLevelAsync(Models.LogLevel.Warning, from, to, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs");
            return Enumerable.Empty<LogEntry>();
        }
    }

    public async Task<IEnumerable<LogEntry>> GetUserActivityAsync(string userId, DateTime? from = null, DateTime? to = null, int limit = 100)
    {
        try
        {
            return await _dbService.GetLogsAsync(from, to, userId, null, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user activity for {UserId}", userId);
            return Enumerable.Empty<LogEntry>();
        }
    }

    public async Task<IEnumerable<LogEntry>> GetChallengeActivityAsync(string challengeId, DateTime? from = null, DateTime? to = null, int limit = 100)
    {
        try
        {
            return await _dbService.GetLogsByChallengeAsync(challengeId, from, to, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving challenge activity for {ChallengeId}", challengeId);
            return Enumerable.Empty<LogEntry>();
        }
    }

    public async Task<Dictionary<string, int>> GetEventStatisticsAsync(DateTime from, DateTime to)
    {
        try
        {
            var logs = await _dbService.GetLogsAsync(from, to, null, null, int.MaxValue);
            return logs.GroupBy(l => l.EventType)
                      .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving event statistics");
            return new Dictionary<string, int>();
        }
    }

    public async Task<Dictionary<string, int>> GetUserActivityStatisticsAsync(DateTime from, DateTime to)
    {
        try
        {
            var logs = await _dbService.GetLogsAsync(from, to, null, null, int.MaxValue);
            return logs.GroupBy(l => l.UserId)
                      .ToDictionary(g => g.Key, g => g.Count())
                      .OrderByDescending(kvp => kvp.Value)
                      .Take(100)
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user activity statistics");
            return new Dictionary<string, int>();
        }
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

    public async Task CleanupOldLogsAsync(int daysToKeep = 90)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var deletedCount = await _dbService.DeleteLogsOlderThanAsync(cutoffDate);
            _logger.LogInformation("Cleaned up {Count} log entries older than {Days} days", deletedCount, daysToKeep);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old logs");
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        _ = FlushLogsAsync().Wait(5000); // Final flush with timeout
    }
} 