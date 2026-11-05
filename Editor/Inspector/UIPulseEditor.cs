using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AnimationComponent = Zeraveene.BetterUGUI.UIPulse;

namespace Zeraveene.BetterUGUI.Editor
{
    [CustomEditor(typeof(AnimationComponent))]
    [CanEditMultipleObjects]
    public sealed class UIPulseEditor : UnityEditor.Editor
    {
        private static UIPulseEditor activePreview;
        private bool preview;
        private double started;
        private readonly List<PreviewSnapshot> snapshots = new List<PreviewSnapshot>();

        private sealed class PreviewSnapshot
        {
            internal readonly AnimationComponent Animation;
            private readonly Transform transform;
            private readonly Vector3 originalScale;
            private readonly CanvasGroup group;
            private readonly UnityEngine.UI.Graphic graphic;
            private readonly float originalAlpha;
            private readonly Color originalColor;

            internal PreviewSnapshot(AnimationComponent animation)
            {
                Animation = animation;
                transform = animation.transform;
                originalScale = transform.localScale;
                group = animation.GetComponent<CanvasGroup>();
                graphic = animation.GetComponent<UnityEngine.UI.Graphic>();
                if (group != null) originalAlpha = group.alpha;
                if (graphic != null) originalColor = graphic.color;
            }

            internal void Restore()
            {
                if (Animation != null) Animation.Stop();
                if (transform != null) transform.localScale = originalScale;
                if (group != null) group.alpha = originalAlpha;
                if (graphic != null) graphic.color = originalColor;
            }
        }

        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector()) StopPreview();
            bool missingAlphaTarget = targets.Cast<AnimationComponent>().Any(animation =>
                new SerializedObject(animation).FindProperty("mode").enumValueIndex == (int)UIPulseMode.Alpha &&
                animation.GetComponent<CanvasGroup>() == null && animation.GetComponent<UnityEngine.UI.Graphic>() == null);
            if (missingAlphaTarget)
                EditorGUILayout.HelpBox("Alpha requires a CanvasGroup or UI Graphic on this GameObject (including procedural UI).", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                {
                    Play(false);
                }
                if (GUILayout.Button("Play Reverse"))
                {
                    Play(true);
                }
                if (GUILayout.Button("Stop"))
                {
                    if (Application.isPlaying)
                        foreach (AnimationComponent animation in targets) animation.Stop();
                    else StopPreview();
                }
            }
        }

        private void Play(bool reverse)
        {
            if (!Application.isPlaying) StartPreviews(targets.Cast<AnimationComponent>().ToArray(), reverse);
            else
            {
                serializedObject.ApplyModifiedProperties();
                foreach (AnimationComponent animation in targets)
                    if (reverse) animation.PlayReverse();
                    else animation.Play();
            }
        }

        private void StartPreviews(AnimationComponent[] animations, bool reverse)
        {
            if (activePreview != null) activePreview.StopPreview();
            StopPreview();
            serializedObject.ApplyModifiedProperties();
            foreach (AnimationComponent animation in animations)
                if (animation != null && animation.isActiveAndEnabled)
                    snapshots.Add(new PreviewSnapshot(animation));
            preview = true;
            activePreview = this;
            started = EditorApplication.timeSinceStartup;
            foreach (PreviewSnapshot snapshot in snapshots)
                if (reverse) snapshot.Animation.PlayReverse();
                else snapshot.Animation.Play();
            EditorApplication.update += UpdatePreview;
        }

        private void UpdatePreview()
        {
            bool running = false;
            float elapsed = (float)(EditorApplication.timeSinceStartup - started);
            foreach (PreviewSnapshot snapshot in snapshots)
            {
                AnimationComponent animation = snapshot.Animation;
                if (animation == null) continue;
                if (!animation.isActiveAndEnabled) { animation.Stop(); continue; }
                running |= animation.EvaluatePreview(elapsed);
            }
            if (!running)
                EditorApplication.update -= UpdatePreview;
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void StopPreview()
        {
            EditorApplication.update -= UpdatePreview;
            if (activePreview == this) activePreview = null;
            if (!preview) return;
            preview = false;
            foreach (PreviewSnapshot snapshot in snapshots) snapshot.Restore();
            snapshots.Clear();
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
