using VulnArena.Core;
using VulnArena.Services;
using VulnArena.Web.Controllers;

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
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LoggingService>();
builder.Services.AddScoped<DBService>();
builder.Services.AddScoped<SandboxService>();

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

// Initialize services
using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<DBService>();
    await dbService.InitializeDatabaseAsync();
    
    var challengeManager = scope.ServiceProvider.GetRequiredService<ChallengeManager>();
    await challengeManager.LoadChallengesAsync();
}

app.Run();
