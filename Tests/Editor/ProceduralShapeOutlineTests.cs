using NUnit.Framework;
using UnityEngine;

namespace Zeraveene.BetterUGUI.Tests
{
    public sealed class ProceduralShapeOutlineTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OutlineFollowsSourceAfterStartingAtZeroScale(bool childOfSource)
        {
            var canvasObject = new GameObject("Outline test", typeof(Canvas));
            try
            {
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var sourceObject = new GameObject("Source", typeof(RectTransform), typeof(ProceduralShape));
                sourceObject.transform.SetParent(canvasObject.transform, false);
                var source = sourceObject.GetComponent<ProceduralShape>();
                source.rectTransform.sizeDelta = new Vector2(100f, 100f);
                source.SnapCornersToPixels = false;
                source.transform.localScale = Vector3.zero;
                var outlineObject = new GameObject("Outline", typeof(RectTransform), typeof(ProceduralShapeOutline));
                outlineObject.transform.SetParent(childOfSource ? source.transform : canvasObject.transform, false);
                var outline = outlineObject.GetComponent<ProceduralShapeOutline>();
                outline.Source = source;
                Canvas.ForceUpdateCanvases();

                source.transform.localScale = new Vector3(2f, 1f, 1f);
                source.rectTransform.anchoredPosition = new Vector2(30f, 20f);
                Canvas.ForceUpdateCanvases();

                var mesh = outline.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                float minX = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    float x = outline.transform.TransformPoint(vertex).x;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                }
                Assert.That(maxX - minX, Is.EqualTo(200f).Within(0.01f));
                Assert.That((minX + maxX) * 0.5f,
                    Is.EqualTo(source.transform.position.x).Within(0.01f));

                int notifications = 0;
                outline.GeometryChanged += () => notifications++;
                Canvas.ForceUpdateCanvases();
                Assert.That(notifications, Is.Zero, "Unchanged transforms should not dirty the outline again.");
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }
    }
}
