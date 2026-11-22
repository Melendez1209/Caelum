package com.melendez.caelum.util.llm

import com.melendez.caelum.data.entity.Settings

/** Settings source backed by a mutable value, for service tests. */
class FakeConfigSource(var settings: Settings) : LlmConfigSource {
    override suspend fun currentSettings(): Settings = settings
}

/** In-memory secret store, equivalent to the desktop tests' InMemorySecretStore. */
class InMemorySecretStore : LlmSecretStore {
    private val values = mutableMapOf<String, String>()
    override fun read(slot: String): String? = values[slot]
    override fun write(slot: String, value: String) {
        values[slot] = value
    }

    override fun delete(slot: String) {
        values.remove(slot)
    }
}

/** Fake localiser returning the resource id as a stable string for context assertions. */
class FakeStrings : LlmStrings {
    private val values = mutableMapOf<Int, String>()
    fun put(resId: Int, value: String) {
        values[resId] = value
    }

    override fun get(resId: Int): String = values[resId] ?: "str:$resId"
    override fun format(resId: Int, vararg args: Any): String =
        get(resId).replace("%1\$s", args.firstOrNull().toString())
}

/** Captures the arguments of the last completion request. */
class RecordingLlmClient : OpenAiCompatibleLlmClient() {
    var result: LlmResult = LlmResult.success("ok")
    var capturedMessages: List<LlmMessage> = emptyList()
    var capturedStream: Boolean = true
    var capturedMaxTokens: Int = 2048
    var capturedBaseUrl: String = ""
    var capturedModel: String = ""

    override suspend fun complete(
        baseUrl: String,
        apiKey: String,
        model: String,
        messages: List<LlmMessage>,
        stream: Boolean,
        maxTokens: Int,
        progress: ((String) -> Unit)?
    ): LlmResult {
        capturedBaseUrl = baseUrl
        capturedModel = model
        capturedMessages = messages
        capturedStream = stream
        capturedMaxTokens = maxTokens
        progress?.invoke(result.content)
        return result
    }

    override suspend fun testConnection(baseUrl: String, apiKey: String, model: String): LlmResult {
        capturedBaseUrl = baseUrl
        capturedModel = model
        capturedStream = false
        return result
    }
}
