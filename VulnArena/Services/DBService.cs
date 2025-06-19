using Microsoft.Data.Sqlite;
using VulnArena.Models;
using VulnArena.Core;

namespace VulnArena.Services;

public class DBService
{
    private readonly ILogger<DBService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DBService(ILogger<DBService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionString = _configuration["VulnArena:Database:ConnectionString"] ?? "Data Source=vulnarena.db";
    }

    public async Task InitializeDatabaseAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Create tables
            await CreateTablesAsync(connection);
            
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }

    private async Task CreateTablesAsync(SqliteConnection connection)
    {
        var commands = new[]
        {
            @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id TEXT PRIMARY KEY,
                    Username TEXT UNIQUE NOT NULL,
                    Email TEXT UNIQUE NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    LastLoginAt TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    Role TEXT NOT NULL DEFAULT 'User',
                    TotalPoints INTEGER NOT NULL DEFAULT 0,
                    SolvedChallenges INTEGER NOT NULL DEFAULT 0,
                    Avatar TEXT,
                    Bio TEXT,
                    Preferences TEXT
                )",
            @"
                CREATE TABLE IF NOT EXISTS Challenges (
                    Id TEXT PRIMARY KEY,
                    Title TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    Flag TEXT NOT NULL,
                    FlagType TEXT NOT NULL DEFAULT 'PlainText',
                    Difficulty TEXT NOT NULL DEFAULT 'Easy',
                    Points INTEGER NOT NULL DEFAULT 100,
                    RequiresContainer INTEGER NOT NULL DEFAULT 0,
                    ContainerImage TEXT,
                    ContainerPort INTEGER,
                    Tags TEXT,
                    Author TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    SolveCount INTEGER NOT NULL DEFAULT 0,
                    Hint TEXT,
                    Files TEXT,
                    Metadata TEXT
                )",
            @"
                CREATE TABLE IF NOT EXISTS Submissions (
                    Id TEXT PRIMARY KEY,
                    ChallengeId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    SubmittedFlag TEXT NOT NULL,
                    IsCorrect INTEGER NOT NULL DEFAULT 0,
                    SubmittedAt TEXT NOT NULL,
                    IpAddress TEXT NOT NULL,
                    UserAgent TEXT,
                    PointsAwarded INTEGER,
                    PointsAwardedAt TEXT,
                    ErrorMessage TEXT,
                    Metadata TEXT,
                    FOREIGN KEY (ChallengeId) REFERENCES Challenges(Id),
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                )",
            @"
                CREATE TABLE IF NOT EXISTS LogEntries (
                    Id TEXT PRIMARY KEY,
                    EventType TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    ChallengeId TEXT,
                    Details TEXT,
                    Timestamp TEXT NOT NULL,
                    IpAddress TEXT NOT NULL,
                    UserAgent TEXT,
                    Level TEXT NOT NULL DEFAULT 'Information',
                    Metadata TEXT
                )",
            @"
                CREATE TABLE IF NOT EXISTS ChallengeStarts (
                    Id TEXT PRIMARY KEY,
                    ChallengeId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    StartedAt TEXT NOT NULL,
                    StoppedAt TEXT,
                    ContainerId TEXT,
                    FOREIGN KEY (ChallengeId) REFERENCES Challenges(Id),
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                )"
        };

        foreach (var command in commands)
        {
            using var cmd = new SqliteCommand(command, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        // Create indexes
        await CreateIndexesAsync(connection);
    }

    private async Task CreateIndexesAsync(SqliteConnection connection)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username)",
            "CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users(Email)",
            "CREATE INDEX IF NOT EXISTS IX_Challenges_Category ON Challenges(Category)",
            "CREATE INDEX IF NOT EXISTS IX_Challenges_IsActive ON Challenges(IsActive)",
            "CREATE INDEX IF NOT EXISTS IX_Submissions_ChallengeId ON Submissions(ChallengeId)",
            "CREATE INDEX IF NOT EXISTS IX_Submissions_UserId ON Submissions(UserId)",
            "CREATE INDEX IF NOT EXISTS IX_Submissions_SubmittedAt ON Submissions(SubmittedAt)",
            "CREATE INDEX IF NOT EXISTS IX_LogEntries_Timestamp ON LogEntries(Timestamp)",
            "CREATE INDEX IF NOT EXISTS IX_LogEntries_UserId ON LogEntries(UserId)",
            "CREATE INDEX IF NOT EXISTS IX_LogEntries_EventType ON LogEntries(EventType)",
            "CREATE INDEX IF NOT EXISTS IX_ChallengeStarts_ChallengeId ON ChallengeStarts(ChallengeId)",
            "CREATE INDEX IF NOT EXISTS IX_ChallengeStarts_UserId ON ChallengeStarts(UserId)"
        };

        foreach (var index in indexes)
        {
            using var cmd = new SqliteCommand(index, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // User operations
    public async Task<User?> GetUserByIdAsync(string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM Users WHERE Id = @UserId";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUserFromReader(reader);
        }

        return null;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM Users WHERE Username = @Username";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@Username", username);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUserFromReader(reader);
        }

        return null;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM Users WHERE Email = @Email";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@Email", email);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUserFromReader(reader);
        }

        return null;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM Users ORDER BY TotalPoints DESC";
        using var cmd = new SqliteCommand(command, connection);

        var users = new List<User>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(MapUserFromReader(reader));
        }

        return users;
    }

    public async Task CreateUserAsync(User user)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            INSERT INTO Users (Id, Username, Email, PasswordHash, Salt, CreatedAt, IsActive, Role, TotalPoints, SolvedChallenges, Avatar, Bio, Preferences)
            VALUES (@Id, @Username, @Email, @PasswordHash, @Salt, @CreatedAt, @IsActive, @Role, @TotalPoints, @SolvedChallenges, @Avatar, @Bio, @Preferences)";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@Id", user.Id);
        cmd.Parameters.AddWithValue("@Username", user.Username);
        cmd.Parameters.AddWithValue("@Email", user.Email);
        cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@Salt", user.Salt);
        cmd.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@Role", user.Role.ToString());
        cmd.Parameters.AddWithValue("@TotalPoints", user.TotalPoints);
        cmd.Parameters.AddWithValue("@SolvedChallenges", user.SolvedChallenges);
        cmd.Parameters.AddWithValue("@Avatar", user.Avatar ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Bio", user.Bio ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Preferences", System.Text.Json.JsonSerializer.Serialize(user.Preferences));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            UPDATE Users 
            SET Username = @Username, Email = @Email, PasswordHash = @PasswordHash, Salt = @Salt, 
                LastLoginAt = @LastLoginAt, IsActive = @IsActive, Role = @Role, TotalPoints = @TotalPoints, 
                SolvedChallenges = @SolvedChallenges, Avatar = @Avatar, Bio = @Bio, Preferences = @Preferences
            WHERE Id = @Id";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@Id", user.Id);
        cmd.Parameters.AddWithValue("@Username", user.Username);
        cmd.Parameters.AddWithValue("@Email", user.Email);
        cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@Salt", user.Salt);
        cmd.Parameters.AddWithValue("@LastLoginAt", user.LastLoginAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@Role", user.Role.ToString());
        cmd.Parameters.AddWithValue("@TotalPoints", user.TotalPoints);
        cmd.Parameters.AddWithValue("@SolvedChallenges", user.SolvedChallenges);
        cmd.Parameters.AddWithValue("@Avatar", user.Avatar ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Bio", user.Bio ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Preferences", System.Text.Json.JsonSerializer.Serialize(user.Preferences));

        await cmd.ExecuteNonQueryAsync();
    }

    // Challenge operations
    public async Task<Challenge?> GetChallengeAsync(string challengeId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM Challenges WHERE Id = @ChallengeId";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@ChallengeId", challengeId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapChallengeFromReader(reader);
        }

        return null;
    }

    // Submission operations
    public async Task<Submission?> GetSubmissionAsync(string challengeId, string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM Submissions WHERE ChallengeId = @ChallengeId AND UserId = @UserId ORDER BY SubmittedAt DESC LIMIT 1";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@ChallengeId", challengeId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapSubmissionFromReader(reader);
        }

        return null;
    }

    public async Task<IEnumerable<Submission>> GetRecentSubmissionsAsync(string userId, TimeSpan timeSpan)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cutoffTime = DateTime.UtcNow.Subtract(timeSpan);
        var command = "SELECT * FROM Submissions WHERE UserId = @UserId AND SubmittedAt >= @CutoffTime ORDER BY SubmittedAt DESC";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@CutoffTime", cutoffTime.ToString("O"));

        var submissions = new List<Submission>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            submissions.Add(MapSubmissionFromReader(reader));
        }

        return submissions;
    }

    public async Task<IEnumerable<Submission>> GetCorrectSubmissionsBeforeAsync(string challengeId, DateTime before)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM Submissions WHERE ChallengeId = @ChallengeId AND IsCorrect = 1 AND SubmittedAt < @Before ORDER BY SubmittedAt ASC";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@ChallengeId", challengeId);
        cmd.Parameters.AddWithValue("@Before", before.ToString("O"));

        var submissions = new List<Submission>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            submissions.Add(MapSubmissionFromReader(reader));
        }

        return submissions;
    }

    public async Task RecordSubmissionAsync(Submission submission)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            INSERT INTO Submissions (Id, ChallengeId, UserId, SubmittedFlag, IsCorrect, SubmittedAt, IpAddress, UserAgent, PointsAwarded, PointsAwardedAt, ErrorMessage, Metadata)
            VALUES (@Id, @ChallengeId, @UserId, @SubmittedFlag, @IsCorrect, @SubmittedAt, @IpAddress, @UserAgent, @PointsAwarded, @PointsAwardedAt, @ErrorMessage, @Metadata)";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@Id", submission.Id);
        cmd.Parameters.AddWithValue("@ChallengeId", submission.ChallengeId);
        cmd.Parameters.AddWithValue("@UserId", submission.UserId);
        cmd.Parameters.AddWithValue("@SubmittedFlag", submission.SubmittedFlag);
        cmd.Parameters.AddWithValue("@IsCorrect", submission.IsCorrect ? 1 : 0);
        cmd.Parameters.AddWithValue("@SubmittedAt", submission.SubmittedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@IpAddress", submission.IpAddress);
        cmd.Parameters.AddWithValue("@UserAgent", submission.UserAgent ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PointsAwarded", submission.PointsAwarded ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PointsAwardedAt", submission.PointsAwardedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ErrorMessage", submission.ErrorMessage ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Metadata", System.Text.Json.JsonSerializer.Serialize(submission.Metadata));

        await cmd.ExecuteNonQueryAsync();
    }

    // Log operations
    public async Task<IEnumerable<LogEntry>> GetLogsAsync(DateTime? from = null, DateTime? to = null, string? userId = null, string? eventType = null, int limit = 1000)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM LogEntries WHERE 1=1";
        var parameters = new List<SqliteParameter>();

        if (from.HasValue)
        {
            command += " AND Timestamp >= @From";
            parameters.Add(new SqliteParameter("@From", from.Value.ToString("O")));
        }

        if (to.HasValue)
        {
            command += " AND Timestamp <= @To";
            parameters.Add(new SqliteParameter("@To", to.Value.ToString("O")));
        }

        if (!string.IsNullOrEmpty(userId))
        {
            command += " AND UserId = @UserId";
            parameters.Add(new SqliteParameter("@UserId", userId));
        }

        if (!string.IsNullOrEmpty(eventType))
        {
            command += " AND EventType = @EventType";
            parameters.Add(new SqliteParameter("@EventType", eventType));
        }

        command += " ORDER BY Timestamp DESC LIMIT @Limit";
        parameters.Add(new SqliteParameter("@Limit", limit));

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddRange(parameters.ToArray());

        var logs = new List<LogEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(MapLogEntryFromReader(reader));
        }

        return logs;
    }

    public async Task BulkInsertLogsAsync(IEnumerable<LogEntry> logs)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            var command = @"
                INSERT INTO LogEntries (Id, EventType, UserId, ChallengeId, Details, Timestamp, IpAddress, UserAgent, Level, Metadata)
                VALUES (@Id, @EventType, @UserId, @ChallengeId, @Details, @Timestamp, @IpAddress, @UserAgent, @Level, @Metadata)";

            using var cmd = new SqliteCommand(command, connection, transaction);

            foreach (var log in logs)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@Id", log.Id);
                cmd.Parameters.AddWithValue("@EventType", log.EventType);
                cmd.Parameters.AddWithValue("@UserId", log.UserId);
                cmd.Parameters.AddWithValue("@ChallengeId", log.ChallengeId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Details", log.Details ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Timestamp", log.Timestamp.ToString("O"));
                cmd.Parameters.AddWithValue("@IpAddress", log.IpAddress);
                cmd.Parameters.AddWithValue("@UserAgent", log.UserAgent ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Level", log.Level.ToString());
                cmd.Parameters.AddWithValue("@Metadata", System.Text.Json.JsonSerializer.Serialize(log.Metadata));

                await cmd.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // Challenge start/stop operations
    public async Task RecordChallengeStartAsync(string challengeId, string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            INSERT INTO ChallengeStarts (Id, ChallengeId, UserId, StartedAt)
            VALUES (@Id, @ChallengeId, @UserId, @StartedAt)";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@ChallengeId", challengeId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RecordChallengeStopAsync(string challengeId, string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            UPDATE ChallengeStarts 
            SET StoppedAt = @StoppedAt
            WHERE ChallengeId = @ChallengeId AND UserId = @UserId AND StoppedAt IS NULL";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@ChallengeId", challengeId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@StoppedAt", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RecordContainerStartAsync(string challengeId, string userId, string containerId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            UPDATE ChallengeStarts 
            SET ContainerId = @ContainerId
            WHERE ChallengeId = @ChallengeId AND UserId = @UserId AND StoppedAt IS NULL";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@ChallengeId", challengeId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ContainerId", containerId);

        await cmd.ExecuteNonQueryAsync();
    }

    // Points operations
    public async Task AwardPointsAsync(string userId, string challengeId, int points)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Update user points
            var updateUserCommand = @"
                UPDATE Users 
                SET TotalPoints = TotalPoints + @Points, SolvedChallenges = SolvedChallenges + 1
                WHERE Id = @UserId";

            using var updateUserCmd = new SqliteCommand(updateUserCommand, connection, transaction);
            updateUserCmd.Parameters.AddWithValue("@UserId", userId);
            updateUserCmd.Parameters.AddWithValue("@Points", points);
            await updateUserCmd.ExecuteNonQueryAsync();

            // Update challenge solve count
            var updateChallengeCommand = @"
                UPDATE Challenges 
                SET SolveCount = SolveCount + 1
                WHERE Id = @ChallengeId";

            using var updateChallengeCmd = new SqliteCommand(updateChallengeCommand, connection, transaction);
            updateChallengeCmd.Parameters.AddWithValue("@ChallengeId", challengeId);
            await updateChallengeCmd.ExecuteNonQueryAsync();

            // Update submission with points
            var updateSubmissionCommand = @"
                UPDATE Submissions 
                SET PointsAwarded = @Points, PointsAwardedAt = @PointsAwardedAt
                WHERE ChallengeId = @ChallengeId AND UserId = @UserId AND IsCorrect = 1
                ORDER BY SubmittedAt DESC LIMIT 1";

            using var updateSubmissionCmd = new SqliteCommand(updateSubmissionCommand, connection, transaction);
            updateSubmissionCmd.Parameters.AddWithValue("@ChallengeId", challengeId);
            updateSubmissionCmd.Parameters.AddWithValue("@UserId", userId);
            updateSubmissionCmd.Parameters.AddWithValue("@Points", points);
            updateSubmissionCmd.Parameters.AddWithValue("@PointsAwardedAt", DateTime.UtcNow.ToString("O"));
            await updateSubmissionCmd.ExecuteNonQueryAsync();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // Additional methods for ScoreManager
    public async Task<IEnumerable<SolvedChallenge>> GetSolvedChallengesAsync(string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            SELECT s.ChallengeId, s.PointsAwarded, s.SubmittedAt as SolvedAt, cs.StartedAt
            FROM Submissions s
            LEFT JOIN ChallengeStarts cs ON s.ChallengeId = cs.ChallengeId AND s.UserId = cs.UserId
            WHERE s.UserId = @UserId AND s.IsCorrect = 1
            ORDER BY s.SubmittedAt DESC";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var solvedChallenges = new List<SolvedChallenge>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            solvedChallenges.Add(new SolvedChallenge
            {
                ChallengeId = reader.GetString(reader.GetOrdinal("ChallengeId")),
                Points = reader.GetInt32(reader.GetOrdinal("PointsAwarded")),
                SolvedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("SolvedAt"))),
                StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt")))
            });
        }

        return solvedChallenges;
    }

    public async Task<IEnumerable<SolvedChallenge>> GetSolvedChallengesByCategoryAsync(string userId, string? category)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = @"
            SELECT s.ChallengeId, s.PointsAwarded, s.SubmittedAt as SolvedAt, cs.StartedAt, c.Category
            FROM Submissions s
            JOIN Challenges c ON s.ChallengeId = c.Id
            LEFT JOIN ChallengeStarts cs ON s.ChallengeId = cs.ChallengeId AND s.UserId = cs.UserId
            WHERE s.UserId = @UserId AND s.IsCorrect = 1";

        if (!string.IsNullOrEmpty(category))
        {
            command += " AND c.Category = @Category";
        }

        command += " ORDER BY s.SubmittedAt DESC";

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        if (!string.IsNullOrEmpty(category))
        {
            cmd.Parameters.AddWithValue("@Category", category);
        }

        var solvedChallenges = new List<SolvedChallenge>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            solvedChallenges.Add(new SolvedChallenge
            {
                ChallengeId = reader.GetString(reader.GetOrdinal("ChallengeId")),
                Points = reader.GetInt32(reader.GetOrdinal("PointsAwarded")),
                SolvedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("SolvedAt"))),
                StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
                Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category"))
            });
        }

        return solvedChallenges;
    }

    // Additional methods for LoggingService
    public async Task<IEnumerable<LogEntry>> GetLogsByLevelAsync(Models.LogLevel level, DateTime? from = null, DateTime? to = null, int limit = 1000)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM LogEntries WHERE Level = @Level";
        var parameters = new List<SqliteParameter>
        {
            new SqliteParameter("@Level", level.ToString())
        };

        if (from.HasValue)
        {
            command += " AND Timestamp >= @From";
            parameters.Add(new SqliteParameter("@From", from.Value.ToString("O")));
        }

        if (to.HasValue)
        {
            command += " AND Timestamp <= @To";
            parameters.Add(new SqliteParameter("@To", to.Value.ToString("O")));
        }

        command += " ORDER BY Timestamp DESC LIMIT @Limit";
        parameters.Add(new SqliteParameter("@Limit", limit));

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddRange(parameters.ToArray());

        var logs = new List<LogEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(MapLogEntryFromReader(reader));
        }

        return logs;
    }

    public async Task<IEnumerable<LogEntry>> GetLogsByChallengeAsync(string challengeId, DateTime? from = null, DateTime? to = null, int limit = 100)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "SELECT * FROM LogEntries WHERE ChallengeId = @ChallengeId";
        var parameters = new List<SqliteParameter>
        {
            new SqliteParameter("@ChallengeId", challengeId)
        };

        if (from.HasValue)
        {
            command += " AND Timestamp >= @From";
            parameters.Add(new SqliteParameter("@From", from.Value.ToString("O")));
        }

        if (to.HasValue)
        {
            command += " AND Timestamp <= @To";
            parameters.Add(new SqliteParameter("@To", to.Value.ToString("O")));
        }

        command += " ORDER BY Timestamp DESC LIMIT @Limit";
        parameters.Add(new SqliteParameter("@Limit", limit));

        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddRange(parameters.ToArray());

        var logs = new List<LogEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(MapLogEntryFromReader(reader));
        }

        return logs;
    }

    public async Task<int> DeleteLogsOlderThanAsync(DateTime cutoffDate)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = "DELETE FROM LogEntries WHERE Timestamp < @CutoffDate";
        using var cmd = new SqliteCommand(command, connection);
        cmd.Parameters.AddWithValue("@CutoffDate", cutoffDate.ToString("O"));

        return await cmd.ExecuteNonQueryAsync();
    }

    // Helper methods for mapping
    private User MapUserFromReader(SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            Salt = reader.GetString(reader.GetOrdinal("Salt")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            LastLoginAt = reader.IsDBNull(reader.GetOrdinal("LastLoginAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastLoginAt"))),
            IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
            Role = Enum.Parse<UserRole>(reader.GetString(reader.GetOrdinal("Role"))),
            TotalPoints = reader.GetInt32(reader.GetOrdinal("TotalPoints")),
            SolvedChallenges = reader.GetInt32(reader.GetOrdinal("SolvedChallenges")),
            Avatar = reader.IsDBNull(reader.GetOrdinal("Avatar")) ? null : reader.GetString(reader.GetOrdinal("Avatar")),
            Bio = reader.IsDBNull(reader.GetOrdinal("Bio")) ? null : reader.GetString(reader.GetOrdinal("Bio")),
            Preferences = reader.IsDBNull(reader.GetOrdinal("Preferences")) ? new Dictionary<string, string>() : 
                         System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("Preferences"))) ?? new Dictionary<string, string>(),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")))
        };
    }

    private Challenge MapChallengeFromReader(SqliteDataReader reader)
    {
        return new Challenge
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Description = reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            Path = reader.GetString(reader.GetOrdinal("Path")),
            Flag = reader.GetString(reader.GetOrdinal("Flag")),
            FlagType = Enum.Parse<FlagType>(reader.GetString(reader.GetOrdinal("FlagType"))),
            Difficulty = Enum.Parse<ChallengeDifficulty>(reader.GetString(reader.GetOrdinal("Difficulty"))),
            Points = reader.GetInt32(reader.GetOrdinal("Points")),
            RequiresContainer = reader.GetInt32(reader.GetOrdinal("RequiresContainer")) == 1,
            ContainerImage = reader.IsDBNull(reader.GetOrdinal("ContainerImage")) ? null : reader.GetString(reader.GetOrdinal("ContainerImage")),
            ContainerPort = reader.IsDBNull(reader.GetOrdinal("ContainerPort")) ? null : reader.GetInt32(reader.GetOrdinal("ContainerPort")),
            Tags = reader.IsDBNull(reader.GetOrdinal("Tags")) ? new List<string>() : 
                   System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("Tags"))) ?? new List<string>(),
            Author = reader.IsDBNull(reader.GetOrdinal("Author")) ? null : reader.GetString(reader.GetOrdinal("Author")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
            SolveCount = reader.GetInt32(reader.GetOrdinal("SolveCount")),
            Hint = reader.IsDBNull(reader.GetOrdinal("Hint")) ? null : reader.GetString(reader.GetOrdinal("Hint")),
            Files = reader.IsDBNull(reader.GetOrdinal("Files")) ? new List<string>() : 
                    System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("Files"))) ?? new List<string>(),
            Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? new Dictionary<string, string>() : 
                      System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("Metadata"))) ?? new Dictionary<string, string>()
        };
    }

    private Submission MapSubmissionFromReader(SqliteDataReader reader)
    {
        return new Submission
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            ChallengeId = reader.GetString(reader.GetOrdinal("ChallengeId")),
            UserId = reader.GetString(reader.GetOrdinal("UserId")),
            SubmittedFlag = reader.GetString(reader.GetOrdinal("SubmittedFlag")),
            IsCorrect = reader.GetInt32(reader.GetOrdinal("IsCorrect")) == 1,
            SubmittedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("SubmittedAt"))),
            IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
            UserAgent = reader.IsDBNull(reader.GetOrdinal("UserAgent")) ? null : reader.GetString(reader.GetOrdinal("UserAgent")),
            PointsAwarded = reader.IsDBNull(reader.GetOrdinal("PointsAwarded")) ? null : reader.GetInt32(reader.GetOrdinal("PointsAwarded")),
            PointsAwardedAt = reader.IsDBNull(reader.GetOrdinal("PointsAwardedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("PointsAwardedAt"))),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? new Dictionary<string, string>() : 
                      System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("Metadata"))) ?? new Dictionary<string, string>()
        };
    }

    private LogEntry MapLogEntryFromReader(SqliteDataReader reader)
    {
        return new LogEntry
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            EventType = reader.GetString(reader.GetOrdinal("EventType")),
            UserId = reader.GetString(reader.GetOrdinal("UserId")),
            ChallengeId = reader.IsDBNull(reader.GetOrdinal("ChallengeId")) ? null : reader.GetString(reader.GetOrdinal("ChallengeId")),
            Details = reader.IsDBNull(reader.GetOrdinal("Details")) ? null : reader.GetString(reader.GetOrdinal("Details")),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
            IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
            UserAgent = reader.IsDBNull(reader.GetOrdinal("UserAgent")) ? null : reader.GetString(reader.GetOrdinal("UserAgent")),
            Level = Enum.Parse<Models.LogLevel>(reader.GetString(reader.GetOrdinal("Level"))),
            Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata")) ? new Dictionary<string, string>() : 
                      System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("Metadata"))) ?? new Dictionary<string, string>()
        };
    }
}

public class SolvedChallenge
{
    public string ChallengeId { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime SolvedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? Category { get; set; }
} 