using Docker.DotNet;
using Docker.DotNet.Models;
using VulnArena.Services;

namespace VulnArena.Core;

public class ContainerService
{
    private readonly ILogger<ContainerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly DockerClient _dockerClient;
    private readonly string _defaultImage;
    private readonly int _maxContainers;

    public ContainerService(
        ILogger<ContainerService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        var socketPath = _configuration["VulnArena:Docker:SocketPath"] ?? "/var/run/docker.sock";
        _dockerClient = new DockerClientConfiguration(new Uri($"unix://{socketPath}")).CreateClient();
        _defaultImage = _configuration["VulnArena:Docker:DefaultImage"] ?? "vulnarena/sandbox:latest";
        _maxContainers = int.Parse(_configuration["VulnArena:Docker:MaxContainers"] ?? "50");
    }

    public async Task<string?> StartContainerAsync(string challengeId, string userId)
    {
        try
        {
            // Check if container already exists for this user and challenge
            var existingContainer = await GetContainerAsync(challengeId, userId);
            if (existingContainer != null)
            {
                _logger.LogInformation("Container already exists for challenge {ChallengeId} and user {UserId}", challengeId, userId);
                return existingContainer.ID;
            }

            // Check container limits
            if (await GetActiveContainerCountAsync() >= _maxContainers)
            {
                _logger.LogWarning("Maximum container limit reached ({MaxContainers})", _maxContainers);
                return null;
            }

            var containerName = $"vulnarena_{challengeId}_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            
            // Create container parameters
            var createParams = new CreateContainerParameters
            {
                Image = _defaultImage,
                Name = containerName,
                Env = new List<string>
                {
                    $"CHALLENGE_ID={challengeId}",
                    $"USER_ID={userId}",
                    "VULNARENA_ENV=production"
                },
                HostConfig = new HostConfig
                {
                    Memory = 512 * 1024 * 1024, // 512MB
                    MemorySwap = 0,
                    PublishAllPorts = true,
                    RestartPolicy = new RestartPolicy
                    {
                        Name = RestartPolicyKind.No
                    },
                    SecurityOpt = new List<string>
                    {
                        "no-new-privileges"
                    },
                    CapDrop = new List<string>
                    {
                        "ALL"
                    },
                    ReadonlyRootfs = true
                },
                Labels = new Dictionary<string, string>
                {
                    { "vulnarena.challenge", challengeId },
                    { "vulnarena.user", userId },
                    { "vulnarena.created", DateTime.UtcNow.ToString("O") }
                }
            };

            // Create the container
            var createResponse = await _dockerClient.Containers.CreateContainerAsync(createParams);
            
            if (createResponse.Warnings?.Any() == true)
            {
                _logger.LogWarning("Container creation warnings: {Warnings}", string.Join(", ", createResponse.Warnings));
            }

            // Start the container
            var started = await _dockerClient.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters());
            
            if (!started)
            {
                _logger.LogError("Failed to start container {ContainerId}", createResponse.ID);
                await _dockerClient.Containers.RemoveContainerAsync(createResponse.ID, new ContainerRemoveParameters());
                return null;
            }

            _logger.LogInformation("Started container {ContainerId} for challenge {ChallengeId} and user {UserId}", 
                createResponse.ID, challengeId, userId);

            return createResponse.ID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting container for challenge {ChallengeId}", challengeId);
            return null;
        }
    }

    public async Task<bool> StopContainerAsync(string challengeId, string userId)
    {
        try
        {
            var container = await GetContainerAsync(challengeId, userId);
            if (container == null)
            {
                _logger.LogInformation("No container found for challenge {ChallengeId} and user {UserId}", challengeId, userId);
                return true;
            }

            // Stop the container
            await _dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 10
            });

            // Remove the container
            await _dockerClient.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters
            {
                Force = true,
                RemoveVolumes = true
            });

            _logger.LogInformation("Stopped and removed container {ContainerId}", container.ID);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping container for challenge {ChallengeId}", challengeId);
            return false;
        }
    }

    public async Task<ContainerListResponse?> GetContainerAsync(string challengeId, string userId)
    {
        try
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "label", new Dictionary<string, bool>
                        {
                            { $"vulnarena.challenge={challengeId}", true },
                            { $"vulnarena.user={userId}", true }
                        }
                    }
                }
            });

            return containers.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container for challenge {ChallengeId}", challengeId);
            return null;
        }
    }

    public async Task<IEnumerable<ContainerListResponse>> GetActiveContainersAsync()
    {
        try
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = false,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "label", new Dictionary<string, bool>
                        {
                            { "vulnarena.challenge", true }
                        }
                    }
                }
            });

            return containers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active containers");
            return Enumerable.Empty<ContainerListResponse>();
        }
    }

    public async Task<int> GetActiveContainerCountAsync()
    {
        var containers = await GetActiveContainersAsync();
        return containers.Count();
    }

    public async Task<bool> IsContainerHealthyAsync(string containerId)
    {
        try
        {
            var container = await _dockerClient.Containers.InspectContainerAsync(containerId);
            return container.State.Running && container.State.Health?.Status == "healthy";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking container health for {ContainerId}", containerId);
            return false;
        }
    }

    public async Task CleanupExpiredContainersAsync()
    {
        try
        {
            var containers = await GetActiveContainersAsync();
            var cutoffTime = DateTime.UtcNow.AddHours(-2); // 2 hour timeout

            foreach (var container in containers)
            {
                if (container.Labels.TryGetValue("vulnarena.created", out var createdStr) &&
                    DateTime.TryParse(createdStr, out var created) &&
                    created < cutoffTime)
                {
                    _logger.LogInformation("Cleaning up expired container {ContainerId}", container.ID);
                    await StopContainerAsync(
                        container.Labels["vulnarena.challenge"],
                        container.Labels["vulnarena.user"]
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired containers");
        }
    }
} 