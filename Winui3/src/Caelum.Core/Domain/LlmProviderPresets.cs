namespace Caelum.Core.Domain;

/// <summary>The endpoint and model a provider preset fills in for the student.</summary>
/// <param name="Option">The provider the preset belongs to.</param>
/// <param name="DefaultBaseUrl">Base URL prefilled when the provider is selected.</param>
/// <param name="DefaultModel">Model name prefilled when the provider is selected.</param>
public sealed record LlmProviderPreset(
    LlmProviderOption Option,
    string DefaultBaseUrl,
    string DefaultModel);

/// <summary>
///     The provider presets offered in settings. LongCat is first because it is the provider this
///     build adapts to; Custom covers every other OpenAI compatible service.
/// </summary>
public static class LlmProviderPresets
{
    /// <summary>Meituan LongCat, the provider this build is adapted to first.</summary>
    public static LlmProviderPreset LongCat { get; } = new(
        LlmProviderOption.LongCat,
        "https://api.longcat.chat/openai",
        "LongCat-2.5");

    /// <summary>A blank preset, so a custom provider stores exactly what the student typed.</summary>
    public static LlmProviderPreset Custom { get; } = new(
        LlmProviderOption.Custom,
        string.Empty,
        string.Empty);

    /// <summary>Finds the preset for a provider, or <see langword="null" /> when it has none.</summary>
    public static LlmProviderPreset? Find(LlmProviderOption option) => option switch
    {
        LlmProviderOption.LongCat => LongCat,
        LlmProviderOption.Custom => Custom,
        _ => null
    };
}
