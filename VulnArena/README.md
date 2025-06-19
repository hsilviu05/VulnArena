# VulnArena - CTF Challenge Platform

VulnArena is a comprehensive Capture The Flag (CTF) challenge platform built with ASP.NET Core. It provides a secure, scalable environment for hosting cybersecurity challenges with features like Docker containerization, automated scoring, and real-time leaderboards.

## Features

- **Multi-Category Challenges**: Support for Web, Crypto, Reversing, Forensics, and more
- **Docker Integration**: Secure sandboxed environments for each challenge
- **Automated Scoring**: Dynamic scoring with time bonuses and first blood rewards
- **User Authentication**: Secure user registration and session management
- **Real-time Leaderboards**: Live scoring and ranking system
- **Comprehensive Logging**: Detailed audit trails and security monitoring
- **RESTful API**: Full API for frontend integration
- **Rate Limiting**: Protection against brute force attacks
- **Challenge Management**: Easy challenge deployment and configuration

## Project Structure

```
VulnArena/
│
├── Program.cs                  # Entry point with dependency injection
├── appsettings.json            # Configuration for challenges, DB, Docker
│
├── Core/                       # Core business logic
│   ├── ChallengeManager.cs     # Challenge loading and lifecycle
│   ├── FlagValidator.cs        # Secure flag validation
│   ├── ContainerService.cs     # Docker API integration
│   └── ScoreManager.cs         # Points and leaderboard logic
│
├── Models/                     # Data models
│   ├── Challenge.cs           # Challenge entity
│   ├── User.cs                # User entity
│   ├── Submission.cs          # Flag submission entity
│   └── LogEntry.cs            # Audit log entity
│
├── Services/                   # Business services
│   ├── AuthService.cs         # Authentication and authorization
│   ├── LoggingService.cs      # Event logging and monitoring
│   ├── DBService.cs           # SQLite database operations
│   └── SandboxService.cs      # Sandbox environment management
│
├── Web/                        # Web API layer
│   ├── Controllers/
│   │   ├── ChallengesController.cs  # Challenge operations
│   │   └── AuthController.cs        # Authentication endpoints
│   ├── Pages/                 # Optional web pages
│   └── wwwroot/               # Static web assets
│
├── Challenges/                 # Challenge definitions
│   ├── Web/                   # Web security challenges
│   ├── Crypto/                # Cryptography challenges
│   ├── Reversing/             # Reverse engineering challenges
│   └── Forensics/             # Digital forensics challenges
│
└── VulnArena.csproj           # Project configuration
```

## Prerequisites

- .NET 9.0 SDK
- Docker Engine (for containerized challenges)
- SQLite (included, no separate installation needed)

## Installation

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd VulnArena
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Configure the application**:
   Edit `appsettings.json` to configure:
   - Database connection string
   - Docker socket path
   - Challenge paths
   - Security settings

4. **Run the application**:
   ```bash
   dotnet run
   ```

5. **Access the API**:
   - API Documentation: `https://localhost:5001/swagger`
   - API Base URL: `https://localhost:5001/api`

## Configuration

### Database Configuration
```json
{
  "VulnArena": {
    "Database": {
      "ConnectionString": "Data Source=vulnarena.db",
      "Type": "SQLite"
    }
  }
}
```

### Docker Configuration
```json
{
  "VulnArena": {
    "Docker": {
      "SocketPath": "/var/run/docker.sock",
      "DefaultImage": "vulnarena/sandbox:latest",
      "MaxContainers": 50
    }
  }
}
```

### Challenge Configuration
```json
{
  "VulnArena": {
    "Challenges": {
      "BasePath": "./Challenges",
      "AutoReload": true,
      "TimeoutSeconds": 300
    }
  }
}
```

## API Endpoints

### Authentication
- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - User login
- `POST /api/auth/logout` - User logout
- `GET /api/auth/me` - Get current user
- `POST /api/auth/change-password` - Change password

### Challenges
- `GET /api/challenges` - List all challenges
- `GET /api/challenges/{id}` - Get challenge details
- `POST /api/challenges/{id}/start` - Start a challenge
- `POST /api/challenges/{id}/stop` - Stop a challenge
- `POST /api/challenges/{id}/submit` - Submit a flag
- `GET /api/challenges/{id}/sandbox` - Get sandbox status
- `POST /api/challenges/{id}/sandbox/extend` - Extend sandbox time
- `GET /api/challenges/categories` - Get challenge categories

## Challenge Format

Each challenge is defined by a `challenge.json` file:

```json
{
  "title": "Challenge Title",
  "description": "Challenge description",
  "category": "Web",
  "flag": "flag{example_flag}",
  "flagType": "PlainText",
  "difficulty": "Easy",
  "points": 100,
  "requiresContainer": true,
  "containerImage": "vulnarena/challenge:latest",
  "containerPort": 8080,
  "tags": ["web", "sql-injection"],
  "author": "Author Name",
  "hint": "Optional hint for the challenge",
  "files": ["file1.txt", "file2.py"]
}
```

### Flag Types
- `PlainText` - Direct string comparison
- `MD5` - MD5 hash comparison
- `SHA256` - SHA256 hash comparison
- `Regex` - Regular expression matching

### Difficulty Levels
- `Easy` - Beginner level
- `Medium` - Intermediate level
- `Hard` - Advanced level
- `Expert` - Expert level

## Security Features

- **Secure Flag Validation**: Constant-time comparison to prevent timing attacks
- **Rate Limiting**: Configurable rate limits for flag submissions
- **Session Management**: Secure session tokens with expiration
- **Input Validation**: Comprehensive input sanitization
- **Audit Logging**: Detailed logs for security monitoring
- **Container Isolation**: Docker-based sandboxing for challenges

## Docker Integration

VulnArena uses Docker to provide isolated environments for challenges:

- **Container Security**: Read-only filesystems, dropped capabilities
- **Resource Limits**: Memory and CPU restrictions
- **Automatic Cleanup**: Expired containers are automatically removed
- **Health Monitoring**: Container health checks and monitoring

## Development

### Adding New Challenges

1. Create a new directory in the appropriate category folder
2. Add a `challenge.json` configuration file
3. Include any necessary challenge files
4. For containerized challenges, create a Dockerfile
5. Restart the application or use the reload endpoint

### Adding New Categories

1. Create a new directory in the `Challenges/` folder
2. Add challenges to the new category
3. The system will automatically detect and load the new category

### Customizing Scoring

Modify the scoring logic in `ScoreManager.cs`:
- Base points per challenge
- Difficulty multipliers
- Time bonuses
- First blood bonuses

## Monitoring and Logging

VulnArena provides comprehensive logging:

- **Application Logs**: Standard .NET logging
- **Security Events**: Authentication, flag submissions, security violations
- **Performance Metrics**: Response times, resource usage
- **Audit Trails**: User actions and system events

## Deployment

### Production Deployment

1. **Environment Configuration**:
   ```bash
   export ASPNETCORE_ENVIRONMENT=Production
   ```

2. **Database Setup**:
   - Ensure SQLite database is writable
   - Consider using PostgreSQL for production

3. **Docker Configuration**:
   - Ensure Docker daemon is accessible
   - Configure resource limits
   - Set up monitoring

4. **Security Hardening**:
   - Use HTTPS
   - Configure firewall rules
   - Set up proper authentication
   - Enable rate limiting

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out
EXPOSE 80
ENTRYPOINT ["dotnet", "out/VulnArena.dll"]
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For support and questions:
- Create an issue on GitHub
- Check the documentation
- Review the API documentation at `/swagger`

## Roadmap

- [ ] Web-based challenge interface
- [ ] Real-time notifications
- [ ] Team support
- [ ] Advanced challenge types
- [ ] Integration with external tools
- [ ] Mobile application
- [ ] Advanced analytics and reporting 