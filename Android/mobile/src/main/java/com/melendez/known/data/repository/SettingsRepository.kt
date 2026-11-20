package com.melendez.known.data.repository

import com.melendez.known.data.dao.SettingsDao
import com.melendez.known.data.entity.Settings
import com.melendez.known.util.llm.LlmConfigSource
import kotlinx.coroutines.flow.Flow

class SettingsRepository(private val settingsDao: SettingsDao) : LlmConfigSource {

    val settings: Flow<Settings?> = settingsDao.getSettings()

    override suspend fun currentSettings(): Settings? = settingsDao.getSettingsSync()

    suspend fun initializeSettings() {
        // Check if settings exist, if not create default settings
        val existingSettings = settingsDao.getSettingsSync()
        if (existingSettings == null) {
            // Create default settings with id = 1
            val defaultSettings = Settings()
            settingsDao.updateSettings(defaultSettings)
        }
    }

    suspend fun updateDarkMode(isDarkMode: Int) {
        settingsDao.updateDarkMode(isDarkMode)
    }

    suspend fun updateHighContrastMode(isHighContrast: Boolean) {
        settingsDao.updateHighContrastMode(isHighContrast)
    }

    suspend fun updateDynamicColor(isDynamicColor: Boolean) {
        settingsDao.updateDynamicColor(isDynamicColor)
    }

    suspend fun updateThemeColor(color: Int) {
        settingsDao.updateThemeColor(color)
    }

    suspend fun updatePaletteStyle(index: Int) {
        settingsDao.updatePaletteStyle(index)
    }

    suspend fun updatePredictiveBack(enabled: Boolean) {
        settingsDao.updatePredictiveBack(enabled)
    }

    suspend fun updateLanguage(language: String) {
        settingsDao.updateLanguage(language)
    }

    suspend fun updateFirstLogin(isFirstLogin: Boolean) {
        settingsDao.updateFirstLogin(isFirstLogin)
    }

    suspend fun updateIdentity(identity: Int) {
        settingsDao.updateIdentity(identity)
    }

    suspend fun updateRegion(region: String) {
        settingsDao.updateRegion(region)
    }

    suspend fun updateSelectedSubjects(subjects: String) {
        settingsDao.updateSelectedSubjects(subjects)
    }

    suspend fun updateHistoryViewMode(mode: Int) {
        settingsDao.updateHistoryViewMode(mode)
    }

    suspend fun updateLlmProvider(value: Int) {
        settingsDao.updateLlmProvider(value)
    }

    suspend fun updateLlmBaseUrl(url: String) {
        settingsDao.updateLlmBaseUrl(url)
    }

    suspend fun updateLlmModel(model: String) {
        settingsDao.updateLlmModel(model)
    }

    suspend fun updateCurrentChatSession(sessionId: Long) {
        settingsDao.updateCurrentChatSession(sessionId)
    }

    suspend fun updateGrade(grade: Int) {
        settingsDao.updateGrade(grade)
    }
}