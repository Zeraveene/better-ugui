using Zeraveene.Shared.Inspector;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProceduralShape))]
public sealed class ProceduralShapeEditor : InspectorAttributeEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.Space();
        if (GUILayout.Button("Add Outline"))
            ProceduralShapeEditorUtility.AddOutline((ProceduralShape)target);
    }
}
