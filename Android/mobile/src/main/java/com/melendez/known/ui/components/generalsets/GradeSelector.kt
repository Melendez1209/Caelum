package com.melendez.known.ui.components.generalsets

import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuAnchorType
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import com.melendez.known.R
import com.melendez.known.util.llm.StudentGrade

/** Dropdown for the school year, shared by onboarding, general settings and the assistant settings. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GradeSelector(
    selected: Int,
    onSelect: (Int) -> Unit,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }
    val label = if (selected == StudentGrade.NONE) {
        stringResource(R.string.grade_none)
    } else {
        stringResource(StudentGrade.label(selected))
    }

    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = label,
            onValueChange = {},
            readOnly = true,
            label = { Text(text = stringResource(R.string.grade)) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            modifier = modifier
                .menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable, enabled = true)
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            (listOf(StudentGrade.NONE) + StudentGrade.ALL).forEach { grade ->
                DropdownMenuItem(
                    text = {
                        Text(
                            text = if (grade == StudentGrade.NONE) {
                                stringResource(R.string.grade_none)
                            } else {
                                stringResource(StudentGrade.label(grade))
                            }
                        )
                    },
                    onClick = {
                        onSelect(grade)
                        expanded = false
                    }
                )
            }
        }
    }
}
