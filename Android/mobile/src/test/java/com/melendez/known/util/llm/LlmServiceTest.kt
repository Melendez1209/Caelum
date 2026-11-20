package com.melendez.known.util.llm

import com.melendez.known.R
import com.melendez.known.data.entity.Settings
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LlmServiceTest {

    private fun configuredSettings() = Settings(
        llmProviderValue = LlmProviderOption.LONG_CAT.value,
        llmBaseUrl = "https://api.longcat.chat/openai",
        llmModel = "LongCat-2.5"
    )

    @Test
    fun isConfiguredRequiresProviderKeyBaseUrlAndModel() = runBlocking {
        val secrets = InMemorySecretStore()
        val config = FakeConfigSource(configuredSettings())
        val service = LlmService(config, secrets, FakeStrings(), RecordingLlmClient())

        assertFalse(service.isConfigured())

        secrets.write(LlmService.API_KEY_SLOT, "secret")
        assertTrue(service.isConfigured())

        config.settings = config.settings.copy(llmProviderValue = LlmProviderOption.NONE.value)
        assertFalse(service.isConfigured())
    }

    @Test
    fun presetsAreUsedWhenStoredUrlAndModelAreBlank() = runBlocking {
        val secrets = InMemorySecretStore()
        secrets.write(LlmService.API_KEY_SLOT, "secret")
        val config = FakeConfigSource(
            Settings(
                llmProviderValue = LlmProviderOption.LONG_CAT.value,
                llmBaseUrl = "",
                llmModel = ""
            )
        )
        val client = RecordingLlmClient()
        val service = LlmService(config, secrets, FakeStrings(), client)

        val result = service.complete(listOf(LlmMessage.user("hi")))

        assertTrue(result.isSuccess)
        assertEquals("https://api.longcat.chat/openai", client.capturedBaseUrl)
        assertEquals("LongCat-2.5", client.capturedModel)
    }

    @Test
    fun completePrependsSystemPromptAndTrimsHistory() = runBlocking {
        val secrets = InMemorySecretStore()
        secrets.write(LlmService.API_KEY_SLOT, "secret")
        val config = FakeConfigSource(configuredSettings())
        val strings = FakeStrings().apply { put(R.string.llm_system_prompt, "SYSTEM") }
        val client = RecordingLlmClient()
        val service = LlmService(config, secrets, strings, client)

        val history = (1..30).map { LlmMessage.user("m$it") }
        service.complete(history, context = "EXAM DATA")

        assertEquals("system", client.capturedMessages[0].role)
        assertEquals("SYSTEM", client.capturedMessages[0].content)
        assertEquals("system", client.capturedMessages[1].role)
        assertTrue(client.capturedMessages[1].content.contains("EXAM DATA"))
        assertEquals(22, client.capturedMessages.size) // 2 system + 20 history
        assertEquals("m11", client.capturedMessages[2].content)
        assertEquals("m30", client.capturedMessages.last().content)
    }

    @Test
    fun completeSkipsSystemPromptAfterFirstRequest() = runBlocking {
        val secrets = InMemorySecretStore()
        secrets.write(LlmService.API_KEY_SLOT, "secret")
        val client = RecordingLlmClient()
        val service = LlmService(
            FakeConfigSource(configuredSettings()),
            secrets,
            FakeStrings(),
            client
        )

        service.complete(listOf(LlmMessage.user("hi")), includeSystemPrompt = false)

        assertEquals("user", client.capturedMessages[0].role)
    }

    @Test
    fun generateTitleUsesNonStreamingRequestAndReturnsTitle() = runBlocking {
        val secrets = InMemorySecretStore()
        secrets.write(LlmService.API_KEY_SLOT, "secret")
        val strings = FakeStrings().apply { put(R.string.llm_title_prompt, "Title of: %1\$s") }
        val client = RecordingLlmClient().apply { result = LlmResult.success("My title") }
        val service = LlmService(
            FakeConfigSource(configuredSettings()),
            secrets,
            strings,
            client
        )

        val title = service.generateTitle("first message")

        assertEquals("My title", title)
        assertFalse(client.capturedStream)
        assertEquals(48, client.capturedMaxTokens)
        assertEquals("Title of: first message", client.capturedMessages[0].content)
    }

    @Test
    fun generateTitleIsEmptyWhenNotConfigured() = runBlocking {
        val service = LlmService(
            FakeConfigSource(Settings()),
            InMemorySecretStore(),
            FakeStrings(),
            RecordingLlmClient()
        )
        assertEquals("", service.generateTitle("hello"))
    }

    @Test
    fun setApiKeyStoresAndClearsTheKey() {
        val secrets = InMemorySecretStore()
        val service = LlmService(
            FakeConfigSource(configuredSettings()),
            secrets,
            FakeStrings(),
            RecordingLlmClient()
        )
        service.setApiKey("  abc  ")
        assertEquals("abc", secrets.read(LlmService.API_KEY_SLOT))
        service.setApiKey("   ")
        assertEquals(null, secrets.read(LlmService.API_KEY_SLOT))
    }
}
