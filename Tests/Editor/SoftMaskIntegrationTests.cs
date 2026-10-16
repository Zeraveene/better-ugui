using System.Collections;
using Coffee.UIEffects;
using Coffee.UISoftMask;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;

namespace Zeraveene.BetterUGUI.Tests
{
    public class SoftMaskIntegrationTests
    {
        [TestCase("Hidden/UI/SoftMask")]
        [TestCase("Hidden/UI/TerminalMaskingShape")]
        public void CoreMaskShadersAreRegisteredBeforeBuilding(string name)
        {
            var shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            var variant = new ShaderVariantCollection.ShaderVariant(shader, UnityEngine.Rendering.PassType.Normal);
            Assert.That(UISoftMaskProjectSettings.shaderVariantCollection.Contains(variant), Is.True);
        }

        [UnityTest]
        public IEnumerator SoftMaskCreatesBufferAndCombinesWithUIEffectAndTmp()
        {
            yield return new EnterPlayMode();
            var root = new GameObject("SoftMask test", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var maskObject = new GameObject("Mask", typeof(RectTransform), typeof(ProceduralShape));
                maskObject.transform.SetParent(root.transform, false);
                maskObject.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 200);
                var mask = maskObject.AddComponent<SoftMask>();
                mask.showMaskGraphic = false;
                var child = new GameObject("Effect", typeof(RectTransform), typeof(ProceduralShape));
                child.transform.SetParent(maskObject.transform, false);
                child.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 300);
                child.AddComponent<UIEffect>().toneFilter = ToneFilter.Grayscale;
                var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(maskObject.transform, false);
                var tmp = text.GetComponent<TextMeshProUGUI>();
                tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                tmp.text = "SoftMask";
                tmp.ForceMeshUpdate();

                for (var i = 0; i < 5; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }

                Assert.That(mask.SoftMaskingEnabled(), Is.True);
                Assert.That(mask.hasSoftMaskBuffer, Is.True);
                Assert.That(mask.softMaskBuffer.IsCreated(), Is.True);
                Assert.That(mask.allowRenderScale, Is.False, "Overlay UI must not use the game camera render scale.");
                Assert.That(child.GetComponent<SoftMaskable>(), Is.Not.Null, "Automatic child discovery must work.");
                var effectMaterial = child.GetComponent<ProceduralShape>().materialForRendering;
                Assert.That(effectMaterial.shader.name, Is.EqualTo("Hidden/UI/Default (UIEffect)"));
                Assert.That(effectMaterial.IsKeywordEnabled("SOFTMASKABLE"), Is.True);
                Assert.That(ShaderUtil.ShaderHasError(effectMaterial.shader), Is.False);
                Assert.That(tmp.materialForRendering.shader.name, Does.EndWith(" (SoftMaskable)"));
                Assert.That(ShaderUtil.ShaderHasError(tmp.materialForRendering.shader), Is.False);

                text.AddComponent<UIEffect>().toneFilter = ToneFilter.Grayscale;
                tmp.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.That(tmp.materialForRendering.shader.name, Does.EndWith(" (UIEffect)"));
                Assert.That(tmp.materialForRendering.IsKeywordEnabled("SOFTMASKABLE"), Is.True);
                mask.enabled = false;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.That(child.GetComponent<ProceduralShape>().materialForRendering.IsKeywordEnabled("SOFTMASKABLE"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            yield return new ExitPlayMode();
        }
    }
}
