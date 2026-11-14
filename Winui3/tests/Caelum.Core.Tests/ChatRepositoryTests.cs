using Caelum.Core.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Caelum.Core.Tests;

/// <summary>
///     Exercises the assistant's session and message storage against a real SQLite database, so the
///     cascade and the ordering the chat page relies on are verified.
/// </summary>
public sealed class ChatRepositoryTests : IAsyncLifetime
{
    private CaelumDbContext _context = null!;
    private SqliteConnection _keepAlive = null!;
    private ChatRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _keepAlive = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = $"caelum-test-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString());

        await _keepAlive.OpenAsync();

        _context = CaelumDatabase.CreateContextFor(_keepAlive.ConnectionString);
        await _context.Database.EnsureCreatedAsync();

        _repository = new ChatRepository(_context);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    [Fact]
    public async Task InsertingASessionAndMessagesRoundTrips()
    {
        var sessionId = await _repository.InsertSessionAsync(
            new ChatSession { Title = "First", CreatedAt = 1_000, UpdatedAt = 2_000 });

        await _repository.InsertMessageAsync(
            new ChatMessage { SessionId = sessionId, Role = "user", Content = "Hi", CreatedAt = 1_500 });

        var session = await _repository.GetSessionAsync(sessionId);

        Assert.NotNull(session);
        Assert.Equal("First", session!.Title);
        Assert.Equal(2_000, session.UpdatedAt);

        var messages = await _repository.GetMessagesAsync(sessionId);

        var message = Assert.Single(messages);
        Assert.Equal("user", message.Role);
        Assert.Equal("Hi", message.Content);
    }

    [Fact]
    public async Task AnUnknownSessionLoadsNothing()
    {
        Assert.Null(await _repository.GetSessionAsync(404));
        Assert.Empty(await _repository.GetMessagesAsync(404));
    }

    [Fact]
    public async Task MessagesComeBackOldestFirst()
    {
        var sessionId = await _repository.InsertSessionAsync(
            new ChatSession { Title = "Order", CreatedAt = 0, UpdatedAt = 0 });

        await _repository.InsertMessageAsync(
            new ChatMessage { SessionId = sessionId, Role = "user", Content = "later", CreatedAt = 2_000 });
        await _repository.InsertMessageAsync(
            new ChatMessage { SessionId = sessionId, Role = "assistant", Content = "earlier", CreatedAt = 1_000 });

        var messages = await _repository.GetMessagesAsync(sessionId);

        Assert.Equal(["later", "earlier"], messages.Select(message => message.Content));
    }

    [Fact]
    public async Task SessionsAreOrderedByActivityNewestFirst()
    {
        await _repository.InsertSessionAsync(
            new ChatSession { Title = "Old", CreatedAt = 0, UpdatedAt = 1_000 });
        await _repository.InsertSessionAsync(
            new ChatSession { Title = "New", CreatedAt = 0, UpdatedAt = 3_000 });
        await _repository.InsertSessionAsync(
            new ChatSession { Title = "Middle", CreatedAt = 0, UpdatedAt = 2_000 });

        var sessions = await _repository.GetSessionsAsync();

        Assert.Equal(["New", "Middle", "Old"], sessions.Select(session => session.Title));
    }

    [Fact]
    public async Task RenamingASessionUpdatesTheTitle()
    {
        var sessionId = await _repository.InsertSessionAsync(
            new ChatSession { Title = "Old", CreatedAt = 0, UpdatedAt = 0 });

        await _repository.RenameSessionAsync(sessionId, "New");

        var session = await _repository.GetSessionAsync(sessionId);

        Assert.NotNull(session);
        Assert.Equal("New", session!.Title);
    }

    [Fact]
    public async Task TouchingASessionUpdatesItsActivityTime()
    {
        var sessionId = await _repository.InsertSessionAsync(
            new ChatSession { Title = "Session", CreatedAt = 0, UpdatedAt = 1_000 });

        await _repository.TouchSessionAsync(sessionId, 9_000);

        var session = await _repository.GetSessionAsync(sessionId);

        Assert.NotNull(session);
        Assert.Equal(9_000, session!.UpdatedAt);
    }

    [Fact]
    public async Task DeletingASessionDeletesItsMessages()
    {
        var keepId = await _repository.InsertSessionAsync(
            new ChatSession { Title = "Keep", CreatedAt = 0, UpdatedAt = 0 });
        var dropId = await _repository.InsertSessionAsync(
            new ChatSession { Title = "Drop", CreatedAt = 0, UpdatedAt = 0 });

        await _repository.InsertMessageAsync(
            new ChatMessage { SessionId = keepId, Role = "user", Content = "keep", CreatedAt = 0 });
        await _repository.InsertMessageAsync(
            new ChatMessage { SessionId = dropId, Role = "user", Content = "drop", CreatedAt = 0 });

        await _repository.DeleteSessionAsync(dropId);

        var sessions = await _repository.GetSessionsAsync();

        Assert.Equal("Keep", Assert.Single(sessions).Title);
        Assert.Empty(await _repository.GetMessagesAsync(dropId));

        var kept = await _repository.GetMessagesAsync(keepId);

        Assert.Equal("keep", Assert.Single(kept).Content);
    }

    [Fact]
    public async Task WritingRaisesAChangeNotification()
    {
        var raised = 0;
        _repository.Changed += (_, _) => raised++;

        var sessionId = await _repository.InsertSessionAsync(
            new ChatSession { Title = "Session", CreatedAt = 0, UpdatedAt = 0 });
        await _repository.InsertMessageAsync(
            new ChatMessage { SessionId = sessionId, Role = "user", Content = "Hi", CreatedAt = 0 });
        await _repository.DeleteSessionAsync(sessionId);

        Assert.Equal(3, raised);
    }
}
