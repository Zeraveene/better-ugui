using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Coffee.UIEffects;
using Coffee.UIExtensions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Zeraveene.BetterUGUI.Tests
{
    public class MobSakaiIntegrationTests
    {
        private const string Root = "Packages/com.zeraveene.better-ugui/ThirdParty/";

        [Test]
        public void BundledShadersAndPresetsResolveWithoutSeparateCoffeePackages()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { Root }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(shader, Is.Not.Null);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, shader.name);
            }

            var additive = AssetDatabase.LoadAssetAtPath<Material>(Root + "UIParticle/Shaders/UIAdditive.mat");
            Assert.That(additive, Is.Not.Null);
            Assert.That(additive.shader.name, Is.EqualTo("UI/Additive"));
            Assert.That(AssetDatabase.FindAssets("t:UIEffectPreset", new[] { Root + "UIEffect/UIEffectPresets" }), Is.Not.Empty);
            foreach (var file in Directory.GetFiles(Root + "UIEffect/UIEffectPresets", "*.asset"))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"guid: ([a-f0-9]{32})"))
                {
                    var guid = match.Groups[1].Value;
                    if (guid.StartsWith("0000000000000000")) continue;
                    Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Not.Empty,
                        file + " references an asset omitted from the bundle: " + guid);
                }
            }
        }

        [Test]
        public void UIEffectSelectsEffectMaterialForProceduralGraphicsAndTmp()
        {
            var canvas = new GameObject("Effect test", typeof(RectTransform), typeof(Canvas));
            try
            {
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var shape = new GameObject("Shape", typeof(RectTransform), typeof(ProceduralShape));
                shape.transform.SetParent(canvas.transform, false);
                var effect = shape.AddComponent<UIEffect>();
                effect.toneFilter = ToneFilter.Grayscale;
                Canvas.ForceUpdateCanvases();
                var material = shape.GetComponent<ProceduralShape>().materialForRendering;
                Assert.That(material.shader.name, Is.EqualTo("Hidden/UI/Default (UIEffect)"));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                var uvMasks = new List<Vector4>();
                shape.GetComponent<CanvasRenderer>().GetMesh().GetUVs(1, uvMasks);
                Assert.That(uvMasks, Is.Not.Empty);
                Assert.That(uvMasks.All(uv => uv.z > uv.x && uv.w > uv.y), Is.True,
                    "UIEffect must receive the VertexHelper mesh callback and populate UV masks; otherwise the panel disappears.");

                var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(canvas.transform, false);
                var tmp = text.GetComponent<TextMeshProUGUI>();
                tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                Assert.That(tmp.font, Is.Not.Null, "Import TMP Essential Resources before running text integration tests.");
                tmp.text = "Better uGUI";
                text.AddComponent<UIEffect>().toneFilter = ToneFilter.Grayscale;
                tmp.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                Assert.That(tmp.materialForRendering.shader.name, Does.EndWith(" (UIEffect)"));
                Assert.That(ShaderUtil.ShaderHasError(tmp.materialForRendering.shader), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [UnityTest]
        public IEnumerator OverlayParticlesBakeAndExpireAndRestoreNativeRendererOnDisable()
        {
            yield return new EnterPlayMode();
            var canvas = new GameObject("Particle test", typeof(RectTransform), typeof(Canvas));
            try
            {
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var root = new GameObject("Effect", typeof(RectTransform));
                root.transform.SetParent(canvas.transform, false);
                var child = new GameObject("Emitter", typeof(ParticleSystem));
                child.transform.SetParent(root.transform, false);
                var ps = child.GetComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.duration = 0.1f;
                main.startLifetime = 0.15f;
                main.startSize = 1;
                main.startSpeed = 0;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var emission = ps.emission;
                emission.enabled = false;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "UIParticle/Shaders/UIAdditive.mat");
                var particle = root.AddComponent<UIParticle>();
                particle.scale = 10;
                particle.RefreshParticles();
                Assert.That(particle.particles, Does.Contain(ps));
                particle.Play();
                ps.Emit(1);
                // Keep the particle alive while slow CI frames prepare the mesh.
                particle.Pause();
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.That(renderer.enabled, Is.False, "Native renderer must not draw a second copy.");
                var hasMesh = root.GetComponentsInChildren<CanvasRenderer>().Any(r =>
                {
                    var baked = r.GetMesh();
                    return baked != null && baked.vertexCount > 0;
                });
                Assert.That(hasMesh, Is.True, "Overlay particles must reach a CanvasRenderer.");
                particle.Resume();
                // Batch-mode tests do not repaint a Game view every frame.
                var deadline = Time.time + 0.4f;
                while (Time.time < deadline)
                {
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }
                Assert.That(ps.particleCount, Is.Zero, "Particles must expire with their authored lifetime.");
                particle.Play();
                ps.Emit(1);
                particle.Clear();
                Assert.That(ps.particleCount, Is.Zero);
                particle.enabled = false;
                Assert.That(renderer.enabled, Is.True, "Disabling the adapter must restore the native renderer.");
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
            yield return new ExitPlayMode();
        }
    }
}
