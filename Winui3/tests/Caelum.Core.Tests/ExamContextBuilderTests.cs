using Caelum.Core.Data;
using Caelum.Core.Domain;
using Caelum.Core.Localization;
using Caelum.Core.Services;
using Xunit;

namespace Caelum.Core.Tests;

/// <summary>
///     Covers the text the assistant is given, so a change to the formatting cannot quietly leave
///     the model without the scores it is supposed to discuss.
/// </summary>
public sealed class ExamContextBuilderTests
{
    private static readonly StringLocalizer Strings = new();

    [Fact]
    public void GlobalContextListsOverallFiguresAndRecentExams()
    {
        var settings = new Settings
        {
            Identity = StudentIdentity.Student,
            Grade = StudentGrade.SeniorFirst,
            Region = "Shanghai",
            SelectedSubjects = "physics,chemistry"
        };

        var exams = new List<ExamWithTotal>
        {
            new()
            {
                Exam = new Exam { Id = 1, Name = "First", StartDate = 1_000, EndDate = 1_000 },
                TotalMark = 400f,
                TotalFullMark = 600f
            },
            new()
            {
                Exam = new Exam { Id = 2, Name = "Second", StartDate = 2_000, EndDate = 2_000 },
                TotalMark = 480f,
                TotalFullMark = 600f
            }
        };

        var context = ExamContextBuilder.BuildGlobal(settings, exams, [], Strings);

        Assert.Contains("First", context);
        Assert.Contains("Second", context);
        Assert.Contains("480", context);
        Assert.Contains("400", context);
        Assert.Contains("Best rank", context);
        Assert.Contains("Student", context);
        Assert.Contains("Shanghai", context);
    }

    [Fact]
    public void ExamContextListsSubjectMarksAndHistory()
    {
        var exam = new ExamWithScores
        {
            Exam = new Exam { Id = 1, Name = "Midterm", StartDate = 1_000, EndDate = 1_000 },
            Scores =
            [
                new ExamScore { SubjectKey = "physics", Mark = 80f, FullMark = 100f },
                new ExamScore { SubjectKey = "chemistry", Mark = 70f, FullMark = 100f }
            ]
        };

        var all = new List<ExamWithTotal>
        {
            new() { Exam = exam.Exam, TotalMark = 150f, TotalFullMark = 200f }
        };

        var stats = new List<SubjectStat>
        {
            new() { SubjectKey = "physics", AvgMark = 75f, AvgFullMark = 100f }
        };

        var context = ExamContextBuilder.BuildExam(exam, all, stats, Strings);

        Assert.Contains("Midterm", context);
        Assert.Contains("80", context);
        Assert.Contains("70", context);
        Assert.Contains("75", context);
    }

    [Fact]
    public void GlobalContextHandlesNoExams()
    {
        var context = ExamContextBuilder.BuildGlobal(new Settings(), [], [], Strings);

        Assert.Contains("0", context);
    }
}
