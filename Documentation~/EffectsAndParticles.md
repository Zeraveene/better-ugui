# Effects and particles

Better uGUI 4.1.0 includes [Mob Sakai UIEffect 5.11.7](https://github.com/mob-sakai/UIEffect/tree/5.11.7) and [UI Particle 4.14.1](https://github.com/mob-sakai/ParticleEffectForUGUI/tree/4.14.1). Install Better uGUI once; neither module requires a separate UPM install.

## UIEffect

Add **UIEffect** to a GameObject with a uGUI Graphic: a procedural shape, Image, RawImage, or TextMeshProUGUI. Configure tone, color, sampling, transition, shadow, edge or gradation in its inspector, or select a bundled preset. UIEffectReplica and UIEffectTweener retain their upstream behavior. Project settings live under **Project Settings > UI > UI Effect**.

Unity 6 TextMeshPro effect shaders are included and active, so no support sample import is needed. TMP Essential Resources are still required for your project's fonts and their shader includes. Standard TMP resource paths work directly; the upstream include-path repair handles moved resources in the Editor. Custom shaders and ShaderGraph shaders need their own UIEffect-compatible variants; optional upstream ShaderGraph samples are not bundled. Built-in procedural graphics use UI/Default and can receive UIEffect; adding multiple mesh effects can change their visual result depending on component order.

```csharp
using Coffee.UIEffects;

var effect = graphic.gameObject.AddComponent<UIEffect>();
effect.toneFilter = ToneFilter.Grayscale;
effect.toneIntensity = 1;
```

Custom asmdefs need `Coffee.UIEffect`. Existing Better uGUI components remain in their existing namespaces and assemblies.

## UI Particle on top of UI

Keep the Canvas in **Screen Space - Overlay**. Create a final child container (or a dedicated Overlay Canvas with a higher sorting order than the other UI canvases), add **UI Particle** to it, and place your particle prefab beneath it. The last sibling renders over earlier siblings in that Canvas. UI Particle automatically collects child systems when enabled; use **Refresh** after changing its child hierarchy. No manually maintained particle list is required for a normal prefab.

Use **GameObject > UI (Canvas) > Particle System** in Unity 6.3 to create a ready-to-use setup with the included `UI/Additive` material. Earlier Unity 6 versions put this under **GameObject > UI**. Use `UI/Default` for simple alpha-blended effects. Adjust UI Particle's Scale for particle prefabs authored in world units. Trails are supported.

This is the upstream mesh-baking implementation: it uses CanvasRenderer, not a second rendering camera or RenderTexture. A hidden disabled camera is used only to calculate baked billboard geometry. UI rendering stays independent of the gameplay camera's URP render scale. Ordinary particle shaders are not universally compatible; retain the upstream UI shader requirements.

```csharp
using Coffee.UIExtensions;

// Place this reference on your controller; add UI Particle once on the effect root.
[SerializeField] private UIParticle particles;

public void PlayEffect()
{
    particles.gameObject.SetActive(true);
    particles.StartEmission();
    particles.Play();
}

// Let existing particles finish their authored startLifetime.
public void FinishEffect() => particles.StopEmission();

// Immediately clear and hide.
public void HideEffect()
{
    particles.Clear();
    particles.gameObject.SetActive(false);
}
```

Particle lifetime remains authored on ParticleSystem. Use non-looping systems for one-shot effects. `UIParticle.Stop()` pauses UI simulation; use `StopEmission()` when existing particles should continue to their natural death. If you disable/pool the root after completion, check `particles.particles` with `ParticleSystem.IsAlive(false)` across all child systems after stopping emission; delayed emitters and sub-emitters can outlive the first system. The package does not automatically destroy effect roots.

Custom asmdefs need `Coffee.UIParticle`. You can leave Maskable off if clipping is unwanted. Place topmost effects outside masked containers and fading CanvasGroups when those behaviors are unwanted.

## Migration and maintenance

Do not install standalone copies of these three packages alongside Better uGUI. Remove them before switching to the bundle. Original component names, C# namespaces, assemblies and asset GUIDs are preserved, allowing existing serialized references to resolve from the bundled location. Test your existing effects after migration, especially if upgrading an older upstream version.

Both modules are MIT licensed by mob-sakai. Exact source commits, license texts and local adaptations are recorded in [Third Party Notices](../Third%20Party%20Notices.md). [SoftMask](SoftMask.md) is included starting in 4.2.0. No gameplay cameras, render modes, scenes or user particle materials are changed on installation.
