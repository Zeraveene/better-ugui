using System.Collections.Generic;
using Zeraveene.Shared.Inspector;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProceduralShapeCustomAnimation))]
public sealed class ProceduralShapeCustomAnimationEditor : InspectorAttributeEditor
{
    private double _previewStarted;
    private bool _previewPlaying;
    private PreviewSnapshot _previewSnapshot;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        ProceduralShapeCustomAnimation animation = (ProceduralShapeCustomAnimation)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(Application.isPlaying ? "Playback" : "Edit Mode Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Play Forward"))
            Play(animation, true);
        if (GUILayout.Button("Play Reverse"))
            Play(animation, false);
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(Application.isPlaying || _previewSnapshot == null))
        {
            if (GUILayout.Button("Stop Preview"))
                StopPreview();
        }

        if (!Application.isPlaying && _previewSnapshot != null)
            EditorGUILayout.HelpBox("Stop Preview to restore the authored shape points.", MessageType.Info);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    protected override void OnDisable()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        StopPreview();
        base.OnDisable();
    }

    private void Play(ProceduralShapeCustomAnimation animation, bool forward)
    {
        if (Application.isPlaying)
        {
            if (forward)
                animation.PlayForward();
            else
                animation.PlayReverse();
            return;
        }

        ProceduralShapeCustom shape = animation.GetComponent<ProceduralShapeCustom>();
        if (shape == null)
            return;

        if (_previewSnapshot == null)
            _previewSnapshot = PreviewSnapshot.Capture(shape);
        else
            StopPreviewTween(animation);

        animation.BeginPreview(forward);
        _previewStarted = EditorApplication.timeSinceStartup;
        _previewPlaying = true;
        EditorApplication.update -= UpdatePreview;
        EditorApplication.update += UpdatePreview;
        UpdatePreview();

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
    }

    private void StopPreview()
    {
        if (_previewSnapshot == null)
            return;

        ProceduralShapeCustomAnimation animation = target as ProceduralShapeCustomAnimation;
        if (animation != null)
            animation.StopAnimation();
        _previewPlaying = false;
        EditorApplication.update -= UpdatePreview;

        _previewSnapshot.Restore();
        _previewSnapshot = null;
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
    }

    private void StopPreviewTween(ProceduralShapeCustomAnimation animation)
    {
        animation.StopAnimation();
        _previewPlaying = false;
        EditorApplication.update -= UpdatePreview;
    }

    private void UpdatePreview()
    {
        if (!_previewPlaying || target == null) return;
        var animation = (ProceduralShapeCustomAnimation)target;
        float progress = animation.Duration <= 0f ? 1f
            : Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - _previewStarted) / animation.Duration);
        animation.EvaluatePreview(progress);
        if (progress >= 1f)
        {
            _previewPlaying = false;
            EditorApplication.update -= UpdatePreview;
        }
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            StopPreview();
    }

    private sealed class PreviewSnapshot
    {
        private readonly ProceduralShapeCustom _shape;
        private readonly ProceduralShapeTemplate _template;
        private readonly bool _hadPointOverrides;
        private readonly List<ProceduralShapePoint> _points;

        private PreviewSnapshot(ProceduralShapeCustom shape)
        {
            _shape = shape;
            _template = shape.PointTemplate;
            _hadPointOverrides = shape.HasPointOverrides;
            _points = new List<ProceduralShapePoint>(shape.Points.Count);
            for (int i = 0; i < shape.Points.Count; i++)
                _points.Add(shape.Points[i]);
        }

        public static PreviewSnapshot Capture(ProceduralShapeCustom shape)
        {
            return new PreviewSnapshot(shape);
        }

        public void Restore()
        {
            if (_shape == null)
                return;

            if (_template != null && !_hadPointOverrides && _shape.PointTemplate == _template)
                _shape.ResetPointsFromTemplate();
            else
                _shape.SetPoints(_points);
        }
    }
}
