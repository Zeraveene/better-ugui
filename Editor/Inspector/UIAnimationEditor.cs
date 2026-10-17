using UnityEditor;
using UnityEngine;
using AnimationComponent = Zeraveene.BetterUGUI.UIAnimation;

namespace Zeraveene.BetterUGUI.Editor
{
    [CustomEditor(typeof(AnimationComponent))]
    public sealed class UIAnimationEditor : UnityEditor.Editor
    {
        private bool fadeOpen = true, scaleOpen = true, positionOpen = true, preview;
        private double started;
        private Vector3 originalScale, originalLocalPosition;
        private Vector2 originalAnchoredPosition;
        private CanvasGroup group;
        private UnityEngine.UI.Graphic graphic;
        private float originalAlpha;
        private Color originalColor;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawChannel("Fade", "fade", ref fadeOpen, DrawFadeTarget);
            DrawChannel("Scale", "scale", ref scaleOpen);
            DrawChannel("Position", "position", ref positionOpen);
            serializedObject.ApplyModifiedProperties();
            var animation = (AnimationComponent)target;
            animation.ResolveFadeTarget(out var fadeGroup, out var fadeGraphic);
            if (serializedObject.FindProperty("fadeEnabled").boolValue && fadeGroup == null && fadeGraphic == null)
                EditorGUILayout.HelpBox("Fade needs a CanvasGroup or Graphic (ProceduralShape, Image, RawImage) on this GameObject, or an assigned Target.", MessageType.Info);
            EditorGUILayout.LabelField("Easing", "OutQuad");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                {
                    if (Application.isPlaying) animation.Play();
                    else StartPreview(animation);
                }
                if (GUILayout.Button("Stop"))
                {
                    if (Application.isPlaying) animation.Stop();
                    else StopPreview();
                }
            }
        }

        private void DrawFadeTarget()
        {
            var property = serializedObject.FindProperty("fadeTarget");
            EditorGUI.BeginChangeCheck();
            var value = EditorGUILayout.ObjectField(new GUIContent("Target", property.tooltip), property.objectReferenceValue, typeof(Component), true);
            if (!EditorGUI.EndChangeCheck()) return;
            if (value is GameObject go) value = go.GetComponent<CanvasGroup>() != null ? go.GetComponent<CanvasGroup>() : go.GetComponent<UnityEngine.UI.Graphic>();
            else if (value is Component c && !(c is CanvasGroup) && !(c is UnityEngine.UI.Graphic))
                value = c.GetComponent<CanvasGroup>() != null ? c.GetComponent<CanvasGroup>() : c.GetComponent<UnityEngine.UI.Graphic>();
            property.objectReferenceValue = value;
        }

        private void DrawChannel(string label, string prefix, ref bool expanded, System.Action extra = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var enabled = serializedObject.FindProperty(prefix + "Enabled");
                enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18));
                expanded = EditorGUILayout.Foldout(expanded, label, true);
            }
            if (!expanded) return;
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty(prefix + "Enabled").boolValue))
            {
                EditorGUI.indentLevel++;
                extra?.Invoke();
                EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "From"), new GUIContent("From"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "To"), new GUIContent("To"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(prefix + "Duration"), new GUIContent("Duration (s)"));
                EditorGUI.indentLevel--;
            }
        }

        private void StartPreview(AnimationComponent animation)
        {
            StopPreview();
            originalScale = animation.transform.localScale;
            originalLocalPosition = animation.transform.localPosition;
            if (animation.transform is RectTransform rect) originalAnchoredPosition = rect.anchoredPosition;
            animation.ResolveFadeTarget(out group, out graphic);
            if (group != null) originalAlpha = group.alpha;
            if (graphic != null) originalColor = graphic.color;
            preview = true;
            started = EditorApplication.timeSinceStartup;
            animation.Play();
            EditorApplication.update += UpdatePreview;
        }

        private void UpdatePreview()
        {
            var animation = target as AnimationComponent;
            if (animation == null || !animation.isActiveAndEnabled) { StopPreview(); return; }
            if (!animation.EvaluatePreview((float)(EditorApplication.timeSinceStartup - started)))
                EditorApplication.update -= UpdatePreview;
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void StopPreview()
        {
            EditorApplication.update -= UpdatePreview;
            if (!preview) return;
            preview = false;
            var animation = target as AnimationComponent;
            if (animation != null)
            {
                animation.Stop();
                animation.transform.localScale = originalScale;
                if (animation.transform is RectTransform rect) rect.anchoredPosition = originalAnchoredPosition;
                else animation.transform.localPosition = originalLocalPosition;
            }
            if (group != null) group.alpha = originalAlpha;
            if (graphic != null) graphic.color = originalColor;
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private void OnDisable()
        {
            StopPreview();
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
        private void OnPlayModeChanged(PlayModeStateChange state) => StopPreview();
    }
}
