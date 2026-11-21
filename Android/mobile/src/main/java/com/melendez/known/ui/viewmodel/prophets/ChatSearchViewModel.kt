package com.melendez.known.ui.viewmodel.prophets

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import com.melendez.known.data.entity.ChatSession
import com.melendez.known.util.llm.AssistantGraph
import kotlinx.coroutines.flow.Flow

/**
 * Exposes chat sessions to screens outside the assistant, such as the history search. Queries are
 * cold [Flow]s, matching the exam search, so the collecting screen owns the subscription.
 */
class ChatSearchViewModel(application: Application) : AndroidViewModel(application) {

    private val deps = AssistantGraph.get(application)

    /** Every session, most recently updated first. */
    val sessions: Flow<List<ChatSession>> = deps.chatRepository.sessions

    /** Sessions whose title contains [query]. */
    fun search(query: String): Flow<List<ChatSession>> = deps.chatRepository.searchSessions(query)

    /** Remembers [sessionId] so the assistant opens it the next time it is visited. */
    suspend fun selectSession(sessionId: Long) {
        deps.settingsRepository.updateCurrentChatSession(sessionId)
    }
}
