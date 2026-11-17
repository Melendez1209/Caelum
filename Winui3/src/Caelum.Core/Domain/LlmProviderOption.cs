namespace Caelum.Core.Domain;

/// <summary>
///     Which model provider the student has chosen for the assistant.
/// </summary>
public enum LlmProviderOption
{
    /// <summary>No provider is configured, so the assistant cannot reach a model.</summary>
    None = 0,

    /// <summary>Meituan LongCat, whose OpenAI compatible endpoint needs no extra setup.</summary>
    LongCat = 1,

    /// <summary>Any other service that speaks the OpenAI chat completion protocol.</summary>
    Custom = 2,
}
