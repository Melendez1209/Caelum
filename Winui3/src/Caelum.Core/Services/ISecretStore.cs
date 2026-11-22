namespace Caelum.Core.Services;

/// <summary>
///     Stores secrets outside the database, so an API key never sits in the SQLite file in plain
///     text. The desktop shell implements this with DPAPI, which works in both packaged and
///     unpackaged builds.
/// </summary>
public interface ISecretStore
{
    /// <summary>Reads a secret, or <see langword="null" /> when it is not stored.</summary>
    string? Read(string key);

    /// <summary>Stores a secret, replacing any value already held under the same key.</summary>
    void Write(string key, string value);

    /// <summary>Removes a secret. Removing a key that is not stored is harmless.</summary>
    void Delete(string key);
}
