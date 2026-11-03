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
