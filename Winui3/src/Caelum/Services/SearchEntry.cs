namespace Caelum.Services;

/// <summary>One row of the shell search suggestions: an exam or an assistant session.</summary>
/// <param name="Title">Display title.</param>
/// <param name="Subtitle">Secondary line, such as the exam dates or the session's last activity.</param>
/// <param name="Id">Exam id or session id.</param>
/// <param name="IsSession">Whether the row opens a chat session rather than an exam.</param>
public sealed record SearchEntry(string Title, string Subtitle, long Id, bool IsSession);
