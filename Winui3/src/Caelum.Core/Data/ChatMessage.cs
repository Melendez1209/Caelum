namespace Caelum.Core.Data;

/// <summary>One turn of an assistant conversation, belonging to a <see cref="ChatSession" />.</summary>
public sealed class ChatMessage
{
    /// <summary>Primary key, assigned by the database on insert.</summary>
    public long Id { get; init; }

    /// <summary>The session this message belongs to.</summary>
    public long SessionId { get; init; }

    /// <summary>Either <c>user</c> or <c>assistant</c>.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>The message text.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Time the message was written, as epoch milliseconds.</summary>
    public long CreatedAt { get; init; }

    /// <summary>Navigation to the session this message belongs to.</summary>
    public ChatSession? Session { get; init; }
}
