using Microsoft.EntityFrameworkCore;
using SkillForge.Core.Data;
using SkillForge.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<SkillForgeDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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
