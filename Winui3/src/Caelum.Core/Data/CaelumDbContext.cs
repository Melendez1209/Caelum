using Microsoft.EntityFrameworkCore;

namespace Caelum.Core.Data;

/// <summary>
///     The application's SQLite database. Schema version 1 matches the Android build table for table:
///     three tables, a cascading foreign key from scores to exams, and a unique index so a subject can
///     only be scored once per exam.
/// </summary>
public sealed class CaelumDbContext(DbContextOptions<CaelumDbContext> options) : DbContext(options)
{
    /// <summary>Every recorded exam.</summary>
    public DbSet<Exam> Exams => Set<Exam>();

    /// <summary>Every subject score belonging to an exam.</summary>
    public DbSet<ExamScore> ExamScores => Set<ExamScore>();

    /// <summary>The single settings row.</summary>
    public DbSet<Settings> Settings => Set<Settings>();

    /// <summary>Every saved assistant conversation.</summary>
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    /// <summary>Every assistant message, belonging to a session.</summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Exam>(entity =>
        {
            entity.ToTable("exams");

            entity.HasKey(exam => exam.Id);
            entity.Property(exam => exam.Id).ValueGeneratedOnAdd();
            entity.Property(exam => exam.Name).IsRequired().HasMaxLength(256);
            entity.Property(exam => exam.IsFavorite).HasDefaultValue(false);

            entity.HasMany(exam => exam.Scores)
                .WithOne(score => score.Exam!)
                .HasForeignKey(score => score.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamScore>(entity =>
        {
            entity.ToTable("exam_scores");

            entity.HasKey(score => score.Id);
            entity.Property(score => score.Id).ValueGeneratedOnAdd();
            entity.Property(score => score.SubjectKey).IsRequired().HasMaxLength(64);

            entity.HasIndex(score => score.ExamId);
            entity.HasIndex(score => new { score.ExamId, score.SubjectKey }).IsUnique();
        });

        modelBuilder.Entity<Settings>(entity =>
        {
            entity.ToTable("settings");

            entity.HasKey(settings => settings.Id);
            entity.Property(settings => settings.Id).ValueGeneratedNever();
            entity.Property(settings => settings.SelectedLanguage).IsRequired().HasMaxLength(32);
            entity.Property(settings => settings.Region).IsRequired().HasMaxLength(128);
            entity.Property(settings => settings.SelectedSubjects).IsRequired().HasMaxLength(512);
            entity.Property(settings => settings.LlmBaseUrl).IsRequired().HasMaxLength(512);
            entity.Property(settings => settings.LlmModel).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("chat_sessions");

            entity.HasKey(session => session.Id);
            entity.Property(session => session.Id).ValueGeneratedOnAdd();
            entity.Property(session => session.Title).IsRequired().HasMaxLength(256);
            entity.Property(session => session.CreatedAt);
            entity.Property(session => session.UpdatedAt);

            entity.HasMany(session => session.Messages)
                .WithOne(message => message.Session!)
                .HasForeignKey(message => message.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");

            entity.HasKey(message => message.Id);
            entity.Property(message => message.Id).ValueGeneratedOnAdd();
            entity.Property(message => message.SessionId);
            entity.Property(message => message.Role).IsRequired().HasMaxLength(16);
            entity.Property(message => message.Content).IsRequired();
            entity.Property(message => message.CreatedAt);

            entity.HasIndex(message => message.SessionId);
        });
    }
}
