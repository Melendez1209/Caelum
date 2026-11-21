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
import com.mikepenz.markdown.model.State
import com.mikepenz.markdown.model.parseMarkdown
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
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
    private var titleJob: Job? = null
    private var persistedMessages: List<ChatMessage> = emptyList()
    private var currentExamId: Long = 0L

    /** Parsed Markdown ASTs keyed by message id, reused when a scrolled-off row is recomposed. */
    private val markdownCache = mutableMapOf<Long, State>()
    private val parsingIds = mutableSetOf<Long>()

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
            if (sessionId != _uiState.value.currentSessionId) {
                // Message ids are session-scoped in the cache; drop the previous session's ASTs.
                markdownCache.clear()
                parsingIds.clear()
            }
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
                // Rebuild the exam context on every turn, not just the first one. Conversation
                // history only carries user/assistant text, so without this the model loses all
                // score data as soon as the student asks a follow-up question.
                val context = buildContext()

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

                val cancelled = result.errorKind == LlmErrorKind.CANCELLED
                if (!result.isSuccess && !cancelled) {
                    _uiState.value = _uiState.value.copy(message = messageFor(result))
                }

                // Only persist real model output. A plain failure with no partial answer is surfaced
                // via the Snackbar and must not leave an error bubble in the transcript. A cancelled
                // stream keeps whatever arrived, shown as a normal (not red) bubble.
                if (result.content.isNotEmpty()) {
                    deps.chatRepository.addMessage(sessionId, "assistant", result.content)
                    deps.chatRepository.touchSession(sessionId)
                }

                // Generate the title in its own coroutine so it is never cancelled by "stop" and
                // never blocks the reply. Only attempt it on the first successful answer of a
                // session that still has no title.
                if (result.isSuccess) {
                    maybeGenerateTitle(sessionId)
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
        // Cancel the coroutine first so the client sees a cancelled job, then cancel the blocking
        // OkHttp call explicitly: a blocked read ignores coroutine cancellation and would otherwise
        // keep producing tokens.
        generateJob?.cancel()
        generateJob = null
        deps.llmService.cancelActive()
    }

    /**
     * Generates a title once, on the first successful reply of an untitled session. Runs separately
     * from the reply job so a later "stop" cannot cancel it, and falls back to the first user's
     * message when the model is unavailable.
     */
    private fun maybeGenerateTitle(sessionId: Long) {
        titleJob?.cancel()
        titleJob = viewModelScope.launch {
            val session = deps.chatRepository.getSession(sessionId) ?: return@launch
            if (session.title.isNotBlank()) return@launch

            val firstUserMessage = deps.chatRepository.getMessagesSync(sessionId)
                .firstOrNull { it.role == "user" }
            val seed = firstUserMessage?.content ?: return@launch

            val generated = try {
                deps.llmService.generateTitle(seed)
            } catch (e: Exception) {
                ""
            }
            val title = generated.ifBlank { seed.take(40) }

            deps.chatRepository.renameSession(sessionId, title)
            if (sessionId == _uiState.value.currentSessionId) {
                _uiState.value = _uiState.value.copy(currentTitle = title)
            }
        }
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
        val base = persistedMessages.map { message ->
            ChatMessageUi(
                id = message.id,
                role = message.role,
                content = message.content,
                markdownState = markdownCache[message.id]
            )
                .also { ensureParsed(message.id, message.role, message.content) }
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

    /** Parse a non-streaming message's Markdown once and cache it for later recompositions. */
    private fun ensureParsed(id: Long, role: String, content: String) {
        if (role == "user" || id in parsingIds || markdownCache.containsKey(id)) return
        parsingIds += id
        viewModelScope.launch {
            val state = withContext(Dispatchers.Default) { parseMarkdown(content) }
            markdownCache[id] = state
            parsingIds -= id
            // Only refresh when the session is unchanged, so a stale parse cannot disturb the UI.
            if (_uiState.value.messages.any { it.id == id }) renderMessages()
        }
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
