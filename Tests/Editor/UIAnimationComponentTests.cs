using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using AnimationComponent = Zeraveene.BetterUGUI.UIAnimation;

namespace Zeraveene.BetterUGUI.Tests
{
    public sealed class UIAnimationComponentTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private AnimationComponent Create(params System.Type[] components)
        {
            go = new GameObject("UIAnimation Test", components);
            return go.AddComponent<AnimationComponent>();
        }

        private static void Set(AnimationComponent animation, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(animation);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TestCase(Animation.UIEase.OutQuad, 0.25f)]
        [TestCase(Animation.UIEase.Linear, 0.5f)]
        [TestCase(Animation.UIEase.InQuad, 0.75f)]
        [TestCase(Animation.UIEase.Custom, 0.875f)]
        public void SelectedEasingControlsChannelProgress(Animation.UIEase ease, float expectedAlpha)
        {
            var animation = Create(typeof(CanvasGroup));
            Set(animation, so =>
            {
                so.FindProperty("ease").intValue = (int)ease;
                so.FindProperty("customEase").animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 0.25f);
                so.FindProperty("fadeDuration").floatValue = 1f;
            });
            animation.Play();
            Assert.IsTrue(animation.EvaluatePreview(0.5f));
            Assert.AreEqual(expectedAlpha, go.GetComponent<CanvasGroup>().alpha, 1e-4f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatWaitsForLongestChannelThenIntervalAndPreservesDirection(bool reverse)
        {
            var animation = Create(typeof(RectTransform), typeof(CanvasGroup));
            Set(animation, so =>
            {
                so.FindProperty("ease").intValue = (int)Animation.UIEase.Linear;
                so.FindProperty("fadeDuration").floatValue = 0.5f;
                so.FindProperty("scaleEnabled").boolValue = true;
                so.FindProperty("scaleDuration").floatValue = 1f;
                so.FindProperty("scaleFrom").floatValue = 1f;
                so.FindProperty("scaleTo").floatValue = 2f;
                so.FindProperty("repeat").boolValue = true;
                so.FindProperty("repeatInterval").floatValue = 0.5f;
            });
            Assert.IsTrue(reverse ? animation.PlayReverse() : animation.Play());
            foreach (float time in new[] { 1f, 1.25f, 1.49f })
            {
                Assert.IsTrue(animation.EvaluatePreview(time));
                Assert.AreEqual(reverse ? 1f : 0f, go.GetComponent<CanvasGroup>().alpha, 1e-4f);
                Assert.AreEqual(reverse ? 1f : 2f, go.transform.localScale.x, 1e-4f);
            }
            Assert.IsTrue(animation.EvaluatePreview(1.5f));
            Assert.AreEqual(reverse ? 0f : 1f, go.GetComponent<CanvasGroup>().alpha, 1e-4f);
            Assert.AreEqual(reverse ? 2f : 1f, go.transform.localScale.x, 1e-4f);
            Assert.IsTrue(animation.EvaluatePreview(1.75f));
            Assert.AreEqual(0.5f, go.GetComponent<CanvasGroup>().alpha, 1e-4f);
            Assert.AreEqual(reverse ? 1.75f : 1.25f, go.transform.localScale.x, 1e-4f);
            Assert.IsTrue(animation.Stop());
            Assert.IsFalse(animation.EvaluatePreview(3f));
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, 0.5f)]
        [TestCase(1f, 0f)]
        public void RepeatingInstantAnimationsAndZeroIntervalRemainFinite(float duration, float interval)
        {
            var animation = Create(typeof(CanvasGroup));
            Set(animation, so =>
            {
                so.FindProperty("fadeDuration").floatValue = duration;
                so.FindProperty("repeat").boolValue = true;
                so.FindProperty("repeatInterval").floatValue = interval;
            });
            Assert.IsTrue(animation.Play());
            Assert.IsTrue(animation.EvaluatePreview(10f));
            Assert.AreEqual(duration == 0f ? 0f : 1f, go.GetComponent<CanvasGroup>().alpha, 1e-4f);
            animation.enabled = false;
            Assert.IsFalse(animation.EvaluatePreview(11f));
            Assert.IsFalse(animation.Stop());
        }

        [Test]
        public void AlwaysPlayDoesNotAutomaticallyAnimateInEditMode()
        {
            var animation = Create(typeof(CanvasGroup));
            Set(animation, so => so.FindProperty("alwaysPlay").boolValue = true);
            animation.enabled = false;
            animation.enabled = true;
            Assert.IsFalse(animation.Stop());
            Assert.AreEqual(1f, go.GetComponent<CanvasGroup>().alpha);
        }

        [TestCase(typeof(Image))]
        [TestCase(typeof(RawImage))]
        [TestCase(typeof(ProceduralShape))]
        public void FadeDrivesGraphicAlphaWithoutCanvasGroup(System.Type graphicType)
        {
            var animation = Create(typeof(RectTransform), typeof(CanvasRenderer), graphicType);
            var graphic = (Graphic)go.GetComponent(graphicType);
            Assert.IsTrue(animation.Play());
            Assert.AreEqual(1f, graphic.color.a, 1e-4f);
            animation.EvaluatePreview(10f);
            Assert.AreEqual(0f, graphic.color.a, 1e-4f);
        }

        [Test]
        public void FadePrefersCanvasGroupThenAssignedTarget()
        {
            var animation = Create(typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var group = go.GetComponent<CanvasGroup>();
            var image = go.GetComponent<Image>();
            animation.Play();
            animation.EvaluatePreview(10f);
            Assert.AreEqual(0f, group.alpha, 1e-4f);
            Assert.AreEqual(1f, image.color.a, 1e-4f);

            group.alpha = 1f;
            Set(animation, so => so.FindProperty("fadeTarget").objectReferenceValue = image);
            animation.Play();
            animation.EvaluatePreview(10f);
            Assert.AreEqual(1f, group.alpha, 1e-4f);
            Assert.AreEqual(0f, image.color.a, 1e-4f);
        }

        [Test]
        public void PositionAnimatesAnchoredPosition()
        {
            var animation = Create(typeof(RectTransform));
            Set(animation, so =>
            {
                so.FindProperty("fadeEnabled").boolValue = false;
                so.FindProperty("positionEnabled").boolValue = true;
                so.FindProperty("positionFrom").vector2Value = new Vector2(10f, -40f);
                so.FindProperty("positionTo").vector2Value = new Vector2(30f, 20f);
            });
            var rect = (RectTransform)go.transform;
            Assert.IsTrue(animation.Play());
            Assert.AreEqual(new Vector2(10f, -40f), rect.anchoredPosition);
            Assert.IsFalse(animation.EvaluatePreview(10f));
            Assert.AreEqual(new Vector2(30f, 20f), rect.anchoredPosition);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacySharedTargetPreservesEntryAndExit(bool reverse)
        {
            var animation = Create(typeof(RectTransform), typeof(CanvasGroup));
            var target = new GameObject("Animation Target", typeof(RectTransform), typeof(CanvasGroup));
            target.transform.SetParent(go.transform, false);
            var rect = (RectTransform)target.transform;
            Set(animation, so =>
            {
                so.FindProperty("animationTarget").objectReferenceValue = rect;
                so.FindProperty("fadeFrom").floatValue = 0f;
                so.FindProperty("fadeTo").floatValue = 1f;
                so.FindProperty("scaleEnabled").boolValue = true;
                so.FindProperty("scaleFrom").floatValue = 0f;
                so.FindProperty("scaleTo").floatValue = 1f;
                so.FindProperty("positionEnabled").boolValue = true;
                so.FindProperty("positionFrom").vector2Value = new Vector2(0f, -50f);
                so.FindProperty("positionTo").vector2Value = Vector2.zero;
            });

            Assert.IsTrue(reverse ? animation.PlayReverse() : animation.Play());
            Assert.AreEqual(Vector3.one * (reverse ? 1f : 0f), rect.localScale);
            Assert.IsFalse(animation.EvaluatePreview(1f));
            Assert.AreEqual(Vector3.one * (reverse ? 0f : 1f), rect.localScale);
            Assert.AreEqual(reverse ? new Vector2(0f, -50f) : Vector2.zero, rect.anchoredPosition);
            Assert.AreEqual(reverse ? 0f : 1f, target.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(Vector3.one, go.transform.localScale);
            Assert.AreEqual(Vector2.zero, ((RectTransform)go.transform).anchoredPosition);
            Assert.AreEqual(1f, go.GetComponent<CanvasGroup>().alpha);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChannelsAnimateIndependentTargets(bool reverse)
        {
            var animation = Create(typeof(RectTransform), typeof(CanvasGroup));
            var scale = new GameObject("Scale Target", typeof(RectTransform));
            var position = new GameObject("Position Target", typeof(RectTransform));
            var fade = new GameObject("Fade Target", typeof(CanvasGroup));
            scale.transform.SetParent(go.transform, false);
            position.transform.SetParent(go.transform, false);
            fade.transform.SetParent(go.transform, false);
            Set(animation, so =>
            {
                so.FindProperty("scaleTarget").objectReferenceValue = scale.transform;
                so.FindProperty("posTarget").objectReferenceValue = position.transform;
                so.FindProperty("fadeTarget").objectReferenceValue = fade.GetComponent<CanvasGroup>();
                so.FindProperty("scaleEnabled").boolValue = true;
                so.FindProperty("positionEnabled").boolValue = true;
            });

            Assert.IsTrue(reverse ? animation.PlayReverse() : animation.Play());
            animation.EvaluatePreview(1f);
            Assert.AreEqual(Vector3.one * (reverse ? 1f : 1.3f), scale.transform.localScale);
            Assert.AreEqual(reverse ? new Vector2(0f, -50f) : Vector2.zero, ((RectTransform)position.transform).anchoredPosition);
            Assert.AreEqual(reverse ? 1f : 0f, fade.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(Vector3.one, position.transform.localScale);
            Assert.AreEqual(Vector2.zero, ((RectTransform)scale.transform).anchoredPosition);
            Assert.AreEqual(Vector3.one, go.transform.localScale);
            Assert.AreEqual(Vector2.zero, ((RectTransform)go.transform).anchoredPosition);
            Assert.AreEqual(1f, go.GetComponent<CanvasGroup>().alpha);
        }

        [Test]
        public void DestroyedScaleTargetDoesNotStopPositionChannel()
        {
            var animation = Create(typeof(RectTransform));
            var scale = new GameObject("Scale Target");
            scale.transform.SetParent(go.transform, false);
            Set(animation, so =>
            {
                so.FindProperty("scaleTarget").objectReferenceValue = scale.transform;
                so.FindProperty("scaleEnabled").boolValue = true;
                so.FindProperty("positionEnabled").boolValue = true;
            });
            animation.Play();
            Object.DestroyImmediate(scale);
            Assert.IsTrue(animation.EvaluatePreview(0.15f));
            Assert.AreEqual(new Vector2(0f, -12.5f), ((RectTransform)go.transform).anchoredPosition);
            Assert.IsFalse(animation.EvaluatePreview(1f));
            Assert.AreEqual(Vector2.zero, ((RectTransform)go.transform).anchoredPosition);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SeparateScaleAxesAnimateIndependently(bool reverse)
        {
            var animation = Create(typeof(RectTransform));
            Vector3 from = new Vector3(0.2f, 0.5f, 1f);
            Vector3 to = new Vector3(1f, 2f, 3f);
            Set(animation, so =>
            {
                so.FindProperty("fadeEnabled").boolValue = false;
                so.FindProperty("scaleEnabled").boolValue = true;
                so.FindProperty("separateScaleAxes").boolValue = true;
                so.FindProperty("scaleFromXYZ").vector3Value = from;
                so.FindProperty("scaleToXYZ").vector3Value = to;
            });
            Assert.IsTrue(reverse ? animation.PlayReverse() : animation.Play());
            Assert.AreEqual(reverse ? to : from, go.transform.localScale);
            Assert.IsTrue(animation.EvaluatePreview(0.15f));
            Vector3 midpoint = reverse ? new Vector3(0.4f, 0.875f, 1.5f) : new Vector3(0.8f, 1.625f, 2.5f);
            Assert.Less(Vector3.Distance(midpoint, go.transform.localScale), 1e-4f);
            Assert.IsFalse(animation.EvaluatePreview(0.3f));
            Assert.AreEqual(reverse ? from : to, go.transform.localScale);
        }

        [Test]
        public void DestroyedAnimationTargetStopsPlayback()
        {
            var animation = Create(typeof(RectTransform));
            var target = new GameObject("Animation Target");
            target.transform.SetParent(go.transform, false);
            Set(animation, so =>
            {
                so.FindProperty("animationTarget").objectReferenceValue = target.transform;
                so.FindProperty("scaleEnabled").boolValue = true;
            });
            Assert.IsTrue(animation.Play());
            Object.DestroyImmediate(target);
            Assert.IsFalse(animation.EvaluatePreview(0.1f));
            Assert.IsFalse(animation.Stop());
            Assert.AreEqual(Vector3.one, go.transform.localScale);
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void SizeAnimatesActualDimensionsWithStretchedAnchors(bool width, bool height, bool reverse)
        {
            var animation = Create(typeof(RectTransform));
            var parent = (RectTransform)go.transform;
            parent.sizeDelta = new Vector2(500f, 400f);
            var child = new GameObject("Size Target", typeof(RectTransform));
            child.transform.SetParent(parent, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            Set(animation, so =>
            {
                so.FindProperty("fadeEnabled").boolValue = false;
                so.FindProperty("sizeEnabled").boolValue = true;
                so.FindProperty("sizeTarget").objectReferenceValue = rect;
                so.FindProperty("widthEnabled").boolValue = width;
                so.FindProperty("heightEnabled").boolValue = height;
                so.FindProperty("widthFrom").floatValue = 100f;
                so.FindProperty("widthTo").floatValue = 300f;
                so.FindProperty("heightFrom").floatValue = 60f;
                so.FindProperty("heightTo").floatValue = 160f;
                so.FindProperty("sizeDuration").floatValue = 0.4f;
            });
            Assert.IsTrue(reverse ? animation.PlayReverse() : animation.Play());
            Assert.AreEqual(width ? (reverse ? 300f : 100f) : 500f, rect.rect.width, 1e-4f);
            Assert.AreEqual(height ? (reverse ? 160f : 60f) : 400f, rect.rect.height, 1e-4f);
            Assert.IsTrue(animation.EvaluatePreview(0.2f));
            Assert.AreEqual(width ? (reverse ? 150f : 250f) : 500f, rect.rect.width, 1e-4f);
            Assert.AreEqual(height ? (reverse ? 85f : 135f) : 400f, rect.rect.height, 1e-4f);
            Assert.IsFalse(animation.EvaluatePreview(0.4f));
            Assert.AreEqual(width ? (reverse ? 100f : 300f) : 500f, rect.rect.width, 1e-4f);
            Assert.AreEqual(height ? (reverse ? 60f : 160f) : 400f, rect.rect.height, 1e-4f);
            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Assert.AreEqual(new Vector2(500f, 400f), parent.rect.size);
        }

        [Test]
        public void SizeRequiresARectTransform()
        {
            var animation = Create();
            Set(animation, so =>
            {
                so.FindProperty("fadeEnabled").boolValue = false;
                so.FindProperty("sizeEnabled").boolValue = true;
            });
            Assert.IsFalse(animation.Play());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InspectorPreviewsHandOffSharedTargetsBeforeCapturingValues(bool reverse)
        {
            var first = Create(typeof(RectTransform), typeof(CanvasGroup));
            var secondObject = new GameObject("Second Animation");
            secondObject.transform.SetParent(go.transform, false);
            var second = secondObject.AddComponent<AnimationComponent>();
            foreach (var animation in new[] { first, second })
                Set(animation, so =>
                {
                    so.FindProperty("fadeTarget").objectReferenceValue = go.GetComponent<CanvasGroup>();
                    so.FindProperty("scaleEnabled").boolValue = true;
                    so.FindProperty("scaleTarget").objectReferenceValue = go.transform;
                    so.FindProperty("scaleFrom").floatValue = 0f;
                    so.FindProperty("scaleTo").floatValue = 1f;
                });

            go.transform.localScale = Vector3.one * 2f;
            go.GetComponent<CanvasGroup>().alpha = 0.4f;
            var firstEditor = UnityEditor.Editor.CreateEditor(first);
            var secondEditor = UnityEditor.Editor.CreateEditor(second);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var start = firstEditor.GetType().GetMethod("StartPreview", flags);
            var stop = firstEditor.GetType().GetMethod("StopPreview", flags);
            try
            {
                // Alternate repeatedly, including after a preview has finished but still owns a snapshot.
                for (int i = 0; i < 4; i++)
                {
                    start.Invoke(firstEditor, new object[] { first, reverse });
                    Assert.IsTrue(first.EvaluatePreview(0.15f));
                    start.Invoke(secondEditor, new object[] { second, !reverse });
                    Assert.IsFalse(first.EvaluatePreview(0.2f), "The previous preview must stop writing to the shared target.");
                    Assert.IsTrue(second.EvaluatePreview(0.15f));
                    Assert.IsFalse(second.EvaluatePreview(1f));
                    stop.Invoke(firstEditor, null);
                    Assert.AreEqual(reverse ? Vector3.one : Vector3.zero, go.transform.localScale,
                        "Stopping an old inspector must not restore over the current preview.");
                    stop.Invoke(secondEditor, null);
                    Assert.AreEqual(Vector3.one * 2f, go.transform.localScale);
                    Assert.AreEqual(0.4f, go.GetComponent<CanvasGroup>().alpha, 1e-4f);
                }
            }
            finally
            {
                Object.DestroyImmediate(firstEditor);
                Object.DestroyImmediate(secondEditor);
            }
        }

        [TestCase(typeof(UIAnimation), false, false)]
        [TestCase(typeof(UIAnimation), true, false)]
        [TestCase(typeof(UIAnimation), false, true)]
        [TestCase(typeof(UIAnimation), true, true)]
        [TestCase(typeof(UIPulse), false, false)]
        [TestCase(typeof(UIPulse), true, false)]
        public void MultiObjectInspectorPlaysSelectionAndRestoresOriginalValues(System.Type componentType, bool reverse, bool sharedTarget)
        {
            go = new GameObject("Batch preview", typeof(RectTransform));
            var other = new GameObject("Second preview", typeof(RectTransform));
            other.transform.SetParent(go.transform, false);
            Component first = go.AddComponent(componentType);
            Component second = other.AddComponent(componentType);
            foreach (Component component in new[] { first, second })
            {
                var settings = new SerializedObject(component);
                if (component is AnimationComponent)
                {
                    settings.FindProperty("fadeEnabled").boolValue = false;
                    settings.FindProperty("scaleEnabled").boolValue = true;
                    settings.FindProperty("scaleFrom").floatValue = 1f;
                    settings.FindProperty("scaleTo").floatValue = 2f;
                    if (sharedTarget) settings.FindProperty("scaleTarget").objectReferenceValue = go.transform;
                }
                else
                {
                    settings.FindProperty("mode").enumValueIndex = (int)UIPulseMode.Scale;
                    settings.FindProperty("from").floatValue = 1f;
                    settings.FindProperty("to").floatValue = 2f;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            go.transform.localScale = Vector3.one * 3f;
            other.transform.localScale = Vector3.one * 4f;
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second });
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                Assert.IsTrue(System.Attribute.IsDefined(editor.GetType(), typeof(CanEditMultipleObjects)));
                editor.GetType().GetMethod("Play", flags).Invoke(editor, new object[] { reverse });
                Assert.AreEqual(Vector3.one * (reverse ? 2f : 1f), go.transform.localScale);
                Assert.AreEqual(Vector3.one * (sharedTarget ? 4f : reverse ? 2f : 1f), other.transform.localScale);

                // A disabled component must not prevent the rest of the batch from advancing.
                ((Behaviour)first).enabled = false;
                editor.GetType().GetField("started", flags).SetValue(editor, EditorApplication.timeSinceStartup - 0.15);
                editor.GetType().GetMethod("UpdatePreview", flags).Invoke(editor, null);
                Transform animated = sharedTarget ? go.transform : other.transform;
                Assert.That(animated.localScale.x, Is.GreaterThan(1f).And.LessThan(2f));

                editor.GetType().GetMethod("StopPreview", flags).Invoke(editor, null);
                Assert.AreEqual(Vector3.one * 3f, go.transform.localScale);
                Assert.AreEqual(Vector3.one * 4f, other.transform.localScale);
                Assert.IsFalse(first is AnimationComponent a ? a.Stop() : ((UIPulse)first).Stop());
                Assert.IsFalse(second is AnimationComponent b ? b.Stop() : ((UIPulse)second).Stop());
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }

        [TestCase(UIPulseMode.Scale, false)]
        [TestCase(UIPulseMode.Scale, true)]
        [TestCase(UIPulseMode.Alpha, false)]
        [TestCase(UIPulseMode.Alpha, true)]
        public void PulseRestartRepeatsOutQuadEveryDuration(UIPulseMode mode, bool reverse)
        {
            go = new GameObject("Pulse Test", typeof(RectTransform), typeof(CanvasGroup));
            var pulse = go.AddComponent<UIPulse>();
            var settings = new SerializedObject(pulse);
            settings.FindProperty("mode").enumValueIndex = (int)mode;
            settings.FindProperty("from").floatValue = 0f;
            settings.FindProperty("to").floatValue = 1f;
            settings.FindProperty("duration").floatValue = 0.3f;
            settings.FindProperty("restart").boolValue = true;
            settings.FindProperty("ease").intValue = (int)Animation.UIEase.OutQuad;
            settings.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(reverse ? pulse.PlayReverse() : pulse.Play());
            foreach (float seconds in new[] { 0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.75f })
            {
                Assert.IsTrue(pulse.EvaluatePreview(seconds));
                float expected = seconds == 0f || seconds == 0.3f || seconds == 0.6f ? 0f : 0.75f;
                if (reverse) expected = 1f - expected;
                float actual = mode == UIPulseMode.Scale ? go.transform.localScale.x : go.GetComponent<CanvasGroup>().alpha;
                Assert.AreEqual(expected, actual, 1e-4f, $"At {seconds} seconds");
            }
            Assert.IsTrue(pulse.Stop());
            Assert.IsFalse(pulse.EvaluatePreview(1f));
        }

        [Test]
        public void PulseDefaultsStillPingPongWithInOutQuad()
        {
            go = new GameObject("Pulse Test", typeof(CanvasGroup));
            var pulse = go.AddComponent<UIPulse>();
            var group = go.GetComponent<CanvasGroup>();
            Assert.IsTrue(pulse.Play());
            Assert.AreEqual(1f, group.alpha, 1e-4f);
            pulse.EvaluatePreview(0.075f);
            Assert.AreEqual(0.9375f, group.alpha, 1e-4f);
            pulse.EvaluatePreview(0.3f);
            Assert.AreEqual(0.5f, group.alpha, 1e-4f);
            pulse.EvaluatePreview(0.6f);
            Assert.AreEqual(1f, group.alpha, 1e-4f);
        }
    }
}
