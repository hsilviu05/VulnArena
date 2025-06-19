using Microsoft.AspNetCore.Mvc;
using VulnArena.Models;
using VulnArena.Services;

namespace VulnArena.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly AuthService _authService;
    private readonly DBService _dbService;

    public AuthController(
        ILogger<AuthController> logger,
        AuthService authService,
        DBService dbService)
    {
        _logger = logger;
        _authService = authService;
        _dbService = dbService;
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username, email, and password are required");
            }

            // Validate email format
            if (!IsValidEmail(request.Email))
            {
                return BadRequest("Invalid email format");
            }

            // Validate password strength
            if (request.Password.Length < 8)
            {
                return BadRequest("Password must be at least 8 characters long");
            }

            var ipAddress = GetClientIpAddress();
            var result = await _authService.RegisterAsync(request.Username, request.Email, request.Password);

            if (result.Success)
            {
                return Ok(new
                {
                    Message = result.Message,
                    User = new
                    {
                        result.User!.Id,
                        result.User.Username,
                        result.User.Email,
                        result.User.CreatedAt,
                        result.User.Role
                    }
                });
            }
            else
            {
                return BadRequest(new { Message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username and password are required");
            }

            var ipAddress = GetClientIpAddress();
            var result = await _authService.LoginAsync(request.Username, request.Password, ipAddress);

            if (result.Success)
            {
                return Ok(new
                {
                    Message = result.Message,
                    SessionToken = result.SessionToken,
                    User = new
                    {
                        result.User!.Id,
                        result.User.Username,
                        result.User.Email,
                        result.User.Role,
                        result.User.TotalPoints,
                        result.User.SolvedChallenges,
                        result.User.LastLoginAt
                    }
                });
            }
            else
            {
                return Unauthorized(new { Message = result.Message });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
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