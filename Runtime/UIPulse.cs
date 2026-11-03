using UnityEngine;
using Zeraveene.BetterUGUI.Animation;

namespace Zeraveene.BetterUGUI
{
    public enum UIPulseMode { Alpha, Scale }

    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Better uGUI/UI Pulse")]
    public sealed class UIPulse : MonoBehaviour
    {
        [SerializeField] private UIPulseMode mode = UIPulseMode.Alpha;
        [SerializeField] private float from = 1f;
        [SerializeField] private float to = 0.5f;
        [SerializeField, Min(0.001f), Tooltip("Seconds for each pass from one value to the other.")]
        private float duration = 0.3f;
        [SerializeField, Tooltip("Repeat from the starting value after each pass instead of ping-ponging.")]
        private bool restart;
        [SerializeField] private UIEase ease = UIEase.InOutQuad;
        [SerializeField, Tooltip("Start pulsing whenever this component is enabled in Play Mode.")]
        private bool alwaysPlay;

        private CanvasGroup group;
        private UnityEngine.UI.Graphic graphic;
        private bool playing;
        private float elapsed;

        private bool reverse;

        /// <summary>Restart the repeating pulse from From. Returns false if disabled or Alpha has no supported target.</summary>
        public bool Play() => BeginPlayback(false);

        /// <summary>Restart from To toward From, using the same durations and easing.</summary>
        public bool PlayReverse() => BeginPlayback(true);

        private bool BeginPlayback(bool playReverse)
        {
            Stop();
            reverse = playReverse;
            if (!isActiveAndEnabled) return false;
            group = GetComponent<CanvasGroup>();
            graphic = GetComponent<UnityEngine.UI.Graphic>();
            if (mode == UIPulseMode.Alpha && group == null && graphic == null) return false;
            elapsed = 0f;
            playing = true;
            EvaluatePreview(0f);
            return true;
        }

        /// <summary>Stop at the current value. Returns whether playback was running.</summary>
        public bool Stop()
        {
            bool wasPlaying = playing;
            playing = false;
            return wasPlaying;
        }

        internal bool EvaluatePreview(float seconds)
        {
            if (!playing) return false;
            float progress = seconds / Mathf.Max(0.001f, duration);
            float t = restart ? Mathf.Repeat(progress, 1f) : Mathf.PingPong(progress, 1f);
            float eased = UIEasing.Evaluate(ease, t);
            float value = Mathf.LerpUnclamped(from, to, reverse ? 1f - eased : eased);
            if (mode == UIPulseMode.Scale)
                transform.localScale = Vector3.one * value;
            else if (group != null)
                group.alpha = Mathf.Clamp01(value);
            else if (graphic != null)
            {
                Color color = graphic.color;
                color.a = Mathf.Clamp01(value);
                graphic.color = color;
            }
            else
                return StopAndFinish();
            return true;
        }

        private bool StopAndFinish()
        {
            Stop();
            return false;
        }

        private void Update()
        {
            if (!playing) return;
            float cycleDuration = Mathf.Max(0.001f, duration) * (restart ? 1f : 2f);
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, cycleDuration);
            EvaluatePreview(elapsed);
        }

        private void OnEnable()
        {
            if (Application.isPlaying && alwaysPlay) Play();
        }

        private void OnDisable() => Stop();
    }
}
