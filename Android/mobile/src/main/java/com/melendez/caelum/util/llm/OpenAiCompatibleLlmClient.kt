package com.melendez.caelum.util.llm

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.withContext
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import okhttp3.Call
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.BufferedReader
import java.io.IOException
import java.util.concurrent.TimeUnit
import kotlin.coroutines.cancellation.CancellationException

/**
 * Talks to any OpenAI-compatible chat completion endpoint. Accepts a root URL, a `/v1` URL or a full
 * `/v1/chat/completions` URL and normalises them to the latter. Streamed replies are delivered to
 * `progress` as they arrive; a stream that breaks keeps the text received so far.
 */
open class OpenAiCompatibleLlmClient(private val http: OkHttpClient = defaultClient()) {

    /** The in-flight request, kept so a stop button can cancel it directly. */
    @Volatile
    private var activeCall: Call? = null

    /**
     * Cancels the current request immediately. Called from the stop button: blocking reads do not
     * respond to coroutine cancellation on their own, so the underlying OkHttp call must be cancelled
     * explicitly or the stream keeps producing tokens.
     */
    fun cancelActive() {
        activeCall?.cancel()
    }

    open suspend fun complete(
        baseUrl: String,
        apiKey: String,
        model: String,
        messages: List<LlmMessage>,
        stream: Boolean = true,
        maxTokens: Int = 2048,
        progress: ((String) -> Unit)? = null
    ): LlmResult = withContext(Dispatchers.IO) {
        val payload = json.encodeToString(
            ChatCompletionRequest.serializer(),
            ChatCompletionRequest(
                model = model,
                messages = messages.map { RequestMessage(it.role, it.content) },
                stream = stream,
                maxTokens = maxTokens
            )
        )
        val request = Request.Builder()
            .url(buildChatUrl(baseUrl))
            .header("Authorization", "Bearer $apiKey")
            .header("Content-Type", "application/json")
            .post(payload.toRequestBody(JSON_MEDIA))
            .build()

        val call = http.newCall(request)
        activeCall = call
        val job = currentCoroutineContext()[Job]
        fun cancelled() = job?.isCancelled == true
        try {
            val response = call.execute()
            response.use {
                if (!isSuccess(it.code)) return@withContext mapHttpError(it.code, it.message)
                val body = it.body
                    ?: return@withContext LlmResult.failure(LlmErrorKind.UNKNOWN, "Empty response")
                val result =
                    if (it.header("Content-Type").orEmpty().contains("text/event-stream", true)) {
                        readEventStream(body.charStream().buffered(), progress)
                    } else {
                        readJson(body.string())
                    }
                // A cancelled stream surfaces as an interrupted read with partial text. Report it as
                // CANCELLED rather than a network failure so the UI can stop cleanly and keep the text.
                if (cancelled() && !result.isSuccess) {
                    return@withContext LlmResult.failure(
                        LlmErrorKind.CANCELLED,
                        "Cancelled",
                        result.content
                    )
                }
                result
            }
        } catch (e: CancellationException) {
            throw e
        } catch (e: IOException) {
            if (cancelled()) {
                LlmResult.failure(LlmErrorKind.CANCELLED, "Cancelled")
            } else {
                LlmResult.failure(LlmErrorKind.NETWORK, e.message ?: "Network error")
            }
        } catch (e: Exception) {
            if (cancelled()) {
                LlmResult.failure(LlmErrorKind.CANCELLED, "Cancelled")
            } else {
                LlmResult.failure(LlmErrorKind.UNKNOWN, e.javaClass.simpleName + ": " + e.message)
            }
        } finally {
            if (activeCall === call) activeCall = null
        }
    }

    /** Non-streaming request with a single token, used by the settings page's test button. */
    open suspend fun testConnection(baseUrl: String, apiKey: String, model: String): LlmResult =
        complete(
            baseUrl,
            apiKey,
            model,
            listOf(LlmMessage.user("Hi")),
            stream = false,
            maxTokens = 1
        )

    private fun isSuccess(code: Int): Boolean = code in 200..299

    private fun readEventStream(reader: BufferedReader, progress: ((String) -> Unit)?): LlmResult {
        val text = StringBuilder()
        try {
            reader.lineSequence().forEach { line ->
                if (!line.startsWith("data:")) return@forEach
                val data = line.removePrefix("data:").trim()
                if (data.isEmpty() || data == "[DONE]") return@forEach
                val chunk = json.decodeFromString<StreamChunk>(data)
                chunk.choices.firstOrNull()?.delta?.content?.let { delta ->
                    text.append(delta)
                    progress?.invoke(delta)
                }
            }
        } catch (e: IOException) {
            return LlmResult.failure(
                LlmErrorKind.NETWORK,
                e.message ?: "Stream interrupted",
                text.toString()
            )
        } catch (_: CancellationException) {
            return LlmResult.failure(LlmErrorKind.CANCELLED, "Cancelled", text.toString())
        }
        return if (text.isEmpty()) {
            LlmResult.failure(LlmErrorKind.UNKNOWN, "Empty stream")
        } else {
            LlmResult.success(text.toString())
        }
    }

    private fun readJson(raw: String): LlmResult = try {
        val content = json.decodeFromString<CompletionResponse>(raw)
            .choices.firstOrNull()?.message?.content.orEmpty()
        if (content.isEmpty()) {
            LlmResult.failure(LlmErrorKind.UNKNOWN, "Empty response")
        } else {
            LlmResult.success(content)
        }
    } catch (e: Exception) {
        LlmResult.failure(LlmErrorKind.UNKNOWN, e.message ?: "Malformed response")
    }

    private fun mapHttpError(code: Int, message: String): LlmResult = when (code) {
        401, 403 -> LlmResult.failure(LlmErrorKind.UNAUTHORIZED, message)
        404 -> LlmResult.failure(LlmErrorKind.NOT_FOUND, message)
        429 -> LlmResult.failure(LlmErrorKind.RATE_LIMITED, message)
        else -> LlmResult.failure(LlmErrorKind.UNKNOWN, "HTTP $code: $message")
    }

    companion object {
        private val JSON_MEDIA = "application/json".toMediaType()
        private val json = Json { ignoreUnknownKeys = true }

        /** Normalise any accepted base URL form to `{base}/v1/chat/completions`. */
        fun buildChatUrl(baseUrl: String): String {
            var url = baseUrl.trim().trimEnd('/')
            if (url.endsWith("/chat/completions")) url = url.removeSuffix("/chat/completions")
            if (url.endsWith("/v1")) url = url.removeSuffix("/v1")
            return "$url/v1/chat/completions"
        }

        private fun defaultClient(): OkHttpClient = OkHttpClient.Builder()
            .connectTimeout(2, TimeUnit.MINUTES)
            .readTimeout(2, TimeUnit.MINUTES)
            .writeTimeout(2, TimeUnit.MINUTES)
            .build()
    }
}

@Serializable
private data class ChatCompletionRequest(
    val model: String,
    val messages: List<RequestMessage>,
    val stream: Boolean,
    @SerialName("max_tokens") val maxTokens: Int
)

@Serializable
private data class RequestMessage(val role: String, val content: String)

@Serializable
private data class StreamChunk(val choices: List<StreamChoice> = emptyList())

@Serializable
private data class StreamChoice(val delta: Delta = Delta())

@Serializable
private data class Delta(val content: String? = null)

@Serializable
private data class CompletionResponse(val choices: List<CompletionChoice> = emptyList())

@Serializable
private data class CompletionChoice(val message: ResponseMessage = ResponseMessage())

@Serializable
private data class ResponseMessage(val content: String = "")
