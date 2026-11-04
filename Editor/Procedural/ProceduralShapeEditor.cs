using Zeraveene.Shared.Inspector;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProceduralShape))]
[CanEditMultipleObjects]
public sealed class ProceduralShapeEditor : InspectorAttributeEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.Space();
        if (GUILayout.Button("Add Outline"))
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Procedural Shape Outlines");
            var outlines = new System.Collections.Generic.List<Object>();
            foreach (Object item in targets)
            {
                ProceduralShapeOutline outline = ProceduralShapeEditorUtility.AddOutline((ProceduralShape)item);
                if (outline != null) outlines.Add(outline.gameObject);
            }
            Undo.CollapseUndoOperations(group);
            Selection.objects = outlines.ToArray();
        }
    }
}
