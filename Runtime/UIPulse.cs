using UnityEngine;
using Zeraveene.BetterUGUI.Animation;

namespace Zeraveene.BetterUGUI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Better uGUI/UI Pulse")]
    public sealed class UIPulse : MonoBehaviour
    {
        [SerializeField] private float from = 1f;
        [SerializeField] private float to = 0.5f;
        [SerializeField, Min(0.001f), Tooltip("Seconds for each direction of the pulse.")]
        private float duration = 0.3f;
        [SerializeField, Tooltip("Start pulsing whenever this component is enabled in Play Mode.")]
        private bool alwaysPlay;

        private bool playing;
        private float elapsed;

        /// <summary>Restart the repeating pulse from From. Returns false if disabled.</summary>
        public bool Play()
        {
            if (!isActiveAndEnabled) return false;
            elapsed = 0f;
            playing = true;
            EvaluatePreview(0f);
            return true;
        }

        /// <summary>Stop at the current scale. Returns whether playback was running.</summary>
        public bool Stop()
        {
            bool wasPlaying = playing;
            playing = false;
            return wasPlaying;
        }

        internal bool EvaluatePreview(float seconds)
        {
            if (!playing) return false;
            float t = Mathf.PingPong(seconds / Mathf.Max(0.001f, duration), 1f);
            float eased = UIEasing.Evaluate(UIEase.InOutQuad, t);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, eased);
            return true;
        }

        private void Update()
        {
            if (!playing) return;
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, Mathf.Max(0.001f, duration) * 2f);
            EvaluatePreview(elapsed);
        }

        private void OnEnable()
        {
            if (Application.isPlaying && alwaysPlay) Play();
        }

        private void OnDisable() => Stop();
    }
}
