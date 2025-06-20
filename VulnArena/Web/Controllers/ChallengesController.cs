using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VulnArena.Core;
using VulnArena.Models;
using VulnArena.Services;

namespace VulnArena.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChallengesController : ControllerBase
{
    private readonly ILogger<ChallengesController> _logger;
    private readonly ChallengeManager _challengeManager;
    private readonly FlagValidator _flagValidator;
    private readonly ScoreManager _scoreManager;
    private readonly SandboxService _sandboxService;
    private readonly AuthService _authService;
    private readonly LoggingService _loggingService;
    private readonly DBService _dbService;

    public ChallengesController(
        ILogger<ChallengesController> logger,
        ChallengeManager challengeManager,
        FlagValidator flagValidator,
        ScoreManager scoreManager,
        SandboxService sandboxService,
        AuthService authService,
        LoggingService loggingService,
        DBService dbService)
    {
        _logger = logger;
        _challengeManager = challengeManager;
        _flagValidator = flagValidator;
        _scoreManager = scoreManager;
        _sandboxService = sandboxService;
        _authService = authService;
        _loggingService = loggingService;
        _dbService = dbService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Challenge>>> GetChallenges([FromQuery] string? category)
    {
        try
        {
            // Log the request
            await _loggingService.LogSystemEventAsync("CHALLENGES_REQUESTED", 
                $"Challenges requested for category: {category ?? "all"}", 
                Models.LogLevel.Information);

            IEnumerable<Challenge> challenges;
            if (!string.IsNullOrEmpty(category))
            {
                challenges = await _challengeManager.GetChallengesByCategoryAsync(category);
            }
            else
            {
                challenges = await _challengeManager.GetAllChallengesAsync();
            }

            // Remove sensitive information before returning
            var safeChallenges = challenges.Select(c => new
            {
                c.Id,
                c.Title,
                c.Description,
                c.Category,
                c.Difficulty,
                c.Points,
                c.RequiresContainer,
                c.Tags,
                c.Author,
                c.CreatedAt,
                c.SolveCount,
                c.Hint,
                c.Files
            });

            // Log successful response
            await _loggingService.LogSystemEventAsync("CHALLENGES_RETURNED", 
                $"Returned {safeChallenges.Count()} challenges", 
                Models.LogLevel.Information);

            return Ok(safeChallenges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting challenges");
            await _loggingService.LogSystemEventAsync("CHALLENGES_ERROR", 
                $"Error getting challenges: {ex.Message}", 
                Models.LogLevel.Error);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Challenge>> GetChallenge(string id)
    {
        try
        {
            var challenge = await _challengeManager.GetChallengeAsync(id);
            if (challenge == null)
            {
                return NotFound("Challenge not found");
            }

            // Remove sensitive information
            var safeChallenge = new
            {
                challenge.Id,
                challenge.Title,
                challenge.Description,
                challenge.Category,
                challenge.Difficulty,
                challenge.Points,
                challenge.RequiresContainer,
                challenge.ContainerPort,
                challenge.Tags,
                challenge.Author,
                challenge.CreatedAt,
                challenge.SolveCount,
                challenge.Hint,
                challenge.Files
            };

            return Ok(safeChallenge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting challenge {ChallengeId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/start")]
    public async Task<ActionResult> StartChallenge(string id)
    {
        try
        {
            // Get user from session
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("Authentication required");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            // Start the challenge
            var success = await _challengeManager.StartChallengeAsync(id, user.Id);
            if (!success)
            {
                return BadRequest("Failed to start challenge");
            }

            // If challenge requires container, start sandbox
            var challenge = await _challengeManager.GetChallengeAsync(id);
            if (challenge?.RequiresContainer == true)
            {
                var sandboxResult = await _sandboxService.StartSandboxAsync(id, user.Id);
                if (!sandboxResult.Success)
                {
                    return BadRequest($"Failed to start sandbox: {sandboxResult.Message}");
                }

                return Ok(new
                {
                    Message = "Challenge started successfully",
                    Sandbox = new
                    {
                        sandboxResult.SandboxId,
                        sandboxResult.Port,
                        sandboxResult.ExpiresAt
                    }
                });
            }

            return Ok(new { Message = "Challenge started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting challenge {ChallengeId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/stop")]
    public async Task<ActionResult> StopChallenge(string id)
    {
        try
        {
            // Get user from session
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("Authentication required");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            // Stop the challenge
            var success = await _challengeManager.StopChallengeAsync(id, user.Id);
            if (!success)
            {
                return BadRequest("Failed to stop challenge");
            }

            // Stop sandbox if exists
            await _sandboxService.StopSandboxAsync(id, user.Id);

            return Ok(new { Message = "Challenge stopped successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping challenge {ChallengeId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/submit")]
    public async Task<ActionResult> SubmitFlag(string id, [FromBody] FlagSubmissionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Flag))
            {
                return BadRequest("Flag is required");
            }

            // Get user from session
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("Authentication required");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            // Validate flag
            var result = await _flagValidator.ValidateFlagAsync(id, user.Id, request.Flag);

            if (result.IsRateLimited)
            {
                return StatusCode(429, new { message = result.Message, isRateLimited = true });
            }

            if (result.IsValid && !result.IsAlreadySolved)
            {
                // Award points
                var challenge = await _challengeManager.GetChallengeAsync(id);
                if (challenge != null)
                {
                    var points = await _scoreManager.CalculateScoreAsync(challenge, DateTime.UtcNow);
                    await _scoreManager.AwardPointsAsync(user.Id, id, points);
                }
            }

            return Ok(new
            {
                isValid = result.IsValid,
                message = result.Message,
                points = result.Points,
                isAlreadySolved = result.IsAlreadySolved,
                isRateLimited = result.IsRateLimited
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting flag for challenge {ChallengeId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}/sandbox")]
    public async Task<ActionResult> GetSandboxStatus(string id)
    {
        try
        {
            // Get user from session
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("Authentication required");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            var sandbox = await _sandboxService.GetSandboxAsync(id, user.Id);
            if (sandbox == null)
            {
                return NotFound("No sandbox found for this challenge");
            }

            var isHealthy = await _sandboxService.IsSandboxHealthyAsync(id, user.Id);

            return Ok(new
            {
                sandbox.Id,
                sandbox.Port,
                sandbox.CreatedAt,
                sandbox.ExpiresAt,
                sandbox.Status,
                IsHealthy = isHealthy,
                TimeRemaining = sandbox.ExpiresAt - DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sandbox status for challenge {ChallengeId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/sandbox/extend")]
    public async Task<ActionResult> ExtendSandbox(string id, [FromBody] ExtendSandboxRequest request)
    {
        try
        {
            // Get user from session
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("Authentication required");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            var extension = TimeSpan.FromMinutes(request.Minutes);
            var success = await _sandboxService.ExtendSandboxAsync(id, user.Id, extension);

            if (!success)
            {
                return BadRequest("Failed to extend sandbox");
            }

            return Ok(new { Message = $"Sandbox extended by {request.Minutes} minutes" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending sandbox for challenge {ChallengeId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        try
        {
            var challenges = await _challengeManager.GetAllChallengesAsync();
            var categories = challenges.Select(c => c.Category).Distinct().OrderBy(c => c);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("reload")]
    public async Task<ActionResult> ReloadChallenges()
    {
        try
        {
            // Get user from session
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("Authentication required");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            // Check if user is admin
            if (user.Role != UserRole.Admin)
            {
                return Forbid("Admin access required");
            }

            await _challengeManager.ReloadChallengesAsync();
            return Ok(new { Message = "Challenges reloaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading challenges");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}/files/{filename}")]
    public async Task<IActionResult> DownloadFile(string id, string filename)
    {
        try
        {
            var challenge = await _challengeManager.GetChallengeAsync(id);
            if (challenge == null)
            {
                return NotFound("Challenge not found");
            }

            // Try to get user from session
            string userId = "Anonymous";
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (!string.IsNullOrEmpty(sessionToken))
            {
                var user = await _authService.ValidateSessionAsync(sessionToken);
                if (user != null)
                {
                    userId = user.Id;
                }
            }

            // Log file download
            await _loggingService.LogFileDownloadAsync(userId, id, filename);

            var filePath = Path.Combine(challenge.Path, filename);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("File not found");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/octet-stream", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {Filename} for challenge {ChallengeId}", filename, id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("scoreboard")]
    public async Task<ActionResult<IEnumerable<object>>> GetScoreboard()
    {
        try
        {
            // Get user from session for validation
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("Authentication required");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            // Get scoreboard data from database
            var scoreboard = await _dbService.GetScoreboardAsync();
            
            // Log the request
            await _loggingService.LogSystemEventAsync("SCOREBOARD_REQUESTED", 
                $"Scoreboard requested by user: {user.Username}", 
                Models.LogLevel.Information);

            return Ok(scoreboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scoreboard");
            await _loggingService.LogSystemEventAsync("SCOREBOARD_ERROR", 
                $"Error getting scoreboard: {ex.Message}", 
                Models.LogLevel.Error);
            return StatusCode(500, "Internal server error");
        }
    }
}

public class FlagSubmissionRequest
{
    public string Flag { get; set; } = string.Empty;
}

public class ExtendSandboxRequest
{
    public int Minutes { get; set; } = 30;
} 