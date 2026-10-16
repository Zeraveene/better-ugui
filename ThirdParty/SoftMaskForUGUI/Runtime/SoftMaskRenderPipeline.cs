using System;

namespace Coffee.UISoftMask
{
    /// <summary>Optional render pipeline queries supplied by the URP integration.</summary>
    public static class SoftMaskRenderPipeline
    {
        public static Func<bool> isUniversalRenderPipelineProvider;
        public static Func<float> renderScaleProvider;
        public static bool isUniversalRenderPipeline => isUniversalRenderPipelineProvider?.Invoke() ?? false;
        public static float renderScale => renderScaleProvider?.Invoke() ?? 1f;
    }
}
