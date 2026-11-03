using UnityEngine;
using UnityEngine.UI;
using Zeraveene.BetterUGUI.Animation;

namespace Zeraveene.BetterUGUI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Better uGUI/UI Animation")]
    public sealed class UIAnimation : MonoBehaviour, IUIAnimationReceiver
    {
        // Retained only to migrate shared targets saved by versions 4.2.7 and 4.2.8.
        [SerializeField, HideInInspector]
        private Transform animationTarget;
        [SerializeField] private bool fadeEnabled = true;
        [SerializeField, Tooltip("CanvasGroup, Graphic, or a component on the object to fade. Empty uses this GameObject's CanvasGroup, then Graphic.")]
        private Component fadeTarget;
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 1f;
        [SerializeField, Range(0f, 1f)] private float fadeTo = 0f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [SerializeField] private bool scaleEnabled;
        [SerializeField, Tooltip("Transform to scale. Empty uses this component's Transform.")]
        private Transform scaleTarget;
        [SerializeField, Tooltip("Animate X, Y and Z independently instead of using one uniform scale.")]
        private bool separateScaleAxes;
        [SerializeField] private float scaleFrom = 1f;
        [SerializeField] private float scaleTo = 1.3f;
        [SerializeField] private Vector3 scaleFromXYZ = Vector3.one;
        [SerializeField] private Vector3 scaleToXYZ = Vector3.one * 1.3f;
        [SerializeField, Min(0f)] private float scaleDuration = 0.3f;
        [SerializeField] private bool positionEnabled;
        [SerializeField, Tooltip("Transform to move. Empty uses this component's Transform.")]
        private Transform posTarget;
        [SerializeField, Tooltip("Anchored position (RectTransform) or local position.")]
        private Vector2 positionFrom = new Vector2(0f, -50f);
        [SerializeField] private Vector2 positionTo = Vector2.zero;
        [SerializeField, Min(0f)] private float positionDuration = 0.3f;
        [SerializeField] private bool sizeEnabled;
        [SerializeField, Tooltip("RectTransform to resize. Empty uses this object's RectTransform. Values are width and height in local UI units.")]
        private RectTransform sizeTarget;
        [SerializeField] private bool widthEnabled = true;
        [SerializeField, Min(0f)] private float widthFrom = 100f;
        [SerializeField, Min(0f)] private float widthTo = 100f;
        [SerializeField] private bool heightEnabled = true;
        [SerializeField, Min(0f)] private float heightFrom = 100f;
        [SerializeField, Min(0f)] private float heightTo = 100f;
        [SerializeField, Min(0f)] private float sizeDuration = 0.3f;

        private CanvasGroup group;
        private Transform playbackScaleTarget, playbackPositionTarget;
        private RectTransform playbackSizeTarget;
        private Graphic graphic;
        private UIAnimationHandle handle;
        private float duration;
        private bool playing, animateFade, animateScale, animatePosition, animateSize;

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
            playbackScaleTarget = ResolveScaleTarget();
            playbackPositionTarget = ResolvePositionTarget();
            playbackSizeTarget = ResolveSizeTarget();
            ResolveFadeTarget(out group, out graphic);
            animateFade = fadeEnabled && (group != null || graphic != null);
            animateScale = scaleEnabled;
            animatePosition = positionEnabled;
            animateSize = sizeEnabled && playbackSizeTarget != null && (widthEnabled || heightEnabled);
            if (!animateFade && !animateScale && !animatePosition && !animateSize) return false;
            duration = Mathf.Max(
                Mathf.Max(animateFade ? fadeDuration : 0f, animateScale ? scaleDuration : 0f),
                Mathf.Max(animatePosition ? positionDuration : 0f, animateSize ? sizeDuration : 0f));
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

        private void MigrateLegacyTarget()
        {
            if (animationTarget == null) return;
            scaleTarget = animationTarget;
            posTarget = animationTarget;
            if (fadeTarget == null) fadeTarget = animationTarget;
            animationTarget = null;
        }

        internal Transform ResolveScaleTarget()
        {
            MigrateLegacyTarget();
            return scaleTarget != null ? scaleTarget : transform;
        }

        internal Transform ResolvePositionTarget()
        {
            MigrateLegacyTarget();
            return posTarget != null ? posTarget : transform;
        }

        internal RectTransform ResolveSizeTarget() => sizeTarget != null ? sizeTarget : transform as RectTransform;

        /// <summary>The assigned CanvasGroup/Graphic, or components on the assigned object, falling back to this GameObject when empty.</summary>
        internal void ResolveFadeTarget(out CanvasGroup canvasGroup, out Graphic fadeGraphic)
        {
            MigrateLegacyTarget();
            canvasGroup = fadeTarget as CanvasGroup;
            fadeGraphic = fadeTarget as Graphic;
            if (canvasGroup != null || fadeGraphic != null) return;
            Component target = fadeTarget != null ? fadeTarget : transform;
            canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null) fadeGraphic = target.GetComponent<Graphic>();
        }

        internal bool EvaluatePreview(float elapsed)
        {
            if (!playing) return false;
            animateFade &= group != null || graphic != null;
            animateScale &= playbackScaleTarget != null;
            animatePosition &= playbackPositionTarget != null;
            animateSize &= playbackSizeTarget != null;
            if (!animateFade && !animateScale && !animatePosition && !animateSize)
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
                playbackScaleTarget.localScale = Vector3.LerpUnclamped(from, to, Progress(elapsed, scaleDuration));
            }
            if (animateSize)
            {
                float progress = Progress(elapsed, sizeDuration);
                if (widthEnabled)
                    playbackSizeTarget.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        Mathf.Max(0f, Mathf.LerpUnclamped(widthFrom, widthTo, progress)));
                if (heightEnabled)
                    playbackSizeTarget.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        Mathf.Max(0f, Mathf.LerpUnclamped(heightFrom, heightTo, progress)));
            }
            if (animatePosition)
            {
                Vector2 position = Vector2.LerpUnclamped(positionFrom, positionTo, Progress(elapsed, positionDuration));
                if (playbackPositionTarget is RectTransform rect) rect.anchoredPosition = position;
                else playbackPositionTarget.localPosition = new Vector3(position.x, position.y, playbackPositionTarget.localPosition.z);
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
            if (transform is RectTransform sizeRect)
            {
                widthFrom = widthTo = sizeRect.rect.width;
                heightFrom = heightTo = sizeRect.rect.height;
            }
        }

        private void OnValidate() => MigrateLegacyTarget();
    }
}
