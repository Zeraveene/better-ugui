using UnityEngine;
using Zeraveene.BetterUGUI.Animation;

namespace Zeraveene.BetterUGUI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Better uGUI/UI Animation")]
    public sealed class UIAnimation : MonoBehaviour, IUIAnimationReceiver
    {
        [SerializeField] private bool fadeEnabled = true;
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 1f;
        [SerializeField, Range(0f, 1f)] private float fadeTo = 0f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [SerializeField] private bool scaleEnabled;
        [SerializeField] private float scaleFrom = 1f;
        [SerializeField] private float scaleTo = 1.3f;
        [SerializeField, Min(0f)] private float scaleDuration = 0.3f;

        private CanvasGroup group;
        private UnityEngine.UI.Image image;
        private UIAnimationHandle handle;
        private float duration;
        private bool playing, animateFade, animateScale;

        /// <summary>Restart enabled channels from their configured values. Returns false if nothing can play.</summary>
        public bool Play()
        {
            Stop();
            if (!isActiveAndEnabled) return false;
            group = GetComponent<CanvasGroup>();
            image = GetComponent<UnityEngine.UI.Image>();
            animateFade = fadeEnabled && (group != null || image != null);
            animateScale = scaleEnabled;
            if (!animateFade && !animateScale) return false;
            duration = Mathf.Max(animateFade ? fadeDuration : 0f, animateScale ? scaleDuration : 0f);
            playing = true;
            EvaluatePreview(0f);
            if (Application.isPlaying && playing)
                handle = UIAnimationRunner.Play(new Animation.UIAnimation
                {
                    Owner = this, Duration = duration, Ease = UIEase.Linear, Receiver = this
                });
            return true;
        }

        /// <summary>Stop at the current values. Returns whether playback was running.</summary>
        public bool Stop()
        {
            bool wasPlaying = playing;
            handle.Cancel();
            handle = default;
            playing = false;
            return wasPlaying;
        }

        internal bool EvaluatePreview(float elapsed)
        {
            if (!playing) return false;
            if (animateFade)
            {
                float alpha = Mathf.LerpUnclamped(fadeFrom, fadeTo, Progress(elapsed, fadeDuration));
                if (group != null) group.alpha = alpha;
                else if (image != null)
                {
                    Color color = image.color;
                    color.a = alpha;
                    image.color = color;
                }
            }
            if (animateScale)
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(scaleFrom, scaleTo, Progress(elapsed, scaleDuration));
            if (elapsed >= duration) playing = false;
            return playing;
        }

        private static float Progress(float elapsed, float seconds)
        {
            float t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
            return 1f - (1f - t) * (1f - t); // OutQuad
        }

        void IUIAnimationReceiver.ApplyAnimation(float progress) => EvaluatePreview(progress * duration);
        void IUIAnimationReceiver.AnimationCompleted() => playing = false;
        private void OnDisable() => Stop();
    }
}
