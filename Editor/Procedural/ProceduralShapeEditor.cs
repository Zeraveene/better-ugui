using Zeraveene.Shared.Inspector;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProceduralShape))]
[CanEditMultipleObjects]
public sealed class ProceduralShapeEditor : InspectorAttributeEditor
{
    private void OnSceneGUI()
    {
        SceneView sceneView = SceneView.currentDrawingSceneView;
        if (sceneView == null || !sceneView.drawGizmos) return;

        foreach (Object item in targets)
        {
            var shape = item as ProceduralShape;
            if (shape == null || !shape.raycastTarget || !EditorGUIUtility.IsGizmosAllowedForObject(shape)) continue;

            RectTransform rectTransform = shape.rectTransform;
            Rect rect = rectTransform.rect;
            // uGUI padding is Left, Bottom, Right, Top. Positive values inset the hit area.
            Vector4 padding = shape.raycastPadding;
            Vector3 bottomLeft = rectTransform.TransformPoint(new Vector3(rect.xMin + padding.x, rect.yMin + padding.y));
            Vector3 topLeft = rectTransform.TransformPoint(new Vector3(rect.xMin + padding.x, rect.yMax - padding.w));
            Vector3 topRight = rectTransform.TransformPoint(new Vector3(rect.xMax - padding.z, rect.yMax - padding.w));
            Vector3 bottomRight = rectTransform.TransformPoint(new Vector3(rect.xMax - padding.z, rect.yMin + padding.y));
            using (new Handles.DrawingScope(Handles.UIColliderHandleColor))
            {
                Handles.DrawLine(bottomLeft, topLeft);
                Handles.DrawLine(topLeft, topRight);
                Handles.DrawLine(topRight, bottomRight);
                Handles.DrawLine(bottomRight, bottomLeft);
            }
        }
    }

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
