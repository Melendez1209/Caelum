package com.melendez.known.ui.navigation

import androidx.compose.animation.AnimatedContentTransitionScope
import androidx.compose.animation.ContentTransform
import androidx.compose.animation.EnterTransition
import androidx.compose.animation.ExitTransition
import androidx.compose.animation.SizeTransform
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.animation.togetherWith
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.MotionScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.unit.IntOffset
import androidx.navigation3.runtime.NavKey
import androidx.navigation3.scene.Scene
import androidx.navigationevent.NavigationEvent

/**
 * Remembers the edge of the last predictive back swipe. The regular pop transition reads it so that
 * a completed gesture and a plain system back press end up with the same geometry.
 */
internal class BackDirection {
    var swipeEdge: Int = NavigationEvent.EDGE_LEFT
}

private const val PUSH_OFFSET_DIVISOR = 5
private const val LIFT_SCALE = 0.94f
private const val FADE_THROUGH_SCALE = 0.96f

private val noClip = SizeTransform(clip = false)

/**
 * Fade through with a subtle scale, used for siblings such as Main -> Settings and for the
 * onboarding hand-off. Both directions use the same transform so they mirror each other.
 */
internal fun fadeThrough(motionScheme: MotionScheme): ContentTransform {
    val effects = motionScheme.defaultEffectsSpec<Float>()
    val spatial = motionScheme.defaultSpatialSpec<Float>()
    return ContentTransform(
        targetContentEnter = fadeIn(effects) + scaleIn(spatial, initialScale = FADE_THROUGH_SCALE),
        initialContentExit = fadeOut(effects) + scaleOut(spatial, targetScale = FADE_THROUGH_SCALE),
        sizeTransform = noClip
    )
}

/**
 * Backwards counterpart of the forward push: the previous screen slides in from the swiped edge
 * while the current screen slides out to the opposite side and lifts slightly.
 */
private fun directionalBack(motionScheme: MotionScheme, swipeEdge: Int): ContentTransform {
    val effects = motionScheme.defaultEffectsSpec<Float>()
    val offset = motionScheme.defaultSpatialSpec<IntOffset>()
    val scale = motionScheme.defaultSpatialSpec<Float>()
    val fromLeft = swipeEdge != NavigationEvent.EDGE_RIGHT
    val incomingOffset: (Int) -> Int =
        if (fromLeft) { width -> -width / PUSH_OFFSET_DIVISOR } else { width -> width / PUSH_OFFSET_DIVISOR }
    val outgoingOffset: (Int) -> Int =
        if (fromLeft) { width -> width / PUSH_OFFSET_DIVISOR } else { width -> -width / PUSH_OFFSET_DIVISOR }
    return ContentTransform(
        targetContentEnter = slideInHorizontally(offset, initialOffsetX = incomingOffset) + fadeIn(
            effects
        ),
        initialContentExit = slideOutHorizontally(
            offset,
            targetOffsetX = outgoingOffset
        ) + scaleOut(scale, targetScale = LIFT_SCALE) + fadeOut(effects),
        sizeTransform = noClip
    )
}

private fun AnimatedContentTransitionScope<Scene<NavKey>>.isTopLevelForward(topLevelKeys: Set<String>): Boolean {
    val incoming = targetState.entries.lastOrNull()?.contentKey
    return incoming in topLevelKeys && targetState.entries.size > initialState.entries.size
}

private fun AnimatedContentTransitionScope<Scene<NavKey>>.isTopLevelBack(topLevelKeys: Set<String>): Boolean {
    val outgoing = initialState.entries.lastOrNull()?.contentKey
    return outgoing in topLevelKeys && initialState.entries.size > targetState.entries.size
}

/** Forward transition for the outer NavDisplay. */
@Composable
internal fun rememberOuterForwardTransition(
    topLevelKeys: Set<String>,
    direction: BackDirection
): AnimatedContentTransitionScope<Scene<NavKey>>.() -> ContentTransform {
    val motionScheme = MaterialTheme.motionScheme
    return remember(motionScheme, topLevelKeys, direction) {
        {
            // A new forward navigation always re-anchors back animations to the standard direction.
            direction.swipeEdge = NavigationEvent.EDGE_LEFT
            if (isTopLevelForward(topLevelKeys)) {
                fadeThrough(motionScheme)
            } else {
                val effects = motionScheme.defaultEffectsSpec<Float>()
                val offset = motionScheme.defaultSpatialSpec<IntOffset>()
                ContentTransform(
                    targetContentEnter = slideInHorizontally(
                        offset,
                        initialOffsetX = { width -> width / PUSH_OFFSET_DIVISOR }) + scaleIn(
                        motionScheme.defaultSpatialSpec(),
                        initialScale = LIFT_SCALE
                    ) + fadeIn(effects),
                    initialContentExit = slideOutHorizontally(
                        offset,
                        targetOffsetX = { width -> -width / PUSH_OFFSET_DIVISOR }) + fadeOut(effects),
                    sizeTransform = noClip
                )
            }
        }
    }
}

/**
 * Pop transition used when the back gesture completes or the system back key is pressed. It mirrors
 * [rememberOuterPredictivePopTransition] so the hand-off at the end of a gesture is seamless.
 */
@Composable
internal fun rememberOuterPopTransition(
    topLevelKeys: Set<String>,
    direction: BackDirection,
    predictiveBackEnabled: Boolean
): AnimatedContentTransitionScope<Scene<NavKey>>.() -> ContentTransform {
    val motionScheme = MaterialTheme.motionScheme
    return remember(motionScheme, topLevelKeys, direction, predictiveBackEnabled) {
        {
            if (!predictiveBackEnabled) {
                EnterTransition.None togetherWith ExitTransition.None
            } else if (isTopLevelBack(topLevelKeys)) {
                fadeThrough(motionScheme)
            } else {
                directionalBack(motionScheme, direction.swipeEdge)
            }
        }
    }
}

/**
 * Predictive back transition. NavDisplay ties the resulting transform to the gesture progress, so a
 * completed gesture already ends in exactly the state that [rememberOuterPopTransition] continues
 * from. When the preference is off, the opaque outgoing screen hides the preview entirely.
 */
@Composable
internal fun rememberOuterPredictivePopTransition(
    topLevelKeys: Set<String>,
    direction: BackDirection,
    predictiveBackEnabled: Boolean
): AnimatedContentTransitionScope<Scene<NavKey>>.(Int) -> ContentTransform {
    val motionScheme = MaterialTheme.motionScheme
    return remember(motionScheme, topLevelKeys, direction, predictiveBackEnabled) {
        { swipeEdge ->
            if (!predictiveBackEnabled) {
                fadeIn(motionScheme.fastEffectsSpec()) togetherWith ExitTransition.None
            } else {
                direction.swipeEdge = swipeEdge
                if (isTopLevelBack(topLevelKeys)) {
                    fadeThrough(motionScheme)
                } else {
                    directionalBack(motionScheme, swipeEdge)
                }
            }
        }
    }
}
