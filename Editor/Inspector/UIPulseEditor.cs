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

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var animation = (AnimationComponent)target;
            EditorGUILayout.LabelField("Easing", "InOutQuad (Yoyo)");
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

        private void StartPreview(AnimationComponent animation)
        {
            StopPreview();
            originalScale = animation.transform.localScale;
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
