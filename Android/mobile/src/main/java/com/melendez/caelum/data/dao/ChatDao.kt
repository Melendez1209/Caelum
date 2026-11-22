package com.melendez.caelum.data.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.Query
import androidx.room.Update
import com.melendez.caelum.data.entity.ChatMessage
import com.melendez.caelum.data.entity.ChatSession
import kotlinx.coroutines.flow.Flow

@Dao
interface ChatDao {

    @Query("SELECT * FROM chat_sessions ORDER BY updatedAt DESC")
    fun getSessions(): Flow<List<ChatSession>>

    @Query(
        """
        SELECT * FROM chat_sessions
        WHERE title LIKE '%' || :query || '%'
        ORDER BY updatedAt DESC
        """
    )
    fun searchSessions(query: String): Flow<List<ChatSession>>

    @Query("SELECT * FROM chat_sessions WHERE id = :sessionId")
    suspend fun getSession(sessionId: Long): ChatSession?

    @Query("SELECT * FROM chat_sessions WHERE examId = :examId ORDER BY updatedAt DESC LIMIT 1")
    suspend fun getSessionForExam(examId: Long): ChatSession?

    @Insert
    suspend fun insertSession(session: ChatSession): Long

    @Update
    suspend fun updateSession(session: ChatSession)

    @Query("UPDATE chat_sessions SET title = :title WHERE id = :sessionId")
    suspend fun renameSession(sessionId: Long, title: String)

    @Query("UPDATE chat_sessions SET updatedAt = :updatedAt WHERE id = :sessionId")
    suspend fun touchSession(sessionId: Long, updatedAt: Long)

    @Query("DELETE FROM chat_sessions WHERE id = :sessionId")
    suspend fun deleteSession(sessionId: Long)

    @Query("SELECT * FROM chat_messages WHERE sessionId = :sessionId ORDER BY id ASC")
    fun getMessages(sessionId: Long): Flow<List<ChatMessage>>

    @Query("SELECT * FROM chat_messages WHERE sessionId = :sessionId ORDER BY id ASC")
    suspend fun getMessagesSync(sessionId: Long): List<ChatMessage>

    @Insert
    suspend fun insertMessage(message: ChatMessage): Long

    @Query("DELETE FROM chat_messages WHERE sessionId = :sessionId")
    suspend fun deleteMessages(sessionId: Long)
}
