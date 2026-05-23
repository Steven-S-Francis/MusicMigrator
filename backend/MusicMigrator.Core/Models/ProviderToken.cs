namespace MusicMigrator.Core.Models;

public record ProviderToken(
    string AccessToken,
    string? RefreshToken,
    DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-2);
}
