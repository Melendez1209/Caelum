using System.Globalization;
using System.Text;
using Caelum.Core.Data;
using Caelum.Core.Domain;
using Caelum.Core.Localization;

namespace Caelum.Core.Services;

/// <summary>
///     Builds the plain-text description of the student's exams that is handed to the model, so
///     the assistant can discuss real scores instead of asking the student to type them out.
/// </summary>
public static class ExamContextBuilder
{
    /// <summary>How many recent exams the global summary lists, keeping the request a sane size.</summary>
    private const int MaxRecentExams = 8;

    /// <summary>The student's overall figures, sent with every request.</summary>
    public static string BuildGlobal(
        Settings settings,
        IReadOnlyList<ExamWithTotal> exams,
        IReadOnlyList<SubjectStat> stats,
        StringLocalizer strings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(exams);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(strings);

        var builder = new StringBuilder();

        var identity = IdentityName(settings.Identity, strings);

        if (identity.Length > 0)
            builder.AppendLine($"{strings.Get("identity")}: {identity}");

        var grade = GradeLabels.NameOf(strings, settings.Grade);

        if (grade.Length > 0)
            builder.AppendLine($"{strings.Get("grade")}: {grade}");

        if (!string.IsNullOrWhiteSpace(settings.Region))
            builder.AppendLine($"{strings.Get("region")}: {settings.Region}");

        var selected = SubjectCatalogue.ToSubjectKeySet(settings.SelectedSubjects);

        builder.AppendLine(
            $"{strings.Get("subjects")}: " +
            string.Join(
                ", ",
                SubjectCatalogue.SubjectKeys.Where(selected.Contains)
                    .Select(key => SubjectLabels.NameOf(strings, key))));

        builder.AppendLine($"{strings.Get("llm_exam_count")}: {exams.Count}");

        builder.AppendLine(
            $"{strings.Get("average_score")}: {Format(ExamScoring.AverageMarkOf(exams))} " +
            $"({Format(ExamScoring.AverageRateOf(exams))}%)");

        builder.AppendLine($"{strings.Get("llm_exam_best_rank")}: {BestRank(exams)}");

        var recent = exams
            .OrderByDescending(exam => exam.Exam.StartDate)
            .Take(MaxRecentExams)
            .ToList();

        if (recent.Count > 0)
        {
            builder.AppendLine(strings.Get("llm_exam_recent"));

            foreach (var exam in recent)
            {
                builder.AppendLine(
                    $"- {exam.Exam.Name} ({ExamDates.FormatDateRange(exam.Exam.StartDate, exam.Exam.EndDate)}): " +
                    $"{Format(exam.TotalMark)}/{Format(exam.TotalFullMark)} " +
                    $"({Format(ExamScoring.Percentage(exam.TotalMark, exam.TotalFullMark))}%), " +
                    $"{strings.Get("rank")} {ExamScoring.RankOf(exam.Exam.Id, exams)}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>One exam in full, appended when the session is linked to that exam.</summary>
    public static string BuildExam(
        ExamWithScores exam,
        IReadOnlyList<ExamWithTotal> exams,
        IReadOnlyList<SubjectStat> stats,
        StringLocalizer strings)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(exams);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(strings);

        var builder = new StringBuilder();

        builder.AppendLine(strings.Get("llm_exam_current"));
        builder.AppendLine(
            $"{strings.Get("exam")}: {exam.Exam.Name} " +
            $"({ExamDates.FormatDateRange(exam.Exam.StartDate, exam.Exam.EndDate)})");

        var totalMark = ExamScoring.TotalMark(exam.Scores);
        var totalFullMark = ExamScoring.TotalFullMark(exam.Scores);

        builder.AppendLine(
            $"{strings.Get("total_score")}: {Format(totalMark)}/{Format(totalFullMark)} " +
            $"({Format(ExamScoring.Percentage(totalMark, totalFullMark))}%)");

        builder.AppendLine(
            $"{strings.Get("rank")}: {ExamScoring.RankOf(exam.Exam.Id, exams)}");

        builder.AppendLine(strings.Get("llm_exam_subjects"));

        foreach (var key in ExamScoring.RecordedSubjectKeys(SubjectCatalogue.ExamSubjectKeys, exam.Scores))
        {
            var score = exam.Scores.First(item => item.SubjectKey == key);
            var average = ExamScoring.HistoricalAverage(key, stats);

            var line =
                $"- {SubjectLabels.NameOf(strings, key)}: " +
                $"{Format(score.Mark)}/{Format(score.FullMark)} " +
                $"({Format(ExamScoring.Percentage(score.Mark, score.FullMark))}%)";

            if (average is not null && average.AvgFullMark > 0f)
            {
                line +=
                    $", {strings.Get("history_average")} " +
                    $"{Format(average.AvgMark)}/{Format(average.AvgFullMark)}";
            }

            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Best rank across the exams, or zero when none carries a score.</summary>
    private static int BestRank(IReadOnlyList<ExamWithTotal> exams) =>
        exams.Where(exam => exam.TotalFullMark > 0f)
            .Select(exam => ExamScoring.RankOf(exam.Exam.Id, exams))
            .DefaultIfEmpty(0)
            .Min();

    /// <summary>Display name of the stored identity, or empty when it has not been chosen.</summary>
    private static string IdentityName(int identity, StringLocalizer strings) => identity switch
    {
        StudentIdentity.Student => strings.Get("student"),
        StudentIdentity.Teacher => strings.Get("teacher"),
        StudentIdentity.Parent => strings.Get("parent"),
        _ => string.Empty
    };

    private static string Format(float value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);
}
