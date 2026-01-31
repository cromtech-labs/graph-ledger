using System.Text.Json;
using GraphLedger.Core.Diff;
using GraphLedger.Core.Models;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure app settings
builder.Services.Configure<AppConfiguration>(
    builder.Configuration.GetSection("GraphLedger"));

// Configure Entity Framework with SQLite
// First check user config file (shared with CLI), then fall back to appsettings.json
var dbPath = GetDatabasePath(builder.Configuration);

var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

static string GetDatabasePath(IConfiguration configuration)
{
    // Check for user config file (same as CLI uses)
    var userConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".graphledger",
        "config.json"
    );

    if (File.Exists(userConfigPath))
    {
        try
        {
            var json = File.ReadAllText(userConfigPath);
            var userConfig = JsonSerializer.Deserialize<AppConfiguration>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (userConfig?.Storage?.DatabasePath is { } path && !string.IsNullOrEmpty(path))
            {
                return path;
            }
        }
        catch
        {
            // Fall through to default
        }
    }

    // Fall back to appsettings.json configuration
    return configuration.GetValue<string>("GraphLedger:Storage:DatabasePath")
        ?? "data/graphledger.db";
}

builder.Services.AddDbContextFactory<GraphLedgerDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register services
builder.Services.AddScoped<ISnapshotRepository, SnapshotRepository>();
builder.Services.AddScoped<IDiffEngine, JsonDiffEngine>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GraphLedgerDbContext>>();
    using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
