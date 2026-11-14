namespace Caelum.Core.Data;

/// <summary>
///     One assistant conversation. Deleting it cascades to every <see cref="ChatMessage" />.
/// </summary>
public sealed class ChatSession
{
    /// <summary>Primary key, assigned by the database on insert.</summary>
    public long Id { get; set; }

    /// <summary>Display title, either the model's summary of the first message or a default.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Creation time as epoch milliseconds.</summary>
    public long CreatedAt { get; set; }

    /// <summary>Time of the most recent message as epoch milliseconds, used to order the list.</summary>
    public long UpdatedAt { get; set; }

    /// <summary>Navigation to the messages of this session.</summary>
    public List<ChatMessage> Messages { get; init; } = [];
}
