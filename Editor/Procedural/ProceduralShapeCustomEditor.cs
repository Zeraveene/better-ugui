using System.Collections.Generic;
using Zeraveene.Shared.Inspector;
using UnityEditor;
using UnityEngine;

public enum ProceduralShapePointTemplate
{
    Rectangle,
    Triangle,
    Trapezoid
}

internal static class ProceduralShapePointTemplates
{
    internal static void Apply(ProceduralShapeCustom shape, ProceduralShapePointTemplate template)
    {
        switch (template)
        {
            case ProceduralShapePointTemplate.Rectangle:
                shape.SetPoints(new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                });
                break;

            case ProceduralShapePointTemplate.Triangle:
                shape.SetPoints(new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0.5f, 1f),
                    new Vector2(1f, 0f)
                });
                break;

            case ProceduralShapePointTemplate.Trapezoid:
                shape.SetPoints(new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0.2f, 1f),
                    new Vector2(0.8f, 1f),
                    new Vector2(1f, 0f)
                });
                break;
        }
    }
}

[CustomEditor(typeof(ProceduralShapeCustom))]
public sealed class ProceduralShapeCustomEditor : InspectorAttributeEditor
{
    private const int PointControlHash = 0x5381A0;
    private const int AddControlHash = 0x5381A1;

    private static readonly FoldoutGroupAttribute BasicSettingsGroup = new FoldoutGroupAttribute("Basic Settings", false);
    private static readonly HashSet<string> BasicSettingMembers = new HashSet<string>
    {
        "m_Material",
        "m_Color",
        "m_RaycastTarget",
        "m_RaycastPadding",
        "m_Maskable",
        "m_OnCullStateChanged",
        "_renderType",
        "_fillMethod",
        "_fillAmount",
        "_fillOriginHorizontal",
        "_fillOriginVertical",
        "_fillOrigin90",
        "_fillOrigin180",
        "_fillOrigin360",
        "_fillClockwise"
    };

    private int _selectedIndex = -1;
    private bool _templateExpanded;

    protected override GroupAttribute GetGroupOverride(string propertyPath) =>
        BasicSettingMembers.Contains(propertyPath) ? BasicSettingsGroup : null;

    public override void OnInspectorGUI()
    {
        ProceduralShapeCustom shape = (ProceduralShapeCustom)target;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _templateExpanded = EditorGUILayout.Foldout(_templateExpanded, "Shape Template", true);
        if (_templateExpanded)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                DrawTemplateSource(shape);
                DrawPointTemplates(shape);
            }
        }
        EditorGUILayout.EndVertical();

        int pointsHashBefore = GetPointsHash(shape.Points);
        base.OnInspectorGUI();

        if (_selectedIndex >= shape.Points.Count)
            _selectedIndex = shape.Points.Count - 1;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Scene view: click and drag a numbered point to move it. Shift-click inside the RectTransform to insert a point on the nearest edge. Delete or Backspace removes the selected point.",
            MessageType.Info);

        if (GUILayout.Button("Add Outline"))
            ProceduralShapeEditorUtility.AddOutline(shape);

        if (shape.SliceHorizontal || shape.SliceVertical)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Slice Editing", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Reference Size", shape.SliceReferenceSize.ToString("0.##"));
            if (GUILayout.Button("Capture Current Rect As Slice Reference"))
            {
                Undo.RecordObject(shape, "Capture Procedural Shape Slice Reference");
                shape.CaptureSliceReferenceSize();
                RecordPrefabChange(shape);
                SceneView.RepaintAll();
            }
        }

        if (_selectedIndex < 0 || _selectedIndex >= shape.Points.Count)
        {
            MarkPointDraftIfChanged(shape, pointsHashBefore);
            return;
        }

        serializedObject.Update();
        SerializedProperty points = serializedObject.FindProperty("_points");
        SerializedProperty point = points.GetArrayElementAtIndex(_selectedIndex);
        SerializedProperty position = point.FindPropertyRelative("_position");
        SerializedProperty radius = point.FindPropertyRelative("_radius");
        SerializedProperty uniformRadius = serializedObject.FindProperty("_uniformRadius");

        EditorGUILayout.LabelField($"Selected Point {_selectedIndex + 1}", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Vector2 newPosition = EditorGUILayout.Vector2Field("Normalized Position", position.vector2Value);
        using (new EditorGUI.DisabledScope(uniformRadius.boolValue))
            radius.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("Radius", radius.floatValue));

        if (uniformRadius.boolValue)
            EditorGUILayout.HelpBox("Disable Uniform Radius to edit this point's radius.", MessageType.None);

        if (EditorGUI.EndChangeCheck())
        {
            position.vector2Value = new Vector2(Mathf.Clamp01(newPosition.x), Mathf.Clamp01(newPosition.y));
            serializedObject.ApplyModifiedProperties();
            RecordPrefabChange(shape);
            SceneView.RepaintAll();
        }

        using (new EditorGUI.DisabledScope(shape.Points.Count <= 3))
        {
            if (GUILayout.Button("Remove Selected Point"))
                RemoveSelectedPoint(shape);
        }

        MarkPointDraftIfChanged(shape, pointsHashBefore);
    }

    private void DrawTemplateSource(ProceduralShapeCustom shape)
    {
        EditorGUI.BeginChangeCheck();
        ProceduralShapeTemplate pointTemplate = (ProceduralShapeTemplate)EditorGUILayout.ObjectField(
            "Template",
            shape.PointTemplate,
            typeof(ProceduralShapeTemplate),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(shape, "Change Procedural Shape Template");
            shape.SetPointTemplate(pointTemplate);
            _selectedIndex = -1;
            RecordPrefabChange(shape);
            SceneView.RepaintAll();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save This Shape As Template"))
                SaveShapeAsTemplate(shape);

            using (new EditorGUI.DisabledScope(shape.PointTemplate == null || !shape.HasPointOverrides))
            {
                if (GUILayout.Button("Update Template"))
                    UpdateTemplate(shape);
            }
        }

        using (new EditorGUI.DisabledScope(shape.PointTemplate == null || !shape.HasPointOverrides))
        {
            if (GUILayout.Button("Reset to Original"))
                ResetToTemplate(shape);
        }

        if (shape.PointTemplate != null)
        {
            string message = shape.HasPointOverrides
                ? "This component has a local point draft. Update the template to commit it, or reset to discard it."
                : "Points are synchronized with the template. The first point edit creates a local draft.";
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        EditorGUILayout.Space();
    }

    private void DrawPointTemplates(ProceduralShapeCustom shape)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Point Templates", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rectangle"))
                ApplyPointTemplate(shape, ProceduralShapePointTemplate.Rectangle);
            if (GUILayout.Button("Triangle"))
                ApplyPointTemplate(shape, ProceduralShapePointTemplate.Triangle);
            if (GUILayout.Button("Trapezoid"))
                ApplyPointTemplate(shape, ProceduralShapePointTemplate.Trapezoid);
        }
    }

    private void ApplyPointTemplate(ProceduralShapeCustom shape, ProceduralShapePointTemplate template)
    {
        Undo.RecordObject(shape, $"Apply {template} Point Template");
        ProceduralShapePointTemplates.Apply(shape, template);
        _selectedIndex = -1;
        RecordPrefabChange(shape);
        Repaint();
        SceneView.RepaintAll();
    }

    private static void SaveShapeAsTemplate(ProceduralShapeCustom shape)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Procedural Shape Template",
            shape.gameObject.name + "ShapeTemplate",
            "asset",
            "Choose where to save the reusable procedural shape template.",
            "Assets");
        if (string.IsNullOrEmpty(path))
            return;

        var pointTemplate = CreateInstance<ProceduralShapeTemplate>();
        pointTemplate.SetPoints(GetPositions(shape.Points));
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        AssetDatabase.CreateAsset(pointTemplate, path);
        Undo.RegisterCreatedObjectUndo(pointTemplate, "Create Procedural Shape Template");

        Undo.RecordObject(shape, "Assign Procedural Shape Template");
        shape.SetPointTemplate(pointTemplate);
        RecordPrefabChange(shape);
        EditorUtility.SetDirty(pointTemplate);
        AssetDatabase.SaveAssets();
        Selection.activeObject = pointTemplate;
        SceneView.RepaintAll();
    }

    private static void UpdateTemplate(ProceduralShapeCustom shape)
    {
        ProceduralShapeTemplate pointTemplate = shape.PointTemplate;
        if (pointTemplate == null || !shape.HasPointOverrides)
            return;

        Undo.RecordObjects(new Object[] { shape, pointTemplate }, "Update Procedural Shape Template");
        shape.UpdatePointTemplate();
        EditorUtility.SetDirty(pointTemplate);
        RecordPrefabChange(shape);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
    }

    private static void ResetToTemplate(ProceduralShapeCustom shape)
    {
        if (shape.PointTemplate == null || !shape.HasPointOverrides)
            return;

        Undo.RecordObject(shape, "Reset Procedural Shape Points From Template");
        shape.ResetPointsFromTemplate();
        RecordPrefabChange(shape);
        SceneView.RepaintAll();
    }

    private static List<Vector2> GetPositions(IReadOnlyList<ProceduralShapePoint> points)
    {
        var positions = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
            positions.Add(points[i].Position);
        return positions;
    }

    private static int GetPointsHash(IReadOnlyList<ProceduralShapePoint> points)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                hash = hash * 31 + points[i].Position.GetHashCode();
                hash = hash * 31 + points[i].Radius.GetHashCode();
            }
            return hash;
        }
    }

    private static void MarkPointDraftIfChanged(ProceduralShapeCustom shape, int previousHash)
    {
        if (shape.PointTemplate == null || shape.HasPointOverrides || previousHash == GetPointsHash(shape.Points))
            return;
        shape.MarkPointOverrides();
        RecordPrefabChange(shape);
    }

    private void OnSceneGUI()
    {
        ProceduralShapeCustom shape = (ProceduralShapeCustom)target;
        serializedObject.Update();
        if (!shape.ShowPointGizmos || shape.Points.Count == 0)
            return;

        Color pointColor = shape.PointGizmoColor;
        RectTransform rectTransform = shape.rectTransform;
        Event currentEvent = Event.current;

        DrawEdges(shape, rectTransform, pointColor);
        DrawSliceGuides(shape, rectTransform);
        HandleDelete(shape, currentEvent);
        if (HandleAdd(shape, rectTransform, currentEvent))
            return;

        for (int i = 0; i < shape.Points.Count; i++)
            DrawAndHandlePoint(shape, rectTransform, i, pointColor, currentEvent);
    }

    private void DrawEdges(ProceduralShapeCustom shape, RectTransform rectTransform, Color color)
    {
        if (Event.current.type != EventType.Repaint || shape.Points.Count < 2)
            return;

        Color previous = Handles.color;
        Color edgeColor = color;
        edgeColor.a *= 0.75f;
        Handles.color = edgeColor;
        Vector3[] line = new Vector3[shape.Points.Count + 1];
        for (int i = 0; i < shape.Points.Count; i++)
            line[i] = NormalizedToWorld(shape, rectTransform, shape.Points[i].Position);
        line[line.Length - 1] = line[0];
        Handles.DrawAAPolyLine(3f, line);
        Handles.color = previous;
    }

    private void DrawAndHandlePoint(
        ProceduralShapeCustom shape,
        RectTransform rectTransform,
        int index,
        Color pointColor,
        Event currentEvent)
    {
        Vector3 world = NormalizedToWorld(shape, rectTransform, shape.Points[index].Position);
        float size = HandleUtility.GetHandleSize(world) * 0.07f;
        int controlId = GUIUtility.GetControlID(PointControlHash + index, FocusType.Passive);

        switch (currentEvent.GetTypeForControl(controlId))
        {
            case EventType.Layout:
                HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(world, size));
                break;

            case EventType.MouseMove:
                if (HandleUtility.nearestControl == controlId)
                    HandleUtility.Repaint();
                break;

            case EventType.MouseDown:
                if (!currentEvent.alt && !currentEvent.shift && currentEvent.button == 0 &&
                    HandleUtility.nearestControl == controlId)
                {
                    _selectedIndex = index;
                    GUIUtility.hotControl = controlId;
                    GUIUtility.keyboardControl = 0;
                    Undo.RecordObject(shape, "Move Procedural Shape Point");
                    currentEvent.Use();
                    Repaint();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId && TryGetNormalizedPoint(shape, rectTransform, currentEvent.mousePosition, false, out Vector2 normalized))
                {
                    shape.SetPoint(index, normalized);
                    RecordPrefabChange(shape);
                    currentEvent.Use();
                    SceneView.RepaintAll();
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
                if (_selectedIndex == index)
                    Handles.color = Handles.selectedColor;
                else if (HandleUtility.nearestControl == controlId)
                    Handles.color = Handles.preselectionColor;
                else
                    Handles.color = pointColor;

                Handles.DotHandleCap(controlId, world, Quaternion.identity, size, EventType.Repaint);
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = Handles.color;
                Handles.Label(world + rectTransform.up * size * 1.5f, (index + 1).ToString(), style);
                Handles.color = previous;
                break;
        }
    }

    private void DrawSliceGuides(ProceduralShapeCustom shape, RectTransform rectTransform)
    {
        if (!shape.SliceHorizontal && !shape.SliceVertical)
            return;

        Rect rect = rectTransform.rect;
        Vector4 borders = shape.SliceBorders;
        Vector4 displayed = shape.GetDisplayedSliceBorders();
        Vector2 reference = shape.SliceReferenceSize;
        Color previous = Handles.color;
        Handles.color = new Color(1f, 0.72f, 0.12f, 0.95f);

        if (shape.SliceHorizontal)
        {
            if (shape.HorizontalSliceMode == ProceduralShapeHorizontalSliceMode.CenterBand)
            {
                float displayedCenter = shape.GetDisplayedHorizontalCenterPixels();
                float halfCenter = displayedCenter * 0.5f;
                DrawGuideLine(rectTransform, new Vector2(rect.center.x - halfCenter, rect.yMin), new Vector2(rect.center.x - halfCenter, rect.yMax));
                DrawGuideLine(rectTransform, new Vector2(rect.center.x + halfCenter, rect.yMin), new Vector2(rect.center.x + halfCenter, rect.yMax));

                if (!Event.current.shift)
                {
                    float centerPixels = DragCenterBandGuide(rectTransform, rect.center.x - halfCenter, rect.center.y, shape.HorizontalCenterPixels);
                    centerPixels = DragCenterBandGuide(rectTransform, rect.center.x + halfCenter, rect.center.y, centerPixels);
                    if (!Mathf.Approximately(centerPixels, shape.HorizontalCenterPixels))
                    {
                        Undo.RecordObject(shape, "Edit Procedural Shape Center Band");
                        shape.HorizontalCenterPixels = Mathf.Clamp(centerPixels, 0f, reference.x);
                        RecordPrefabChange(shape);
                        SceneView.RepaintAll();
                    }
                }
            }
            else
            {
                DrawGuideLine(rectTransform, new Vector2(rect.xMin + displayed.x, rect.yMin), new Vector2(rect.xMin + displayed.x, rect.yMax));
                DrawGuideLine(rectTransform, new Vector2(rect.xMax - displayed.z, rect.yMin), new Vector2(rect.xMax - displayed.z, rect.yMax));

                if (!Event.current.shift)
                {
                    float left = DragVerticalGuide(rectTransform, rect.xMin + displayed.x, rect.center.y, displayed.x, borders.x);
                    float right = DragVerticalGuide(rectTransform, rect.xMax - displayed.z, rect.center.y, displayed.z, borders.z, true);
                    if (!Mathf.Approximately(left, borders.x) || !Mathf.Approximately(right, borders.z))
                    {
                        Undo.RecordObject(shape, "Edit Procedural Shape Slice Borders");
                        borders.x = Mathf.Clamp(left, 0f, Mathf.Max(0f, reference.x - right));
                        borders.z = Mathf.Clamp(right, 0f, Mathf.Max(0f, reference.x - borders.x));
                        shape.SliceBorders = borders;
                        RecordPrefabChange(shape);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        if (shape.SliceVertical)
        {
            DrawGuideLine(rectTransform, new Vector2(rect.xMin, rect.yMin + displayed.y), new Vector2(rect.xMax, rect.yMin + displayed.y));
            DrawGuideLine(rectTransform, new Vector2(rect.xMin, rect.yMax - displayed.w), new Vector2(rect.xMax, rect.yMax - displayed.w));

            if (!Event.current.shift)
            {
                float bottom = DragHorizontalGuide(rectTransform, rect.center.x, rect.yMin + displayed.y, displayed.y, borders.y);
                float top = DragHorizontalGuide(rectTransform, rect.center.x, rect.yMax - displayed.w, displayed.w, borders.w, true);
                if (!Mathf.Approximately(bottom, borders.y) || !Mathf.Approximately(top, borders.w))
                {
                    Undo.RecordObject(shape, "Edit Procedural Shape Slice Borders");
                    borders.y = Mathf.Clamp(bottom, 0f, Mathf.Max(0f, reference.y - top));
                    borders.w = Mathf.Clamp(top, 0f, Mathf.Max(0f, reference.y - borders.y));
                    shape.SliceBorders = borders;
                    RecordPrefabChange(shape);
                    SceneView.RepaintAll();
                }
            }
        }

        Handles.color = previous;
    }

    private static void DrawGuideLine(RectTransform rectTransform, Vector2 a, Vector2 b)
    {
        Handles.DrawDottedLine(rectTransform.TransformPoint(a), rectTransform.TransformPoint(b), 4f);
    }

    private static float DragVerticalGuide(
        RectTransform rectTransform,
        float localX,
        float localY,
        float displayedBorder,
        float sourceBorder,
        bool measuredFromFarEdge = false)
    {
        Vector3 world = rectTransform.TransformPoint(new Vector3(localX, localY, 0f));
        float size = HandleUtility.GetHandleSize(world) * 0.08f;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(world, rectTransform.right, size, Handles.RectangleHandleCap, 0f);
        if (!EditorGUI.EndChangeCheck()) return sourceBorder;
        float movedX = rectTransform.InverseTransformPoint(moved).x;
        Rect rect = rectTransform.rect;
        float destinationBorder = measuredFromFarEdge ? rect.xMax - movedX : movedX - rect.xMin;
        return DestinationBorderToSource(destinationBorder, displayedBorder, sourceBorder);
    }

    private static float DragHorizontalGuide(
        RectTransform rectTransform,
        float localX,
        float localY,
        float displayedBorder,
        float sourceBorder,
        bool measuredFromFarEdge = false)
    {
        Vector3 world = rectTransform.TransformPoint(new Vector3(localX, localY, 0f));
        float size = HandleUtility.GetHandleSize(world) * 0.08f;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(world, rectTransform.up, size, Handles.RectangleHandleCap, 0f);
        if (!EditorGUI.EndChangeCheck()) return sourceBorder;
        float movedY = rectTransform.InverseTransformPoint(moved).y;
        Rect rect = rectTransform.rect;
        float destinationBorder = measuredFromFarEdge ? rect.yMax - movedY : movedY - rect.yMin;
        return DestinationBorderToSource(destinationBorder, displayedBorder, sourceBorder);
    }

    private static float DragCenterBandGuide(
        RectTransform rectTransform,
        float localX,
        float localY,
        float sourceCenterPixels)
    {
        Vector3 world = rectTransform.TransformPoint(new Vector3(localX, localY, 0f));
        float size = HandleUtility.GetHandleSize(world) * 0.08f;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(world, rectTransform.right, size, Handles.RectangleHandleCap, 0f);
        if (!EditorGUI.EndChangeCheck()) return sourceCenterPixels;
        float movedX = rectTransform.InverseTransformPoint(moved).x;
        return Mathf.Abs(movedX - rectTransform.rect.center.x) * 2f;
    }

    private static float DestinationBorderToSource(float destinationBorder, float displayedBorder, float sourceBorder)
    {
        float scale = sourceBorder > 0.0001f ? displayedBorder / sourceBorder : 1f;
        return Mathf.Max(0f, sourceBorder + (destinationBorder - displayedBorder) / Mathf.Max(scale, 0.0001f));
    }

    private bool HandleAdd(ProceduralShapeCustom shape, RectTransform rectTransform, Event currentEvent)
    {
        if (!currentEvent.shift || currentEvent.alt || !TryGetNormalizedPoint(shape, rectTransform, currentEvent.mousePosition, true, out Vector2 normalized))
            return false;

        int addControlId = GUIUtility.GetControlID(AddControlHash, FocusType.Passive);
        if (currentEvent.type == EventType.Layout)
            HandleUtility.AddDefaultControl(addControlId);

        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            return false;

        int edge = FindNearestEdge(shape, rectTransform, normalized);
        int insertionIndex = edge + 1;
        Undo.RecordObject(shape, "Add Procedural Shape Point");
        shape.InsertPoint(insertionIndex, new ProceduralShapePoint(normalized));
        _selectedIndex = insertionIndex;
        RecordPrefabChange(shape);
        currentEvent.Use();
        Repaint();
        SceneView.RepaintAll();
        return true;
    }

    private void HandleDelete(ProceduralShapeCustom shape, Event currentEvent)
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

        RemoveSelectedPoint(shape);
        currentEvent.Use();
    }

    private void RemoveSelectedPoint(ProceduralShapeCustom shape)
    {
        Undo.RecordObject(shape, "Remove Procedural Shape Point");
        shape.RemovePoint(_selectedIndex);
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, shape.Points.Count - 1);
        RecordPrefabChange(shape);
        Repaint();
        SceneView.RepaintAll();
    }

    private static int FindNearestEdge(ProceduralShapeCustom shape, RectTransform rectTransform, Vector2 normalized)
    {
        Vector2 point = shape.NormalizedToLocalPoint(normalized);
        int nearest = 0;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < shape.Points.Count; i++)
        {
            Vector2 a = shape.NormalizedToLocalPoint(shape.Points[i].Position);
            Vector2 b = shape.NormalizedToLocalPoint(shape.Points[(i + 1) % shape.Points.Count].Position);
            float distance = DistanceToSegmentSquared(point, a, b);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }
        return nearest;
    }

    private static bool TryGetNormalizedPoint(
        ProceduralShapeCustom shape,
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
        Rect rect = rectTransform.rect;
        if (requireInsideRect && !rect.Contains(local))
        {
            normalized = default;
            return false;
        }

        normalized = shape.LocalToNormalizedPoint(local);
        return true;
    }

    private static Vector3 NormalizedToWorld(ProceduralShapeCustom shape, RectTransform rectTransform, Vector2 normalized)
    {
        return rectTransform.TransformPoint(shape.NormalizedToLocalPoint(normalized));
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        if (edge.sqrMagnitude < 0.0000001f)
            return (point - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
        return (point - (a + edge * t)).sqrMagnitude;
    }

    private static void RecordPrefabChange(ProceduralShapeCustom shape)
    {
        EditorUtility.SetDirty(shape);
        PrefabUtility.RecordPrefabInstancePropertyModifications(shape);
    }
}
