package com.melendez.known.ui.viewmodel.prophets

import com.mikepenz.markdown.model.State

/** A message as shown in the chat. The streaming row is a transient item with a negative id. */
data class ChatMessageUi(
    val id: Long,
    val role: String,
    val content: String,
    val isStreaming: Boolean = false,
    val isError: Boolean = false,
    /** Pre-parsed Markdown AST, cached so scrolling an item off-screen does not reparse it. */
    val markdownState: State? = null
)

/** A conversation as shown in the session list. */
data class ChatSessionUi(
    val id: Long,
    val title: String,
    val examId: Long?,
    val updatedAt: Long
)

data class ProphetsUiState(
    val sessions: List<ChatSessionUi> = emptyList(),
    val currentSessionId: Long = 0L,
    val currentTitle: String = "",
    val messages: List<ChatMessageUi> = emptyList(),
    val draft: String = "",
    val isGenerating: Boolean = false,
    val streamingText: String? = null,
    val isConfigured: Boolean = false,
    val showSessions: Boolean = false,
    val message: String? = null
)
