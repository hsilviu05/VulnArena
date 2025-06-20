using VulnArena.Core;
using VulnArena.Services;
using VulnArena.Web.Controllers;
using Microsoft.Extensions.FileProviders;
using BCrypt.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register custom services
builder.Services.AddSingleton<ChallengeManager>();
builder.Services.AddSingleton<ContainerService>();
builder.Services.AddScoped<FlagValidator>();
builder.Services.AddScoped<ScoreManager>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<DBService>();
builder.Services.AddScoped<SandboxService>();
builder.Services.AddHttpContextAccessor();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Serve static files from the client directory
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "../client")),
    RequestPath = ""
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "../client")),
    RequestPath = ""
});

// Fallback to index.html for SPA, but NOT for /api routes
app.MapFallback(async context =>
{
    var path = context.Request.Path.Value;
    if (path != null && path.StartsWith("/api"))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Not Found");
        return;
    }
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(Directory.GetCurrentDirectory(), "../client/index.html"));
});

// Initialize services
using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<DBService>();
    await dbService.InitializeDatabaseAsync();
    
    // Create default admin user if it doesn't exist
    var adminUser = await dbService.GetUserByUsernameAsync("admin");
    if (adminUser == null)
    {
        var admin = new VulnArena.Models.User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "admin",
            Email = "admin@vulnarena.local",
            Role = VulnArena.Models.UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Hash password (default: admin123)
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", salt);
        admin.Salt = salt;

        await dbService.CreateUserAsync(admin);
        Console.WriteLine("Default admin user created: admin/admin123");
    }
    
    var challengeManager = scope.ServiceProvider.GetRequiredService<ChallengeManager>();
    await challengeManager.LoadChallengesAsync();
}

app.Run();
