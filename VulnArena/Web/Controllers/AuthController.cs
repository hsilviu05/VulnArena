using Microsoft.AspNetCore.Mvc;
using VulnArena.Models;
using VulnArena.Services;
using BCrypt.Net;

namespace VulnArena.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly AuthService _authService;
    private readonly DBService _dbService;
    private readonly LoggingService _loggingService;

    public AuthController(
        ILogger<AuthController> logger,
        AuthService authService,
        DBService dbService,
        LoggingService loggingService)
    {
        _logger = logger;
        _authService = authService;
        _dbService = dbService;
        _loggingService = loggingService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Username, password, and email are required");
            }

            // Check if username already exists
            var existingUser = await _dbService.GetUserByUsernameAsync(request.Username);
            if (existingUser != null)
            {
                return BadRequest("Username already exists");
            }

            // Check if email already exists
            var existingEmail = await _dbService.GetUserByEmailAsync(request.Email);
            if (existingEmail != null)
            {
                return BadRequest("Email already exists");
            }

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = request.Username,
                Email = request.Email,
                Role = UserRole.User, // Default role
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Hash password
            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, salt);
            user.Salt = salt;

            await _dbService.CreateUserAsync(user);

            // Log the registration
            await _loggingService.LogSystemEventAsync("USER_REGISTERED", $"New user registered: {request.Username}", Models.LogLevel.Information);

            return Ok(new RegisterResponse
            {
                Message = "User registered successfully",
                User = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user {Username}", request.Username);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request.Username, request.Password, GetClientIpAddress());
            if (!result.Success)
            {
                // Log failed login attempt
                await _loggingService.LogUserLoginAsync(request.Username, false, "Invalid credentials");
                return Unauthorized("Invalid username or password");
            }

            var user = result.User;
            var sessionToken = result.SessionToken;
            
            // Log successful login
            await _loggingService.LogUserLoginAsync(user.Id, true);

            return Ok(new LoginResponse
            {
                SessionToken = sessionToken,
                User = user
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", request.Username);
            await _loggingService.LogUserLoginAsync(request.Username, false, ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        try
        {
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return BadRequest("No session token provided");
            }

            var success = await _authService.LogoutAsync(sessionToken);
            if (success)
            {
                return Ok(new { Message = "Logged out successfully" });
            }
            else
            {
                return BadRequest("Failed to logout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult> GetCurrentUser()
    {
        try
        {
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

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.TotalPoints,
                user.SolvedChallenges,
                user.CreatedAt,
                user.LastLoginAt,
                user.Avatar,
                user.Bio
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest("Current password and new password are required");
            }

            if (request.NewPassword.Length < 8)
            {
                return BadRequest("New password must be at least 8 characters long");
            }

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

            var success = await _authService.ChangePasswordAsync(user.Id, request.CurrentPassword, request.NewPassword);
            if (success)
            {
                return Ok(new { Message = "Password changed successfully" });
            }
            else
            {
                return BadRequest("Current password is incorrect");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("validate-session")]
    public async Task<ActionResult> ValidateSession()
    {
        try
        {
            var sessionToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Unauthorized("No session token provided");
            }

            var user = await _authService.ValidateSessionAsync(sessionToken);
            if (user == null)
            {
                return Unauthorized("Invalid session");
            }

            return Ok(new
            {
                IsValid = true,
                User = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.Role,
                    user.TotalPoints,
                    user.SolvedChallenges
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("debug-sessions")]
    public async Task<ActionResult> DebugSessions()
    {
        try
        {
            // Get all active sessions for debugging
            var activeSessions = _authService.GetAllActiveSessions();
            
            var sessionInfo = activeSessions.Select(kvp => new
            {
                Token = kvp.Key,
                UserId = kvp.Value.UserId,
                CreatedAt = kvp.Value.CreatedAt,
                ExpiresAt = kvp.Value.ExpiresAt,
                IsExpired = kvp.Value.ExpiresAt < DateTime.UtcNow
            }).ToList();

            return Ok(new { 
                Message = "Active sessions debug info",
                TotalSessions = activeSessions.Count,
                Sessions = sessionInfo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting debug sessions");
            return StatusCode(500, "Internal server error");
        }
    }

    private string GetClientIpAddress()
    {
        // Try to get the real IP address from various headers
        var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedHeader))
        {
            return forwardedHeader.Split(',')[0].Trim();
        }

        var realIpHeader = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIpHeader))
        {
            return realIpHeader;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string SessionToken { get; set; } = string.Empty;
    public User User { get; set; } = new();
}

public class RegisterResponse
{
    public string Message { get; set; } = string.Empty;
    public object User { get; set; } = new();
} 