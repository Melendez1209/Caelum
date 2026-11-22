@file:Suppress("DEPRECATION")

package com.melendez.caelum.data.repository

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import com.melendez.caelum.util.llm.LlmSecretStore

/**
 * Stores the assistant API key encrypted at rest, using a key held by the Android Keystore. This is
 * the Android equivalent of the desktop app's DPAPI-backed secret store: the key never enters the
 * database or the build, and is only ever read back at request time.
 */
class SecretStore(context: Context) : LlmSecretStore {

    private val preferences: SharedPreferences by lazy {
        val masterKey = MasterKey.Builder(context.applicationContext)
            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
            .build()
        EncryptedSharedPreferences.create(
            context.applicationContext,
            FILE_NAME,
            masterKey,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        )
    }

    override fun read(slot: String): String? = preferences.getString(slot, null)

    override fun write(slot: String, value: String) {
        preferences.edit().putString(slot, value).apply()
    }

    override fun delete(slot: String) {
        preferences.edit().remove(slot).apply()
    }

    private companion object {
        const val FILE_NAME = "caelum_secrets"
    }
}
