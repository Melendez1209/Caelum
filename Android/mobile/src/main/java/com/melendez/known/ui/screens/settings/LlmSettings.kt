package com.melendez.known.ui.screens.settings

import android.app.Application
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Visibility
import androidx.compose.material.icons.rounded.VisibilityOff
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.melendez.known.R
import com.melendez.known.ui.components.LocalScreenType
import com.melendez.known.ui.components.PreferenceSubtitle
import com.melendez.known.ui.components.SharedTopBar
import com.melendez.known.ui.navigation.Navigator
import com.melendez.known.ui.viewmodel.settings.LlmSettingsViewModel
import com.melendez.known.util.llm.LlmProviderOption

@Composable
fun LlmSettings(navigator: Navigator) {
    val application = LocalContext.current.applicationContext as Application
    val factory = object : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T =
            LlmSettingsViewModel(application) as T
    }
    val viewModel: LlmSettingsViewModel = viewModel(factory = factory)
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    val scrollBehavior = TopAppBarDefaults.exitUntilCollapsedScrollBehavior()
    val screenType = LocalScreenType.current

    Column {
        SharedTopBar(
            title = stringResource(R.string.prophets),
            screenType = screenType,
            navController = navigator,
            scrollBehavior = scrollBehavior
        )
        LazyColumn(
            modifier = Modifier.nestedScroll(scrollBehavior.nestedScrollConnection)
        ) {
            item { PreferenceSubtitle(text = stringResource(R.string.llm_provider)) }
            item {
                Row(modifier = Modifier.padding(horizontal = 16.dp)) {
                    listOf(
                        LlmProviderOption.NONE,
                        LlmProviderOption.LONG_CAT,
                        LlmProviderOption.CUSTOM
                    ).forEach { option ->
                        FilterChip(
                            selected = uiState.provider == option,
                            onClick = { viewModel.onProviderChange(option) },
                            label = {
                                Text(
                                    text = stringResource(
                                        when (option) {
                                            LlmProviderOption.NONE -> R.string.llm_provider_none
                                            LlmProviderOption.LONG_CAT -> R.string.llm_provider_longcat
                                            LlmProviderOption.CUSTOM -> R.string.llm_provider_custom
                                        }
                                    )
                                )
                            },
                            modifier = Modifier.padding(end = 8.dp)
                        )
                    }
                }
            }

            item { PreferenceSubtitle(text = stringResource(R.string.llm_base_url)) }
            item {
                OutlinedTextField(
                    value = uiState.baseUrl,
                    onValueChange = viewModel::onBaseUrlChange,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp),
                    singleLine = true
                )
            }

            item { PreferenceSubtitle(text = stringResource(R.string.llm_model)) }
            item {
                OutlinedTextField(
                    value = uiState.model,
                    onValueChange = viewModel::onModelChange,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp),
                    singleLine = true
                )
            }

            item { PreferenceSubtitle(text = stringResource(R.string.llm_api_key)) }
            item { ApiKeyField(value = uiState.apiKey, onValueChange = viewModel::onApiKeyChange) }

            item {
                Row(modifier = Modifier.padding(horizontal = 16.dp, vertical = 16.dp)) {
                    FilledTonalButton(
                        onClick = viewModel::testConnection,
                        enabled = !uiState.isTesting
                    ) {
                        Text(
                            text = stringResource(
                                if (uiState.isTesting) R.string.llm_testing
                                else R.string.llm_test_connection
                            )
                        )
                    }
                }
            }

            item {
                uiState.resultMessage?.let { message ->
                    Text(
                        text = message,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.padding(horizontal = 16.dp)
                    )
                }
            }

            item {
                Text(
                    text = stringResource(R.string.llm_privacy_note),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp)
                )
            }

            item {
                FilledTonalButton(
                    onClick = viewModel::save,
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
                ) {
                    Text(text = stringResource(R.string.save))
                }
            }
        }
    }
}

@Composable
private fun ApiKeyField(value: String, onValueChange: (String) -> Unit) {
    var visible by remember { mutableStateOf(false) }
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp),
        label = { Text(text = stringResource(R.string.llm_api_key_hint)) },
        singleLine = true,
        visualTransformation = if (visible) VisualTransformation.None else PasswordVisualTransformation(),
        trailingIcon = {
            IconButton(onClick = { visible = !visible }) {
                Icon(
                    imageVector = if (visible) Icons.Rounded.VisibilityOff else Icons.Rounded.Visibility,
                    contentDescription = null
                )
            }
        }
    )
}

