using System.Collections.Concurrent;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Services;

public class OAuthStateStore
{
    private readonly ConcurrentDictionary<string, (OAuthState State, string SessionId)> _store = new();

    public void Save(string state, OAuthState oauthState, string sessionId)
    {
        _store[state] = (oauthState, sessionId);
    }

    public (OAuthState? State, string? SessionId) Consume(string state)
    {
        if (_store.TryRemove(state, out var entry))
            return (entry.State, entry.SessionId);

        return (null, null);
    }
}
