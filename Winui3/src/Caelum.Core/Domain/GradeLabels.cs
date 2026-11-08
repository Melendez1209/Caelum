using Caelum.Core.Localization;

namespace Caelum.Core.Domain;

/// <summary>
///     Maps the stable grade values used in the database onto the translatable strings shown in the
///     interface, keeping the resource keys out of the screens.
/// </summary>
public static class GradeLabels
{
    /// <summary>
    ///     Resource key for the display name of a grade. An unset grade has no name, so an empty key
    ///     is returned and the caller shows nothing rather than a blank label.
    /// </summary>
    private static string ResourceKeyFor(int grade)
    {
        return grade switch
        {
            StudentGrade.JuniorFirst => "grade_junior_first",
            StudentGrade.JuniorSecond => "grade_junior_second",
            StudentGrade.JuniorThird => "grade_junior_third",
            StudentGrade.SeniorFirst => "grade_senior_first",
            StudentGrade.SeniorSecond => "grade_senior_second",
            StudentGrade.SeniorThird => "grade_senior_third",
            _ => string.Empty
        };
    }

    /// <summary>Display name of a grade, resolved through the supplied localiser.</summary>
    public static string NameOf(StringLocalizer strings, int grade)
    {
        var key = ResourceKeyFor(grade);

        return string.IsNullOrEmpty(key) ? string.Empty : strings.Get(key);
    }
}
