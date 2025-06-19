using VulnArena.Models;
using VulnArena.Services;

namespace VulnArena.Core;

public class ScoreManager
{
    private readonly ILogger<ScoreManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly DBService _dbService;
    private readonly int _basePoints;
    private readonly bool _timeBonus;
    private readonly double _difficultyMultiplier;

    public ScoreManager(
        ILogger<ScoreManager> logger,
        IConfiguration configuration,
        DBService dbService)
    {
        _logger = logger;
        _configuration = configuration;
        _dbService = dbService;
        _basePoints = int.Parse(_configuration["VulnArena:Scoring:BasePoints"] ?? "100");
        _timeBonus = bool.Parse(_configuration["VulnArena:Scoring:TimeBonus"] ?? "true");
        _difficultyMultiplier = double.Parse(_configuration["VulnArena:Scoring:DifficultyMultiplier"] ?? "1.5");
    }

    public async Task<int> CalculateScoreAsync(Challenge challenge, DateTime solvedAt, DateTime? startedAt = null)
    {
        var baseScore = _basePoints;

        // Apply difficulty multiplier
        var difficultyScore = (int)(baseScore * GetDifficultyMultiplier(challenge.Difficulty));

        // Apply time bonus if enabled and we have start time
        if (_timeBonus && startedAt.HasValue)
        {
            var timeBonus = CalculateTimeBonus(startedAt.Value, solvedAt, challenge.Difficulty);
            difficultyScore += timeBonus;
        }

        // Apply first blood bonus (first person to solve)
        var isFirstBlood = await IsFirstBloodAsync(challenge.Id, solvedAt);
        if (isFirstBlood)
        {
            var firstBloodBonus = (int)(difficultyScore * 0.1); // 10% bonus
            difficultyScore += firstBloodBonus;
            _logger.LogInformation("First blood bonus awarded for challenge {ChallengeId}", challenge.Id);
        }

        return Math.Max(1, difficultyScore); // Ensure minimum 1 point
    }

    private double GetDifficultyMultiplier(ChallengeDifficulty difficulty)
    {
        return difficulty switch
        {
            ChallengeDifficulty.Easy => 1.0,
            ChallengeDifficulty.Medium => _difficultyMultiplier,
            ChallengeDifficulty.Hard => _difficultyMultiplier * 2,
            ChallengeDifficulty.Expert => _difficultyMultiplier * 3,
            _ => 1.0
        };
    }

    private int CalculateTimeBonus(DateTime startedAt, DateTime solvedAt, ChallengeDifficulty difficulty)
    {
        var timeTaken = solvedAt - startedAt;
        var maxTimeBonus = _basePoints * 0.5; // 50% of base points as max time bonus

        // Shorter time = more bonus
        var timeRatio = Math.Max(0, 1 - (timeTaken.TotalMinutes / 60)); // 1 hour = no bonus
        var timeBonus = (int)(maxTimeBonus * timeRatio);

        // Apply difficulty modifier to time bonus
        var difficultyModifier = difficulty switch
        {
            ChallengeDifficulty.Easy => 0.5,
            ChallengeDifficulty.Medium => 1.0,
            ChallengeDifficulty.Hard => 1.5,
            ChallengeDifficulty.Expert => 2.0,
            _ => 1.0
        };

        return (int)(timeBonus * difficultyModifier);
    }

    private async Task<bool> IsFirstBloodAsync(string challengeId, DateTime solvedAt)
    {
        var earlierSubmissions = await _dbService.GetCorrectSubmissionsBeforeAsync(challengeId, solvedAt);
        return !earlierSubmissions.Any();
    }

    public async Task<bool> AwardPointsAsync(string userId, string challengeId, int points)
    {
        try
        {
            await _dbService.AwardPointsAsync(userId, challengeId, points);
            _logger.LogInformation("Awarded {Points} points to user {UserId} for challenge {ChallengeId}", 
                points, userId, challengeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error awarding points to user {UserId}", userId);
            return false;
        }
    }

    public async Task<IEnumerable<LeaderboardEntry>> GetLeaderboardAsync(int top = 100)
    {
        try
        {
            var users = await _dbService.GetAllUsersAsync();
            var leaderboard = new List<LeaderboardEntry>();

            foreach (var user in users)
            {
                var solvedChallenges = await _dbService.GetSolvedChallengesAsync(user.Id);
                var totalPoints = solvedChallenges.Sum(c => c.Points);
                var solveCount = solvedChallenges.Count();

                leaderboard.Add(new LeaderboardEntry
                {
                    UserId = user.Id,
                    Username = user.Username,
                    TotalPoints = totalPoints,
                    SolvedChallenges = solveCount,
                    LastSolvedAt = solvedChallenges.Max(c => c.SolvedAt)
                });
            }

            return leaderboard
                .OrderByDescending(e => e.TotalPoints)
                .ThenBy(e => e.LastSolvedAt)
                .Take(top);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaderboard");
            return Enumerable.Empty<LeaderboardEntry>();
        }
    }

    public async Task<IEnumerable<LeaderboardEntry>> GetLeaderboardByCategoryAsync(string category, int top = 50)
    {
        try
        {
            var users = await _dbService.GetAllUsersAsync();
            var leaderboard = new List<LeaderboardEntry>();

            foreach (var user in users)
            {
                var solvedChallenges = await _dbService.GetSolvedChallengesByCategoryAsync(user.Id, category);
                var totalPoints = solvedChallenges.Sum(c => c.Points);
                var solveCount = solvedChallenges.Count();

                if (solveCount > 0)
                {
                    leaderboard.Add(new LeaderboardEntry
                    {
                        UserId = user.Id,
                        Username = user.Username,
                        TotalPoints = totalPoints,
                        SolvedChallenges = solveCount,
                        LastSolvedAt = solvedChallenges.Max(c => c.SolvedAt),
                        Category = category
                    });
                }
            }

            return leaderboard
                .OrderByDescending(e => e.TotalPoints)
                .ThenBy(e => e.LastSolvedAt)
                .Take(top);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaderboard for category {Category}", category);
            return Enumerable.Empty<LeaderboardEntry>();
        }
    }

    public async Task<UserStats> GetUserStatsAsync(string userId)
    {
        try
        {
            var solvedChallenges = await _dbService.GetSolvedChallengesAsync(userId);
            var totalPoints = solvedChallenges.Sum(c => c.Points);
            var solveCount = solvedChallenges.Count();

            var categoryStats = await _dbService.GetSolvedChallengesByCategoryAsync(userId, null);
            var categoryBreakdown = categoryStats
                .GroupBy(c => c.Category)
                .Select(g => new CategoryStats
                {
                    Category = g.Key,
                    SolvedCount = g.Count(),
                    TotalPoints = g.Sum(c => c.Points)
                })
                .OrderByDescending(s => s.TotalPoints)
                .ToList();

            var rank = await GetUserRankAsync(userId);

            return new UserStats
            {
                UserId = userId,
                TotalPoints = totalPoints,
                SolvedChallenges = solveCount,
                Rank = rank,
                CategoryBreakdown = categoryBreakdown,
                LastSolvedAt = solvedChallenges.Max(c => c.SolvedAt),
                AverageSolveTime = CalculateAverageSolveTime(solvedChallenges)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats for user {UserId}", userId);
            return new UserStats { UserId = userId };
        }
    }

    private async Task<int> GetUserRankAsync(string userId)
    {
        var leaderboard = await GetLeaderboardAsync();
        var userEntry = leaderboard.FirstOrDefault(e => e.UserId == userId);
        return userEntry != null ? leaderboard.ToList().IndexOf(userEntry) + 1 : -1;
    }

    private TimeSpan CalculateAverageSolveTime(IEnumerable<SolvedChallenge> solvedChallenges)
    {
        var solveTimes = new List<TimeSpan>();
        
        foreach (var challenge in solvedChallenges)
        {
            if (challenge.StartedAt.HasValue)
            {
                solveTimes.Add(challenge.SolvedAt - challenge.StartedAt.Value);
            }
        }

        return solveTimes.Any() ? TimeSpan.FromTicks((long)solveTimes.Average(t => t.Ticks)) : TimeSpan.Zero;
    }
}

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int SolvedChallenges { get; set; }
    public DateTime? LastSolvedAt { get; set; }
    public string? Category { get; set; }
}

public class UserStats
{
    public string UserId { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int SolvedChallenges { get; set; }
    public int Rank { get; set; }
    public List<CategoryStats> CategoryBreakdown { get; set; } = new();
    public DateTime? LastSolvedAt { get; set; }
    public TimeSpan AverageSolveTime { get; set; }
}

public class CategoryStats
{
    public string Category { get; set; } = string.Empty;
    public int SolvedCount { get; set; }
    public int TotalPoints { get; set; }
}

public enum ChallengeDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert
} 