namespace Caelum.Core.Domain;

/// <summary>
///     The school years a student can be seated in, stored as an integer in the settings table.
///     The values run from the first to the last year of junior high and then senior high.
/// </summary>
/// <remarks>Named with a prefix so it never collides with the <c>Settings.Grade</c> property.</remarks>
public static class StudentGrade
{
    public const int None = 0;
    public const int JuniorFirst = 1;
    public const int JuniorSecond = 2;
    public const int JuniorThird = 3;
    public const int SeniorFirst = 4;
    public const int SeniorSecond = 5;
    public const int SeniorThird = 6;

    /// <summary>The years offered during onboarding and on the settings page, in display order.</summary>
    public static IReadOnlyList<int> All { get; } =
    [
        JuniorFirst,
        JuniorSecond,
        JuniorThird,
        SeniorFirst,
        SeniorSecond,
        SeniorThird
    ];
}
