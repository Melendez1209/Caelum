package com.melendez.known.ui.viewmodel.settings

import com.melendez.known.util.llm.LlmProviderOption

data class LlmSettingsUiState(
    val provider: LlmProviderOption = LlmProviderOption.NONE,
    val baseUrl: String = "",
    val model: String = "",
    val apiKey: String = "",
    val grade: Int = 0,
    val isTesting: Boolean = false,
    val resultMessage: String? = null
)
