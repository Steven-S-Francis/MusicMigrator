using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Interfaces;

public interface ITokenStore
{
    void Store(string sessionId, string provider, ProviderToken token);

    ProviderToken? Get(string sessionId, string provider);

    void Remove(string sessionId, string provider);

    bool IsConnected(string sessionId, string provider);
}
