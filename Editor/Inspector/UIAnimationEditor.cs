using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AnimationComponent = Zeraveene.BetterUGUI.UIAnimation;

namespace Zeraveene.BetterUGUI.Editor
{
    [CustomEditor(typeof(AnimationComponent))]
    [CanEditMultipleObjects]
    public sealed class UIAnimationEditor : UnityEditor.Editor
    {
        private static UIAnimationEditor activePreview;
        private bool preview;
        private double started;
        private readonly List<PreviewSnapshot> snapshots = new List<PreviewSnapshot>();

        private sealed class PreviewSnapshot
        {
            internal readonly AnimationComponent Animation;
            private readonly Transform previewScaleTarget, previewPositionTarget;
            private RectTransform previewSizeTarget;
            private Vector2 originalSizeDelta;
            private Vector3 originalScale, originalLocalPosition;
            private Vector2 originalAnchoredPosition;
            private CanvasGroup group;
            private UnityEngine.UI.Graphic graphic;
            private float originalAlpha;
            private Color originalColor;

            internal PreviewSnapshot(AnimationComponent animation)
            {
                Animation = animation;
                previewScaleTarget = animation.ResolveScaleTarget();
                previewPositionTarget = animation.ResolvePositionTarget();
                var settings = new SerializedObject(animation);
                previewSizeTarget = settings.FindProperty("sizeEnabled").boolValue ? animation.ResolveSizeTarget() : null;
                if (previewSizeTarget != null) originalSizeDelta = previewSizeTarget.sizeDelta;
                originalScale = previewScaleTarget.localScale;
                originalLocalPosition = previewPositionTarget.localPosition;
                if (previewPositionTarget is RectTransform rect) originalAnchoredPosition = rect.anchoredPosition;
                animation.ResolveFadeTarget(out group, out graphic);
                if (group != null) originalAlpha = group.alpha;
                if (graphic != null) originalColor = graphic.color;
            }

            internal void Restore()
            {
                if (Animation != null) Animation.Stop();
                if (previewScaleTarget != null) previewScaleTarget.localScale = originalScale;
                if (previewSizeTarget != null) previewSizeTarget.sizeDelta = originalSizeDelta;
                if (previewPositionTarget != null)
                {
                    if (previewPositionTarget is RectTransform rect) rect.anchoredPosition = originalAnchoredPosition;
                    else previewPositionTarget.localPosition = originalLocalPosition;
                }
                if (group != null) group.alpha = originalAlpha;
                if (graphic != null) graphic.color = originalColor;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("ui-animation-inspector");
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string stylesheetPath = System.IO.Path.ChangeExtension(scriptPath, ".uss");
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylesheetPath);
            if (stylesheet != null) root.styleSheets.Add(stylesheet);

            var fade = AddChannel(root, "Fade", "fade");
            fade.Add(Field("fadeTarget", "Fade Target"));
            fade.Add(Field("fadeFrom", "From"));
            fade.Add(Field("fadeTo", "To"));
            fade.Add(Field("fadeDuration", "Duration (s)"));
            var warning = new HelpBox("Fade Target needs a CanvasGroup or Graphic. When empty, this GameObject is used.", HelpBoxMessageType.Info);
            fade.Add(warning);

            var scale = AddChannel(root, "Scale", "scale");
            scale.Add(Field("scaleTarget", "Scale Target"));
            scale.Add(Field("separateScaleAxes", "Separate Axes"));
            var uniformScale = new VisualElement();
            uniformScale.Add(Field("scaleFrom", "From"));
            uniformScale.Add(Field("scaleTo", "To"));
            scale.Add(uniformScale);
            var xyzScale = new VisualElement();
            xyzScale.Add(Field("scaleFromXYZ", "From"));
            xyzScale.Add(Field("scaleToXYZ", "To"));
            scale.Add(xyzScale);
            scale.Add(Field("scaleDuration", "Duration (s)"));
            var axesProperty = serializedObject.FindProperty("separateScaleAxes");
            void UpdateScaleFields(SerializedProperty property)
            {
                uniformScale.EnableInClassList("hidden", !property.hasMultipleDifferentValues && property.boolValue);
                xyzScale.EnableInClassList("hidden", !property.hasMultipleDifferentValues && !property.boolValue);
            }
            UpdateScaleFields(axesProperty);
            root.TrackPropertyValue(axesProperty, UpdateScaleFields);

            var position = AddChannel(root, "Position", "position");
            position.Add(Field("posTarget", "Position Target"));
            position.Add(Field("positionFrom", "From"));
            position.Add(Field("positionTo", "To"));
            position.Add(Field("positionDuration", "Duration (s)"));

            var size = AddChannel(root, "Size", "size");
            size.Add(Field("sizeTarget", "Size Target"));
            AddSizeAxis(size, "width", "Width");
            AddSizeAxis(size, "height", "Height");
            size.Add(Field("sizeDuration", "Duration (s)"));
            var sizeWarning = new HelpBox("Size needs a RectTransform on Size Target or this GameObject.", HelpBoxMessageType.Info);
            size.Add(sizeWarning);

            var easing = new Label("Easing · OutQuad");
            easing.AddToClassList("animation-hint");
            root.Add(easing);
            var controls = new VisualElement();
            controls.AddToClassList("playback-controls");
            controls.Add(new Button(() => Play(false)) { text = "Play" });
            controls.Add(new Button(() => Play(true)) { text = "Play Reverse" });
            controls.Add(new Button(() =>
            {
                if (Application.isPlaying)
                    foreach (AnimationComponent animation in targets) animation.Stop();
                else StopPreview();
            }) { text = "Stop" });
            root.Add(controls);

            void RefreshWarning()
            {
                bool fadeValid = true, sizeValid = true;
                foreach (AnimationComponent animation in targets)
                {
                    animation.ResolveFadeTarget(out var fadeGroup, out var fadeGraphic);
                    fadeValid &= fadeGroup != null || fadeGraphic != null;
                    sizeValid &= animation.ResolveSizeTarget() != null;
                }
                warning.EnableInClassList("hidden", fadeValid);
                sizeWarning.EnableInClassList("hidden", sizeValid);
            }
            RefreshWarning();
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                StopPreview();
                RefreshWarning();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ => StopPreview());
            return root;
        }

        private PropertyField Field(string propertyName, string label)
        {
            return new PropertyField(serializedObject.FindProperty(propertyName), label);
        }

        private VisualElement AddChannel(VisualElement root, string label, string prefix)
        {
            var box = new Box();
            box.AddToClassList("animation-channel");
            var header = new VisualElement();
            header.AddToClassList("channel-header");
            string stateKey = "BetterUGUI.UIAnimation." + prefix;
            var foldout = new Foldout { text = label, value = SessionState.GetBool(stateKey, true) };
            foldout.AddToClassList("channel-foldout");
            header.Add(foldout);
            var enabledProperty = serializedObject.FindProperty(prefix + "Enabled");
            var enabled = new Toggle("Enabled") { bindingPath = enabledProperty.propertyPath };
            enabled.AddToClassList("channel-enabled");
            header.Add(enabled);
            box.Add(header);
            var content = new VisualElement();
            content.AddToClassList("channel-content");
            content.EnableInClassList("hidden", !foldout.value);
            content.SetEnabled(enabledProperty.hasMultipleDifferentValues || enabledProperty.boolValue);
            foldout.RegisterValueChangedCallback(evt =>
            {
                content.EnableInClassList("hidden", !evt.newValue);
                SessionState.SetBool(stateKey, evt.newValue);
            });
            content.TrackPropertyValue(enabledProperty, property => content.SetEnabled(property.hasMultipleDifferentValues || property.boolValue));
            box.Add(content);
            root.Add(box);
            return content;
        }

        private void AddSizeAxis(VisualElement parent, string prefix, string label)
        {
            parent.Add(Field(prefix + "Enabled", "Animate " + label));
            var values = new VisualElement();
            values.Add(Field(prefix + "From", "From " + label));
            values.Add(Field(prefix + "To", "To " + label));
            var enabled = serializedObject.FindProperty(prefix + "Enabled");
            values.SetEnabled(enabled.hasMultipleDifferentValues || enabled.boolValue);
            values.TrackPropertyValue(enabled, property => values.SetEnabled(property.hasMultipleDifferentValues || property.boolValue));
            parent.Add(values);
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

        private void StartPreview(AnimationComponent animation, bool reverse = false)
            => StartPreviews(new[] { animation }, reverse);

        private void StartPreviews(AnimationComponent[] animations, bool reverse)
        {
            // Restore the previous preview before capturing shared targets for this one.
            if (activePreview != null) activePreview.StopPreview();
            StopPreview();
            serializedObject.ApplyModifiedProperties();
            // Capture the entire selection before playback changes any shared target.
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
