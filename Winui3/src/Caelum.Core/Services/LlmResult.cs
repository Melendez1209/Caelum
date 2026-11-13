namespace Caelum.Core.Services;

/// <summary>The outcome of a model request.</summary>
/// <param name="Content">
///     The reply text. A failed result may still carry the part of the answer that arrived before the
///     failure, so the interface can show it instead of discarding the turn.
/// </param>
/// <param name="ErrorKind">Why the request failed, or <see cref="LlmErrorKind.None" /> on success.</param>
/// <param name="Detail">Provider supplied detail, for diagnostics only.</param>
public sealed record LlmResult(string Content, LlmErrorKind ErrorKind, string? Detail)
{
    /// <summary>Whether the request produced a complete reply.</summary>
    public bool IsSuccess => ErrorKind == LlmErrorKind.None;

    /// <summary>Builds a successful result.</summary>
    public static LlmResult Success(string content) => new(content, LlmErrorKind.None, null);

    /// <summary>Builds a failed result, optionally keeping a partial answer.</summary>
    public static LlmResult Failure(LlmErrorKind errorKind, string? detail = null, string content = "") =>
        new(content, errorKind, detail);
}
