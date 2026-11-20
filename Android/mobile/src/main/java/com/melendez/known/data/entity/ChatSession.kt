package com.melendez.known.data.entity

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

/**
 * One assistant conversation. A session may be linked to the exam the student was viewing when they
 * opened the assistant, so the first request can include that exam's detailed scores. Deleting the
 * exam keeps the conversation but clears the link.
 */
@Entity(
    tableName = "chat_sessions",
    foreignKeys = [
        ForeignKey(
            entity = Exam::class,
            parentColumns = ["id"],
            childColumns = ["examId"],
            onDelete = ForeignKey.SET_NULL
        )
    ],
    indices = [Index(value = ["examId"])]
)
data class ChatSession(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val title: String = "",
    val examId: Long? = null,
    val createdAt: Long = System.currentTimeMillis(),
    val updatedAt: Long = System.currentTimeMillis()
)
