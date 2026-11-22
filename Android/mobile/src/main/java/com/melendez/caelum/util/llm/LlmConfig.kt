package com.melendez.caelum.util.llm

import com.melendez.caelum.data.entity.Settings

/** Supplies the current settings to the assistant service, so it can be faked in tests. */
interface LlmConfigSource {
    suspend fun currentSettings(): Settings?
}

/** Where the assistant API key is kept, so it can be faked in tests. */
interface LlmSecretStore {
    fun read(slot: String): String?
    fun write(slot: String, value: String)
    fun delete(slot: String)
}
