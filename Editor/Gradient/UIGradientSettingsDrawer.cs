using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(UIGradientSettings))]
public sealed class UIGradientSettingsDrawer : PropertyDrawer
{
    #region Override
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        bool showHeader = label != null && !string.IsNullOrEmpty(label.text);
        if (showHeader)
        {
            property.isExpanded = EditorGUI.Foldout(
                NextLine(ref position),
                property.isExpanded,
                label,
                toggleOnLabelClick: true);
        }
        else
        {
            property.isExpanded = true;
        }

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            DrawProperty(ref position, property, "_gradientType");
            DrawProperty(ref position, property, "_intensity");

            SerializedProperty gradientType = property.FindPropertyRelative("_gradientType");
            UIGradientType type = (UIGradientType)gradientType.enumValueIndex;
            if (type != UIGradientType.SingleColor)
                DrawProperty(ref position, property, "_angle");

            switch (type)
            {
                case UIGradientType.SingleColor:
                    DrawProperty(ref position, property, "_singleColor", "Color");
                    break;

                case UIGradientType.Linear:
                    DrawProperty(ref position, property, "_startColor", "Start");
                    DrawProperty(ref position, property, "_endColor", "End");
                    break;

                case UIGradientType.Corner:
                    DrawProperty(ref position, property, "_upperLeftColor", "Upper Left");
                    DrawProperty(ref position, property, "_upperRightColor", "Upper Right");
                    DrawProperty(ref position, property, "_lowerRightColor", "Lower Right");
                    DrawProperty(ref position, property, "_lowerLeftColor", "Lower Left");
                    break;

                case UIGradientType.MultiStop:
                    DrawProperty(ref position, property, "_gradient", "Gradient");
                    break;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool showHeader = label != null && !string.IsNullOrEmpty(label.text);
        if (showHeader && !property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        SerializedProperty gradientType = property.FindPropertyRelative("_gradientType");
        int modeLines = (UIGradientType)gradientType.enumValueIndex switch
        {
            UIGradientType.SingleColor => 1,
            UIGradientType.Linear => 2,
            UIGradientType.Corner => 4,
            UIGradientType.MultiStop => 1,
            _ => 0
        };

        bool showAngle = (UIGradientType)gradientType.enumValueIndex != UIGradientType.SingleColor;
        int lineCount = 2 + (showAngle ? 1 : 0) + modeLines + (showHeader ? 1 : 0);
        return lineCount * EditorGUIUtility.singleLineHeight
            + (lineCount - 1) * EditorGUIUtility.standardVerticalSpacing;
    }
    #endregion

    #region Private Methods
    private static void DrawProperty(
        ref Rect position,
        SerializedProperty parent,
        string relativeName,
        string label = null)
    {
        SerializedProperty child = parent.FindPropertyRelative(relativeName);
        GUIContent childLabel = label == null
            ? new GUIContent(child.displayName)
            : new GUIContent(label);
        EditorGUI.PropertyField(
            NextLine(ref position),
            child,
            childLabel);
    }

    private static Rect NextLine(ref Rect position)
    {
        var line = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return line;
    }
    #endregion
}
