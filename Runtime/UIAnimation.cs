using UnityEngine;
using UnityEngine.UI;
using Zeraveene.BetterUGUI.Animation;

namespace Zeraveene.BetterUGUI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Better uGUI/UI Animation")]
    public sealed class UIAnimation : MonoBehaviour, IUIAnimationReceiver
    {
        [SerializeField] private bool fadeEnabled = true;
        [SerializeField, Tooltip("CanvasGroup or Graphic (ProceduralShape, Image, RawImage, ...). Empty uses this GameObject's CanvasGroup, then Graphic.")]
        private Component fadeTarget;
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 1f;
        [SerializeField, Range(0f, 1f)] private float fadeTo = 0f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [SerializeField] private bool scaleEnabled;
        [SerializeField] private float scaleFrom = 1f;
        [SerializeField] private float scaleTo = 1.3f;
        [SerializeField, Min(0f)] private float scaleDuration = 0.3f;
        [SerializeField] private bool positionEnabled;
        [SerializeField, Tooltip("Anchored position (RectTransform) or local position.")]
        private Vector2 positionFrom = new Vector2(0f, -50f);
        [SerializeField] private Vector2 positionTo = Vector2.zero;
        [SerializeField, Min(0f)] private float positionDuration = 0.3f;

        private CanvasGroup group;
        private Graphic graphic;
        private UIAnimationHandle handle;
        private float duration;
        private bool playing, animateFade, animateScale, animatePosition;

        /// <summary>Restart enabled channels from their configured values. Returns false if nothing can play.</summary>
        public bool Play()
        {
            Stop();
            if (!isActiveAndEnabled) return false;
            ResolveFadeTarget(out group, out graphic);
            animateFade = fadeEnabled && (group != null || graphic != null);
            animateScale = scaleEnabled;
            animatePosition = positionEnabled;
            if (!animateFade && !animateScale && !animatePosition) return false;
            duration = Mathf.Max(animateFade ? fadeDuration : 0f,
                Mathf.Max(animateScale ? scaleDuration : 0f, animatePosition ? positionDuration : 0f));
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

        /// <summary>The fade target: the assigned CanvasGroup/Graphic, else this GameObject's CanvasGroup, then Graphic.</summary>
        internal void ResolveFadeTarget(out CanvasGroup canvasGroup, out Graphic fadeGraphic)
        {
            canvasGroup = fadeTarget as CanvasGroup;
            fadeGraphic = fadeTarget as Graphic;
            if (canvasGroup != null || fadeGraphic != null) return;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) fadeGraphic = GetComponent<Graphic>();
        }

        internal bool EvaluatePreview(float elapsed)
        {
            if (!playing) return false;
            if (animateFade)
            {
                float alpha = Mathf.LerpUnclamped(fadeFrom, fadeTo, Progress(elapsed, fadeDuration));
                if (group != null) group.alpha = alpha;
                else if (graphic != null)
                {
                    Color color = graphic.color;
                    color.a = alpha;
                    graphic.color = color;
                }
            }
            if (animateScale)
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(scaleFrom, scaleTo, Progress(elapsed, scaleDuration));
            if (animatePosition)
            {
                Vector2 position = Vector2.LerpUnclamped(positionFrom, positionTo, Progress(elapsed, positionDuration));
                if (transform is RectTransform rect) rect.anchoredPosition = position;
                else transform.localPosition = new Vector3(position.x, position.y, transform.localPosition.z);
            }
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

        private void Reset()
        {
            // Start Position at the current spot so adding the component never teleports the object.
            Vector2 current = transform is RectTransform rect ? rect.anchoredPosition : (Vector2)transform.localPosition;
            positionTo = current;
            positionFrom = current + new Vector2(0f, -50f);
        }

        private void OnValidate()
        {
            if (fadeTarget == null || fadeTarget is CanvasGroup || fadeTarget is Graphic) return;
            Component group = fadeTarget.GetComponent<CanvasGroup>();
            fadeTarget = group != null ? group : fadeTarget.GetComponent<Graphic>();
        }
    }
}
