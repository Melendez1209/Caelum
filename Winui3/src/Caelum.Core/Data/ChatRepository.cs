using Microsoft.EntityFrameworkCore;

namespace Caelum.Core.Data;

/// <summary>Access to the assistant's saved conversations and their messages.</summary>
public interface IChatRepository
{
    /// <summary>Raised after any write so observers can reload.</summary>
    event EventHandler? Changed;

    /// <summary>Every session, most recently active first.</summary>
    Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>A single session, or <see langword="null" /> when the id is unknown.</summary>
    Task<ChatSession?> GetSessionAsync(long sessionId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a session and returns the generated id.</summary>
    Task<long> InsertSessionAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>Renames a session.</summary>
    Task RenameSessionAsync(long sessionId, string title, CancellationToken cancellationToken = default);

    /// <summary>Deletes a session, cascading to its messages.</summary>
    Task DeleteSessionAsync(long sessionId, CancellationToken cancellationToken = default);

    /// <summary>Every message of a session, oldest first.</summary>
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(long sessionId, CancellationToken cancellationToken = default);

    /// <summary>Appends a message to a session.</summary>
    Task InsertMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>Moves a session's activity time forward.</summary>
    Task TouchSessionAsync(long sessionId, long updatedAt, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ChatRepository(CaelumDbContext context) : IChatRepository
{
    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatSession>> GetSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.ChatSessions
            .AsNoTracking()
            .OrderByDescending(session => session.UpdatedAt)
            .ThenByDescending(session => session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<ChatSession?> GetSessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        return context.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> InsertSessionAsync(
        ChatSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        context.ChatSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        RaiseChanged();

        return session.Id;
    }

    /// <inheritdoc />
    public async Task RenameSessionAsync(
        long sessionId,
        string title,
        CancellationToken cancellationToken = default)
    {
        await context.ChatSessions
            .Where(session => session.Id == sessionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.Title, title),
                cancellationToken)
            .ConfigureAwait(false);

        RaiseChanged();
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        // The cascade configured on the relationship removes the messages with the session
        await context.ChatSessions
            .Where(session => session.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        RaiseChanged();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        return await context.ChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId)
            .OrderBy(message => message.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InsertMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        RaiseChanged();
    }

    /// <inheritdoc />
    public async Task TouchSessionAsync(
        long sessionId,
        long updatedAt,
        CancellationToken cancellationToken = default)
    {
        await context.ChatSessions
            .Where(session => session.Id == sessionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.UpdatedAt, updatedAt),
                cancellationToken)
            .ConfigureAwait(false);

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
