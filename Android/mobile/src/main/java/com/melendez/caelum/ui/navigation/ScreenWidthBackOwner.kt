package com.melendez.caelum.ui.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalResources
import androidx.navigationevent.DirectNavigationEventInput
import androidx.navigationevent.NavigationEvent
import androidx.navigationevent.NavigationEventDispatcher
import androidx.navigationevent.NavigationEventDispatcherOwner
import androidx.navigationevent.NavigationEventHandler
import androidx.navigationevent.NavigationEventInfo
import androidx.navigationevent.compose.LocalNavigationEventDispatcherOwner

/**
 * Bridges system back events into a private dispatcher whose progress is the finger travel as a
 * fraction of the screen width, rather than the system back affordance progress. NavDisplay only
 * sees this rewritten progress, so its predictive animation follows the finger across the full
 * screen width.
 */
@Composable
internal fun rememberScreenWidthBackOwner(onExit: () -> Unit): NavigationEventDispatcherOwner {
    val resources = LocalResources.current
    val parentOwner = LocalNavigationEventDispatcherOwner.current
    val dispatcher =
        remember(onExit) {
            NavigationEventDispatcher { onExit() }
        }

    DisposableEffect(resources, parentOwner, dispatcher) {
        val input = DirectNavigationEventInput()
        dispatcher.addInput(input)

        var startX = 0f
        var swipeEdge = NavigationEvent.EDGE_LEFT

        fun rewritten(event: NavigationEvent, progress: Float) =
            NavigationEvent(
                touchX = event.touchX,
                touchY = event.touchY,
                progress = progress,
                swipeEdge = event.swipeEdge,
                frameTimeMillis = event.frameTimeMillis,
            )

        val bridge =
            object : NavigationEventHandler<NavigationEventInfo>(
                initialInfo = NavigationEventInfo.None,
                isBackEnabled = true,
            ) {
                override fun onBackStarted(event: NavigationEvent) {
                    startX = event.touchX
                    swipeEdge = event.swipeEdge
                    input.backStarted(rewritten(event, 0f))
                }

                override fun onBackProgressed(event: NavigationEvent) {
                    val widthPx = resources.displayMetrics.widthPixels.coerceAtLeast(1)
                    val travelled =
                        if (swipeEdge == NavigationEvent.EDGE_RIGHT) {
                            startX - event.touchX
                        } else {
                            event.touchX - startX
                        }
                    input.backProgressed(rewritten(event, (travelled / widthPx).coerceIn(0f, 1f)))
                }

                override fun onBackCompleted() {
                    input.backCompleted()
                }

                override fun onBackCancelled() {
                    input.backCancelled()
                }
            }

        // Keep the host default input active by registering the bridge at DEFAULT priority.
        parentOwner?.navigationEventDispatcher?.addHandler(
            bridge,
            NavigationEventDispatcher.PRIORITY_DEFAULT
        )
        onDispose {
            bridge.remove()
            dispatcher.removeInput(input)
            dispatcher.dispose()
        }
    }

    return remember(dispatcher) { ScreenWidthBackOwner(dispatcher) }
}

private class ScreenWidthBackOwner(
    override val navigationEventDispatcher: NavigationEventDispatcher
) : NavigationEventDispatcherOwner
