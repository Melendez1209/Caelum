package com.melendez.caelum.util.llm

import com.melendez.caelum.R
import com.melendez.caelum.data.entity.ExamWithScores
import com.melendez.caelum.data.entity.ExamWithTotal
import com.melendez.caelum.data.entity.Settings
import com.melendez.caelum.data.entity.SubjectStat
import com.melendez.caelum.util.averageMarkOf
import com.melendez.caelum.util.averageRateOf
import com.melendez.caelum.util.formatDateRange
import com.melendez.caelum.util.historicalAverage
import com.melendez.caelum.util.percentage
import com.melendez.caelum.util.rankOf
import com.melendez.caelum.util.recordedSubjectKeys
import com.melendez.caelum.util.settings.Identity
import com.melendez.caelum.util.settings.examSubjectKeys
import com.melendez.caelum.util.settings.subjectKeyToStringResource
import com.melendez.caelum.util.settings.toSubjectKeySet
import com.melendez.caelum.util.totalFullMark
import com.melendez.caelum.util.totalMark
import kotlin.math.roundToInt

/**
 * Builds the plain-text description of the student's exams handed to the model, so the assistant can
 * discuss real scores instead of asking the student to type them out. Uses the same deterministic
 * aggregates as the history and detail screens.
 */
object ExamContextBuilder {

    /** How many recent exams the global summary lists, keeping the request a sane size. */
    private const val MAX_RECENT_EXAMS = 8

    /** The student's overall figures, sent with the first request of a session. */
    fun buildGlobal(
        settings: Settings,
        exams: List<ExamWithTotal>,
        stats: List<SubjectStat>,
        strings: LlmStrings
    ): String {
        val out = StringBuilder()

        val identity = identityName(settings.identity, strings)
        if (identity.isNotEmpty()) out.appendLine("${strings.get(R.string.identity)}: $identity")

        val gradeLabel = StudentGrade.label(settings.grade)
        if (gradeLabel != 0) out.appendLine("${strings.get(R.string.grade)}: ${strings.get(gradeLabel)}")

        if (settings.region.isNotBlank()) {
            out.appendLine("${strings.get(R.string.region)}: ${settings.region}")
        }

        val selected = settings.selectedSubjects.toSubjectKeySet()
        val subjectNames = examSubjectKeys.filter { it in selected }
            .map { strings.get(subjectKeyToStringResource(it)) }
        out.appendLine("${strings.get(R.string.subjects)}: ${subjectNames.joinToString(", ")}")

        out.appendLine("${strings.get(R.string.llm_exam_count)}: ${exams.size}")
        out.appendLine(
            "${strings.get(R.string.average_score)}: ${format(averageMarkOf(exams))} " +
                "(${format(averageRateOf(exams))}%)"
        )
        out.appendLine("${strings.get(R.string.llm_exam_best_rank)}: ${bestRank(exams)}")

        val statsBySubject = stats.associateBy { it.subjectKey }
        if (statsBySubject.isNotEmpty()) {
            out.appendLine(strings.get(R.string.llm_exam_subject_averages))
            examSubjectKeys.forEach { key ->
                val stat = statsBySubject[key] ?: return@forEach
                out.appendLine(
                    "- ${strings.get(subjectKeyToStringResource(key))}: " +
                        "${format(stat.avgMark)}/${format(stat.avgFullMark)} " +
                        "(${format(percentage(stat.avgMark, stat.avgFullMark))}%)"
                )
            }
        }

        val recent = exams.sortedByDescending { it.exam.startDate }.take(MAX_RECENT_EXAMS)
        if (recent.isNotEmpty()) {
            out.appendLine(strings.get(R.string.llm_exam_recent))
            recent.forEach { exam ->
                out.appendLine(
                    "- ${exam.exam.name} (${formatDateRange(exam.exam.startDate, exam.exam.endDate)}): " +
                        "${format(exam.totalMark)}/${format(exam.totalFullMark)} " +
                        "(${format(percentage(exam.totalMark, exam.totalFullMark))}%), " +
                        "${strings.get(R.string.rank)} ${rankOf(exam.exam.id, exams)}"
                )
            }
        }

        return out.toString().trimEnd()
    }

    /** One exam in full, appended when the session is linked to that exam. */
    fun buildExam(
        exam: ExamWithScores,
        exams: List<ExamWithTotal>,
        stats: List<SubjectStat>,
        strings: LlmStrings
    ): String {
        val out = StringBuilder()

        out.appendLine(strings.get(R.string.llm_exam_current))
        out.appendLine(
            "${strings.get(R.string.exam)}: ${exam.exam.name} " +
                "(${formatDateRange(exam.exam.startDate, exam.exam.endDate)})"
        )

        val totalMark = totalMark(exam.scores)
        val totalFullMark = totalFullMark(exam.scores)
        out.appendLine(
            "${strings.get(R.string.total_score)}: ${format(totalMark)}/${format(totalFullMark)} " +
                "(${format(percentage(totalMark, totalFullMark))}%)"
        )
        out.appendLine("${strings.get(R.string.rank)}: ${rankOf(exam.exam.id, exams)}")
        out.appendLine(strings.get(R.string.llm_exam_subjects))

        recordedSubjectKeys(examSubjectKeys, exam.scores).forEach { key ->
            val score = exam.scores.first { it.subjectKey == key }
            val average = historicalAverage(key, stats)
            var line =
                "- ${strings.get(subjectKeyToStringResource(key))}: " +
                    "${format(score.mark)}/${format(score.fullMark)} " +
                    "(${format(percentage(score.mark, score.fullMark))}%)"
            if (average != null && average.avgFullMark > 0f) {
                line += ", ${strings.get(R.string.history_average)} " +
                    "${format(average.avgMark)}/${format(average.avgFullMark)}"
            }
            out.appendLine(line)
        }

        return out.toString().trimEnd()
    }

    private fun bestRank(exams: List<ExamWithTotal>): Int =
        exams.filter { it.totalFullMark > 0f }.minOfOrNull { rankOf(it.exam.id, exams) } ?: 0

    private fun identityName(identity: Int, strings: LlmStrings): String = when (identity) {
        Identity.STUDENT -> strings.get(R.string.student)
        Identity.TEACHER -> strings.get(R.string.teacher)
        Identity.PARENT -> strings.get(R.string.parent)
        else -> ""
    }

    private fun format(value: Float): String {
        val rounded = (value * 10f).roundToInt() / 10f
        return if (rounded == rounded.toLong().toFloat()) {
            rounded.toLong().toString()
        } else {
            rounded.toString()
        }
    }
}
