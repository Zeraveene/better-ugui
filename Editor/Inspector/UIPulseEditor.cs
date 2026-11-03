using UnityEditor;
using UnityEngine;
using AnimationComponent = Zeraveene.BetterUGUI.UIPulse;

namespace Zeraveene.BetterUGUI.Editor
{
    [CustomEditor(typeof(AnimationComponent))]
    public sealed class UIPulseEditor : UnityEditor.Editor
    {
        private bool preview;
        private double started;
        private Vector3 originalScale;
        private CanvasGroup group;
        private UnityEngine.UI.Graphic graphic;
        private float originalAlpha;
        private Color originalColor;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var animation = (AnimationComponent)target;
            if (serializedObject.FindProperty("mode").enumValueIndex == (int)UIPulseMode.Alpha &&
                animation.GetComponent<CanvasGroup>() == null && animation.GetComponent<UnityEngine.UI.Graphic>() == null)
                EditorGUILayout.HelpBox("Alpha requires a CanvasGroup or UI Graphic on this GameObject (including procedural UI).", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                {
                    if (Application.isPlaying) animation.Play();
                    else StartPreview(animation);
                }
                if (GUILayout.Button("Play Reverse"))
                {
                    if (Application.isPlaying) animation.PlayReverse();
                    else StartPreview(animation, true);
                }
                if (GUILayout.Button("Stop"))
                {
                    if (Application.isPlaying) animation.Stop();
                    else StopPreview();
                }
            }
        }

        private void StartPreview(AnimationComponent animation, bool reverse = false)
        {
            StopPreview();
            originalScale = animation.transform.localScale;
            group = animation.GetComponent<CanvasGroup>();
            graphic = animation.GetComponent<UnityEngine.UI.Graphic>();
            if (group != null) originalAlpha = group.alpha;
            if (graphic != null) originalColor = graphic.color;
            preview = true;
            started = EditorApplication.timeSinceStartup;
            if (reverse) animation.PlayReverse();
            else animation.Play();
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
