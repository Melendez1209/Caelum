package com.melendez.caelum.util.llm

import androidx.annotation.StringRes
import com.melendez.caelum.R

/** Which model provider the student has chosen for the assistant. */
enum class LlmProviderOption(val value: Int) {
    NONE(0),
    LONG_CAT(1),
    CUSTOM(2);

    companion object {
        fun from(value: Int): LlmProviderOption = entries.firstOrNull { it.value == value } ?: NONE
    }
}

/** The endpoint and model a provider preset fills in for the student. */
data class LlmProviderPreset(
    val option: LlmProviderOption,
    val defaultBaseUrl: String,
    val defaultModel: String
)

object LlmProviderPresets {
    val LONG_CAT = LlmProviderPreset(
        LlmProviderOption.LONG_CAT,
        "https://api.longcat.chat/openai",
        "LongCat-2.5"
    )
    val CUSTOM = LlmProviderPreset(LlmProviderOption.CUSTOM, "", "")

    fun find(option: LlmProviderOption): LlmProviderPreset? = when (option) {
        LlmProviderOption.LONG_CAT -> LONG_CAT
        LlmProviderOption.CUSTOM -> CUSTOM
        else -> null
    }
}

/** Stable grade values stored in settings, matching the desktop app's StudentGrade. */
object StudentGrade {
    const val NONE = 0
    const val JUNIOR_FIRST = 1
    const val JUNIOR_SECOND = 2
    const val JUNIOR_THIRD = 3
    const val SENIOR_FIRST = 4
    const val SENIOR_SECOND = 5
    const val SENIOR_THIRD = 6

    val ALL = listOf(
        JUNIOR_FIRST,
        JUNIOR_SECOND,
        JUNIOR_THIRD,
        SENIOR_FIRST,
        SENIOR_SECOND,
        SENIOR_THIRD
    )

    @StringRes
    fun label(grade: Int): Int = when (grade) {
        JUNIOR_FIRST -> R.string.grade_junior_first
        JUNIOR_SECOND -> R.string.grade_junior_second
        JUNIOR_THIRD -> R.string.grade_junior_third
        SENIOR_FIRST -> R.string.grade_senior_first
        SENIOR_SECOND -> R.string.grade_senior_second
        SENIOR_THIRD -> R.string.grade_senior_third
        else -> 0
    }
}
