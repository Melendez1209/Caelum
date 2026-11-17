namespace Caelum.Core.Services;

/// <summary>Why a model request did not produce a reply.</summary>
public enum LlmErrorKind
{
    /// <summary>The request succeeded.</summary>
    None = 0,

    /// <summary>No provider, key, base URL or model has been configured.</summary>
    NotConfigured = 1,

    /// <summary>The request never reached the model, or the answer could not be read.</summary>
    Network = 2,

    /// <summary>The provider rejected the API key.</summary>
    Unauthorized = 3,

    /// <summary>The endpoint or model name was not found.</summary>
    NotFound = 4,

    /// <summary>The provider asked the client to slow down.</summary>
    RateLimited = 5,

    /// <summary>The student or the page cancelled the request.</summary>
    Cancelled = 6,

    /// <summary>Any other failure, including an unreadable answer.</summary>
    Unknown = 7,
}
