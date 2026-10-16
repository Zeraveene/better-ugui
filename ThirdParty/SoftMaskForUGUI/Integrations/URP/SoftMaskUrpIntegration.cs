using Coffee.UISoftMask;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zeraveene.BetterUGUI.Integrations
{
    public static class SoftMaskUrpIntegration
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SoftMaskRenderPipeline.isUniversalRenderPipelineProvider =
                () => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
            SoftMaskRenderPipeline.renderScaleProvider =
                () => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset ? asset.renderScale : 1f;
        }
    }
}
