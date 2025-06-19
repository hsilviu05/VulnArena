using System.Text.Json;
using System.Text.Json.Serialization;
using VulnArena.Models;
using VulnArena.Services;

namespace VulnArena.Core;

public class ChallengeManager
{
    private readonly ILogger<ChallengeManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly ContainerService _containerService;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Challenge> _challenges;
    private readonly string _challengesPath;

    public ChallengeManager(
        ILogger<ChallengeManager> logger,
        IConfiguration configuration,
        ContainerService containerService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _containerService = containerService;
        _serviceProvider = serviceProvider;
        _challenges = new Dictionary<string, Challenge>();
        _challengesPath = _configuration["VulnArena:Challenges:BasePath"] ?? "./Challenges";
    }

    public async Task LoadChallengesAsync()
    {
        try
        {
            _logger.LogInformation("Loading challenges from {Path}", _challengesPath);
            
            if (!Directory.Exists(_challengesPath))
            {
                _logger.LogWarning("Challenges directory does not exist: {Path}", _challengesPath);
                return;
            }

            _logger.LogInformation("Challenges directory exists, scanning for categories...");
            var categoryDirs = Directory.GetDirectories(_challengesPath);
            _logger.LogInformation("Found {Count} category directories: {Categories}", 
                categoryDirs.Length, string.Join(", ", categoryDirs.Select(Path.GetFileName)));
            
            foreach (var categoryDir in categoryDirs)
            {
                var category = Path.GetFileName(categoryDir);
                _logger.LogInformation("Loading challenges from category: {Category}", category);
                await LoadChallengesFromCategoryAsync(category, categoryDir);
            }

            _logger.LogInformation("Loaded {Count} challenges", _challenges.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading challenges");
        }
    }

    private async Task LoadChallengesFromCategoryAsync(string category, string categoryPath)
    {
        var challengeDirs = Directory.GetDirectories(categoryPath);
        _logger.LogInformation("Found {Count} challenge directories in category {Category}", challengeDirs.Length, category);
        
        foreach (var challengeDir in challengeDirs)
        {
            var challengeId = Path.GetFileName(challengeDir);
            var configPath = Path.Combine(challengeDir, "challenge.json");
            
            _logger.LogInformation("Processing challenge {ChallengeId} from {ConfigPath}", challengeId, configPath);
            
            if (File.Exists(configPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(configPath);
                    _logger.LogInformation("Read JSON for challenge {ChallengeId}: {JsonLength} characters", challengeId, json.Length);
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                    };
                    var challenge = JsonSerializer.Deserialize<Challenge>(json, options);
                    
                    if (challenge != null)
                    {
                        challenge.Id = challengeId;
                        challenge.Category = category;
                        challenge.Path = challengeDir;
                        _challenges[challengeId] = challenge;
                        
                        _logger.LogInformation("Successfully loaded challenge: {Id} ({Category}) - {Title}", challengeId, category, challenge.Title);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize challenge {ChallengeId} - result is null", challengeId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading challenge {ChallengeId}", challengeId);
                }
            }
            else
            {
                _logger.LogWarning("Challenge config file not found: {ConfigPath}", configPath);
            }
        }
    }

    public async Task<Challenge?> GetChallengeAsync(string challengeId)
    {
        return _challenges.TryGetValue(challengeId, out var challenge) ? challenge : null;
    }

    public async Task<IEnumerable<Challenge>> GetAllChallengesAsync()
    {
        return _challenges.Values;
    }

    public async Task<IEnumerable<Challenge>> GetChallengesByCategoryAsync(string category)
    {
        return _challenges.Values.Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> StartChallengeAsync(string challengeId, string userId)
    {
        try
        {
            if (!_challenges.TryGetValue(challengeId, out var challenge))
            {
                _logger.LogWarning("Challenge not found: {ChallengeId}", challengeId);
                return false;
            }

            if (challenge.RequiresContainer)
            {
                var containerId = await _containerService.StartContainerAsync(challengeId, userId);
                if (string.IsNullOrEmpty(containerId))
                {
                    _logger.LogError("Failed to start container for challenge {ChallengeId}", challengeId);
                    return false;
                }
                
                // Store container info in database
                using var scope = _serviceProvider.CreateScope();
                var containerDbService = scope.ServiceProvider.GetRequiredService<DBService>();
                await containerDbService.RecordContainerStartAsync(challengeId, userId, containerId);
            }

            using var dbScope = _serviceProvider.CreateScope();
            var challengeDbService = dbScope.ServiceProvider.GetRequiredService<DBService>();
            await challengeDbService.RecordChallengeStartAsync(challengeId, userId);
            _logger.LogInformation("Started challenge {ChallengeId} for user {UserId}", challengeId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting challenge {ChallengeId}", challengeId);
            return false;
        }
    }

    public async Task<bool> StopChallengeAsync(string challengeId, string userId)
    {
        try
        {
            if (challengeId != null && _challenges.TryGetValue(challengeId, out var challenge) && challenge.RequiresContainer)
            {
                await _containerService.StopContainerAsync(challengeId, userId);
            }

            using var scope = _serviceProvider.CreateScope();
            var dbService = scope.ServiceProvider.GetRequiredService<DBService>();
            await dbService.RecordChallengeStopAsync(challengeId, userId);
            _logger.LogInformation("Stopped challenge {ChallengeId} for user {UserId}", challengeId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping challenge {ChallengeId}", challengeId);
            return false;
        }
    }

    public async Task ReloadChallengesAsync()
    {
        _challenges.Clear();
        await LoadChallengesAsync();
    }
} 