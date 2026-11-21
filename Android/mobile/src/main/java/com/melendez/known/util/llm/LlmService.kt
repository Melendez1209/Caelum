package com.melendez.known.util.llm

import com.melendez.known.R

/**
 * The model behind the assistant. Reads the stored provider configuration and the API key, then sends
 * the conversation to an OpenAI-compatible endpoint through [client].
 */
class LlmService(
    private val config: LlmConfigSource,
    private val secrets: LlmSecretStore,
    private val strings: LlmStrings,
    private val client: OpenAiCompatibleLlmClient = OpenAiCompatibleLlmClient()
) {

    /** How many conversation turns are sent, counting from the most recent. */
    private val maxHistoryMessages = 20

    suspend fun isConfigured(): Boolean {
        val settings = config.currentSettings() ?: return false
        val option = LlmProviderOption.from(settings.llmProviderValue)
        if (option == LlmProviderOption.NONE) return false
        if (readApiKey().isNullOrBlank()) return false
        val baseUrl = resolveBaseUrl(option, settings.llmBaseUrl)
        val model = resolveModel(option, settings.llmModel)
        return baseUrl.isNotBlank() && model.isNotBlank()
    }

    /** Cancels the in-flight completion, e.g. when the user presses stop. */
    fun cancelActive() = client.cancelActive()

    /** Stores or clears the API key. An empty value removes it. */
    fun setApiKey(apiKey: String?) {
        if (apiKey.isNullOrBlank()) secrets.delete(API_KEY_SLOT)
        else secrets.write(API_KEY_SLOT, apiKey.trim())
    }

    /**
     * Sends the conversation and streams the reply through [progress]. The system prompt is only
     * stated on the session's first request; the exam data is labelled as data and kept out of the
     * history the student sees.
     */
    suspend fun complete(
        history: List<LlmMessage>,
        context: String? = null,
        progress: ((String) -> Unit)? = null,
        includeSystemPrompt: Boolean = true
    ): LlmResult {
        val settings =
            config.currentSettings() ?: return LlmResult.failure(LlmErrorKind.NOT_CONFIGURED)
        val option = LlmProviderOption.from(settings.llmProviderValue)

        if (option == LlmProviderOption.NONE) return LlmResult.failure(LlmErrorKind.NOT_CONFIGURED)

        val apiKey = readApiKey()
        if (apiKey.isNullOrBlank()) return LlmResult.failure(LlmErrorKind.NOT_CONFIGURED)

        val baseUrl = resolveBaseUrl(option, settings.llmBaseUrl)
        val model = resolveModel(option, settings.llmModel)
        if (baseUrl.isBlank() || model.isBlank()) return LlmResult.failure(LlmErrorKind.NOT_CONFIGURED)

        val messages = mutableListOf<LlmMessage>()
        if (includeSystemPrompt) {
            messages += LlmMessage.system(strings.get(R.string.llm_system_prompt))
        }
        if (!context.isNullOrBlank()) {
            messages += LlmMessage.system(
                strings.get(R.string.llm_exam_context_lead) + "\n\n" + context
            )
        }
        messages += history.takeLast(maxHistoryMessages)

        return client.complete(baseUrl, apiKey, model, messages, progress = progress)
    }

    /** Tests configuration typed into settings without saving it first. */
    suspend fun testConnection(baseUrl: String, model: String, apiKey: String? = null): LlmResult {
        val key = if (apiKey.isNullOrBlank()) readApiKey() else apiKey.trim()
        if (baseUrl.isBlank() || model.isBlank() || key.isNullOrBlank()) {
            return LlmResult.failure(LlmErrorKind.NOT_CONFIGURED)
        }
        return client.testConnection(baseUrl, key, model)
    }

    /** Asks the model for a short title; returns an empty string when it is unavailable. */
    suspend fun generateTitle(firstMessage: String): String {
        val settings = config.currentSettings() ?: return ""
        val option = LlmProviderOption.from(settings.llmProviderValue)
        if (option == LlmProviderOption.NONE) return ""

        val apiKey = readApiKey() ?: return ""
        if (apiKey.isBlank()) return ""

        val baseUrl = resolveBaseUrl(option, settings.llmBaseUrl)
        val model = resolveModel(option, settings.llmModel)
        if (baseUrl.isBlank() || model.isBlank()) return ""

        val prompt = strings.format(R.string.llm_title_prompt, firstMessage)
        val result = client.complete(
            baseUrl,
            apiKey,
            model,
            listOf(LlmMessage.user(prompt)),
            stream = false,
            maxTokens = 48
        )
        if (!result.isSuccess) return ""

        val title = result.content.trim().ifEmpty { return "" }
        val firstLine = title.lineSequence().first().trim()
        return firstLine.take(40)
    }

    private fun readApiKey(): String? = secrets.read(API_KEY_SLOT)

    private fun resolveBaseUrl(option: LlmProviderOption, stored: String): String =
        stored.takeIf { it.isNotBlank() }?.trim()
            ?: LlmProviderPresets.find(option)?.defaultBaseUrl.orEmpty()

    private fun resolveModel(option: LlmProviderOption, stored: String): String =
        stored.takeIf { it.isNotBlank() }?.trim()
            ?: LlmProviderPresets.find(option)?.defaultModel.orEmpty()

    companion object {
        const val API_KEY_SLOT = "llm_api_key"
    }
}
