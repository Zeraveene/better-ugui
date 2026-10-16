using UnityEditor;
using UnityEngine;
using AnimationComponent = Zeraveene.BetterUGUI.UIAnimation;

namespace Zeraveene.BetterUGUI.Editor
{
    [CustomEditor(typeof(AnimationComponent))]
    public sealed class UIAnimationEditor : UnityEditor.Editor
    {
        private bool fadeOpen = true, scaleOpen = true, preview;
        private double started;
        private Vector3 originalScale;
        private CanvasGroup group;
        private UnityEngine.UI.Image image;
        private float originalAlpha;
        private Color originalColor;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawChannel("Fade", "fade", ref fadeOpen);
            DrawChannel("Scale", "scale", ref scaleOpen);
            serializedObject.ApplyModifiedProperties();
            var animation = (AnimationComponent)target;
            if (serializedObject.FindProperty("fadeEnabled").boolValue &&
                animation.GetComponent<CanvasGroup>() == null && animation.GetComponent<UnityEngine.UI.Image>() == null)
                EditorGUILayout.HelpBox("Fade requires a CanvasGroup or Image on this GameObject.", MessageType.Info);
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

        private void DrawChannel(string label, string prefix, ref bool expanded)
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
            group = animation.GetComponent<CanvasGroup>();
            image = animation.GetComponent<UnityEngine.UI.Image>();
            if (group != null) originalAlpha = group.alpha;
            if (image != null) originalColor = image.color;
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
            }
            if (group != null) group.alpha = originalAlpha;
            if (image != null) image.color = originalColor;
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
