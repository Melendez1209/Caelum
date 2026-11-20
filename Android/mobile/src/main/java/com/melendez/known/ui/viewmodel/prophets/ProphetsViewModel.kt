package com.melendez.known.ui.viewmodel.prophets

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.melendez.known.R
import com.melendez.known.data.entity.ChatMessage
import com.melendez.known.data.entity.Settings
import com.melendez.known.util.llm.AssistantGraph
import com.melendez.known.util.llm.ExamContextBuilder
import com.melendez.known.util.llm.LlmErrorKind
import com.melendez.known.util.llm.LlmMessage
import com.melendez.known.util.llm.LlmResult
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlin.coroutines.cancellation.CancellationException

/**
 * Drives the assistant chat: session management, history, streaming replies and the exam context
 * injected on the first request of a session.
 */
class ProphetsViewModel(
    application: Application,
    private val initialExamId: Long = 0L
) : AndroidViewModel(application) {

    private val deps = AssistantGraph.get(application)
    private val _uiState = MutableStateFlow(ProphetsUiState())
    val uiState: StateFlow<ProphetsUiState> = _uiState.asStateFlow()

    private var messagesJob: Job? = null
    private var generateJob: Job? = null
    private var persistedMessages: List<ChatMessage> = emptyList()
    private var currentExamId: Long = 0L
    private val errorIds = mutableSetOf<Long>()

    init {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isConfigured = deps.llmService.isConfigured())
            selectSession(resolveInitialSession())
        }
        viewModelScope.launch {
            deps.chatRepository.sessions.collect { sessions ->
                _uiState.value = _uiState.value.copy(
                    sessions = sessions.map {
                        ChatSessionUi(it.id, it.title, it.examId, it.updatedAt)
                    }
                )
            }
        }
    }

    private suspend fun resolveInitialSession(): Long {
        val remembered = deps.settingsRepository.currentSettings()?.currentChatSessionId ?: 0L
        val sessionId = when {
            initialExamId != 0L ->
                deps.chatRepository.getSessionForExam(initialExamId)?.id
                    ?: deps.chatRepository.createSession(examId = initialExamId)

            remembered != 0L && deps.chatRepository.getSession(remembered) != null -> remembered

            else -> deps.chatRepository.createSession()
        }
        deps.settingsRepository.updateCurrentChatSession(sessionId)
        return sessionId
    }

    fun onDraftChange(text: String) {
        _uiState.value = _uiState.value.copy(draft = text)
    }

    fun dismissMessage() {
        _uiState.value = _uiState.value.copy(message = null)
    }

    fun setShowSessions(show: Boolean) {
        _uiState.value = _uiState.value.copy(showSessions = show)
    }

    fun refreshConfigured() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isConfigured = deps.llmService.isConfigured())
        }
    }

    fun newSession() {
        viewModelScope.launch {
            selectSession(deps.chatRepository.createSession())
        }
    }

    fun selectSession(sessionId: Long) {
        if (sessionId == 0L) return
        viewModelScope.launch {
            val session = deps.chatRepository.getSession(sessionId) ?: return@launch
            currentExamId = session.examId ?: 0L
            deps.settingsRepository.updateCurrentChatSession(sessionId)
            _uiState.value = _uiState.value.copy(
                currentSessionId = sessionId,
                currentTitle = session.title,
                showSessions = false,
                message = null
            )
            observeMessages(sessionId)
        }
    }

    fun renameSession(sessionId: Long, title: String) {
        viewModelScope.launch {
            val clean = title.trim().take(40)
            if (clean.isEmpty()) return@launch
            deps.chatRepository.renameSession(sessionId, clean)
            if (sessionId == _uiState.value.currentSessionId) {
                _uiState.value = _uiState.value.copy(currentTitle = clean)
            }
        }
    }

    fun deleteSession(sessionId: Long) {
        viewModelScope.launch {
            deps.chatRepository.deleteSession(sessionId)
            if (sessionId == _uiState.value.currentSessionId) {
                val next = deps.chatRepository.sessions.first().firstOrNull()?.id
                    ?: deps.chatRepository.createSession()
                selectSession(next)
            }
        }
    }

    fun send() {
        val text = _uiState.value.draft.trim()
        val sessionId = _uiState.value.currentSessionId
        if (text.isEmpty() || sessionId == 0L || _uiState.value.isGenerating) return

        generateJob = viewModelScope.launch {
            // Re-check at send time: settings may have changed since the screen opened. When the
            // assistant is not configured, keep the draft, show a hint and leave no error bubble.
            if (!deps.llmService.isConfigured()) {
                _uiState.value = _uiState.value.copy(
                    message = deps.strings.get(R.string.llm_not_configured)
                )
                return@launch
            }

            _uiState.value = _uiState.value.copy(
                draft = "",
                isGenerating = true,
                streamingText = "",
                message = null
            )

            try {
                val isFirst = deps.chatRepository.getMessagesSync(sessionId).isEmpty()

                deps.chatRepository.addMessage(sessionId, "user", text)
                deps.chatRepository.touchSession(sessionId)

                val history = deps.chatRepository.getMessagesSync(sessionId)
                    .map { LlmMessage(it.role, it.content) }
                val context = if (isFirst) buildContext() else null

                val result = deps.llmService.complete(
                    history = history,
                    context = context,
                    includeSystemPrompt = isFirst,
                    progress = { delta ->
                        _uiState.value = _uiState.value.copy(
                            streamingText = _uiState.value.streamingText.orEmpty() + delta
                        )
                        renderMessages()
                    }
                )

                _uiState.value = _uiState.value.copy(streamingText = null)
                renderMessages()

                if (!result.isSuccess) {
                    _uiState.value = _uiState.value.copy(message = messageFor(result))
                }

                // Only persist real model output. A failure with no partial answer is surfaced via
                // the snackbar above and must not leave an error text bubble in the transcript.
                if (result.content.isNotEmpty()) {
                    val messageId = deps.chatRepository.addMessage(sessionId, "assistant", result.content)
                    if (!result.isSuccess) errorIds += messageId
                    deps.chatRepository.touchSession(sessionId)
                }

                if (isFirst && result.isSuccess) {
                    val title = deps.llmService.generateTitle(text)
                    deps.chatRepository.renameSession(sessionId, title.ifBlank { text.take(40) })
                }
            } catch (e: CancellationException) {
                val partial = _uiState.value.streamingText
                if (!partial.isNullOrBlank()) {
                    deps.chatRepository.addMessage(sessionId, "assistant", partial)
                    deps.chatRepository.touchSession(sessionId)
                }
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    message = deps.strings.get(R.string.llm_error_unknown) +
                            (e.message?.let { ": $it" } ?: "")
                )
            } finally {
                _uiState.value = _uiState.value.copy(isGenerating = false, streamingText = null)
                renderMessages()
                generateJob = null
            }
        }
    }

    fun stop() {
        generateJob?.cancel()
        generateJob = null
    }

    private fun observeMessages(sessionId: Long) {
        messagesJob?.cancel()
        messagesJob = viewModelScope.launch {
            deps.chatRepository.messages(sessionId).collect { messages ->
                persistedMessages = messages
                renderMessages()
            }
        }
    }

    private fun renderMessages() {
        val base = persistedMessages.map {
            ChatMessageUi(it.id, it.role, it.content, isError = it.id in errorIds)
        }
        val streaming = _uiState.value.streamingText
        _uiState.value = _uiState.value.copy(
            messages = if (streaming == null) {
                base
            } else {
                base + ChatMessageUi(
                    id = -1L,
                    role = "assistant",
                    content = streaming,
                    isStreaming = true
                )
            }
        )
    }

    private suspend fun buildContext(): String {
        val settings = deps.settingsRepository.currentSettings() ?: Settings()
        val exams = deps.examRepository.exams.first()
        val stats = deps.examRepository.getSubjectStats(currentExamId).first()
        val parts = mutableListOf(
            ExamContextBuilder.buildGlobal(settings, exams, stats, deps.strings)
        )
        if (currentExamId != 0L) {
            deps.examRepository.getExamWithScores(currentExamId).first()?.let { detail ->
                parts += ExamContextBuilder.buildExam(detail, exams, stats, deps.strings)
            }
        }
        return parts.joinToString("\n\n")
    }

    private fun messageFor(result: LlmResult): String {
        val resource = when (result.errorKind) {
            LlmErrorKind.NOT_CONFIGURED -> R.string.llm_error_not_configured
            LlmErrorKind.NETWORK -> R.string.llm_error_network
            LlmErrorKind.UNAUTHORIZED -> R.string.llm_error_unauthorized
            LlmErrorKind.NOT_FOUND -> R.string.llm_error_not_found
            LlmErrorKind.RATE_LIMITED -> R.string.llm_error_rate_limited
            LlmErrorKind.CANCELLED -> R.string.llm_error_cancelled
            LlmErrorKind.UNKNOWN, null -> R.string.llm_error_unknown
        }
        val base = deps.strings.get(resource)
        return if (result.message.isNotBlank()) "$base: ${result.message}" else base
    }
}
