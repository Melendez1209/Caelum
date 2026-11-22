package com.melendez.caelum.ui.screens

import android.app.Application
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.ArrowBack
import androidx.compose.material.icons.automirrored.rounded.Send
import androidx.compose.material.icons.rounded.AddComment
import androidx.compose.material.icons.rounded.Delete
import androidx.compose.material.icons.rounded.Edit
import androidx.compose.material.icons.rounded.Forum
import androidx.compose.material.icons.rounded.Person
import androidx.compose.material.icons.rounded.Settings
import androidx.compose.material.icons.rounded.Stop
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.melendez.caelum.R
import com.melendez.caelum.ui.navigation.Navigator
import com.melendez.caelum.ui.viewmodel.prophets.ChatMessageUi
import com.melendez.caelum.ui.viewmodel.prophets.ChatSessionUi
import com.melendez.caelum.ui.viewmodel.prophets.ProphetsViewModel
import com.mikepenz.markdown.m3.Markdown

@Composable
fun Prophets(
    navigator: Navigator,
    examId: Long = 0L,
    onBack: () -> Unit = { navigator.goBack() },
    contentPadding: PaddingValues = PaddingValues(),
    showBackButton: Boolean = true
) {
    val application = LocalContext.current.applicationContext as Application
    val factory = object : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T =
            ProphetsViewModel(application, examId) as T
    }
    val viewModel: ProphetsViewModel = viewModel(factory = factory)
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    val snackbarHostState = remember { SnackbarHostState() }
    val listState = rememberLazyListState()

    LaunchedEffect(uiState.message) {
        uiState.message?.let {
            snackbarHostState.showSnackbar(it)
            viewModel.dismissMessage()
        }
    }
    // Scroll to the trailing anchor rather than the last message. Scrolling to a message aligns it
    // to the viewport top; when a streaming bubble grows taller than the screen the newest text at
    // its bottom stays hidden. The anchor is the final item, so scrolling to it clamps the list to
    // the absolute bottom and the latest text is always visible.
    fun bottomAnchorIndex(): Int = uiState.messages.size

    LaunchedEffect(uiState.currentSessionId, uiState.messages.size) {
        if (uiState.messages.isNotEmpty()) {
            listState.scrollToItem(bottomAnchorIndex())
        }
    }
    // Follow the tail on every streaming delta. scrollToItem is instant, so a new token cannot
    // cancel the scroll the way an animation would.
    LaunchedEffect(uiState.streamingText) {
        if (uiState.isGenerating && uiState.messages.isNotEmpty()) {
            listState.scrollToItem(bottomAnchorIndex())
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = uiState.currentTitle.ifBlank { stringResource(R.string.prophets) }
                    )
                },
                navigationIcon = {
                    if (showBackButton) {
                        IconButton(onClick = onBack) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Rounded.ArrowBack,
                                contentDescription = stringResource(R.string.back)
                            )
                        }
                    }
                },
                actions = {
                    IconButton(onClick = { viewModel.setShowSessions(true) }) {
                        Icon(
                            imageVector = Icons.Rounded.Forum,
                            contentDescription = stringResource(R.string.llm_sessions)
                        )
                    }
                    IconButton(onClick = { navigator.navigate(Screens.LlmSettings) }) {
                        Icon(
                            imageVector = Icons.Rounded.Settings,
                            contentDescription = stringResource(R.string.open_settings)
                        )
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(bottom = contentPadding.calculateBottomPadding())
        ) {
            if (!uiState.isConfigured) {
                Surface(color = MaterialTheme.colorScheme.errorContainer) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = stringResource(R.string.llm_not_configured),
                            modifier = Modifier.weight(1f),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onErrorContainer
                        )
                        TextButton(onClick = { navigator.navigate(Screens.LlmSettings) }) {
                            Text(stringResource(R.string.open_settings))
                        }
                    }
                }
            }

            LazyColumn(
                modifier = Modifier
                    .weight(1f)
                    .fillMaxWidth(),
                state = listState,
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                items(uiState.messages, key = { it.id }) { message ->
                    ChatBubble(message = message)
                }
                // Trailing anchor: scrolling here clamps the list to the exact bottom.
                item(key = "bottom_anchor") {
                    Spacer(modifier = Modifier.height(1.dp))
                }
            }

            ChatInputBar(
                draft = uiState.draft,
                isGenerating = uiState.isGenerating,
                onDraftChange = viewModel::onDraftChange,
                onSend = viewModel::send,
                onStop = viewModel::stop
            )
        }
    }

    if (uiState.showSessions) {
        SessionSheet(
            sessions = uiState.sessions,
            currentSessionId = uiState.currentSessionId,
            onDismiss = { viewModel.setShowSessions(false) },
            onNew = { viewModel.newSession() },
            onSelect = { viewModel.selectSession(it) },
            onRename = { id, title -> viewModel.renameSession(id, title) },
            onDelete = { viewModel.deleteSession(it) }
        )
    }
}

@Composable
private fun ChatBubble(message: ChatMessageUi) {
    val isUser = message.role == "user"
    val containerColor = when {
        message.isError -> MaterialTheme.colorScheme.errorContainer
        isUser -> MaterialTheme.colorScheme.primaryContainer
        else -> MaterialTheme.colorScheme.surfaceContainerHigh
    }
    val contentColor = when {
        message.isError -> MaterialTheme.colorScheme.onErrorContainer
        isUser -> MaterialTheme.colorScheme.onPrimaryContainer
        else -> MaterialTheme.colorScheme.onSurface
    }

    BoxWithConstraints(
        modifier = Modifier.fillMaxWidth(),
        contentAlignment = if (isUser) Alignment.CenterEnd else Alignment.CenterStart
    ) {
        Row(
            modifier = Modifier
                .widthIn(max = maxWidth * 0.88f)
                .background(containerColor, MaterialTheme.shapes.extraLarge)
                .padding(horizontal = 14.dp, vertical = 10.dp)
        ) {
            if (!isUser) {
                Icon(
                    imageVector = Icons.Rounded.Person,
                    contentDescription = null,
                    modifier = Modifier
                        .size(20.dp)
                        .padding(end = 6.dp),
                    tint = contentColor
                )
            }
            // SelectionContainer makes the bubble's text long-press selectable and copyable.
            SelectionContainer {
                if (isUser || message.isStreaming) {
                    Text(text = message.content, color = contentColor)
                } else {
                    val cached = message.markdownState
                    if (cached != null) {
                        // Reuse the pre-parsed AST so scrolling the row off-screen and back does not
                        // reparse the Markdown and stutter the list.
                        Markdown(state = cached, modifier = Modifier)
                    } else {
                        Markdown(content = message.content, modifier = Modifier)
                    }
                }
            }
        }
    }
}

@Composable
private fun ChatInputBar(
    draft: String,
    isGenerating: Boolean,
    onDraftChange: (String) -> Unit,
    onSend: () -> Unit,
    onStop: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        OutlinedTextField(
            value = draft,
            onValueChange = onDraftChange,
            modifier = Modifier.weight(1f),
            placeholder = { Text(text = stringResource(R.string.llm_ask_hint)) },
            shape = MaterialTheme.shapes.extraLarge,
            maxLines = 4,
            enabled = !isGenerating
        )
        Spacer(modifier = Modifier.width(8.dp))
        FilledIconButton(
            onClick = { if (isGenerating) onStop() else onSend() },
            modifier = Modifier.size(56.dp),
            enabled = isGenerating || draft.isNotBlank()
        ) {
            Icon(
                imageVector = if (isGenerating) Icons.Rounded.Stop else Icons.AutoMirrored.Rounded.Send,
                contentDescription = stringResource(
                    if (isGenerating) R.string.llm_stop else R.string.ask
                )
            )
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SessionSheet(
    sessions: List<ChatSessionUi>,
    currentSessionId: Long,
    onDismiss: () -> Unit,
    onNew: () -> Unit,
    onSelect: (Long) -> Unit,
    onRename: (Long, String) -> Unit,
    onDelete: (Long) -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = stringResource(R.string.llm_sessions),
                style = MaterialTheme.typography.titleLarge,
                modifier = Modifier.weight(1f)
            )
            FilledIconButton(onClick = onNew) {
                Icon(
                    imageVector = Icons.Rounded.AddComment,
                    contentDescription = stringResource(R.string.llm_new_session)
                )
            }
        }
        LazyColumn(
            modifier = Modifier.fillMaxWidth(),
            contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
        ) {
            if (sessions.isEmpty()) {
                item {
                    Text(
                        text = stringResource(R.string.llm_no_sessions),
                        modifier = Modifier.padding(16.dp)
                    )
                }
            }
            items(sessions, key = { it.id }) { session ->
                SessionRow(
                    session = session,
                    selected = session.id == currentSessionId,
                    onClick = { onSelect(session.id) },
                    onRename = { onRename(session.id, it) },
                    onDelete = { onDelete(session.id) }
                )
            }
        }
    }
}

@Composable
private fun SessionRow(
    session: ChatSessionUi,
    selected: Boolean,
    onClick: () -> Unit,
    onRename: (String) -> Unit,
    onDelete: () -> Unit
) {
    var editing by remember { mutableStateOf(false) }
    var title by remember(session.title) { mutableStateOf(session.title) }

    Surface(
        onClick = onClick,
        color = if (selected) MaterialTheme.colorScheme.secondaryContainer else MaterialTheme.colorScheme.surface,
        shape = MaterialTheme.shapes.medium,
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = session.title.ifBlank { stringResource(R.string.llm_new_session) },
                modifier = Modifier.weight(1f),
                style = MaterialTheme.typography.bodyLarge
            )
            IconButton(onClick = { editing = true }) {
                Icon(
                    imageVector = Icons.Rounded.Edit,
                    contentDescription = stringResource(R.string.llm_rename)
                )
            }
            IconButton(onClick = onDelete) {
                Icon(
                    imageVector = Icons.Rounded.Delete,
                    contentDescription = stringResource(R.string.llm_delete_session)
                )
            }
        }
    }

    if (editing) {
        AlertDialog(
            onDismissRequest = { editing = false },
            title = { Text(text = stringResource(R.string.llm_rename)) },
            text = {
                OutlinedTextField(
                    value = title,
                    onValueChange = { title = it },
                    singleLine = true,
                    label = { Text(text = stringResource(R.string.llm_session_title_hint)) }
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        onRename(title)
                        editing = false
                    }
                ) {
                    Text(text = stringResource(R.string.confirm))
                }
            },
            dismissButton = {
                TextButton(onClick = { editing = false }) {
                    Text(text = stringResource(R.string.cancel))
                }
            }
        )
    }
}
