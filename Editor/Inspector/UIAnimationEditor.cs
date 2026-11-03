using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine;
using AnimationComponent = Zeraveene.BetterUGUI.UIAnimation;

namespace Zeraveene.BetterUGUI.Editor
{
    [CustomEditor(typeof(AnimationComponent))]
    public sealed class UIAnimationEditor : UnityEditor.Editor
    {
        private bool preview;
        private double started;
        private Transform previewTarget;
        private Vector3 originalScale, originalLocalPosition;
        private Vector2 originalAnchoredPosition;
        private CanvasGroup group;
        private UnityEngine.UI.Graphic graphic;
        private float originalAlpha;
        private Color originalColor;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("ui-animation-inspector");
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string stylesheetPath = System.IO.Path.ChangeExtension(scriptPath, ".uss");
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylesheetPath);
            if (stylesheet != null) root.styleSheets.Add(stylesheet);

            root.Add(Field("animationTarget", "Target"));
            var fade = AddChannel(root, "Fade", "fade");
            fade.Add(Field("fadeTarget", "Fade Target"));
            fade.Add(Field("fadeFrom", "From"));
            fade.Add(Field("fadeTo", "To"));
            fade.Add(Field("fadeDuration", "Duration (s)"));
            var warning = new HelpBox("Assign a CanvasGroup or Graphic to Fade Target, or add one to the animation Target.", HelpBoxMessageType.Info);
            fade.Add(warning);

            var scale = AddChannel(root, "Scale", "scale");
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
                uniformScale.EnableInClassList("hidden", property.boolValue);
                xyzScale.EnableInClassList("hidden", !property.boolValue);
            }
            UpdateScaleFields(axesProperty);
            root.TrackPropertyValue(axesProperty, UpdateScaleFields);

            var position = AddChannel(root, "Position", "position");
            position.Add(Field("positionFrom", "From"));
            position.Add(Field("positionTo", "To"));
            position.Add(Field("positionDuration", "Duration (s)"));

            var easing = new Label("Easing · OutQuad");
            easing.AddToClassList("animation-hint");
            root.Add(easing);
            var controls = new VisualElement();
            controls.AddToClassList("playback-controls");
            controls.Add(new Button(() => Play(false)) { text = "Play" });
            controls.Add(new Button(() => Play(true)) { text = "Play Reverse" });
            controls.Add(new Button(() =>
            {
                if (Application.isPlaying) ((AnimationComponent)target).Stop();
                else StopPreview();
            }) { text = "Stop" });
            root.Add(controls);

            void RefreshWarning()
            {
                var animation = (AnimationComponent)target;
                animation.ResolveFadeTarget(out var fadeGroup, out var fadeGraphic);
                warning.EnableInClassList("hidden", fadeGroup != null || fadeGraphic != null);
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
            content.SetEnabled(enabledProperty.boolValue);
            foldout.RegisterValueChangedCallback(evt =>
            {
                content.EnableInClassList("hidden", !evt.newValue);
                SessionState.SetBool(stateKey, evt.newValue);
            });
            content.TrackPropertyValue(enabledProperty, property => content.SetEnabled(property.boolValue));
            box.Add(content);
            root.Add(box);
            return content;
        }

        private void Play(bool reverse)
        {
            var animation = (AnimationComponent)target;
            if (!Application.isPlaying) StartPreview(animation, reverse);
            else if (reverse) animation.PlayReverse();
            else animation.Play();
        }

        private void StartPreview(AnimationComponent animation, bool reverse = false)
        {
            StopPreview();
            previewTarget = animation.ResolveAnimationTarget();
            originalScale = previewTarget.localScale;
            originalLocalPosition = previewTarget.localPosition;
            if (previewTarget is RectTransform rect) originalAnchoredPosition = rect.anchoredPosition;
            animation.ResolveFadeTarget(out group, out graphic);
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
                animation.Stop();
            if (previewTarget != null)
            {
                previewTarget.localScale = originalScale;
                if (previewTarget is RectTransform rect) rect.anchoredPosition = originalAnchoredPosition;
                else previewTarget.localPosition = originalLocalPosition;
            }
            previewTarget = null;
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
