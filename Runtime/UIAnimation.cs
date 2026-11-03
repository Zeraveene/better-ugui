using UnityEngine;
using UnityEngine.UI;
using Zeraveene.BetterUGUI.Animation;

namespace Zeraveene.BetterUGUI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Better uGUI/UI Animation")]
    public sealed class UIAnimation : MonoBehaviour, IUIAnimationReceiver
    {
        [SerializeField, Tooltip("Object to animate. Empty uses this component's Transform.")]
        private Transform animationTarget;
        [SerializeField] private bool fadeEnabled = true;
        [SerializeField, Tooltip("CanvasGroup or Graphic (ProceduralShape, Image, RawImage, ...). Empty uses the animation target's CanvasGroup, then Graphic.")]
        private Component fadeTarget;
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 1f;
        [SerializeField, Range(0f, 1f)] private float fadeTo = 0f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [SerializeField] private bool scaleEnabled;
        [SerializeField, Tooltip("Animate X, Y and Z independently instead of using one uniform scale.")]
        private bool separateScaleAxes;
        [SerializeField] private float scaleFrom = 1f;
        [SerializeField] private float scaleTo = 1.3f;
        [SerializeField] private Vector3 scaleFromXYZ = Vector3.one;
        [SerializeField] private Vector3 scaleToXYZ = Vector3.one * 1.3f;
        [SerializeField, Min(0f)] private float scaleDuration = 0.3f;
        [SerializeField] private bool positionEnabled;
        [SerializeField, Tooltip("Anchored position (RectTransform) or local position.")]
        private Vector2 positionFrom = new Vector2(0f, -50f);
        [SerializeField] private Vector2 positionTo = Vector2.zero;
        [SerializeField, Min(0f)] private float positionDuration = 0.3f;

        private CanvasGroup group;
        private Transform playbackTarget;
        private Graphic graphic;
        private UIAnimationHandle handle;
        private float duration;
        private bool playing, animateFade, animateScale, animatePosition;

        private bool reverse;

        /// <summary>Restart enabled channels from their configured values. Returns false if nothing can play.</summary>
        public bool Play() => BeginPlayback(false);

        /// <summary>Restart from To toward From, using the same durations and easing.</summary>
        public bool PlayReverse() => BeginPlayback(true);

        private bool BeginPlayback(bool playReverse)
        {
            Stop();
            reverse = playReverse;
            if (!isActiveAndEnabled) return false;
            playbackTarget = ResolveAnimationTarget();
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

        internal Transform ResolveAnimationTarget() => animationTarget != null ? animationTarget : transform;

        /// <summary>The assigned CanvasGroup/Graphic, else the animation target's CanvasGroup, then Graphic.</summary>
        internal void ResolveFadeTarget(out CanvasGroup canvasGroup, out Graphic fadeGraphic)
        {
            canvasGroup = fadeTarget as CanvasGroup;
            fadeGraphic = fadeTarget as Graphic;
            if (canvasGroup != null || fadeGraphic != null) return;
            Transform target = ResolveAnimationTarget();
            canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null) fadeGraphic = target.GetComponent<Graphic>();
        }

        internal bool EvaluatePreview(float elapsed)
        {
            if (!playing) return false;
            if (playbackTarget == null)
            {
                Stop();
                return false;
            }
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
            {
                Vector3 from = separateScaleAxes ? scaleFromXYZ : Vector3.one * scaleFrom;
                Vector3 to = separateScaleAxes ? scaleToXYZ : Vector3.one * scaleTo;
                playbackTarget.localScale = Vector3.LerpUnclamped(from, to, Progress(elapsed, scaleDuration));
            }
            if (animatePosition)
            {
                Vector2 position = Vector2.LerpUnclamped(positionFrom, positionTo, Progress(elapsed, positionDuration));
                if (playbackTarget is RectTransform rect) rect.anchoredPosition = position;
                else playbackTarget.localPosition = new Vector3(position.x, position.y, playbackTarget.localPosition.z);
            }
            if (elapsed >= duration) playing = false;
            return playing;
        }

        private float Progress(float elapsed, float seconds)
        {
            float t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
            float eased = 1f - (1f - t) * (1f - t); // OutQuad
            return reverse ? 1f - eased : eased;
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
