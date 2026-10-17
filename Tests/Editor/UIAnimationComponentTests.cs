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
    }
}
