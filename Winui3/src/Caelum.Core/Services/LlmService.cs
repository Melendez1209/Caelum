using Caelum.Core.Data;
using Caelum.Core.Domain;
using Caelum.Core.Localization;

namespace Caelum.Core.Services;

/// <summary>
///     The model behind the assistant. Reads the stored provider configuration and the API key from
///     the secret store, then sends the conversation to an OpenAI compatible endpoint.
/// </summary>
public sealed class LlmService(
    SettingsService settings,
    ISecretStore secrets,
    OpenAiCompatibleLlmClient? client = null) : IDisposable
{
    /// <summary>Key the API key is stored under in the secret store.</summary>
    public const string ApiKeySlot = "llm_api_key";

    /// <summary>How many conversation turns are sent, counting from the most recent.</summary>
    private const int MaxHistoryMessages = 20;

    private readonly OpenAiCompatibleLlmClient _client = client ?? new();
    private readonly StringLocalizer _strings = new();

    /// <summary>Whether a provider, key, base URL and model are all present.</summary>
    public bool IsConfigured
    {
        get
        {
            var current = settings.Current;
            var option = (LlmProviderOption)current.LlmProviderValue;

            if (option == LlmProviderOption.None) return false;
            if (string.IsNullOrWhiteSpace(ReadApiKey())) return false;

            var baseUrl = ResolveBaseUrl(option, current.LlmBaseUrl);
            var model = ResolveModel(option, current.LlmModel);

            return !string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(model);
        }
    }

    /// <summary>Stores or clears the API key. An empty value removes it.</summary>
    public void SetApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            secrets.Delete(ApiKeySlot);
        else
            secrets.Write(ApiKeySlot, apiKey.Trim());
    }

    /// <summary>
    ///     Sends the conversation and streams the reply through <paramref name="progress" />. The
    ///     system prompt and the most recent turns are prepended by this method.
    /// </summary>
    public async Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> history,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var current = settings.Current;
        var option = (LlmProviderOption)current.LlmProviderValue;

        if (option == LlmProviderOption.None)
            return LlmResult.Failure(LlmErrorKind.NotConfigured);

        var apiKey = ReadApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
            return LlmResult.Failure(LlmErrorKind.NotConfigured);

        var baseUrl = ResolveBaseUrl(option, current.LlmBaseUrl);
        var model = ResolveModel(option, current.LlmModel);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
            return LlmResult.Failure(LlmErrorKind.NotConfigured);

        var messages = new List<LlmMessage>
        {
            new("system", _strings.Get("llm_system_prompt"))
        };

        messages.AddRange(history.TakeLast(MaxHistoryMessages));

        return await _client
            .CompleteAsync(baseUrl, apiKey, model, messages, progress: progress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Tests the configuration currently typed into settings, without saving it first. The key
    ///     falls back to the stored one when the caller passes an empty value.
    /// </summary>
    public async Task<LlmResult> TestConnectionAsync(
        string baseUrl,
        string model,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(apiKey) ? ReadApiKey() : apiKey.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(key))
            return LlmResult.Failure(LlmErrorKind.NotConfigured);

        return await _client
            .TestConnectionAsync(baseUrl, key, model, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Asks the model for a short title that summarises the first message of a new session. The
    ///     title falls back to an empty string when the model is unavailable, leaving the caller to
    ///     use the truncated message instead.
    /// </summary>
    public async Task<string> GenerateTitleAsync(
        string firstMessage,
        CancellationToken cancellationToken = default)
    {
        var current = settings.Current;
        var option = (LlmProviderOption)current.LlmProviderValue;

        if (option == LlmProviderOption.None) return string.Empty;

        var apiKey = ReadApiKey();

        if (string.IsNullOrWhiteSpace(apiKey)) return string.Empty;

        var baseUrl = ResolveBaseUrl(option, current.LlmBaseUrl);
        var model = ResolveModel(option, current.LlmModel);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model)) return string.Empty;

        var prompt = _strings.Format("llm_title_prompt", firstMessage);

        var result = await _client
            .CompleteAsync(
                baseUrl,
                apiKey,
                model,
                [new LlmMessage("user", prompt)],
                stream: false,
                maxTokens: 48,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess) return string.Empty;

        var title = result.Content.Trim();

        if (title.Length == 0) return string.Empty;

        // Keep the title on one line and inside the column's reach
        var firstLine = title.Split('\n')[0].Trim();

        return firstLine.Length > 40 ? firstLine[..40] : firstLine;
    }

    private string? ReadApiKey() => secrets.Read(ApiKeySlot);

    private static string ResolveBaseUrl(LlmProviderOption option, string stored)
    {
        if (!string.IsNullOrWhiteSpace(stored)) return stored.Trim();

        return LlmProviderPresets.Find(option)?.DefaultBaseUrl ?? string.Empty;
    }

    private static string ResolveModel(LlmProviderOption option, string stored)
    {
        if (!string.IsNullOrWhiteSpace(stored)) return stored.Trim();

        return LlmProviderPresets.Find(option)?.DefaultModel ?? string.Empty;
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}
