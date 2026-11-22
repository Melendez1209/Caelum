package com.melendez.caelum.util.llm

import android.content.Context
import com.melendez.caelum.data.AppDatabase
import com.melendez.caelum.data.repository.ChatRepository
import com.melendez.caelum.data.repository.ExamRepository
import com.melendez.caelum.data.repository.SecretStore
import com.melendez.caelum.data.repository.SettingsRepository

/**
 * Manual dependency assembly for the assistant feature. The project has no DI framework, so the
 * graph is created once from the application context and shared by the chat and settings screens.
 */
object AssistantGraph {

    @Volatile
    private var dependencies: Dependencies? = null

    fun get(context: Context): Dependencies = dependencies ?: synchronized(this) {
        dependencies ?: create(context.applicationContext).also { dependencies = it }
    }

    private fun create(app: Context): Dependencies {
        val database = AppDatabase.getDatabase(app)
        val settingsRepository = SettingsRepository(database.settingsDao())
        val secretStore = SecretStore(app)
        val strings = AndroidLlmStrings(app)
        return Dependencies(
            chatRepository = ChatRepository(database),
            examRepository = ExamRepository(database),
            settingsRepository = settingsRepository,
            secretStore = secretStore,
            strings = strings,
            llmService = LlmService(settingsRepository, secretStore, strings)
        )
    }

    data class Dependencies(
        val chatRepository: ChatRepository,
        val examRepository: ExamRepository,
        val settingsRepository: SettingsRepository,
        val secretStore: LlmSecretStore,
        val strings: LlmStrings,
        val llmService: LlmService
    )
}
