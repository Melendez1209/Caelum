using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Caelum.Core.Data;
using Caelum.Core.Services;

namespace Caelum.Services;

/// <summary>
///     Stores secrets encrypted with the current user's DPAPI credentials. The Windows credential
///     vault is not usable inside the MSIX container a packaged WinUI 3 app runs in, so DPAPI is
///     used instead; it works in both packaged and unpackaged builds and never leaves the machine.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public DpapiSecretStore()
    {
        _path = Path.Combine(CaelumDatabase.ResolveDataDirectory(), "secrets.dat");
    }

    /// <inheritdoc />
    public string? Read(string key)
    {
        lock (_gate)
        {
            try
            {
                var container = ReadContainer();

                if (!container.TryGetValue(key, out var protectedBytes)) return null;

                var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                // A file that cannot be read or decrypted is treated as empty, so the settings page
                // reports the assistant as not configured rather than crashing
                return null;
            }
        }
    }

    /// <inheritdoc />
    public void Write(string key, string value)
    {
        lock (_gate)
        {
            var container = ReadContainer();

            container[key] = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value),
                null,
                DataProtectionScope.CurrentUser);

            WriteContainer(container);
        }
    }

    /// <inheritdoc />
    public void Delete(string key)
    {
        lock (_gate)
        {
            var container = ReadContainer();

            if (!container.Remove(key)) return;

            if (container.Count == 0)
            {
                File.Delete(_path);
                return;
            }

            WriteContainer(container);
        }
    }

    private Dictionary<string, byte[]> ReadContainer()
    {
        try
        {
            if (!File.Exists(_path)) return new Dictionary<string, byte[]>(StringComparer.Ordinal);

            var json = File.ReadAllText(_path);

            return JsonSerializer.Deserialize<Dictionary<string, byte[]>>(json)
                ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            // A corrupted file is treated as empty, so the next write can rebuild it
            return new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }
    }

    private void WriteContainer(Dictionary<string, byte[]> container)
    {
        var directory = Path.GetDirectoryName(_path);

        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(container);

        File.WriteAllText(_path, json);
    }
}
