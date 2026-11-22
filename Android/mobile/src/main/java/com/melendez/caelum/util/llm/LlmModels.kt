package com.melendez.caelum.util.llm

/** One conversation turn sent to the model. */
data class LlmMessage(val role: String, val content: String) {
    companion object {
        fun system(content: String) = LlmMessage("system", content)
        fun user(content: String) = LlmMessage("user", content)
        fun assistant(content: String) = LlmMessage("assistant", content)
    }
}

/** Why a request did not produce a complete answer. */
enum class LlmErrorKind {
    NOT_CONFIGURED,
    NETWORK,
    UNAUTHORIZED,
    NOT_FOUND,
    RATE_LIMITED,
    CANCELLED,
    UNKNOWN
}

/**
 * Result of a completion. On a stream that breaks midway, [content] carries the partial answer so
 * the caller can still show what arrived before the failure.
 */
data class LlmResult(
    val isSuccess: Boolean,
    val content: String = "",
    val errorKind: LlmErrorKind? = null,
    val message: String = ""
) {
    companion object {
        fun success(content: String) = LlmResult(isSuccess = true, content = content)

        fun failure(
            kind: LlmErrorKind,
            message: String = "",
            partial: String = ""
        ) = LlmResult(isSuccess = false, content = partial, errorKind = kind, message = message)
    }
}
