using System.Collections.Concurrent;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Services;

public class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, ProviderToken> _tokens = new();

    private static string BuildKey(string sessionId, string provider) =>
        $"{sessionId}:{provider}";

    public void Store(string sessionId, string provider, ProviderToken token) =>
        _tokens[BuildKey(sessionId, provider)] = token;

    public ProviderToken? Get(string sessionId, string provider) =>
        _tokens.TryGetValue(BuildKey(sessionId, provider), out var token) ? token : null;

    public void Remove(string sessionId, string provider) =>
        _tokens.TryRemove(BuildKey(sessionId, provider), out _);

    public bool IsConnected(string sessionId, string provider) =>
        _tokens.ContainsKey(BuildKey(sessionId, provider));
}
