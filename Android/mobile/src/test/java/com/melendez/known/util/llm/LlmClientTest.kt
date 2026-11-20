package com.melendez.known.util.llm

import kotlinx.coroutines.runBlocking
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LlmClientTest {

    @Test
    fun buildChatUrlAcceptsEveryBaseUrlForm() {
        assertEquals(
            "https://api.longcat.chat/openai/v1/chat/completions",
            OpenAiCompatibleLlmClient.buildChatUrl("https://api.longcat.chat/openai")
        )
        assertEquals(
            "https://api.longcat.chat/openai/v1/chat/completions",
            OpenAiCompatibleLlmClient.buildChatUrl("https://api.longcat.chat/openai/")
        )
        assertEquals(
            "https://api.openai.com/v1/chat/completions",
            OpenAiCompatibleLlmClient.buildChatUrl("https://api.openai.com/v1")
        )
        assertEquals(
            "https://api.openai.com/v1/chat/completions",
            OpenAiCompatibleLlmClient.buildChatUrl("https://api.openai.com/v1/chat/completions")
        )
    }

    @Test
    fun parsesStreamedDeltas() = runBlocking {
        val sse = "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n" +
            "data: [DONE]\n\n"
        val client = clientReturning(200, sse, "text/event-stream")
        val deltas = mutableListOf<String>()
        val result = client.complete(
            "https://api.example.com", "key", "model",
            listOf(LlmMessage.user("hi")),
            progress = { deltas.add(it) }
        )
        assertTrue(result.isSuccess)
        assertEquals("Hello", result.content)
        assertEquals(listOf("Hel", "lo"), deltas)
    }

    @Test
    fun parsesPlainJsonWhenServerIgnoresStreaming() = runBlocking {
        val json = "{\"choices\":[{\"message\":{\"content\":\"plain answer\"}}]}"
        val client = clientReturning(200, json, "application/json")
        val result = client.complete(
            "https://api.example.com", "key", "model",
            listOf(LlmMessage.user("hi"))
        )
        assertTrue(result.isSuccess)
        assertEquals("plain answer", result.content)
    }

    @Test
    fun mapsHttpErrors() = runBlocking {
        val cases = mapOf(
            401 to LlmErrorKind.UNAUTHORIZED,
            403 to LlmErrorKind.UNAUTHORIZED,
            404 to LlmErrorKind.NOT_FOUND,
            429 to LlmErrorKind.RATE_LIMITED,
            500 to LlmErrorKind.UNKNOWN
        )
        cases.forEach { (code, expected) ->
            val client = clientReturning(code, "{}", "application/json")
            val result = client.complete(
                "https://api.example.com", "key", "model",
                listOf(LlmMessage.user("hi"))
            )
            assertFalse(result.isSuccess)
            assertEquals(expected, result.errorKind)
        }
    }

    @Test
    fun testConnectionIsNonStreaming() = runBlocking {
        val client = RecordingLlmClient()
        client.testConnection("https://api.example.com", "key", "model")
        assertEquals("https://api.example.com", client.capturedBaseUrl)
        assertFalse(client.capturedStream)
    }

    private fun clientReturning(code: Int, body: String, contentType: String): OpenAiCompatibleLlmClient {
        val interceptor = object : Interceptor {
            override fun intercept(chain: Interceptor.Chain): Response =
                okhttp3.Response.Builder()
                    .request(chain.request())
                    .protocol(okhttp3.Protocol.HTTP_1_1)
                    .code(code)
                    .message("test")
                    .header("Content-Type", contentType)
                    .body(body.toResponseBody(contentType.toMediaTypeOrNull()))
                    .build()
        }
        return OpenAiCompatibleLlmClient(OkHttpClient.Builder().addInterceptor(interceptor).build())
    }
}
