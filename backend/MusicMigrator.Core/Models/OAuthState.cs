namespace MusicMigrator.Core.Models;

public record OAuthState(
    string Provider,
    string CodeVerifier,
    string ReturnUrl);
