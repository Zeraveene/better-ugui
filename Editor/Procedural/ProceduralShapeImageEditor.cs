using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(ProceduralShapeImage))]
public sealed class ProceduralShapeImageEditor : ImageEditor
{
    private const int PointControlHash = 0x6381A0;
    private const int AddControlHash = 0x6381A1;

    private int _selectedIndex = -1;

    private static readonly string[] ProceduralProperties =
    {
        "_points", "_uniformRadius", "_cornerRadius", "_cornerSegments", "_snapPointsToPixels",
        "_borderWidth", "_falloffDistance", "_measurementMode", "_sliceHorizontal", "_horizontalSliceMode",
        "_horizontalCenterPixels", "_sliceVertical", "_sliceReferenceSize", "_sliceLeftPixels",
        "_sliceRightPixels", "_sliceBottomPixels", "_sliceTopPixels", "_showPointGizmos", "_pointGizmoColor"
    };

    public override void OnInspectorGUI()
    {
        ProceduralShapeImage shape = (ProceduralShapeImage)target;
        base.OnInspectorGUI();
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Procedural Geometry", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scene view: drag a numbered point to move it. Shift-click inside the RectTransform to insert a point on the nearest edge. Delete or Backspace removes the selected point.",
            MessageType.Info);
        DrawTemplate(shape);
        for (int i = 0; i < ProceduralProperties.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(ProceduralProperties[i]);
            if (property == null || !ShouldShow(property.name, shape)) continue;
            EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Flip Horizontal"))
            {
                Undo.RecordObject(shape, "Flip Procedural Shape Image Horizontal");
                shape.FlipHorizontal();
                RecordChange(shape);
            }
            if (GUILayout.Button("Flip Vertical"))
            {
                Undo.RecordObject(shape, "Flip Procedural Shape Image Vertical");
                shape.FlipVertical();
                RecordChange(shape);
            }
        }

        if ((shape.SliceHorizontal || shape.SliceVertical) && GUILayout.Button("Capture Current Rect As Slice Reference"))
        {
            Undo.RecordObject(shape, "Capture Procedural Shape Image Slice Reference");
            shape.CaptureSliceReferenceSize();
            RecordChange(shape);
        }

        if (GUILayout.Button("Add Outline"))
            ProceduralShapeEditorUtility.AddOutline(shape);

        if (shape.type != UnityEngine.UI.Image.Type.Simple && shape.type != UnityEngine.UI.Image.Type.Filled)
            EditorGUILayout.HelpBox("Procedural Shape Image supports only Simple and Filled. Other Image types are normalized to Simple.", MessageType.Warning);
    }

    private static bool ShouldShow(string propertyName, ProceduralShapeImage shape)
    {
        if (propertyName == "_cornerRadius") return shape.UniformRadius;
        if (propertyName == "_horizontalSliceMode" || propertyName == "_horizontalCenterPixels" ||
            propertyName == "_sliceLeftPixels" || propertyName == "_sliceRightPixels") return shape.SliceHorizontal;
        if (propertyName == "_sliceBottomPixels" || propertyName == "_sliceTopPixels") return shape.SliceVertical;
        if (propertyName == "_sliceReferenceSize") return shape.SliceHorizontal || shape.SliceVertical;
        return true;
    }

    private static void DrawTemplate(ProceduralShapeImage shape)
    {
        EditorGUI.BeginChangeCheck();
        ProceduralShapeTemplate template = (ProceduralShapeTemplate)EditorGUILayout.ObjectField(
            "Point Template", shape.PointTemplate, typeof(ProceduralShapeTemplate), false);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(shape, "Change Procedural Shape Image Template");
        shape.SetPointTemplate(template);
        RecordChange(shape);
    }

    private void OnSceneGUI()
    {
        ProceduralShapeImage shape = (ProceduralShapeImage)target;
        if (!shape.ShowPointGizmos || shape.Points.Count == 0) return;
        RectTransform rectTransform = shape.rectTransform;
        Event currentEvent = Event.current;
        Color previous = Handles.color;
        Handles.color = shape.PointGizmoColor;
        Vector3[] edge = new Vector3[shape.Points.Count + 1];
        for (int i = 0; i < shape.Points.Count; i++)
            edge[i] = rectTransform.TransformPoint(shape.NormalizedToLocalPoint(shape.Points[i].Position));
        edge[edge.Length - 1] = edge[0];
        Handles.DrawAAPolyLine(3f, edge);

        HandleDelete(shape, currentEvent);
        if (HandleAdd(shape, rectTransform, currentEvent))
        {
            Handles.color = previous;
            return;
        }

        for (int i = 0; i < shape.Points.Count; i++)
            DrawAndHandlePoint(shape, rectTransform, i, currentEvent);
        Handles.color = previous;
    }

    private void DrawAndHandlePoint(
        ProceduralShapeImage shape,
        RectTransform rectTransform,
        int index,
        Event currentEvent)
    {
        Vector3 world = rectTransform.TransformPoint(shape.NormalizedToLocalPoint(shape.Points[index].Position));
        float size = HandleUtility.GetHandleSize(world) * 0.07f;
        int controlId = GUIUtility.GetControlID(PointControlHash + index, FocusType.Passive);

        switch (currentEvent.GetTypeForControl(controlId))
        {
            case EventType.Layout:
                HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(world, size));
                break;

            case EventType.MouseMove:
                if (HandleUtility.nearestControl == controlId) HandleUtility.Repaint();
                break;

            case EventType.MouseDown:
                if (!currentEvent.alt && !currentEvent.shift && currentEvent.button == 0 &&
                    HandleUtility.nearestControl == controlId)
                {
                    _selectedIndex = index;
                    GUIUtility.hotControl = controlId;
                    GUIUtility.keyboardControl = 0;
                    Undo.RecordObject(shape, "Move Procedural Shape Image Point");
                    currentEvent.Use();
                    Repaint();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId &&
                    TryGetNormalizedPoint(shape, rectTransform, currentEvent.mousePosition, false, out Vector2 normalized))
                {
                    shape.SetPoint(index, normalized);
                    RecordChange(shape);
                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId && currentEvent.button == 0)
                {
                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                }
                break;

            case EventType.Repaint:
                Color previous = Handles.color;
                if (_selectedIndex == index) Handles.color = Handles.selectedColor;
                else if (HandleUtility.nearestControl == controlId) Handles.color = Handles.preselectionColor;
                else Handles.color = shape.PointGizmoColor;
                Handles.DotHandleCap(controlId, world, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(world + rectTransform.up * size * 1.5f, (index + 1).ToString());
                Handles.color = previous;
                break;
        }
    }

    private bool HandleAdd(
        ProceduralShapeImage shape,
        RectTransform rectTransform,
        Event currentEvent)
    {
        if (!currentEvent.shift || currentEvent.alt ||
            !TryGetNormalizedPoint(shape, rectTransform, currentEvent.mousePosition, true, out Vector2 normalized))
            return false;

        int addControlId = GUIUtility.GetControlID(AddControlHash, FocusType.Passive);
        if (currentEvent.type == EventType.Layout) HandleUtility.AddDefaultControl(addControlId);
        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0) return false;

        int insertionIndex = FindNearestEdge(shape, normalized) + 1;
        Undo.RecordObject(shape, "Add Procedural Shape Image Point");
        shape.InsertPoint(insertionIndex, new ProceduralShapePoint(normalized));
        _selectedIndex = insertionIndex;
        RecordChange(shape);
        currentEvent.Use();
        Repaint();
        return true;
    }

    private void HandleDelete(ProceduralShapeImage shape, Event currentEvent)
    {
        if (currentEvent.type != EventType.KeyDown ||
            (currentEvent.keyCode != KeyCode.Delete && currentEvent.keyCode != KeyCode.Backspace) ||
            _selectedIndex < 0 || _selectedIndex >= shape.Points.Count)
            return;

        if (shape.Points.Count <= 3)
        {
            SceneView.currentDrawingSceneView?.ShowNotification(new GUIContent("A shape requires at least 3 points."));
            currentEvent.Use();
            return;
        }

        Undo.RecordObject(shape, "Remove Procedural Shape Image Point");
        shape.RemovePoint(_selectedIndex);
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, shape.Points.Count - 1);
        RecordChange(shape);
        currentEvent.Use();
        Repaint();
    }

    private static int FindNearestEdge(ProceduralShapeImage shape, Vector2 normalized)
    {
        Vector2 point = shape.NormalizedToLocalPoint(normalized);
        int nearest = 0;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < shape.Points.Count; i++)
        {
            Vector2 a = shape.NormalizedToLocalPoint(shape.Points[i].Position);
            Vector2 b = shape.NormalizedToLocalPoint(shape.Points[(i + 1) % shape.Points.Count].Position);
            float distance = DistanceToSegmentSquared(point, a, b);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = i;
        }
        return nearest;
    }

    private static bool TryGetNormalizedPoint(
        ProceduralShapeImage shape,
        RectTransform rectTransform,
        Vector2 guiPoint,
        bool requireInsideRect,
        out Vector2 normalized)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
        Plane plane = new Plane(rectTransform.forward, rectTransform.position);
        if (!plane.Raycast(ray, out float distance))
        {
            normalized = default;
            return false;
        }

        Vector3 local3 = rectTransform.InverseTransformPoint(ray.GetPoint(distance));
        Vector2 local = new Vector2(local3.x, local3.y);
        if (requireInsideRect && !rectTransform.rect.Contains(local))
        {
            normalized = default;
            return false;
        }

        normalized = shape.LocalToNormalizedPoint(local);
        return true;
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        if (edge.sqrMagnitude < 0.0000001f) return (point - a).sqrMagnitude;
        float interpolation = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
        return (point - (a + edge * interpolation)).sqrMagnitude;
    }

    private static void RecordChange(ProceduralShapeImage shape)
    {
        EditorUtility.SetDirty(shape);
        PrefabUtility.RecordPrefabInstancePropertyModifications(shape);
        SceneView.RepaintAll();
    }
}
