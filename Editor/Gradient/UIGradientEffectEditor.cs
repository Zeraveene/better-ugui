using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIGradientEffect))]
[CanEditMultipleObjects]
public sealed class UIGradientEffectEditor : Editor
{
    #region Inspector
    private SerializedProperty _source;
    private SerializedProperty _preset;
    private SerializedProperty _blendMode;
    private SerializedProperty _includeAlpha;
    private SerializedProperty _localSettings;
    #endregion

    #region Initialization
    private void OnEnable()
    {
        _source = serializedObject.FindProperty("_source");
        _preset = serializedObject.FindProperty("_preset");
        _blendMode = serializedObject.FindProperty("_blendMode");
        _includeAlpha = serializedObject.FindProperty("_includeAlpha");
        _localSettings = serializedObject.FindProperty("_localSettings");
    }
    #endregion

    #region Override
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(_source);
        DrawSourceSettings();
        EditorGUILayout.PropertyField(_blendMode);
        EditorGUILayout.PropertyField(_includeAlpha);

        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawActions();

        if (changed)
            RefreshTargets();
    }
    #endregion

    #region Private Methods
    private void DrawSourceSettings()
    {
        if (_source.hasMultipleDifferentValues)
            return;

        if ((UIGradientSource)_source.enumValueIndex == UIGradientSource.Preset)
        {
            EditorGUILayout.PropertyField(_preset);
            if (_preset.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Assign a preset, or switch Source to Local.", MessageType.Info);

            using (new EditorGUI.DisabledScope(_preset.objectReferenceValue == null))
            {
                if (GUILayout.Button("Edit From This Preset"))
                    EditFromPreset();
            }
        }
        else
        {
            EditorGUILayout.PropertyField(_localSettings, GUIContent.none, includeChildren: true);
        }
    }

    private void DrawActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh"))
                RefreshTargets();

            if (GUILayout.Button("Reverse Direction"))
                ReverseTargets();
        }

        using (new EditorGUI.DisabledScope(targets.Length != 1))
        {
            if (GUILayout.Button("Create Preset From Active Settings"))
                CreatePresetFromTarget();
        }
    }

    private void RefreshTargets()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is UIGradientEffect effect)
                effect.Refresh();
        }
    }

    private void EditFromPreset()
    {
        serializedObject.ApplyModifiedProperties();
        Undo.RecordObjects(targets, "Edit UI Gradient From Preset");

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not UIGradientEffect effect || effect.Preset == null)
                continue;

            effect.EditFromPreset();
            EditorUtility.SetDirty(effect);
        }

        serializedObject.Update();
    }

    private void ReverseTargets()
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not UIGradientEffect effect)
                continue;

            Object undoTarget = effect.Source == UIGradientSource.Preset && effect.Preset != null
                ? effect.Preset
                : effect;
            Undo.RecordObject(undoTarget, "Reverse UI Gradient");
            effect.ReverseDirection();
            EditorUtility.SetDirty(undoTarget);
        }
    }

    private void CreatePresetFromTarget()
    {
        if (target is not UIGradientEffect effect)
            return;

        string path = EditorUtility.SaveFilePanelInProject(
            "Create UI Gradient Preset",
            effect.gameObject.name + "Gradient",
            "asset",
            "Choose where to save the reusable gradient preset.",
            "Assets");

        if (string.IsNullOrEmpty(path))
            return;

        var preset = CreateInstance<UIGradientPreset>();
        preset.SetSettings(effect.ActiveSettings);
        AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
        Undo.RegisterCreatedObjectUndo(preset, "Create UI Gradient Preset");
        AssetDatabase.SaveAssets();

        Undo.RecordObject(effect, "Assign UI Gradient Preset");
        effect.SetPreset(preset);
        EditorUtility.SetDirty(effect);
        if (PrefabUtility.IsPartOfPrefabInstance(effect))
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
        Selection.activeObject = preset;
    }

    #endregion
}
