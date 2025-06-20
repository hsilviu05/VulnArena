using System.Security.Cryptography;
using System.Text;
using VulnArena.Models;
using BCrypt.Net;

namespace VulnArena.Services;

public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly DBService _dbService;
    private readonly LoggingService _loggingService;
    // Use static dictionary to ensure sessions persist across service instances
    private static readonly Dictionary<string, UserSession> _activeSessions = new Dictionary<string, UserSession>();

    public AuthService(
        ILogger<AuthService> logger,
        DBService dbService,
        LoggingService loggingService)
    {
        _logger = logger;
        _dbService = dbService;
        _loggingService = loggingService;
        
        // Add diagnostic logging
        _logger.LogInformation("AuthService instance created. Instance ID: {InstanceId}, Active sessions: {SessionCount}", 
            GetHashCode(), _activeSessions.Count);
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
            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, salt);

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
            await _loggingService.LogSystemEventAsync("USER_REGISTERED", $"Username: {username}", Models.LogLevel.Information);

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
                await _loggingService.LogSystemEventAsync("LOGIN_FAILED", $"Username: {username}, Reason: User not found", Models.LogLevel.Warning);
                return new AuthResult { Success = false, Message = "Invalid username or password." };
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                await _loggingService.LogSystemEventAsync("LOGIN_FAILED", $"Username: {username}, Reason: Invalid password", Models.LogLevel.Warning);
                return new AuthResult { Success = false, Message = "Invalid username or password." };
            }

            if (!user.IsActive)
            {
                await _loggingService.LogSystemEventAsync("LOGIN_FAILED", $"Username: {username}, Reason: Account disabled", Models.LogLevel.Warning);
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

            _logger.LogInformation("Session created for user {Username}. Token: {SessionToken}. Total active sessions: {SessionCount}", 
                username, sessionToken, _activeSessions.Count);

            await _loggingService.LogSystemEventAsync("LOGIN_SUCCESS", $"Username: {username}", Models.LogLevel.Information);

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
                await _loggingService.LogSystemEventAsync("LOGOUT", $"Session: {sessionToken}", Models.LogLevel.Information);
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
            _logger.LogInformation("Validating session token: {SessionToken}. Active sessions count: {SessionCount}", 
                sessionToken, _activeSessions.Count);
            
            // Log all active session tokens for debugging
            var activeTokens = string.Join(", ", _activeSessions.Keys.Take(5));
            _logger.LogInformation("Active session tokens (first 5): {ActiveTokens}", activeTokens);
            
            if (!_activeSessions.TryGetValue(sessionToken, out var session))
            {
                _logger.LogWarning("Session token not found in active sessions: {SessionToken}", sessionToken);
                return null;
            }

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogInformation("Session expired for token: {SessionToken}", sessionToken);
                _activeSessions.Remove(sessionToken);
                return null;
            }

            var user = await _dbService.GetUserByIdAsync(session.UserId);
            var isValid = user?.IsActive == true;
            _logger.LogInformation("Session validation result: {IsValid} for user: {UserId}", isValid, session.UserId);
            return isValid ? user : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session");
            return null;
        }
    }

    // Debug method to list all active sessions
    public Dictionary<string, UserSession> GetAllActiveSessions()
    {
        return new Dictionary<string, UserSession>(_activeSessions);
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
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                return false;
            }

            // Generate new salt and hash
            var newSalt = BCrypt.Net.BCrypt.GenerateSalt();
            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword, newSalt);

            user.PasswordHash = newHash;
            user.Salt = newSalt;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbService.UpdateUserAsync(user);
            await _loggingService.LogSystemEventAsync("PASSWORD_CHANGED", $"Password changed successfully", Models.LogLevel.Information);

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
        return BCrypt.Net.BCrypt.GenerateSalt();
    }

    private string HashPassword(string password, string salt)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, salt);
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