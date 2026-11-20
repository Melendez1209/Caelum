package com.melendez.known.ui.viewmodel.settings

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.melendez.known.R
import com.melendez.known.data.entity.Settings
import com.melendez.known.util.llm.AssistantGraph
import com.melendez.known.util.llm.LlmProviderOption
import com.melendez.known.util.llm.LlmProviderPresets
import com.melendez.known.util.llm.LlmService
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class LlmSettingsViewModel(application: Application) : AndroidViewModel(application) {

    private val deps = AssistantGraph.get(application)
    private val _uiState = MutableStateFlow(LlmSettingsUiState())
    val uiState: StateFlow<LlmSettingsUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            val settings = deps.settingsRepository.currentSettings() ?: Settings()
            val option = LlmProviderOption.from(settings.llmProviderValue)
            val preset = LlmProviderPresets.find(option)
            _uiState.value = LlmSettingsUiState(
                provider = option,
                baseUrl = settings.llmBaseUrl.ifBlank { preset?.defaultBaseUrl.orEmpty() },
                model = settings.llmModel.ifBlank { preset?.defaultModel.orEmpty() },
                apiKey = deps.secretStore.read(LlmService.API_KEY_SLOT).orEmpty(),
                grade = settings.grade
            )
        }
    }

    fun onProviderChange(option: LlmProviderOption) {
        val preset = LlmProviderPresets.find(option)
        _uiState.value = _uiState.value.copy(
            provider = option,
            baseUrl = preset?.defaultBaseUrl.orEmpty(),
            model = preset?.defaultModel.orEmpty(),
            resultMessage = null
        )
    }

    fun onBaseUrlChange(value: String) {
        _uiState.value = _uiState.value.copy(baseUrl = value, resultMessage = null)
    }

    fun onModelChange(value: String) {
        _uiState.value = _uiState.value.copy(model = value, resultMessage = null)
    }

    fun onApiKeyChange(value: String) {
        _uiState.value = _uiState.value.copy(apiKey = value, resultMessage = null)
    }

    fun onGradeChange(grade: Int) {
        _uiState.value = _uiState.value.copy(grade = grade)
    }

    fun save() {
        viewModelScope.launch {
            val state = _uiState.value
            deps.settingsRepository.updateLlmProvider(state.provider.value)
            deps.settingsRepository.updateLlmBaseUrl(state.baseUrl.trim())
            deps.settingsRepository.updateLlmModel(state.model.trim())
            deps.settingsRepository.updateGrade(state.grade)
            deps.llmService.setApiKey(state.apiKey)
            _uiState.value = _uiState.value.copy(
                resultMessage = deps.strings.get(R.string.llm_settings_saved)
            )
        }
    }

    fun testConnection() {
        viewModelScope.launch {
            val state = _uiState.value
            _uiState.value = state.copy(isTesting = true, resultMessage = null)
            val result = deps.llmService.testConnection(
                baseUrl = state.baseUrl.trim(),
                model = state.model.trim(),
                apiKey = state.apiKey
            )
            val message = if (result.isSuccess) {
                deps.strings.get(R.string.llm_test_success)
            } else {
                deps.strings.format(
                    R.string.llm_test_failure,
                    result.message.ifBlank { deps.strings.get(R.string.llm_error_unknown) }
                )
            }
            _uiState.value = _uiState.value.copy(isTesting = false, resultMessage = message)
        }
    }
}
