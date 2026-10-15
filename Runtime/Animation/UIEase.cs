using UnityEngine.Scripting.APIUpdating;
using UnityEngine;

namespace Zeraveene.BetterUGUI.Animation
{
    // Explicit values retain easing settings in existing scenes and presets.
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.ProceduralUI.Animation", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public enum UIEase
    {
        Unset = 0, Linear = 1,
        InSine = 2, OutSine = 3, InOutSine = 4,
        InQuad = 5, OutQuad = 6, InOutQuad = 7,
        InCubic = 8, OutCubic = 9, InOutCubic = 10,
        InQuart = 11, OutQuart = 12, InOutQuart = 13,
        InQuint = 14, OutQuint = 15, InOutQuint = 16,
        InExpo = 17, OutExpo = 18, InOutExpo = 19,
        InCirc = 20, OutCirc = 21, InOutCirc = 22,
        InElastic = 23, OutElastic = 24, InOutElastic = 25,
        InBack = 26, OutBack = 27, InOutBack = 28,
        InBounce = 29, OutBounce = 30, InOutBounce = 31,
        Flash = 32, InFlash = 33, OutFlash = 34, InOutFlash = 35,
        Zero = 36, Custom = 37
    }

    [MovedFrom(true, sourceNamespace: "MaxsusWorks.ProceduralUI.Animation", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public static class UIEasing
    {
        public static float Evaluate(UIEase ease, float t, AnimationCurve curve = null)
        {
            t = Mathf.Clamp01(t);
            if (ease == UIEase.Custom) return curve == null ? t : curve.Evaluate(t);
            if (ease == UIEase.Zero) return 0f;
            if (t == 0f || t == 1f) return t;
            const float back = 1.70158f;
            float u = t - 1f;
            switch (ease)
            {
                case UIEase.InSine: return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case UIEase.OutSine: return Mathf.Sin(t * Mathf.PI * 0.5f);
                case UIEase.InOutSine: return (1f - Mathf.Cos(Mathf.PI * t)) * 0.5f;
                case UIEase.InQuad: case UIEase.InFlash: return t * t;
                case UIEase.Unset: case UIEase.OutQuad: case UIEase.OutFlash: return 1f - u * u;
                case UIEase.InOutQuad: case UIEase.InOutFlash: return t < 0.5f ? 2f * t * t : 1f - 2f * u * u;
                case UIEase.InCubic: return t * t * t;
                case UIEase.OutCubic: return 1f + u * u * u;
                case UIEase.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f + 4f * u * u * u;
                case UIEase.InQuart: return t * t * t * t;
                case UIEase.OutQuart: return 1f - u * u * u * u;
                case UIEase.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - 8f * u * u * u * u;
                case UIEase.InQuint: return t * t * t * t * t;
                case UIEase.OutQuint: return 1f + u * u * u * u * u;
                case UIEase.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f + 16f * u * u * u * u * u;
                case UIEase.InExpo: return Mathf.Pow(2f, 10f * t - 10f);
                case UIEase.OutExpo: return 1f - Mathf.Pow(2f, -10f * t);
                case UIEase.InOutExpo: return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f : 1f - Mathf.Pow(2f, -20f * t + 10f) * 0.5f;
                case UIEase.InCirc: return 1f - Mathf.Sqrt(1f - t * t);
                case UIEase.OutCirc: return Mathf.Sqrt(1f - u * u);
                case UIEase.InOutCirc: return t < 0.5f ? (1f - Mathf.Sqrt(1f - 4f * t * t)) * 0.5f : (Mathf.Sqrt(1f - 4f * u * u) + 1f) * 0.5f;
                case UIEase.InBack: return (back + 1f) * t * t * t - back * t * t;
                case UIEase.OutBack: return 1f + (back + 1f) * u * u * u + back * u * u;
                case UIEase.InOutBack:
                    float b = back * 1.525f;
                    return t < 0.5f ? 2f * t * t * ((b + 1f) * 2f * t - b) : 1f + 2f * u * u * ((b + 1f) * 2f * u + b);
                case UIEase.InBounce: return 1f - Bounce(1f - t);
                case UIEase.OutBounce: return Bounce(t);
                case UIEase.InOutBounce: return t < 0.5f ? (1f - Bounce(1f - 2f * t)) * 0.5f : (1f + Bounce(2f * t - 1f)) * 0.5f;
                case UIEase.InElastic: return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((10f * t - 10.75f) * (2f * Mathf.PI / 3f));
                case UIEase.OutElastic: return Mathf.Pow(2f, -10f * t) * Mathf.Sin((10f * t - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
                case UIEase.InOutElastic:
                    return t < 0.5f
                        ? -0.5f * Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI / 4.5f))
                        : 0.5f * Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI / 4.5f)) + 1f;
                default: return t;
            }
        }

        private static float Bounce(float t)
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
            if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }
    }
}
