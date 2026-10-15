using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIGradientPreset))]
[CanEditMultipleObjects]
public sealed class UIGradientPresetEditor : Editor
{
    #region Inspector
    private SerializedProperty _settings;
    #endregion

    #region Initialization
    private void OnEnable()
    {
        _settings = serializedObject.FindProperty("_settings");
    }
    #endregion

    #region Override
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_settings, GUIContent.none, includeChildren: true);
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Reverse Direction"))
            ReverseTargets();

        if (changed)
            RepaintViews();
    }
    #endregion

    #region Private Methods
    private void ReverseTargets()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not UIGradientPreset preset)
                continue;

            Undo.RecordObject(preset, "Reverse UI Gradient Preset");
            preset.Settings.ReverseDirection();
            EditorUtility.SetDirty(preset);
        }

        RepaintViews();
    }

    private static void RepaintViews()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
    #endregion
}
