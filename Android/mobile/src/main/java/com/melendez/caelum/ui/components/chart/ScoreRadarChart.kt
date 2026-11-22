package com.melendez.caelum.ui.components.chart

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.DrawStyle
import androidx.compose.ui.graphics.drawscope.Fill
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.drawText
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.melendez.caelum.R
import com.melendez.caelum.data.entity.ExamScore
import com.melendez.caelum.util.percentage
import com.melendez.caelum.util.settings.subjectKeyToStringResource
import kotlin.math.cos
import kotlin.math.sin

private const val GRID_LEVELS = 4
private const val START_ANGLE_DEGREES = -90.0

/**
 * A radar chart showing each subject's mark rate as one spoke, used in place of the mark
 * distribution pie chart on the detail screen.
 *
 * @param scores List of exam scores for each subject.
 * @param modifier Modifier for the chart.
 */
@Composable
fun ScoreRadarChart(
    scores: List<ExamScore>,
    modifier: Modifier = Modifier
) {
    if (scores.isEmpty()) {
        EmptyChartPlaceholder(
            modifier = modifier,
            text = stringResource(R.string.no_score_data)
        )
        return
    }

    // String resources and percentages must be resolved in the composable context, before the
    // Canvas draw scope takes over.
    val labels = scores.map { stringResource(subjectKeyToStringResource(it.subjectKey)) }
    val fractions = scores.map { percentage(it.mark, it.fullMark) / 100f }

    val primaryColor = MaterialTheme.colorScheme.primary
    val gridColor = MaterialTheme.colorScheme.outlineVariant
    val labelColor = MaterialTheme.colorScheme.onSurfaceVariant
    val textMeasurer = rememberTextMeasurer()
    val labelStyle = TextStyle(fontSize = 10.sp, color = labelColor)

    Canvas(
        modifier = modifier
            .fillMaxWidth()
            .height(280.dp)
            .padding(horizontal = 12.dp, vertical = 8.dp)
    ) {
        val center = Offset(size.width / 2f, size.height / 2f)
        val radius = (minOf(size.width, size.height) / 2f - 48.dp.toPx()).coerceAtLeast(1f)
        val angleStep = 360.0 / scores.size

        fun pointAt(index: Int, fraction: Float): Offset {
            val angleRad = Math.toRadians(START_ANGLE_DEGREES + index * angleStep)
            return Offset(
                x = center.x + radius * fraction * cos(angleRad).toFloat(),
                y = center.y + radius * fraction * sin(angleRad).toFloat()
            )
        }

        // Concentric grid polygons and one radial spoke per subject
        for (level in 1..GRID_LEVELS) {
            val fraction = level / GRID_LEVELS.toFloat()
            drawPolygon(
                points = List(scores.size) { index -> pointAt(index, fraction) },
                color = gridColor,
                style = Stroke(width = 1.dp.toPx())
            )
        }
        for (index in scores.indices) {
            drawLine(
                color = gridColor,
                start = center,
                end = pointAt(index, 1f),
                strokeWidth = 1.dp.toPx()
            )
        }

        // The data polygon: translucent fill, solid outline and a dot on every vertex
        val dataPoints = List(scores.size) { index -> pointAt(index, fractions[index]) }
        drawPolygon(points = dataPoints, color = primaryColor.copy(alpha = 0.25f), style = Fill)
        drawPolygon(
            points = dataPoints,
            color = primaryColor,
            style = Stroke(width = 2.dp.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round)
        )
        dataPoints.forEach { point ->
            drawCircle(color = primaryColor, radius = 4.dp.toPx(), center = point)
        }

        // Subject labels just outside the outer ring, aligned towards their own spoke
        for (index in scores.indices) {
            val angleRad = Math.toRadians(START_ANGLE_DEGREES + index * angleStep)
            val labelPoint = pointAt(index, 1f + 12.dp.toPx() / radius)
            val textLayout = textMeasurer.measure(labels[index], labelStyle)
            val textWidth = textLayout.size.width.toFloat()
            val textHeight = textLayout.size.height.toFloat()
            val horizontalDirection = cos(angleRad)
            val x = when {
                horizontalDirection > 0.1 -> labelPoint.x
                horizontalDirection < -0.1 -> labelPoint.x - textWidth
                else -> labelPoint.x - textWidth / 2f
            }
            drawText(textLayout, topLeft = Offset(x, labelPoint.y - textHeight / 2f))
        }
    }
}

/** Draws a closed polygon through [points] with the supplied [style]. */
private fun DrawScope.drawPolygon(
    points: List<Offset>,
    color: Color,
    style: DrawStyle
) {
    if (points.isEmpty()) return
    val path = Path().apply {
        moveTo(points.first().x, points.first().y)
        points.drop(1).forEach { lineTo(it.x, it.y) }
        close()
    }
    drawPath(path = path, color = color, style = style)
}
