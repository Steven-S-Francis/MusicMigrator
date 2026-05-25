using MusicMigrator.API;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Services;
using MusicMigrator.Providers.Anghami;
using MusicMigrator.Providers.Spotify;
using MusicMigrator.Providers.YouTube;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".MusicMigrator.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(2);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<OAuthStateStore>();
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<IMigrationJobStore, InMemoryMigrationJobStore>();
builder.Services.AddSingleton<AnghamiPlaywrightWriter>();

builder.Services.AddScoped<IMusicProvider, SpotifyService>();
builder.Services.AddScoped<IMusicProvider, YouTubeMusicService>();
// SDK-based: activate when Anghami SDK is available
// builder.Services.AddScoped<IMusicProvider, AnghamiService>()
builder.Services.AddScoped<IMusicProvider, AnghamiPlaywrightFullService>();
builder.Services.AddScoped<ITrackMatcher, FuzzyTrackMatcher>();
builder.Services.AddScoped<YouTubeMusicService>();
builder.Services.AddScoped<MigrationOrchestrator>();
builder.Services.AddScoped<SpotifyAuthHandler>();
builder.Services.AddScoped<YouTubeAuthHandler>();
builder.Services.AddScoped<AnghamiAuthHandler>();

builder.Services.AddHttpClient<YouTubeAuthHandler>();
builder.Services.AddHttpClient<AnghamiAuthHandler>();
builder.Services.AddHttpClient<AnghamiApiClient>(client =>
{
    client.BaseAddress = new Uri("https://sdk.anghami.com");
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHttpClient<AnghamiGatewayClient>(client =>
{
    client.BaseAddress = new Uri("https://coussa.anghami.com");
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseSession();

app.MapAuthEndpoints();
app.MapPlaylistEndpoints();
app.MapMigrationEndpoints();
app.MapGatewayEndpoints();

app.Run();
