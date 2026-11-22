package com.melendez.caelum.ui.viewmodel.settings

import com.melendez.caelum.util.llm.LlmProviderOption

data class LlmSettingsUiState(
    val provider: LlmProviderOption = LlmProviderOption.NONE,
    val baseUrl: String = "",
    val model: String = "",
    val apiKey: String = "",
    val isTesting: Boolean = false,
    val resultMessage: String? = null
)
