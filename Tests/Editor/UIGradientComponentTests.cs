using NUnit.Framework;
using UnityEngine;

namespace Zeraveene.BetterUGUI.Tests
{
    public sealed class UIGradientComponentTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AddingGradientToEmptyObjectCreatesShape(bool inactive)
        {
            var go = new GameObject("Gradient test");
            try
            {
                go.SetActive(!inactive);
                go.AddComponent<UIGradientEffect>();
                Assert.IsNotNull(go.GetComponent<RectTransform>());
                Assert.IsNotNull(go.GetComponent<CanvasRenderer>());
                Assert.IsNotNull(go.GetComponent<ProceduralShape>());
                go.SetActive(true);
                Assert.AreEqual(1, go.GetComponents<UnityEngine.UI.Graphic>().Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase(typeof(UnityEngine.UI.Image))]
        [TestCase(typeof(UnityEngine.UI.RawImage))]
        [TestCase(typeof(TMPro.TextMeshProUGUI))]
        [TestCase(typeof(ProceduralShapeOutline))]
        public void AddingGradientPreservesExistingGraphic(System.Type graphicType)
        {
            var go = new GameObject("Gradient test", typeof(RectTransform), graphicType);
            try
            {
                var original = go.GetComponent<UnityEngine.UI.Graphic>();
                go.AddComponent<UIGradientEffect>();
                Assert.IsNull(go.GetComponent<ProceduralShape>());
                Assert.AreSame(original, go.GetComponent<UnityEngine.UI.Graphic>());
                Assert.AreEqual(1, go.GetComponents<UnityEngine.UI.Graphic>().Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
