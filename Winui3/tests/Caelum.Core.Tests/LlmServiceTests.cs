using Caelum.Core.Data;
using Caelum.Core.Domain;
using Caelum.Core.Services;
using Xunit;

namespace Caelum.Core.Tests;

/// <summary>
///     Covers the assistant service's configuration checks and the shape of the request it sends.
/// </summary>
public sealed class LlmServiceTests
{
    private static readonly Settings ConfiguredSettings = new()
    {
        LlmProviderValue = (int)LlmProviderOption.LongCat,
        LlmBaseUrl = "https://api.longcat.chat/openai",
        LlmModel = "LongCat-2.5"
    };

    [Fact]
    public void IsConfiguredRequiresAKey()
    {
        var secrets = new InMemorySecretStore();
        var service = new LlmService(CreateSettingsService(ConfiguredSettings), secrets);

        Assert.False(service.IsConfigured);

        secrets.Write(LlmService.ApiKeySlot, "key");

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void IsConfiguredFallsBackToTheLongCatPreset()
    {
        var settings = new Settings { LlmProviderValue = (int)LlmProviderOption.LongCat };

        var secrets = new InMemorySecretStore();
        secrets.Write(LlmService.ApiKeySlot, "key");

        var service = new LlmService(CreateSettingsService(settings), secrets);

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void IsConfiguredIsFalseWithoutAProvider()
    {
        var secrets = new InMemorySecretStore();
        secrets.Write(LlmService.ApiKeySlot, "key");

        var service = new LlmService(CreateSettingsService(new Settings()), secrets);

        Assert.False(service.IsConfigured);
    }

    [Fact]
    public void SetApiKeyWritesAndClearsTheSecret()
    {
        var secrets = new InMemorySecretStore();
        var service = new LlmService(CreateSettingsService(new Settings()), secrets);

        service.SetApiKey("abc");

        Assert.Equal("abc", secrets.Read(LlmService.ApiKeySlot));

        service.SetApiKey(string.Empty);

        Assert.Null(secrets.Read(LlmService.ApiKeySlot));
    }

    [Fact]
    public async Task CompleteAsyncPrependsTheSystemPromptAndTrimsHistory()
    {
        var secrets = new InMemorySecretStore();
        secrets.Write(LlmService.ApiKeySlot, "key");

        var handler = new FakeHttpMessageHandler(_ => JsonResponseFactory.Completion("ok"));

        using var client = new OpenAiCompatibleLlmClient(handler);

        var service = new LlmService(CreateSettingsService(ConfiguredSettings), secrets, client);

        var history = Enumerable.Range(0, 30)
            .Select(index => new LlmMessage("user", $"message {index}"))
            .ToList();

        var result = await service.CompleteAsync(history);

        Assert.True(result.IsSuccess);

        var body = handler.LastBody!;

        Assert.StartsWith("{\"model\":\"LongCat-2.5\"", body);
        Assert.Contains("\"role\":\"system\"", body);
        Assert.Contains("\"role\":\"user\"", body);

        // The system prompt and the most recent turns are kept, the oldest turns are dropped
        Assert.DoesNotContain("message 0", body);
        Assert.Contains("message 29", body);
    }

    private static SettingsService CreateSettingsService(Settings settings)
    {
        var service = new SettingsService(new FakeSettingsRepository(settings));

        service.LoadAsync().GetAwaiter().GetResult();

        return service;
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private Settings _settings;

        public FakeSettingsRepository(Settings settings) => _settings = settings;

        public event EventHandler? Changed;

        public Task<Settings> InitialiseAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task<Settings?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Settings?>(_settings);

        public Task SaveAsync(Settings updated, CancellationToken cancellationToken = default)
        {
            _settings = updated;

            Changed?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public string? Read(string key) =>
            _secrets.TryGetValue(key, out var value) ? value : null;

        public void Write(string key, string value) => _secrets[key] = value;

        public void Delete(string key) => _secrets.Remove(key);
    }
}
