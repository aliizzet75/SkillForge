using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;
using SkillForge.Api.Hubs;
using SkillForge.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<SkillForgeDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add JWT service
builder.Services.AddScoped<SkillForge.Core.Services.IJwtService, SkillForge.Core.Services.JwtService>();

// Add HTTP Context Accessor for SignalR auth
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SkillForge.Api.Services.IUserContextAccessor, SkillForge.Api.Services.HttpUserContextAccessor>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR with custom auth
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:3001",
                "http://187.124.28.216:3001",
                "https://skillforge.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();

// Add JWT Authentication Middleware before SignalR
app.UseJwtAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map SignalR hub
app.MapHub<GameHub>("/hubs/game");

// Health check endpoint
app.MapGet("/health", async (SkillForgeDbContext dbContext) =>
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
        return Results.Ok(new { status = "healthy", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
});

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SkillForgeDbContext>();
    dbContext.Database.Migrate();
}

app.Run();
