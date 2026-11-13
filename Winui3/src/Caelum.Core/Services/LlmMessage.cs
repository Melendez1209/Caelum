namespace Caelum.Core.Services;

/// <summary>One turn of a model conversation.</summary>
/// <param name="Role">One of <c>system</c>, <c>user</c> or <c>assistant</c>.</param>
/// <param name="Content">The text of the turn.</param>
public sealed record LlmMessage(string Role, string Content);
