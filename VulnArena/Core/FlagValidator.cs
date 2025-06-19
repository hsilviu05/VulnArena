using System.Security.Cryptography;
using System.Text;
using VulnArena.Models;
using VulnArena.Services;
using System.Text.Json.Serialization;

namespace VulnArena.Core;

public class FlagValidator
{
    private readonly ILogger<FlagValidator> _logger;
    private readonly IConfiguration _configuration;
    private readonly DBService _dbService;
    private readonly LoggingService _loggingService;
    private readonly int _timeoutSeconds;

    public FlagValidator(
        ILogger<FlagValidator> logger,
        IConfiguration configuration,
        DBService dbService,
        LoggingService loggingService)
    {
        _logger = logger;
        _configuration = configuration;
        _dbService = dbService;
        _loggingService = loggingService;
        _timeoutSeconds = int.Parse(_configuration["VulnArena:Security:FlagValidationTimeout"] ?? "30");
    }

    public async Task<FlagValidationResult> ValidateFlagAsync(string challengeId, string userId, string submittedFlag)
    {
        try
        {
            // Check rate limiting
            if (await IsRateLimitedAsync(userId))
            {
                _logger.LogWarning("Rate limit exceeded for user {UserId}", userId);
                await _loggingService.LogEventAsync("FLAG_SUBMISSION_RATE_LIMITED", userId, challengeId, submittedFlag);
                return new FlagValidationResult
                {
                    IsValid = false,
                    Message = "Rate limit exceeded. Please wait before submitting again.",
                    IsRateLimited = true
                };
            }

            // Get challenge and expected flag
            var challenge = await _dbService.GetChallengeAsync(challengeId);
            if (challenge == null)
            {
                _logger.LogWarning("Challenge not found: {ChallengeId}", challengeId);
                return new FlagValidationResult
                {
                    IsValid = false,
                    Message = "Challenge not found."
                };
            }

            // Check if user has already solved this challenge
            var existingSubmission = await _dbService.GetSubmissionAsync(challengeId, userId);
            if (existingSubmission?.IsCorrect == true)
            {
                return new FlagValidationResult
                {
                    IsValid = true,
                    Message = "Flag already submitted correctly.",
                    IsAlreadySolved = true
                };
            }

            // Validate flag using secure comparison
            var isValid = await ValidateFlagSecurelyAsync(challenge, submittedFlag);

            // Record submission
            var submission = new Submission
            {
                ChallengeId = challengeId,
                UserId = userId,
                SubmittedFlag = submittedFlag,
                IsCorrect = isValid,
                SubmittedAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1" // TODO: Get actual IP
            };

            await _dbService.RecordSubmissionAsync(submission);

            // Log the attempt
            await _loggingService.LogEventAsync(
                isValid ? "FLAG_SUBMISSION_CORRECT" : "FLAG_SUBMISSION_INCORRECT",
                userId,
                challengeId,
                submittedFlag
            );

            if (isValid)
            {
                _logger.LogInformation("Correct flag submitted for challenge {ChallengeId} by user {UserId}", challengeId, userId);
                return new FlagValidationResult
                {
                    IsValid = true,
                    Message = "Correct flag! Well done!",
                    Points = challenge.Points
                };
            }
            else
            {
                _logger.LogInformation("Incorrect flag submitted for challenge {ChallengeId} by user {UserId}", challengeId, userId);
                return new FlagValidationResult
                {
                    IsValid = false,
                    Message = "Incorrect flag. Try again!"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating flag for challenge {ChallengeId}", challengeId);
            return new FlagValidationResult
            {
                IsValid = false,
                Message = "An error occurred while validating the flag."
            };
        }
    }

    private async Task<bool> ValidateFlagSecurelyAsync(Challenge challenge, string submittedFlag)
    {
        // Use constant-time comparison to prevent timing attacks
        if (string.IsNullOrEmpty(submittedFlag) || string.IsNullOrEmpty(challenge.Flag))
        {
            return false;
        }

        // For simple string comparison
        if (challenge.FlagType == FlagType.PlainText)
        {
            return SecureStringEquals(challenge.Flag, submittedFlag);
        }

        // For hash-based flags
        if (challenge.FlagType == FlagType.MD5)
        {
            var submittedHash = ComputeMD5Hash(submittedFlag);
            return SecureStringEquals(challenge.Flag, submittedHash);
        }

        if (challenge.FlagType == FlagType.SHA256)
        {
            var submittedHash = ComputeSHA256Hash(submittedFlag);
            return SecureStringEquals(challenge.Flag, submittedHash);
        }

        // For regex-based flags
        if (challenge.FlagType == FlagType.Regex)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(submittedFlag, challenge.Flag);
            }
            catch (ArgumentException)
            {
                _logger.LogError("Invalid regex pattern in challenge flag");
                return false;
            }
        }

        return false;
    }

    private bool SecureStringEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    private string ComputeMD5Hash(string input)
    {
        using (var md5 = MD5.Create())
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }

    private string ComputeSHA256Hash(string input)
    {
        using (var sha256 = SHA256.Create())
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }

    private async Task<bool> IsRateLimitedAsync(string userId)
    {
        var recentSubmissions = await _dbService.GetRecentSubmissionsAsync(userId, TimeSpan.FromMinutes(1));
        var maxSubmissions = int.Parse(_configuration["VulnArena:Security:RateLimitPerMinute"] ?? "10");
        
        return recentSubmissions.Count() >= maxSubmissions;
    }
}

public class FlagValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool IsRateLimited { get; set; }
    public bool IsAlreadySolved { get; set; }
}

public enum FlagType
{
    [JsonPropertyName("PlainText")]
    PlainText,
    [JsonPropertyName("MD5")]
    MD5,
    [JsonPropertyName("SHA256")]
    SHA256,
    [JsonPropertyName("Regex")]
    Regex
} 