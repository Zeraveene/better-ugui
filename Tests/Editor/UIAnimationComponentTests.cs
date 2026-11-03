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
