using System.Collections.ObjectModel;
using Caelum.Core.Data;
using Caelum.Core.Services;

namespace Caelum.Services;

/// <summary>
///     Holds the assistant's saved sessions in memory as an observable collection, so the chat page
///     and the session list both react to one write. It mirrors <see cref="ExamService" />.
/// </summary>
public sealed class ChatService
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SettingsService _settings;

    /// <summary>Creates the service over a repository and starts listening for writes.</summary>
    public ChatService(IChatRepository repository, SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(settings);

        Repository = repository;
        _settings = settings;
        repository.Changed += OnRepositoryChanged;
    }

    /// <summary>The repository underneath, for the pages that need a one-off query.</summary>
    private IChatRepository Repository { get; }

    /// <summary>Every saved session, most recently active first.</summary>
    public ObservableCollection<ChatSession> Sessions { get; } = [];

    /// <summary>The session the chat page is currently showing.</summary>
    public ChatSession? CurrentSession { get; private set; }

    /// <summary>Raised on the UI thread after <see cref="Sessions" /> reload.</summary>
    public event EventHandler? Reloaded;

    /// <summary>
    ///     Loads the sessions and picks the one the student left open. When there is none at all, a
    ///     default session is created so the page is usable on the first run.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(true);

        try
        {
            var sessions = await Repository.GetSessionsAsync(cancellationToken).ConfigureAwait(true);

            var storedId = _settings.Current.CurrentChatSessionId;
            var current = sessions.FirstOrDefault(session => session.Id == storedId)
                ?? sessions.FirstOrDefault();

            // A stored id that no longer matches (for example after the session was deleted) is
            // replaced with the most recent one, so the next start reopens the same conversation
            if (current is not null && current.Id != storedId)
                await _settings.SetCurrentChatSessionAsync(current.Id, cancellationToken).ConfigureAwait(true);

            if (current is null)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var id = await Repository.InsertSessionAsync(
                    new ChatSession
                    {
                        Title = string.Empty,
                        CreatedAt = now,
                        UpdatedAt = now
                    },
                    cancellationToken).ConfigureAwait(true);

                sessions = await Repository.GetSessionsAsync(cancellationToken).ConfigureAwait(true);
                current = sessions.First(session => session.Id == id);
            }

            Replace(Sessions, sessions);
            CurrentSession = current;

            Reloaded?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>Creates a new empty session and makes it current.</summary>
    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var id = await Repository.InsertSessionAsync(
            new ChatSession
            {
                Title = string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            },
            cancellationToken).ConfigureAwait(true);

        await LoadAsync(cancellationToken).ConfigureAwait(true);
        await SwitchAsync(id, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    ///     Deletes a session. Another one takes its place, or a default is created when it was the
    ///     last, so the chat page never ends up without a session.
    /// </summary>
    public async Task DeleteAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        await Repository.DeleteSessionAsync(sessionId, cancellationToken).ConfigureAwait(true);

        var remaining = await Repository.GetSessionsAsync(cancellationToken).ConfigureAwait(true);

        if (remaining.Count == 0)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            return;
        }

        if (CurrentSession?.Id == sessionId)
            await SwitchAsync(remaining[0].Id, cancellationToken).ConfigureAwait(true);
        else
            await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Makes a session current and stores the choice in the settings row.</summary>
    public async Task SwitchAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(true);

        try
        {
            var session = await Repository.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(true);

            if (session is null) return;

            CurrentSession = session;

            await _settings.SetCurrentChatSessionAsync(sessionId, cancellationToken).ConfigureAwait(true);

            Reloaded?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>Renames a session and refreshes the list.</summary>
    public async Task RenameAsync(long sessionId, string title, CancellationToken cancellationToken = default)
    {
        await Repository.RenameSessionAsync(sessionId, title, cancellationToken).ConfigureAwait(true);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Every message of a session, oldest first.</summary>
    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        return Repository.GetMessagesAsync(sessionId, cancellationToken);
    }

    /// <summary>Appends a message to a session and moves its activity time forward.</summary>
    public async Task AddMessageAsync(
        long sessionId,
        string role,
        string content,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await Repository.InsertMessageAsync(
            new ChatMessage
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                CreatedAt = now
            },
            cancellationToken).ConfigureAwait(true);

        await Repository.TouchSessionAsync(sessionId, now, cancellationToken).ConfigureAwait(true);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items) target.Add(item);
    }

    private async void OnRepositoryChanged(object? sender, EventArgs e)
    {
        try
        {
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // A refresh failure must not tear the process down; the next write will retry
        }
    }
}
