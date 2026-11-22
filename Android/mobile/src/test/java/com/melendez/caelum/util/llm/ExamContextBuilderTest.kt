package com.melendez.caelum.util.llm

import com.melendez.caelum.R
import com.melendez.caelum.data.entity.Exam
import com.melendez.caelum.data.entity.ExamScore
import com.melendez.caelum.data.entity.ExamWithScores
import com.melendez.caelum.data.entity.ExamWithTotal
import com.melendez.caelum.data.entity.Settings
import com.melendez.caelum.data.entity.SubjectStat
import com.melendez.caelum.util.settings.Identity
import org.junit.Assert.assertTrue
import org.junit.Test

class ExamContextBuilderTest {

    private fun strings() = FakeStrings().apply {
        put(R.string.identity, "Identity")
        put(R.string.student, "Student")
        put(R.string.grade, "Grade")
        put(R.string.grade_senior_first, "Senior 1")
        put(R.string.region, "Region")
        put(R.string.subjects, "Subjects")
        put(R.string.chinese, "Chinese")
        put(R.string.maths, "Maths")
        put(R.string.llm_exam_count, "Exams recorded")
        put(R.string.average_score, "Average score")
        put(R.string.llm_exam_best_rank, "Best rank")
        put(R.string.llm_exam_subject_averages, "Subject averages")
        put(R.string.llm_exam_recent, "Recent exams")
        put(R.string.rank, "Rank")
        put(R.string.history_average, "Historical average")
        put(R.string.llm_exam_current, "Current exam")
        put(R.string.exam, "Exam")
        put(R.string.total_score, "Total score")
        put(R.string.llm_exam_subjects, "Subjects")
    }

    private val exam = Exam(id = 1L, name = "Midterm", startDate = 1_700_000_000L, endDate = 1_700_000_000L)
    private val scores = listOf(
        ExamScore(id = 1L, examId = 1L, subjectKey = "chinese", mark = 90f, fullMark = 100f),
        ExamScore(id = 2L, examId = 1L, subjectKey = "maths", mark = 80f, fullMark = 100f)
    )
    private val exams = listOf(ExamWithTotal(exam, 170f, 200f))
    private val stats = listOf(SubjectStat("chinese", 85f, 100f, 3))

    @Test
    fun globalContextContainsIdentityGradeRegionSubjectsAndFigures() {
        val settings = Settings(
            identity = Identity.STUDENT,
            grade = StudentGrade.SENIOR_FIRST,
            region = "Beijing",
            selectedSubjects = "chinese,maths"
        )
        val context = ExamContextBuilder.buildGlobal(settings, exams, stats, strings())

        assertTrue(context.contains("Identity: Student"))
        assertTrue(context.contains("Grade: Senior 1"))
        assertTrue(context.contains("Region: Beijing"))
        assertTrue(context.contains("Subjects: Chinese, Maths"))
        assertTrue(context.contains("Exams recorded: 1"))
        assertTrue(context.contains("Average score: 170 (85%)"))
        assertTrue(context.contains("Best rank: 1"))
        assertTrue(context.contains("Subject averages"))
        assertTrue(context.contains("Chinese: 85/100"))
        assertTrue(context.contains("Recent exams"))
        assertTrue(context.contains("Midterm"))
    }

    @Test
    fun examContextContainsScoresAndHistoricalAverage() {
        val context = ExamContextBuilder.buildExam(
            ExamWithScores(exam, scores),
            exams,
            stats,
            strings()
        )

        assertTrue(context.contains("Current exam"))
        assertTrue(context.contains("Midterm"))
        assertTrue(context.contains("Total score: 170/200 (85%)"))
        assertTrue(context.contains("Rank: 1"))
        assertTrue(context.contains("Chinese: 90/100 (90%)"))
        assertTrue(context.contains("Historical average 85/100"))
    }

    @Test
    fun emptyExamListDoesNotCrash() {
        val context = ExamContextBuilder.buildGlobal(Settings(), emptyList(), emptyList(), strings())
        assertTrue(context.contains("Exams recorded: 0"))
        assertTrue(context.contains("Best rank: 0"))
    }
}
