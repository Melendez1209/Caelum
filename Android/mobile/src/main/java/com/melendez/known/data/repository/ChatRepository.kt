package com.melendez.known.data.repository

import androidx.room.withTransaction
import com.melendez.known.data.AppDatabase
import com.melendez.known.data.dao.ChatDao
import com.melendez.known.data.entity.ChatMessage
import com.melendez.known.data.entity.ChatSession
import kotlinx.coroutines.flow.Flow

/**
 * Assistant conversation storage. Takes the database so that deleting a session and its messages is
 * a single transaction, mirroring the desktop repository.
 */
class ChatRepository(private val database: AppDatabase) {

    private val chatDao: ChatDao = database.chatDao()

    val sessions: Flow<List<ChatSession>> = chatDao.getSessions()

    fun messages(sessionId: Long): Flow<List<ChatMessage>> = chatDao.getMessages(sessionId)

    suspend fun getSession(sessionId: Long): ChatSession? = chatDao.getSession(sessionId)

    suspend fun getSessionForExam(examId: Long): ChatSession? = chatDao.getSessionForExam(examId)

    suspend fun createSession(examId: Long? = null, title: String = ""): Long =
        chatDao.insertSession(ChatSession(title = title, examId = examId))

    suspend fun renameSession(sessionId: Long, title: String) =
        chatDao.renameSession(sessionId, title)

    suspend fun touchSession(sessionId: Long) =
        chatDao.touchSession(sessionId, System.currentTimeMillis())

    suspend fun deleteSession(sessionId: Long) {
        database.withTransaction {
            chatDao.deleteMessages(sessionId)
            chatDao.deleteSession(sessionId)
        }
    }

    suspend fun addMessage(sessionId: Long, role: String, content: String): Long =
        chatDao.insertMessage(ChatMessage(sessionId = sessionId, role = role, content = content))

    suspend fun getMessagesSync(sessionId: Long): List<ChatMessage> =
        chatDao.getMessagesSync(sessionId)
}
