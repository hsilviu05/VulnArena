using System.Security.Cryptography;
using System.Text;
using VulnArena.Models;

namespace VulnArena.Services;

public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly DBService _dbService;
    private readonly LoggingService _loggingService;
    private readonly Dictionary<string, UserSession> _activeSessions;

    public AuthService(
        ILogger<AuthService> logger,
        DBService dbService,
        LoggingService loggingService)
    {
        _logger = logger;
        _dbService = dbService;
        _loggingService = loggingService;
        _activeSessions = new Dictionary<string, UserSession>();
    }

    public async Task<AuthResult> RegisterAsync(string username, string email, string password)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return new AuthResult { Success = false, Message = "All fields are required." };
            }

            if (password.Length < 8)
            {
                return new AuthResult { Success = false, Message = "Password must be at least 8 characters long." };
            }

            // Check if username or email already exists
            var existingUser = await _dbService.GetUserByUsernameAsync(username);
            if (existingUser != null)
            {
                return new AuthResult { Success = false, Message = "Username already exists." };
            }

            existingUser = await _dbService.GetUserByEmailAsync(email);
            if (existingUser != null)
            {
                return new AuthResult { Success = false, Message = "Email already exists." };
            }

            // Generate salt and hash password
            var salt = GenerateSalt();
            var passwordHash = HashPassword(password, salt);

            // Create user
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                Salt = salt,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Role = UserRole.User
            };

            await _dbService.CreateUserAsync(user);
            await _loggingService.LogEventAsync("USER_REGISTERED", user.Id, null, $"Username: {username}");

            _logger.LogInformation("User registered: {Username}", username);
            return new AuthResult { Success = true, Message = "Registration successful.", User = user };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return new AuthResult { Success = false, Message = "Registration failed. Please try again." };
        }
    }

    public async Task<AuthResult> LoginAsync(string username, string password, string ipAddress)
    {
        try
        {
            // Get user by username
            var user = await _dbService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                await _loggingService.LogEventAsync("LOGIN_FAILED", "unknown", null, $"Username: {username}, Reason: User not found");
                return new AuthResult { Success = false, Message = "Invalid username or password." };
            }

            // Verify password
            var passwordHash = HashPassword(password, user.Salt);
            if (passwordHash != user.PasswordHash)
            {
                await _loggingService.LogEventAsync("LOGIN_FAILED", user.Id, null, $"Username: {username}, Reason: Invalid password");
                return new AuthResult { Success = false, Message = "Invalid username or password." };
            }

            if (!user.IsActive)
            {
                await _loggingService.LogEventAsync("LOGIN_FAILED", user.Id, null, $"Username: {username}, Reason: Account disabled");
                return new AuthResult { Success = false, Message = "Account is disabled." };
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _dbService.UpdateUserAsync(user);

            // Create session
            var sessionToken = GenerateSessionToken();
            var session = new UserSession
            {
                Token = sessionToken,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                IpAddress = ipAddress
            };

            _activeSessions[sessionToken] = session;

            await _loggingService.LogEventAsync("LOGIN_SUCCESS", user.Id, null, $"Username: {username}");

            _logger.LogInformation("User logged in: {Username}", username);
            return new AuthResult 
            { 
                Success = true, 
                Message = "Login successful.", 
                User = user, 
                SessionToken = sessionToken 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return new AuthResult { Success = false, Message = "Login failed. Please try again." };
        }
    }

    public async Task<bool> LogoutAsync(string sessionToken)
    {
        try
        {
            if (_activeSessions.Remove(sessionToken))
            {
                await _loggingService.LogEventAsync("LOGOUT", "unknown", null, $"Session: {sessionToken}");
                _logger.LogInformation("User logged out: {SessionToken}", sessionToken);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return false;
        }
    }

    public async Task<User?> ValidateSessionAsync(string sessionToken)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionToken, out var session))
            {
                return null;
            }

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _activeSessions.Remove(sessionToken);
                return null;
            }

            var user = await _dbService.GetUserByIdAsync(session.UserId);
            return user?.IsActive == true ? user : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session");
            return null;
        }
    }

    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        try
        {
            var user = await _dbService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            // Verify current password
            var currentHash = HashPassword(currentPassword, user.Salt);
            if (currentHash != user.PasswordHash)
            {
                return false;
            }

            // Generate new salt and hash
            var newSalt = GenerateSalt();
            var newHash = HashPassword(newPassword, newSalt);

            user.PasswordHash = newHash;
            user.Salt = newSalt;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbService.UpdateUserAsync(user);
            await _loggingService.LogEventAsync("PASSWORD_CHANGED", userId, null, "Password changed successfully");

            _logger.LogInformation("Password changed for user: {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return false;
        }
    }

    private string GenerateSalt()
    {
        var salt = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        return Convert.ToBase64String(salt);
    }

    private string HashPassword(string password, string salt)
    {
        using (var sha256 = SHA256.Create())
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password + salt);
            var hashBytes = sha256.ComputeHash(passwordBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }

    private string GenerateSessionToken()
    {
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes);
    }

    public void CleanupExpiredSessions()
    {
        var expiredSessions = _activeSessions
            .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionToken in expiredSessions)
        {
            _activeSessions.Remove(sessionToken);
        }

        if (expiredSessions.Any())
        {
            _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
        }
    }
}

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public User? User { get; set; }
    public string? SessionToken { get; set; }
}

public class UserSession
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
} 