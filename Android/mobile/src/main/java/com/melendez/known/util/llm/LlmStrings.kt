package com.melendez.known.util.llm

import android.content.Context
import androidx.annotation.StringRes

/**
 * Abstraction over string resources so the context builder and the service can be unit tested on the
 * JVM without an Android framework. The app supplies [AndroidLlmStrings]; tests supply a fake.
 */
interface LlmStrings {
    fun get(@StringRes resId: Int): String
    fun format(@StringRes resId: Int, vararg args: Any): String
}

class AndroidLlmStrings(private val context: Context) : LlmStrings {
    override fun get(resId: Int): String = context.getString(resId)
    override fun format(resId: Int, vararg args: Any): String = context.getString(resId, *args)
}
